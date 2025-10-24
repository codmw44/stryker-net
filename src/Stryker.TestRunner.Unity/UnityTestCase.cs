using System;
using Stryker.Abstractions.Testing;

namespace Stryker.TestRunner.Unity;

public sealed class UnityTestCase : ITestCase
{
    public UnityTestCase(string id, string name, string fullyQualifiedName, string source, string codeFilePath, int lineNumber)
    {
        Id = id;
        Guid = System.Guid.TryParse(id, out var guid) ? guid : System.Guid.Empty;
        Name = name;
        FullyQualifiedName = fullyQualifiedName;
        Source = source;
        CodeFilePath = codeFilePath;
        LineNumber = lineNumber;
    }

    public string Id { get; }

    public Guid Guid { get; }

    public string Name { get; }

    public string Source { get; }

    public string CodeFilePath { get; }

    public string FullyQualifiedName { get; }

    public Uri Uri { get; }

    public int LineNumber { get; }
}
