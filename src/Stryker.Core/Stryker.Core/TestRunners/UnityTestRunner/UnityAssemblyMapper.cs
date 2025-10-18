using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stryker.Abstractions;

namespace Stryker.Core.TestRunners.UnityTestRunner;

public class UnityAssemblyMapper
{
    private readonly Dictionary<string, string> _fileToAssemblyCache = new();

    public string GetAssemblyForMutant(IMutant mutant)
    {
        if (mutant?.Mutation.OriginalNode.SyntaxTree.FilePath == null)
        {
            return null;
        }

        var filePath = mutant.Mutation.OriginalNode.SyntaxTree.FilePath;
        var normalizedPath = Path.GetFullPath(filePath);

        if (_fileToAssemblyCache.TryGetValue(normalizedPath, out var cachedAssembly))
        {
            return cachedAssembly;
        }

        var directory = Path.GetDirectoryName(normalizedPath);
        while (!string.IsNullOrEmpty(directory))
        {
            var asmDefFiles = Directory.GetFiles(directory, "*.asmdef", SearchOption.TopDirectoryOnly);
            if (asmDefFiles.Any())
            {
                var assemblyName = AsmdefParser.GetAssemblyName(asmDefFiles.First());
                _fileToAssemblyCache[normalizedPath] = assemblyName;
                return assemblyName;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
