using System;

namespace Stryker.TestRunner.Unity.RunUnity.UnityPath;

public class FailedToGetPathToUnityException : Exception
{
    public FailedToGetPathToUnityException(string message) : base(message)
    {
    }
}