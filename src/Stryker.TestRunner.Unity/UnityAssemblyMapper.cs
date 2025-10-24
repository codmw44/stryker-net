using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Stryker.Abstractions;

namespace Stryker.TestRunner.Unity;

public class UnityAssemblyMapper
{
    private readonly Dictionary<string, string> _fileToAssemblyCache = new();
    private readonly IFileSystem _fileSystem;
    private readonly AsmdefParser _asmdefParser;

    public UnityAssemblyMapper(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _asmdefParser = new AsmdefParser(_fileSystem);
    }

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
            var asmDefFiles = _fileSystem.Directory.GetFiles(directory, "*.asmdef", SearchOption.TopDirectoryOnly);
            if (asmDefFiles.Any())
            {
                var assemblyName = _asmdefParser.GetAssemblyName(asmDefFiles.First());
                _fileToAssemblyCache[normalizedPath] = assemblyName;
                return assemblyName;
            }

            directory = _fileSystem.Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}