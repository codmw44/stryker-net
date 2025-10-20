using Stryker.Abstractions.Options;

namespace Stryker.TestRunner.Unity.RunUnity.UnityPath;

public interface IUnityPath
{
    /// <summary>
    /// Get path to unity instance for the project at path
    /// </summary>
    /// <exception cref="FailedToGetPathToUnityException"></exception>
    string GetPath(IStrykerOptions options);
}