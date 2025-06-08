using System.IO;
using System.Linq;
using Stryker.Abstractions.Options;

namespace Stryker.Core.Initialisation;

public static class UnityStrykerOptionsExtension
{
    public static bool IsUnityProject(this IStrykerOptions options)
    {
        if (options == null)
            return false;

        var path = options.GetUnityProjectDirectory();

        var directories = Directory.GetDirectories(path);
        return directories.Contains(Path.Combine(path, "Assets")) && directories.Contains(Path.Combine(path, "Packages")) && directories.Contains(Path.Combine(path, "ProjectSettings"));
    }

    public static string GetUnityProjectDirectory(this IStrykerOptions options)
    {
        if (options == null)
            return null;

        var path = options.ProjectPath ?? options.SolutionPath;
        var isDirectory = File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        if (isDirectory)
        {
            return path;
        }
        else
        {
            return Directory.GetParent(path)!.FullName;
        }
    }
}
