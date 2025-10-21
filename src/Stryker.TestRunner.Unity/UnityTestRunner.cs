using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Buildalyzer;
using Microsoft.Extensions.Logging;
using Stryker.Abstractions;
using Stryker.Abstractions.Options;
using Stryker.Abstractions.ProjectComponents;
using Stryker.Abstractions.Testing;
using Stryker.TestRunner.Unity.RunUnity;
using Stryker.TestRunner.Results;
using Stryker.TestRunner.Tests;
using Stryker.TestRunner.VsTest;
using Stryker.Utilities.Buildalyzer;

namespace Stryker.TestRunner.Unity;

public class UnityTestRunner(
    IStrykerOptions strykerOptions,
    ILogger logger,
    IRunUnity runUnity) : ITestRunner
{
    private bool _firstMutationTestStarted;
    private TestRunResult _initialRunTestResult;
    private TestSet _testSet;
    private Dictionary<string, IFrameworkTestDescription> _testDescriptions = new();
    private readonly UnityTestAssemblyAnalyzer _assemblyAnalyzer = new();

    public bool DiscoverTests(IAnalyzerResult assembly)
    {
        if (_testSet == null)
        {
            var testResultsXml = RunTests(out var duration, testMode: strykerOptions.UnityTestMode);

            _assemblyAnalyzer.AnalyzeProject(assembly);

            _testSet = new TestSet();
            _testDescriptions = testResultsXml
                .Descendants("test-case")
                .Where(element => element.Attribute("result").Value is "Passed" or "Failed")
                .Select(element =>
                {
                    var id = element.Attribute("id").Value;
                    var name = element.Attribute("name").Value;
                    var fullname = element.Attribute("fullname").Value;

                    // Find the parent test-suite with type="Assembly" for this test case
                    var assemblyPath = element.Ancestors("test-suite")
                        .FirstOrDefault(ancestor => ancestor.Attribute("type")?.Value == "Assembly")
                        ?.Attribute("fullname")?.Value ?? string.Empty;

                    var codeFilePath = FindMatchesWithLineNumbers(strykerOptions.ProjectPath, name).FirstOrDefault();
                    var lineNumber = codeFilePath == null ? 1 : File.ReadAllLines(codeFilePath).TakeWhile(line => !line.Contains(name)).Count();
                    var testDescription = new TestDescription(id, name, fullname);
                    var testCase = new UnityTestCase(id, name, fullname, assemblyPath, codeFilePath, lineNumber);
                    return new UnityTestDescription(testDescription, testCase);
                })
                .DistinctBy(element => element.Id).ToDictionary(element => element.Id, IFrameworkTestDescription (element) => element);

            _testSet.RegisterTests(_testDescriptions.Values.Select(description => description.Description));

            _initialRunTestResult = new TestRunResult(_testDescriptions.Values, GetPassedTests(testResultsXml),
                GetFailedTests(testResultsXml),
                GetTimeoutTestGuidsList(), GetErrorMessages(testResultsXml), GetMessages(testResultsXml), duration);
        }

        _assemblyAnalyzer.AnalyzeProject(assembly);

        return _assemblyAnalyzer.TryGetTestAssemblyInfo(assembly.GetAssemblyName(), out var testAssemblyInfo)
               && testAssemblyInfo.SupportedModes.HasFlag(strykerOptions.UnityTestMode);
    }
    private static IEnumerable<string> FindMatchesWithLineNumbers(
        string rootPath, string searchText, string filePattern = "*.cs")
    {
        var files = Directory.EnumerateFiles(rootPath, filePattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (line.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    yield return file;
                }
            }
        }
    }
    public ITestSet GetTests(IProjectAndTests project)
    {
        if (_testSet == null)
        {
            return new TestSet();
        }

        var projectTestAssemblies = project.GetTestAssemblies();
        if (!projectTestAssemblies.Any())
        {
            return _testSet;
        }

        var modeCompatibleAssemblies = projectTestAssemblies;
        if (!modeCompatibleAssemblies.Any())
        {
            return new TestSet();
        }

        var filteredTestSet = new TestSet();
        var filteredTestDescriptions = _testDescriptions.Values
            .Where(testDesc => modeCompatibleAssemblies.Contains(testDesc.Case.Source))
            .ToList();

        filteredTestSet.RegisterTests(filteredTestDescriptions.Select(desc => desc.Description));
        return filteredTestSet;
    }

    public ITestRunResult InitialTest(IProjectAndTests project)
    {
        if (_initialRunTestResult == null)
        {
            return new TestRunResult(new List<IFrameworkTestDescription>(), TestIdentifierList.NoTest(),
                TestIdentifierList.NoTest(), TestIdentifierList.NoTest(), string.Empty, new List<string>(), TimeSpan.Zero);
        }

        var projectTestAssemblies = project.GetTestAssemblies();
        if (!projectTestAssemblies.Any())
        {
            return _initialRunTestResult;
        }

        var modeCompatibleAssemblies = projectTestAssemblies;
        if (!modeCompatibleAssemblies.Any())
        {
            return new TestRunResult(new List<IFrameworkTestDescription>(), TestIdentifierList.NoTest(),
                TestIdentifierList.NoTest(), TestIdentifierList.NoTest(), string.Empty, new List<string>(), TimeSpan.Zero);
        }

        var filteredTestDescriptions = _testDescriptions.Values
            .Where(testDesc => modeCompatibleAssemblies.Contains(testDesc.Case.Source))
            .ToList();

        // Filter passed and failed tests to only include those from the project
        var filteredPassedTests = FilterTestIdentifiers(_initialRunTestResult.ExecutedTests, modeCompatibleAssemblies);
        var filteredFailedTests = FilterTestIdentifiers(_initialRunTestResult.FailingTests, modeCompatibleAssemblies);

        return new TestRunResult(filteredTestDescriptions, filteredPassedTests, filteredFailedTests,
            _initialRunTestResult.TimedOutTests, _initialRunTestResult.ResultMessage,
            _initialRunTestResult.Messages, _initialRunTestResult.Duration);
    }

    public IEnumerable<ICoverageRunResult> CaptureCoverage(IProjectAndTests project) => [];

    public void Dispose()
    {
        // Required to avoid not relevant experience for the developer when open the project after Stryker run
        runUnity.RemoveScriptAssembliesDirectory(strykerOptions.WorkingDirectory);
        runUnity.Dispose();
        if (!strykerOptions.DevMode)
        {
            CleanupTestArtifacts();
        }
    }

    public ITestRunResult TestMultipleMutants(IProjectAndTests project, ITimeoutValueCalculator timeoutCalc, IReadOnlyList<IMutant> mutants, ITestRunner.TestUpdateHandler update)
    {
        if (!_firstMutationTestStarted)
            //rerun unity to apply modifications and reload domain
            runUnity.ReloadDomain(strykerOptions, strykerOptions.WorkingDirectory);

        // Determine target test assemblies based on mutants and test mode
        IEnumerable<string> targetAssemblies = null;
        var skipRun = false;

        var relevantTestAssemblies = _assemblyAnalyzer.GetFilteredTestAssemblies(mutants, strykerOptions.UnityTestMode);
        targetAssemblies = relevantTestAssemblies.Select(ta => ta.AssemblyName).ToList();

        var testModeFromFilteredAssemblies = UnityTestMode.None;
        foreach (var unityTestAssemblyInfo in relevantTestAssemblies)
            testModeFromFilteredAssemblies |= unityTestAssemblyInfo.SupportedModes;
        if (testModeFromFilteredAssemblies.HasFlag(UnityTestMode.EditMode) && !strykerOptions.UnityTestMode.HasFlag(UnityTestMode.EditMode))
            testModeFromFilteredAssemblies &= ~UnityTestMode.EditMode;
        if (testModeFromFilteredAssemblies.HasFlag(UnityTestMode.PlayMode) && !strykerOptions.UnityTestMode.HasFlag(UnityTestMode.PlayMode))
            testModeFromFilteredAssemblies &= ~UnityTestMode.PlayMode;

        if (targetAssemblies.Any())
        {
            logger.LogDebug("Running tests for assemblies: {0}", string.Join(", ", targetAssemblies));
        }
        else
        {
            skipRun = true;
            logger.LogDebug("No relevant test assemblies found for mutants, skip run");
        }

        var duration = TimeSpan.Zero;
        var testResultsXml = !skipRun
            ? RunTests(out duration, mutants.Single().Id.ToString(), project.HelperNamespace, targetAssemblies, testModeFromFilteredAssemblies)
            : new XDocument();

        var passedTests = GetPassedTests(testResultsXml);
        var failedTests = GetFailedTests(testResultsXml);
        var remainingMutants =
            update?.Invoke(mutants, failedTests, TestIdentifierList.EveryTest(), GetTimeoutTestGuidsList());

        if (remainingMutants == false)
            logger.LogDebug("Each mutant's fate has been established, we can stop.");

        _firstMutationTestStarted = true;

        return new TestRunResult(_testDescriptions.Values, passedTests, failedTests,
            GetTimeoutTestGuidsList(), GetErrorMessages(testResultsXml), GetMessages(testResultsXml), duration);
    }

    private XDocument RunTests(out TimeSpan duration, string activeMutantId = null, string helperNamespace = null, IEnumerable<string> targetTestAssemblies = null, UnityTestMode testMode = UnityTestMode.All)
    {
        var startTime = DateTime.UtcNow;

        var xmlTestResults = runUnity.RunTests(strykerOptions, strykerOptions.WorkingDirectory,
            activeMutantId: activeMutantId, helperNamespace: helperNamespace, targetTestAssemblies: targetTestAssemblies, testMode: testMode);

        duration = DateTime.UtcNow - startTime;
        return xmlTestResults;
    }

    private static ITestIdentifiers GetTimeoutTestGuidsList() =>
        //NUnit result has no result of time-out https://docs.nunit.org/articles/nunit/technical-notes/usage/Test-Result-XML-Format.html#test-case
        TestIdentifierList.NoTest();

    private ITestIdentifiers GetPassedTests(XContainer testResultsXml)
    {
        var ids = testResultsXml.Descendants("test-case")
            .Where(element => element.Attribute("result").Value == "Passed")
            .Select(element => element.Attribute("id").Value);
        var passedTests = new TestIdentifierList(_testSet.Extract(ids));

        return passedTests;
    }

    private ITestIdentifiers GetFailedTests(XContainer testResultsXml)
    {
        var ids = testResultsXml.Descendants("test-case")
            .Where(element => element.Attribute("result").Value == "Failed")
            .Select(element => element.Attribute("id").Value);
        var failedTests = new TestIdentifierList(_testSet.Extract(ids));

        return failedTests;
    }

    private static string GetErrorMessages(XContainer testResultsXml)
    {
        var errorMessages = testResultsXml.Descendants("test-case")
            .Where(element => element.Attribute("result")?.Value == "Failed")
            .Select(testCase =>
            {
                var testName = testCase.Attribute("fullname")?.Value ?? testCase.Attribute("name")?.Value ?? "Unknown Test";
                var failure = testCase.Element("failure");
                var message = failure?.Element("message")?.Value ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"{testName}{Environment.NewLine}{Environment.NewLine}{message}";
                }
                return string.Empty;
            })
            .Where(msg => !string.IsNullOrWhiteSpace(msg));

        return string.Join(Environment.NewLine, errorMessages);
    }

    private static IEnumerable<string> GetMessages(XContainer testResultsXml)
    {
        return testResultsXml.Descendants("test-case")
            .Select(testCase =>
            {
                var testName = testCase.Attribute("fullname")?.Value ?? testCase.Attribute("name")?.Value ?? "Unknown Test";
                var output = testCase.Element("output")?.Value ?? string.Empty;
                var failure = testCase.Element("failure");

                var messages = new List<string>();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    messages.Add(output);
                }

                if (failure != null)
                {
                    var message = failure.Element("message")?.Value;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        messages.Add(message);
                    }
                }

                if (messages.Count > 0)
                {
                    return $"{testName}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, messages)}";
                }

                return string.Empty;
            })
            .Where(msg => !string.IsNullOrWhiteSpace(msg));
    }

    private ITestIdentifiers FilterTestIdentifiers(ITestIdentifiers testIdentifiers, IReadOnlyList<string> projectTestAssemblies)
    {
        if (testIdentifiers == null || testIdentifiers.IsEmpty)
        {
            return TestIdentifierList.NoTest();
        }

        // If it's "EveryTest", we need to check if all tests in the project are included
        if (testIdentifiers.IsEveryTest)
        {
            // Check if all project tests are in our test set
            var projectTestIds = _testDescriptions.Values
                .Where(testDesc => projectTestAssemblies.Contains(testDesc.Case.Source))
                .Select(testDesc => testDesc.Id)
                .ToList();

            return new TestIdentifierList(projectTestIds);
        }

        // For specific test lists, filter to only include tests from the project
        var filteredIds = testIdentifiers.GetIdentifiers()
            .Where(id => _testDescriptions.TryGetValue(id, out var testDesc) &&
                        projectTestAssemblies.Contains(testDesc.Case.Source))
            .ToList();

        return new TestIdentifierList(filteredIds);
    }

    private void CleanupTestArtifacts()
    {
        try
        {
            var testResultsFiles = System.IO.Directory.GetFiles(strykerOptions.OutputPath, "test_results*", System.IO.SearchOption.TopDirectoryOnly);
            foreach (var file in testResultsFiles)
            {
                logger.LogDebug("Removing test result file: {0}", file);
                System.IO.File.Delete(file);
            }

            var unityListensPath = System.IO.Path.Combine(strykerOptions.OutputPath, "UnityListens.txt");
            if (System.IO.File.Exists(unityListensPath))
            {
                logger.LogDebug("Removing UnityListens.txt file: {0}", unityListensPath);
                System.IO.File.Delete(unityListensPath);
            }

            var activeMutantFiles = System.IO.Directory.GetFiles(strykerOptions.OutputPath, "Stryker*.txt", System.IO.SearchOption.TopDirectoryOnly);
            foreach (var file in activeMutantFiles)
            {
                logger.LogDebug("Removing active mutant file: {0}", file);
                System.IO.File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cleanup test artifacts");
        }
    }
}
