using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Stryker.Abstractions.Options;
using Stryker.Core.Helpers.ProcessUtil;
using Stryker.Core.TestRunners.UnityTestRunner.RunUnity.UnityPath;
using Stryker.Utilities.Logging;

namespace Stryker.Core.TestRunners.UnityTestRunner.RunUnity;

public class RunUnity(IProcessExecutor processExecutor, IUnityPath unityPath, ILogger logger) : IRunUnity
{
    private const int TestFailedExitCode = 2;

    private static RunUnity _instance;
    private bool _unityInProgress;
    private string _currentUnityRunArguments;
    private long _unityMemoryConsumptionLimitInMb;

    private string _pathToUnityListenFile;
    private string _pathToActiveMutantsListenFile;
    private Task _unityProcessTask;
    private Process _unityProcess;

    public static RunUnity GetSingleInstance(Func<IProcessExecutor> processExecutor = null,
        Func<IUnityPath> unityPath = null,
        Func<ILogger> logger = null)
    {
        if (_instance != null) return _instance;

        _instance = new RunUnity(processExecutor?.Invoke() ?? new ProcessExecutor(),
            unityPath?.Invoke() ?? new UnityPath.UnityPath(new FileSystem()),
            logger?.Invoke() ?? ApplicationLogging.LoggerFactory.CreateLogger<RunUnity>());

        return _instance;
    }


    public void ReloadDomain(IStrykerOptions strykerOptions, string projectPath, string additionalArgumentsForCli = null)
    {
        logger.LogDebug("Request to reload domain");
        _unityMemoryConsumptionLimitInMb = strykerOptions.UnityMemoryConsumptionLimitInMb;

        TryOpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);
        SendCommandToUnity_ReloadDomain();
        WaitUntilEndOfCommand();
        ThrowExceptionIfExists();
    }

    public XDocument RunTests(IStrykerOptions strykerOptions, string projectPath,
        string additionalArgumentsForCli = null, string helperNamespace = null, string activeMutantId = null,
        IEnumerable<string> targetTestAssemblies = null, UnityTestMode testModeInput = UnityTestMode.All)
    {
        logger.LogDebug("Request to run tests Unity");
        _unityMemoryConsumptionLimitInMb = strykerOptions.UnityMemoryConsumptionLimitInMb;

        TryOpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);

        var pathToActiveMutantForSpecificProject = Path.Combine(_pathToActiveMutantsListenFile, helperNamespace + ".txt");
        if(!string.IsNullOrEmpty(activeMutantId))
        {
            logger.LogDebug("Run tests for Mutant: {0} {1}", helperNamespace, activeMutantId);
            File.WriteAllText(pathToActiveMutantForSpecificProject,  activeMutantId);
        }

        var combinedResults = new XDocument(new XElement("TestRun"));

        switch (testModeInput)
        {
            case UnityTestMode.All:

                var editModeResults = RunTestsForMode(UnityTestMode.EditMode, strykerOptions, helperNamespace, activeMutantId, targetTestAssemblies);
                if (editModeResults != null && editModeResults.Root != null)
                {
                    combinedResults.Root.Add(editModeResults.Root.Elements());
                }

                var playModeResults = RunTestsForMode(UnityTestMode.PlayMode, strykerOptions, helperNamespace, activeMutantId, targetTestAssemblies);
                if (playModeResults != null && playModeResults.Root != null)
                {
                    combinedResults.Root.Add(playModeResults.Root.Elements());
                }
                break;

            case UnityTestMode.PlayMode:
            case UnityTestMode.EditMode:
                var playModeOnlyResults = RunTestsForMode(testModeInput, strykerOptions, helperNamespace, activeMutantId, targetTestAssemblies);
                if (playModeOnlyResults != null && playModeOnlyResults.Root != null)
                {
                    combinedResults.Root.Add(playModeOnlyResults.Root.Elements());
                }
                break;
        }

        ResetActiveMutant();
        ThrowExceptionIfExists();

        return combinedResults;

        XDocument RunTestsForMode(UnityTestMode testMode, IStrykerOptions strykerOptions, string helperNamespace, string activeMutantId, IEnumerable<string> targetAssemblies)
        {
            TryOpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);

            logger.LogDebug("Running Unity tests in {0} mode", testMode);

            var pathToTestResultXml =
                Path.Combine(strykerOptions.OutputPath, $"test_results_{testMode.ToString().ToLowerInvariant()}_{DateTime.Now.ToFileTime()}.xml");

            SendCommandToUnity_RunTests(testMode, pathToTestResultXml, targetAssemblies);

            //WaitUntilEndOfCommand
            while (!string.IsNullOrWhiteSpace(File.ReadAllText(_pathToUnityListenFile)))
            {
                ThrowExceptionIfExists();
                if (_unityProcess!= null && _unityProcess.HasExited)
                {
                    logger.LogWarning("Unity process has exited without intention. Potentially because of mutation. Failing all tests");
                    break;
                }

                var memoryOverUsed = CheckMemoryUsageAndRestartIfOverThreshold(); //some tests can go to infinitive loop and go allocate infinitive amount of memory and time. And Unity tests doesn't catch this
                if (memoryOverUsed)
                {
                    return new XDocument(); //we cannot rerun tests, and unity doesn't detect timeout for them, so we just return empty result
                }
            }

            ThrowExceptionIfExists();

            if (File.Exists(pathToTestResultXml))
            {
                return XDocument.Load(pathToTestResultXml);
            }
            else
            {
                return new XDocument();
            }
        }

        void ResetActiveMutant()
        {
            if (!string.IsNullOrEmpty(activeMutantId))
            {
                File.WriteAllText(pathToActiveMutantForSpecificProject, "-1");
            }
        }

        bool CheckMemoryUsageAndRestartIfOverThreshold()
        {
            try
            {
                _unityProcess?.Refresh();

                // Check if process has exited before accessing WorkingSet64
                if (_unityProcess == null || _unityProcess.HasExited)
                {
                    return false;
                }

                var unityMemoryUsage = _unityProcess.WorkingSet64 / (1000 * 1000);
                if (unityMemoryUsage >= _unityMemoryConsumptionLimitInMb)
                {
                    logger.LogInformation(
                        $"Close Unity to flush used memory probably mutant lead to infinitive cycle. Reached {unityMemoryUsage} mb. Restart configured after reaching {_unityMemoryConsumptionLimitInMb}");

                    KillUnity();
                    return true;
                }

                return false;
            }
            catch (InvalidOperationException)
            {
                logger.LogDebug("Unity process is no longer accessible during memory check");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Unexpected error during memory check");
                return false;
            }
        }
    }


    public void Dispose()
    {
        ThrowExceptionIfExists();
    }


    private void TryOpenUnity(IStrykerOptions strykerOptions, string projectPath,
        string additionalArgumentsForCli = null)
    {
        ThrowExceptionIfExists();

        if (_unityInProgress && _currentUnityRunArguments != GetArgumentsToRun(projectPath, additionalArgumentsForCli))
        {
            logger.LogError("Trying to run unity with other arguments when instance already opened. Waiting for closing the current one.");
            KillUnity();
        }
        else if (_unityInProgress && (_unityProcess == null || _unityProcess.HasExited))
        {
            logger.LogInformation("Unity process has exited. Restarting Unity.");
            KillUnity();
        }
        else if (_unityInProgress)
        {
            logger.LogDebug("Trying to run unity when instance already opened. Nothing to do");
            return;
        }

        OpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);
    }

    private void OpenUnity(IStrykerOptions strykerOptions, string projectPath, string additionalArgumentsForCli = null)
    {
        logger.LogDebug("OpenUnity started");

        CheckAndAddStrykerUnityPackage(projectPath);

        _unityInProgress = true;

        _pathToUnityListenFile = Path.Combine(strykerOptions.OutputPath, "UnityListens.txt");
        Environment.SetEnvironmentVariable("Stryker.Unity.PathToListen", _pathToUnityListenFile);
        _pathToActiveMutantsListenFile = strykerOptions.OutputPath;
        Environment.SetEnvironmentVariable("ActiveMutationPath", _pathToActiveMutantsListenFile);

        CleanupCommandBuffer();

        var pathToUnityLogFile =
            Path.Combine(strykerOptions.OutputPath, "logs", "unity_" + DateTime.Now.ToFileTime() + ".log");

        _currentUnityRunArguments = GetArgumentsToRun(projectPath, additionalArgumentsForCli);

        _unityProcessTask = Task.Run(() =>
        {
            var processResult = processExecutor.Start(projectPath, unityPath.GetPath(strykerOptions),
                $"-logFile {pathToUnityLogFile} " + _currentUnityRunArguments, ref _unityProcess);
            logger.LogDebug("OpenUnity finished");
            _unityInProgress = false;

            if (processResult.ExitCode != 0 && processResult.ExitCode != TestFailedExitCode)
            {
                throw new UnityExecuteException(processResult.ExitCode, pathToUnityLogFile);
            }
        });

        while (_unityProcess != null)
        {
            //wait for process to start
        }
    }

    private string GetArgumentsToRun(string projectPath, string additionalArgumentsForCli = null) =>
        $"-batchmode -projectPath {projectPath} " +
        additionalArgumentsForCli;

    private void SendCommandToUnity_ReloadDomain() => SendCommandToUnity("reloadDomain");
    private void SendCommandToUnity_Exit() => SendCommandToUnity("exit");
    private void SendCommandToUnity_RunTests(UnityTestMode testMode, string pathToSaveTestResult, IEnumerable<string> targetAssemblies = null)
    {
        var command = $"{testMode.ToString().ToLowerInvariant()} {pathToSaveTestResult}";

        if (targetAssemblies != null && targetAssemblies.Any())
        {
            var assembliesParam = string.Join(";", targetAssemblies);
            command += $" {assembliesParam}";
        }

        SendCommandToUnity(command);
    }
    private void SendCommandToUnity(string command) => File.WriteAllText(_pathToUnityListenFile, command);

    private void CleanupCommandBuffer() => File.WriteAllText(_pathToUnityListenFile, string.Empty);

    private void WaitUntilEndOfCommand()
    {
        while (!string.IsNullOrWhiteSpace(File.ReadAllText(_pathToUnityListenFile)))
        {
            ThrowExceptionIfExists();
        }
    }

    private void ThrowExceptionIfExists()
    {
        if (_unityProcessTask?.Exception != null)
        {
            if (_unityProcessTask.Exception.GetBaseException() is UnityExecuteException unityEx && unityEx.ExitCode == 134)
            {
                logger.LogError("Another Unity process with this project is already running. Close Unity project and restart Stryker.");
                throw unityEx;
            }
            throw _unityProcessTask.Exception;
        }
    }


    private void KillUnity()
    {
        if (!_unityInProgress)
        {
            logger.LogDebug("Request to kill Unity. Unity is not running. Do nothing");
            return;
        }

        logger.LogDebug("Request to kill Unity");
        SendCommandToUnity_Exit();

        if (_unityProcess != null && !_unityProcess.HasExited)
        {
            _unityProcess.Kill(true);
            Thread.Sleep(10_000); //wait to kill the app
        }

        try
        {
            _unityProcessTask?.GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            //ignore exception
        }
        _unityProcess = null;
        _unityProcessTask = null;
        _unityInProgress = false;
    }

    private void CheckAndAddStrykerUnityPackage(string projectPath)
    {
        var packagesManifestPath = Path.Combine(projectPath, "Packages", "manifest.json");

        if (File.Exists(packagesManifestPath))
        {
            var manifestContent = File.ReadAllText(packagesManifestPath);

            // Check if the com.stryker.unity package is already in the manifest
            if (!manifestContent.Contains("\"com.stryker.unity\""))
            {
                logger.LogInformation("Adding com.stryker.unity package to Unity project");

                // Determine where to insert the new package dependency
                int insertPosition;
                if (manifestContent.Contains("\"dependencies\": {"))
                {
                    insertPosition = manifestContent.IndexOf("\"dependencies\": {", StringComparison.Ordinal) + "\"dependencies\": {".Length;

                    // Add the package to dependencies
                    //todo replace it to https git and original repo
                    var packageEntry =
                        "\n    \"com.stryker.unity\": \"https://github.com/codmw44/stryker-net.git?path=src/Stryker.UnitySDK#feature/add_unity_support\",";

                    // If there are already dependencies, add a comma to the new entry
                    if (manifestContent.Substring(insertPosition).TrimStart().StartsWith("\""))
                    {
                        manifestContent = manifestContent.Insert(insertPosition, packageEntry);
                    }
                    else
                    {
                        manifestContent = manifestContent.Insert(insertPosition, packageEntry.TrimEnd(','));
                    }

                    File.WriteAllText(packagesManifestPath, manifestContent);
                    logger.LogInformation("Successfully added com.stryker.unity package to Unity project");
                }
                else
                {
                    logger.LogWarning("Could not find dependencies section in manifest.json. Stryker package was not added.");
                }
            }
            else
            {
                logger.LogDebug("com.stryker.unity package is already in the Unity project");
            }
        }
        else
        {
            logger.LogError("Could not find manifest.json in Unity project at {0}", packagesManifestPath);
        }

    }

    public void RemoveScriptAssembliesDirectory(string projectPath)
    {
        var scriptAssembliesPath = Path.Combine(projectPath, "Library", "ScriptAssemblies");

        return;
        if (Directory.Exists(scriptAssembliesPath))
        {
            try
            {
                Directory.Delete(scriptAssembliesPath, true);
                Directory.CreateDirectory(scriptAssembliesPath);
                logger.LogInformation("Successfully removed Library/ScriptAssemblies directory");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove Library/ScriptAssemblies directory: {0}", scriptAssembliesPath);
            }
        }
        else
        {
            logger.LogDebug("Library/ScriptAssemblies directory does not exist: {0}", scriptAssembliesPath);
        }
    }

}
