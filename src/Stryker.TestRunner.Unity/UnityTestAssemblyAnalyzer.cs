using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Buildalyzer;
using Stryker.Abstractions;
using Stryker.Abstractions.Options;
using Stryker.Utilities.Buildalyzer;

namespace Stryker.TestRunner.Unity;

public class UnityTestAssemblyInfo
{
    public string AssemblyName { get; set; }
    public string ProjectPath { get; set; }
    public UnityTestMode SupportedModes { get; set; }
    public List<string> ReferencedAssemblies { get; set; } = new();
}

public class UnityTestAssemblyAnalyzer
{
    private readonly Dictionary<string, UnityTestAssemblyInfo> _testAssemblies = new();
    private readonly Dictionary<string, List<string>> _assemblyReferences = new();
    private readonly UnityAssemblyMapper _unityAssemblyMapper;
    private readonly AsmdefParser _asmdefParser;

    public UnityTestAssemblyAnalyzer(IFileSystem fileSystem)
    {
        _unityAssemblyMapper = new UnityAssemblyMapper(fileSystem);
        _asmdefParser = new AsmdefParser(fileSystem);
    }

    public void AnalyzeSolution(IProjectAndTests project)
    {
        foreach (var analyzerResult in project.TestProjectsInfo.AnalyzerResults)
        {
            AnalyzeProject(analyzerResult);
        }
    }

    public void AnalyzeProject(IAnalyzerResult analyzerResult)
    {
        if (!IsUnityTestProject(analyzerResult))
        {
            return;
        }

        var assemblyName = analyzerResult.GetAssemblyName();
        var referencedAssemblies = GetReferencedAssemblies(analyzerResult);

        var testAssemblyInfo = new UnityTestAssemblyInfo
        {
            AssemblyName = assemblyName,
            ProjectPath = analyzerResult.ProjectFilePath,
            SupportedModes = DetermineTestModes(analyzerResult),
            ReferencedAssemblies = referencedAssemblies
        };

        _testAssemblies[assemblyName] = testAssemblyInfo;
        _assemblyReferences[assemblyName] = referencedAssemblies;
    }

    public bool TryGetTestAssemblyInfo(string assemblyName, out UnityTestAssemblyInfo testAssemblyInfo) => _testAssemblies.TryGetValue(assemblyName, out testAssemblyInfo);

    private UnityTestMode DetermineTestModes(IAnalyzerResult analyzerResult)
    {
        var supportedModes = UnityTestMode.All;

        try
        {
            // Look for .asmdef files referenced in the project
            // These appear as <None Include="**/*.asmdef" /> entries in csproj files
            var items = analyzerResult.Items;
            if (items.TryGetValue("None", out var noneItems))
            {
                var asmdefFiles = noneItems
                    .Where(item => item.ItemSpec.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (asmdefFiles.Any())
                {
                    var projectDir = Path.GetDirectoryName(analyzerResult.ProjectFilePath);
                    var foundTestAssembly = false;
                    supportedModes = UnityTestMode.All; // Start with All, narrow down based on findings

                    foreach (var asmdefItem in asmdefFiles)
                    {
                        var asmdefPath = Path.IsPathRooted(asmdefItem.ItemSpec)
                            ? asmdefItem.ItemSpec
                            : Path.Combine(projectDir ?? "", asmdefItem.ItemSpec);

                        if (_asmdefParser.IsTestAssembly(asmdefPath))
                        {
                            foundTestAssembly = true;
                            var testMode = _asmdefParser.GetTestMode(asmdefPath);

                            if (testMode == UnityTestMode.EditMode)
                            {
                                // If we find any EditMode-only test assembly, this project is EditMode only
                                supportedModes = UnityTestMode.EditMode;
                                break;
                            }
                            else if (testMode == UnityTestMode.PlayMode)
                            {
                                // If we haven't found EditMode yet, this could be PlayMode only
                                if (supportedModes == UnityTestMode.All)
                                {
                                    supportedModes = UnityTestMode.PlayMode;
                                }
                            }
                            // If testMode is All, keep supportedModes as All
                        }
                    }

                    // If we found test assemblies but none specified modes, default to All
                    if (foundTestAssembly && supportedModes == UnityTestMode.All)
                    {
                        supportedModes = UnityTestMode.All;
                    }
                    // If no test assemblies found, but we have asmdef files, assume All modes
                    else if (!foundTestAssembly)
                    {
                        supportedModes = UnityTestMode.All;
                    }
                }
            }
        }
        catch
        {
            // If there's any error parsing, default to All modes
            supportedModes = UnityTestMode.All;
        }

        return supportedModes;
    }

    private List<string> GetReferencedAssemblies(IAnalyzerResult analyzerResult)
    {
        var referencedAssemblies = new List<string>();

        // Add project references
        if (analyzerResult.Items.TryGetValue("ProjectReference", out var projectRefs))
        {
            referencedAssemblies.AddRange(projectRefs
                .Select(pr => Path.GetFileNameWithoutExtension(pr.ItemSpec))
                .Where(name => !string.IsNullOrEmpty(name)));
        }

        // Add assembly references
        referencedAssemblies.AddRange(analyzerResult.References
            .Select(r => Path.GetFileNameWithoutExtension(r))
            .Where(name => !string.IsNullOrEmpty(name)));

        return referencedAssemblies.Distinct().ToList();
    }

    public IEnumerable<UnityTestAssemblyInfo> GetTestAssembliesForMutants(IEnumerable<IMutant> mutants)
    {
        var relevantAssemblies = new HashSet<string>();

        foreach (var mutant in mutants)
        {
            // Get the assembly that contains this mutant
            var mutantAssembly = _unityAssemblyMapper?.GetAssemblyForMutant(mutant);
            if (!string.IsNullOrEmpty(mutantAssembly))
            {
                // Find test assemblies that reference this assembly
                var testAssemblies = _testAssemblies.Values
                    .Where(ta => ta.ReferencedAssemblies.Contains(mutantAssembly))
                    .Select(ta => ta.AssemblyName);

                foreach (var testAssembly in testAssemblies)
                {
                    relevantAssemblies.Add(testAssembly);
                }
            }
        }

        return _testAssemblies.Values
            .Where(ta => relevantAssemblies.Contains(ta.AssemblyName))
            .ToList();
    }

    public IEnumerable<UnityTestAssemblyInfo> GetTestAssembliesForMode(UnityTestMode requestedMode)
    {
        return _testAssemblies.Values
            .Where(ta => (ta.SupportedModes & requestedMode) != 0)
            .ToList();
    }

    public IEnumerable<UnityTestAssemblyInfo> GetFilteredTestAssemblies(IEnumerable<IMutant> mutants, UnityTestMode requestedMode)
    {
        var mutantRelevantAssemblies = GetTestAssembliesForMutants(mutants);
        var modeCompatibleAssemblies = GetTestAssembliesForMode(requestedMode);

        return mutantRelevantAssemblies.Intersect(modeCompatibleAssemblies,
            new TestAssemblyComparer()).ToList();
    }


    public bool IsUnityTestProject(IAnalyzerResult analyzerResult)
    {
        try
        {
            // Look for .asmdef files referenced in the project
            // These appear as <None Include="**/*.asmdef" /> entries in csproj files
            var items = analyzerResult.Items;
            if (items.TryGetValue("None", out var noneItems))
            {
                var asmdefFiles = noneItems
                    .Where(item => item.ItemSpec.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (asmdefFiles.Any())
                {
                    var projectDir = Path.GetDirectoryName(analyzerResult.ProjectFilePath);

                    foreach (var asmdefItem in asmdefFiles)
                    {
                        var asmdefPath = Path.IsPathRooted(asmdefItem.ItemSpec)
                            ? asmdefItem.ItemSpec
                            : Path.Combine(projectDir ?? "", asmdefItem.ItemSpec);

                        // Use AsmdefParser to check if this is a test assembly
                        if (_asmdefParser.IsTestAssembly(asmdefPath))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // If there's any error parsing, fall back to false
        }

        return false;
    }

    private class TestAssemblyComparer : IEqualityComparer<UnityTestAssemblyInfo>
    {
        public bool Equals(UnityTestAssemblyInfo x, UnityTestAssemblyInfo y)
        {
            return string.Equals(x?.AssemblyName, y?.AssemblyName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(UnityTestAssemblyInfo obj)
        {
            return obj?.AssemblyName?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0;
        }
    }
}
