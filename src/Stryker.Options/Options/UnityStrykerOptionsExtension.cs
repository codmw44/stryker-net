using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Stryker.Abstractions.Options;

namespace Stryker.Core.Initialisation;

public static class UnityStrykerOptionsExtension
{
    public static bool IsUnityProject(this IStrykerOptions? options, IFileSystem fileSystem)
    {
        if (options == null)
            return false;

        var path = options.GetUnityProjectDirectory(fileSystem);
        return path.IsUnitProjectInternal(fileSystem);
    }

    public static bool IsUnityProject(this string basePath, IFileSystem fileSystem)
    {
        if (string.IsNullOrEmpty(basePath))
            return false;

        var path = basePath.GetUnityProjectDirectory(fileSystem);
        return path.IsUnitProjectInternal(fileSystem);
    }

    private static bool IsUnitProjectInternal(this string path, IFileSystem fileSystem)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        if (!fileSystem.File.Exists(path) && !fileSystem.Directory.Exists(path))
            return false;
        var directories = fileSystem.Directory.GetDirectories(path);
        return directories.Contains(fileSystem.Path.Combine(path, "Assets")) && directories.Contains(fileSystem.Path.Combine(path, "Packages")) && directories.Contains(fileSystem.Path.Combine(path, "ProjectSettings"));
    }

    public static string GetUnityProjectDirectory(this IStrykerOptions options, IFileSystem fileSystem)
    {
        return GetUnityProjectDirectory(options?.ProjectPath ?? options?.SolutionPath ?? string.Empty, fileSystem);
    }

    public static string GetUnityProjectDirectory(this string path, IFileSystem fileSystem)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (!fileSystem.File.Exists(path) && !fileSystem.Directory.Exists(path))
            return null;

        var isDirectory = fileSystem.File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        if (isDirectory)
        {
            return path;
        }
        else
        {
            return fileSystem.Directory.GetParent(path)!.FullName;
        }
    }
}
