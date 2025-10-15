using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildalyzer;
using Stryker.Abstractions;
using Stryker.Core.ProjectComponents.SourceProjects;
using Stryker.Utilities.Buildalyzer;

namespace Stryker.Core.TestRunners.UnityTestRunner;

public class MutantAssemblyMapper
{
    private readonly Dictionary<string, string> _fileToAssemblyMap = new();

    public void BuildMapping(IProjectAndTests project)
    {
        // Build mapping from file paths to assembly names by reading csproj files
        // and getting <Compile Include="..." /> records

        // Process main source project (non-test assemblies)
        if (project is SourceProjectInfo sourceProject && sourceProject.AnalyzerResult != null)
        {
            ProcessAnalyzerResult(sourceProject.AnalyzerResult);
        }

        // Process test projects
        foreach (var analyzerResult in project.TestProjectsInfo.AnalyzerResults)
        {
            ProcessAnalyzerResult(analyzerResult);
        }
    }

    private void ProcessAnalyzerResult(IAnalyzerResult analyzerResult)
    {
        var assemblyName = analyzerResult.GetAssemblyName();
        if (!string.IsNullOrEmpty(assemblyName))
        {
            // Get compile items directly from the analyzer result
            // These correspond to <Compile Include="..." /> entries in csproj
            if (analyzerResult.Items.TryGetValue("Compile", out var compileItems))
            {
                var projectDir = Path.GetDirectoryName(analyzerResult.ProjectFilePath);

                foreach (var item in compileItems)
                {
                    var filePath = item.ItemSpec;
                    if (!string.IsNullOrEmpty(filePath) && filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert relative paths to absolute paths
                        var absolutePath = Path.IsPathRooted(filePath)
                            ? filePath
                            : Path.Combine(projectDir ?? "", filePath);

                        var normalizedPath = Path.GetFullPath(absolutePath);
                        _fileToAssemblyMap[normalizedPath] = assemblyName;
                    }
                }
            }
        }
    }

    public string GetAssemblyForMutant(IMutant mutant)
    {
        if (mutant?.Mutation.OriginalNode.SyntaxTree.FilePath == null)
        {
            return null;
        }

        var filePath = mutant.Mutation.OriginalNode.SyntaxTree.FilePath;
        var normalizedPath = Path.GetFullPath(filePath);

        // Try exact match first
        if (_fileToAssemblyMap.TryGetValue(normalizedPath, out var assembly))
        {
            return assembly;
        }

        // Try to find the closest match by comparing directory paths
        var directory = Path.GetDirectoryName(normalizedPath);
        while (!string.IsNullOrEmpty(directory))
        {
            var matchingEntries = _fileToAssemblyMap
                .Where(kvp => kvp.Key.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingEntries.Count == 1)
            {
                return matchingEntries.First().Value;
            }

            if (matchingEntries.Count > 1)
            {
                // Multiple matches, try to find the most specific one
                var bestMatch = matchingEntries
                    .OrderByDescending(kvp => kvp.Key.Length)
                    .FirstOrDefault();

                if (bestMatch.Key != null)
                {
                    return bestMatch.Value;
                }
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

}
