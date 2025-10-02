using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Stryker.Abstractions;
using Stryker.Abstractions.Options;
using Stryker.Abstractions.Testing;
using Stryker.Core.TestRunners.UnityTestRunner.RunUnity;
using Stryker.TestRunner.Results;
using Stryker.TestRunner.Tests;
using Stryker.TestRunner.VsTest;

namespace Stryker.Core.TestRunners.UnityTestRunner;

public class UnityTestRunner(
    IStrykerOptions strykerOptions,
    ILogger logger,
    IRunUnity runUnity) : ITestRunner
{
    private bool _firstMutationTestStarted;
    private TestRunResult _initialRunTestResult;
    private TestSet _testSet;

    public bool DiscoverTests(string assembly)
    {
        if (_testSet != null) return true;

        //Required to have fresh not modified dlls
        runUnity.RemoveScriptAssembliesDirectory(strykerOptions.WorkingDirectory);

        var testResultsXml = RunTests(out var duration);

        //todo add valid test file path. It used for checking diff by git and ut of the box dont provides in xml
        _testSet = new TestSet();
        _testSet.RegisterTests(testResultsXml
            .Descendants("test-case")
            .Where(element => element.Attribute("result").Value is "Passed" or "Failed")
            .Select(element => new TestDescription(element.Attribute("id").Value,
                element.Attribute("name").Value, element.Attribute("fullname").Value)));

        _initialRunTestResult = new TestRunResult(Enumerable.Empty<VsTestDescription>(), GetPassedTests(testResultsXml),
            GetFailedTests(testResultsXml),
            GetTimeoutTestGuidsList(), string.Empty, Enumerable.Empty<string>(), duration);
        return true;
    }

    public ITestSet GetTests(IProjectAndTests project) => _testSet;

    public ITestRunResult InitialTest(IProjectAndTests project) => _initialRunTestResult;

    public IEnumerable<ICoverageRunResult> CaptureCoverage(IProjectAndTests project) => [];

    public void Dispose()
    {
        // Required to avoid not relevant experience for the developer when open the project after Stryker run
        runUnity.RemoveScriptAssembliesDirectory(strykerOptions.WorkingDirectory);
        runUnity.Dispose();
    }

    //todo remove all modifications
    //todo remove installed package
    public ITestRunResult TestMultipleMutants(IProjectAndTests project, ITimeoutValueCalculator timeoutCalc, IReadOnlyList<IMutant> mutants, ITestRunner.TestUpdateHandler update)
    {
        if (!_firstMutationTestStarted)
            //rerun unity to apply modifications and reload domain
            runUnity.ReloadDomain(strykerOptions, strykerOptions.WorkingDirectory);

        var testResultsXml = RunTests(out var duration, mutants.Single().Id.ToString(), project.HelperNamespace);

        var passedTests = GetPassedTests(testResultsXml);
        var failedTests = GetFailedTests(testResultsXml);
        var remainingMutants =
            update?.Invoke(mutants, failedTests, TestIdentifierList.EveryTest(), GetTimeoutTestGuidsList());

        if (remainingMutants == false)
            // all mutants status have been resolved, we can stop
            logger.LogDebug("Each mutant's fate has been established, we can stop.");

        _firstMutationTestStarted = true;

        return new TestRunResult(Enumerable.Empty<VsTestDescription>(), passedTests, failedTests,
            GetTimeoutTestGuidsList(), string.Empty, Enumerable.Empty<string>(), duration);
    }

    private XDocument RunTests(out TimeSpan duration, string activeMutantId = null, string helperNamespace = null)
    {
        var startTime = DateTime.UtcNow;

        var xmlTestResults = runUnity.RunTests(strykerOptions, strykerOptions.WorkingDirectory,
            activeMutantId: activeMutantId, helperNamespace: helperNamespace);

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
        var passedTests = ids.Count() == _testSet.Count
            ? TestIdentifierList.EveryTest()
            : new TestIdentifierList(_testSet.Extract(ids));

        return passedTests;
    }

    private ITestIdentifiers GetFailedTests(XContainer testResultsXml)
    {
        var ids = testResultsXml.Descendants("test-case")
            .Where(element => element.Attribute("result").Value == "Failed")
            .Select(element => element.Attribute("id").Value);
        var failedTests = ids.Count() == _testSet.Count
            ? TestIdentifierList.EveryTest()
            : new TestIdentifierList(_testSet.Extract(ids));

        return failedTests;
    }
}
