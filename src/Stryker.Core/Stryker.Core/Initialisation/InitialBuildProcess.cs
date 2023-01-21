using System;
using Microsoft.Extensions.Logging;
using Stryker.Core.Exceptions;
using Stryker.Core.Logging;
using Stryker.Core.Testing;
using Stryker.Core.ToolHelpers;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Stryker.Core.TestRunners.UnityTestRunner;
using Stryker.Core.TestRunners.UnityTestRunner.UnityPath;

namespace Stryker.Core.Initialisation
{
    public interface IInitialBuildProcess
    {
        void InitialBuild(bool fullFramework, string projectPath, string solutionPath, string msbuildPath = null,
            bool isUnity = false);

        void SolutionInitialBuild(string solutionPath, string msbuildPath = null,
            bool isUnity = false);
    }

    public class InitialBuildProcess : IInitialBuildProcess
    {
        private readonly IFileSystem _fileSystem;
        private readonly IUnityPath _unityPath;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger _logger;

        public InitialBuildProcess(IFileSystem fileSystem, IProcessExecutor processExecutor = null,
            IUnityPath unityPath = null)
        {
            _fileSystem = fileSystem;
            _unityPath = unityPath ?? new UnityPath(new FileSystem());
            _processExecutor = processExecutor ?? new ProcessExecutor();
            _logger = ApplicationLogging.LoggerFactory.CreateLogger<InitialBuildProcess>();
        }

        public void InitialBuild(bool fullFramework, string projectPath, string solutionPath, string msbuildPath = null,
            bool isUnity = false)
        {
            ProcessResult result;
            if (isUnity)
            {
                _logger.LogDebug("Started initial build using Unity");

                UnityInitialBuild(solutionPath, projectPath);
            }
            else if (fullFramework)
            {
                _logger.LogDebug("Started initial build using msbuild.exe");

                if (string.IsNullOrEmpty(solutionPath))
                {
                    throw new InputException(
                        "Stryker could not build your project as no solution file was presented. Please pass the solution path to stryker.");
                }

                solutionPath = Path.GetFullPath(solutionPath);
                var solutionDir = Path.GetDirectoryName(solutionPath);
                msbuildPath ??= new MsBuildHelper().GetMsBuildPath(_processExecutor);

                // Build project with MSBuild.exe
                result = _processExecutor.Start(solutionDir, msbuildPath, $"\"{solutionPath}\"");
                CheckBuildResult(result, msbuildPath, $"\"{solutionPath}\"");
            }
            else
            {
                _logger.LogDebug("Started initial build using dotnet build");

                var buildPath = !string.IsNullOrEmpty(solutionPath) ? solutionPath : Path.GetFileName(projectPath);

                _logger.LogDebug("Initial build using path: {buildPath}", buildPath);
                // Build with dotnet build
                result = _processExecutor.Start(projectPath, "dotnet", $"build \"{buildPath}\"");

                CheckBuildResult(result, "dotnet build", $"\"{Path.GetFileName(projectPath)}\"");
            }
        }

        public void SolutionInitialBuild(string solutionPath, string msbuildPath = null,
            bool isUnity = false)
        {
            if (isUnity)
            {
                UnityInitialBuild(solutionPath);
            }
        }

        private void UnityInitialBuild(string solutionPath, string projectPath = null)
        {
            var unityProjectPath = Directory.GetParent(projectPath ?? solutionPath).FullName;

            CopyUnitySdkInTargetUnityProject(unityProjectPath);
            RemoveUnityCompileCache(unityProjectPath);
            var openUnityResult = OpenUnityForCompiling(unityProjectPath);
            if (openUnityResult.ExitCode != 0)
            {
                throw new UnityExecuteException(openUnityResult.ExitCode, openUnityResult.Output);
            }
        }


        private ProcessResult OpenUnityForCompiling(string unityProjectPath) =>
            _processExecutor.Start(unityProjectPath, _unityPath.GetPath(unityProjectPath),
                $" -quit -batchmode -projectPath={unityProjectPath} -logFile {DateTime.Now.ToFileTime()}.log");

        private void RemoveUnityCompileCache(string unityProjectPath)
        {
            var cachePath = Path.Combine(unityProjectPath, "Library", "ScriptAssemblies");
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, true);
            }
        }

        private void CopyUnitySdkInTargetUnityProject(string unityProjectPath)
        {
            var allFilesOfSdk = typeof(VsTestHelper).Assembly
                .GetManifestResourceNames().Where(name => name.Contains("Stryker.UnitySDK"));

            var pathToPackageOfSdk = Path.Combine(unityProjectPath, "Packages", "Stryker.UnitySDK");
            Directory.CreateDirectory(pathToPackageOfSdk);

            File.WriteAllText(Path.Combine(pathToPackageOfSdk, ".gitignore"), "*");
            foreach (var nameEmbeddedResource in allFilesOfSdk)
            {
                using var file = _fileSystem.FileStream.New(
                    Path.Combine(pathToPackageOfSdk, GetFinalNameOfResource(nameEmbeddedResource)),
                    FileMode.Create);

                typeof(VsTestHelper).Assembly.GetManifestResourceStream(nameEmbeddedResource)?.CopyTo(file);
            }

            string GetFinalNameOfResource(string name)
            {
                return name.Split("Stryker.UnitySDK.").Last();
            }
        }

        private void CheckBuildResult(ProcessResult result, string buildCommand, string buildPath)
        {
            _logger.LogTrace("Initial build output {0}", result.Output);
            if (result.ExitCode != ExitCodes.Success)
            {
                // Initial build failed
                throw new InputException(result.Output,
                    $"Initial build of targeted project failed. Please make sure the targeted project is buildable. You can reproduce this error yourself using: \"{buildCommand} {buildPath}\"");
            }

            _logger.LogDebug("Initial build successful");
        }
    }
}
