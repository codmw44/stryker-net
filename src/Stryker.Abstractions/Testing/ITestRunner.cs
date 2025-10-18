using System;
using System.Collections.Generic;
using Buildalyzer;

namespace Stryker.Abstractions.Testing;

public interface ITestRunner : IDisposable
{
    public delegate bool TestUpdateHandler(IReadOnlyList<IMutant> testedMutants,
       ITestIdentifiers failedTests,
       ITestIdentifiers ranTests,
       ITestIdentifiers timedOutTests);

    bool DiscoverTests(IAnalyzerResult assembly);

    ITestSet GetTests(IProjectAndTests project);

    ITestRunResult InitialTest(IProjectAndTests project);

    IEnumerable<ICoverageRunResult> CaptureCoverage(IProjectAndTests project);

    ITestRunResult TestMultipleMutants(IProjectAndTests project, ITimeoutValueCalculator timeoutCalc, IReadOnlyList<IMutant> mutants, TestUpdateHandler update);
}
