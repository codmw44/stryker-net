using System.IO.Abstractions;
using System.Text.Json;
using Stryker.Abstractions.Options;

namespace Stryker.TestRunner.Unity;

public class AsmdefParser(IFileSystem fileSystem)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

    public UnityTestMode GetTestMode(string asmdefPath)
    {
        try
        {
            var json = _fileSystem.File.ReadAllText(asmdefPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("includePlatforms", out var includePlatforms) &&
                includePlatforms.ValueKind == JsonValueKind.Array)
            {
                foreach (var platform in includePlatforms.EnumerateArray())
                {
                    if (platform.GetString() == "Editor")
                    {
                        return UnityTestMode.EditMode;
                    }
                }
                return UnityTestMode.PlayMode;
            }

            return UnityTestMode.All;
        }
        catch
        {
            return UnityTestMode.All;
        }
    }

    public bool IsTestAssembly(string asmdefPath)
    {
        try
        {
            var json = _fileSystem.File.ReadAllText(asmdefPath);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("references", out var references) &&
                references.ValueKind == JsonValueKind.Array)
            {
                foreach (var reference in references.EnumerateArray())
                {
                    var refName = reference.GetString();
                    if (refName != null && (refName.Contains("UnityEngine.TestRunner") ||
                                          refName.Contains("UnityEditor.TestRunner")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public string GetAssemblyName(string asmdefPath)
    {
        //we should use name from asmdef instead of file name because file name can missmatch with assembly name

        var json = _fileSystem.File.ReadAllText(asmdefPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return root.TryGetProperty("name", out var name) ? name.GetString() : null;
    }
}
