using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.CodeCoverage;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace Stryker.UnitySDK
{
    public static class RunTests
    {
        private static TestRunnerApi _testRunnerApi;
        private static string _runPathToOutput;
        public static string _coverageOutputPath;

        public static bool TestsInProgress
        {
            get => SessionState.GetBool("TestsInProgress", false);
            set => SessionState.SetBool("TestsInProgress", value);
        }

        public static bool CodeCoverageEnabled
        {
            get => SessionState.GetBool("CodeCoverageEnabled", false);
            set => SessionState.SetBool("CodeCoverageEnabled", value);
        }

        [InitializeOnLoadMethod]
        public static void Run()
        {
            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _testRunnerApi.RegisterCallbacks(new TestCallbacks(() => _runPathToOutput));
            EditorCoroutine.Start(Coroutine());
        }

        private static IEnumerator Coroutine()
        {
            Log("Run coroutine");
            var textFileToListen = Environment.GetEnvironmentVariable("Stryker.Unity.PathToListen");

            if (string.IsNullOrEmpty(textFileToListen) || !File.Exists(textFileToListen))
            {
                yield break;
            }

            while (true)
            {
                var command = File.ReadAllText(textFileToListen);
                if (command == "exit")
                {
                    Log("Got exit command. Close unity");

                    EditorApplication.Exit(0);
                    yield break;
                }

                if (command == "reloadDomain")
                {
                    Log("Got reloadDomain command");

                    File.WriteAllText(textFileToListen, string.Empty);

                    EditorUtility.RequestScriptReload();
                    yield return new WaitForSeconds(0.1f);
                    Log("After RequestScriptReload");

                }
				else if (!string.IsNullOrWhiteSpace(command))
				{
                    Log("Got RequestToRunTests with command '" + command + "'");

                    var commands = command.Split(" ");

					_runPathToOutput = commands.LastOrDefault();
                    var testMode = (commands.FirstOrDefault() ?? string.Empty).Contains("playmode") ? TestMode.PlayMode : TestMode.EditMode;
                    string[] assemblyNames = null;
                    var enableCoverage = false;

                    if (commands.Length >= 3)
                    {
                        assemblyNames = commands[1].Split(";");
                    }

                    // Check if coverage is enabled via environment variable
                    var coverageEnabled = Environment.GetEnvironmentVariable("Stryker.Unity.EnableCoverage");
                    enableCoverage = !string.IsNullOrEmpty(coverageEnabled) && coverageEnabled.ToLower() == "true";

                    if (enableCoverage)
                    {
                        CodeCoverageEnabled = true;
                        _coverageOutputPath = Path.Combine(Path.GetDirectoryName(_runPathToOutput), "coverage");
                        // Ensure the directory exists
                        if (!Directory.Exists(_coverageOutputPath))
                        {
                            Directory.CreateDirectory(_coverageOutputPath);
                        }

                        // new UnityEditor.SettingsManagement.
                        // Configure coverage using EditorPrefs (the way the package stores settings)
                        EditorPrefs.SetInt("CodeCoverage_ResultsPathType", 1); // 1 = Custom path
                        EditorPrefs.SetString("CodeCoverage_ResultsPath", _coverageOutputPath);
                        EditorPrefs.SetBool("CodeCoverage_GenerateHTMLReport", false);
                        EditorPrefs.SetBool("CodeCoverage_GenerateBadge", false);
                        EditorPrefs.SetBool("CodeCoverage_GenerateAdditionalMetrics", true);

                        Coverage.ResetAll();
                        Coverage.enabled = true;
                        CodeCoverage.StartRecording();

                        Log("Code coverage enabled, results will be saved to: " + _coverageOutputPath);
                    }

                    if (EditorApplication.isPlaying && !TestsInProgress)
                    {
                        EditorApplication.ExitPlaymode();
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (!EditorApplication.isPlaying && TestsInProgress)
                    {
                        Log("playmode tests were finished and we need to save result. TODO RECHECK THAT RESULTS WERE SAVED");
                        //then playmode tests were finished and we need to save result
                        TestsInProgress = false;
                    }
                    else if (!EditorApplication.isPlaying)
                    {
                        Log("Start testRunnerApi.Execute with command '" + command + "'");

                        var executionSettings = new ExecutionSettings(new Filter() { testMode = testMode, assemblyNames = assemblyNames });
                        _testRunnerApi.Execute(executionSettings);
                        TestsInProgress = true;
                    }
                    else if (TestsInProgress)
                    {
                        Log("Playmode test is active");
                        //then play mode test is active.
                        TestsInProgress = true;
                    }
                    else
                    {
                        Log("Unexpected state");
                    }

                    while (TestsInProgress)
                    {
                        yield return new WaitForSeconds(0.1f);
                    }

                    Log("Clean command buffer");
                    File.WriteAllText(textFileToListen, string.Empty);
                }
				else
				{
					yield return new WaitForSeconds(0.1f);
				}
			}
		}

        public static void Log(string message) => Console.WriteLine($"[Stryker] [{DateTime.Now:HH:mm:ss.fff}] " + message);
    }

    public class EditorCoroutine
	{
		private IEnumerator routine;

		private EditorCoroutine(IEnumerator routine)
		{
			this.routine = routine;
		}

		public static EditorCoroutine Start(IEnumerator routine)
		{
			var coroutine = new EditorCoroutine(routine);
			coroutine.Start();
			return coroutine;
		}

		private void Start()
		{
			EditorApplication.update += Update;
		}

		public void Stop()
		{
			EditorApplication.update -= Update;
		}

		private void Update()
		{
			if (!routine.MoveNext())
			{
				Stop();
			}
		}
	}

	public class TestCallbacks : ICallbacks
	{
		private readonly Func<string> _getOutputFileName;

		public TestCallbacks(Func<string> getOutputFileName)
		{
			_getOutputFileName = getOutputFileName;
		}

		public void RunStarted(ITestAdaptor testsToRun)
		{
            RunTests.Log("Tests run started");
            RunTests.TestsInProgress = true;
		}

		public void RunFinished(ITestResultAdaptor result)
		{
			var sts = new XmlWriterSettings()
			{
				Indent = true,
			};
            RunTests.Log("Tests run finished");

			var outputFileName = _getOutputFileName.Invoke();
			using var writer = XmlWriter.Create(outputFileName, sts);
			writer.WriteStartDocument();

			result.ToXml().WriteTo(writer);
			writer.WriteEndDocument();
            RunTests.Log("Run finished and was write at " + outputFileName);

			RunTests.TestsInProgress = false;
            Coverage.enabled = false;
            CodeCoverage.StopRecording();

		}

		public void TestStarted(ITestAdaptor test)
		{
            RunTests.Log("Test started: " + test.FullName + " " + test.RunState);
		}

		public void TestFinished(ITestResultAdaptor result)
		{
            RunTests.Log("Test finished: " + result.FullName + " " + result.ResultState);

		}
	}
}
