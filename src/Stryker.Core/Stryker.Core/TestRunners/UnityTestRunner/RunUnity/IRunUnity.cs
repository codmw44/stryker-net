using System;
using System.Xml.Linq;
using Stryker.Abstractions.Options;

namespace Stryker.Core.TestRunners.UnityTestRunner.RunUnity;

public interface IRunUnity : IDisposable
{
    void ReloadDomain(IStrykerOptions strykerOptions, string projectPath,
        string additionalArgumentsForCli = null);

    XDocument RunTests(IStrykerOptions strykerOptions, string projectPath,
        string additionalArgumentsForCli = null, string helperNamespace = null, string activeMutantId = null);
}
