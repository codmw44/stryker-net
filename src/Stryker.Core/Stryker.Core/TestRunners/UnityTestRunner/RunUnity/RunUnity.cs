using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
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
    private const long UnityMemoryConsumptionLimitInMb = 4000;

    private static RunUnity _instance;
    private bool _unityInProgress;
    private string _currentUnityRunArguments;

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

        TryOpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);
        SendCommandToUnity_ReloadDomain();
        WaitUntilEndOfCommand();
        ThrowExceptionIfExists();
    }

    public XDocument RunTests(IStrykerOptions strykerOptions, string projectPath,
        string additionalArgumentsForCli = null, string helperNamespace = null, string activeMutantId = null)
    {
        logger.LogDebug("Request to run tests Unity");

        CheckMemoryUsageAndRestartIfOverThreshold();

        TryOpenUnity(strykerOptions, projectPath, additionalArgumentsForCli);

        var pathToTestResultXml =
            Path.Combine(strykerOptions.OutputPath, $"test_results_{DateTime.Now.ToFileTime()}.xml");

        var pathToActiveMutantForSpecificProject = Path.Combine(_pathToActiveMutantsListenFile, helperNamespace + ".txt");
        if(!string.IsNullOrEmpty(activeMutantId))
        {
            logger.LogDebug("Run tests for Mutant: {0} {1}", helperNamespace, activeMutantId);
            File.WriteAllText(pathToActiveMutantForSpecificProject,  activeMutantId);
        }

        SendCommandToUnity_RunTests(pathToTestResultXml);

        //WaitUntilEndOfCommand
        while (!string.IsNullOrWhiteSpace(File.ReadAllText(_pathToUnityListenFile)))
        {
            ThrowExceptionIfExists();
            var memoryOverUsed = CheckMemoryUsageAndRestartIfOverThreshold(); //some tests can go to infinitive loop and go allocate infinitive amount of memory and time
            if (memoryOverUsed)
            {
                ResetActiveMutant();
                return new XDocument(); //we cannot rerun tests, and unity doesn't detect timeout for them, so we just return empty result
            }
        }

        ResetActiveMutant();

        ThrowExceptionIfExists();

        return XDocument.Load(pathToTestResultXml);

        void ResetActiveMutant()
        {
            if (!string.IsNullOrEmpty(activeMutantId))
            {
                File.WriteAllText(pathToActiveMutantForSpecificProject, "-1");
            }
        }

        bool CheckMemoryUsageAndRestartIfOverThreshold()
        {
            _unityProcess?.Refresh();
            var unityMemoryUsage = (_unityProcess?.WorkingSet64 ?? 0) / (1000 * 1000);
            if (unityMemoryUsage >= UnityMemoryConsumptionLimitInMb)
            {
                logger.LogInformation(
                    $"Close Unity to flush used memory and speed up process. Reached {unityMemoryUsage} mb. Restart configured after reaching {UnityMemoryConsumptionLimitInMb}");

                KillUnity();
                return true;
            }

            return false;
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
            logger.LogError(
                "Trying to run unity with other arguments when instance already opened. Waiting for closing the current one.");
            CloseUnity();
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
    }

    private string GetArgumentsToRun(string projectPath, string additionalArgumentsForCli = null) =>
        $"-batchmode -projectPath={projectPath} " +
        additionalArgumentsForCli;

    private void SendCommandToUnity_ReloadDomain() => SendCommandToUnity("reloadDomain");
    private void SendCommandToUnity_Exit() => SendCommandToUnity("exit");
    private void SendCommandToUnity_RunTests(string pathToSaveTestResult) => SendCommandToUnity("playmode " + pathToSaveTestResult);
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

    private void CloseUnity()
    {
        if (!_unityInProgress)
        {
            logger.LogDebug("Request to close Unity. Unity is not running. Do nothing");
            return;
        }

        logger.LogDebug("Request to close Unity");

        SendCommandToUnity_Exit();
        _unityProcessTask.GetAwaiter().GetResult();
    }

    private void KillUnity()
    {
        if (!_unityInProgress)
        {
            logger.LogDebug("Request to kill Unity. Unity is not running. Do nothing");
            return;
        }
        logger.LogDebug("Request to kill Unity");

        _unityProcess.KillTree(TimeSpan.FromSeconds(15));
        // don't use this cause it would through exception because of kill _unityProcessTask.GetAwaiter().GetResult();
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

}
