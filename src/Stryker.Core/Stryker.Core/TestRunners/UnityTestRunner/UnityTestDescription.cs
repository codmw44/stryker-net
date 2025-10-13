using System;
using Stryker.Abstractions.Testing;

namespace Stryker.Core.TestRunners.UnityTestRunner;

public sealed class UnityTestDescription(ITestDescription description, ITestCase testCase) : IFrameworkTestDescription
{
    private TimeSpan _initialRunTime;

    public TestFrameworks Framework => TestFrameworks.NUnit;

    public ITestDescription Description { get; } = description;

    public TimeSpan InitialRunTime => _initialRunTime;

    public string Id => testCase.Id;

    public int NbSubCases => 1;

    public ITestCase Case => testCase;

    public void RegisterInitialTestResult(ITestResult result)
    {
        if (result != null)
        {
            _initialRunTime = result.Duration;
        }
    }

    public void AddSubCase()
    {
        // Not needed for Unity tests
    }

    public void ClearInitialResult()
    {
        _initialRunTime = TimeSpan.Zero;
    }
}
