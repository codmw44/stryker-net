using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.Abstractions.Exceptions;
using Stryker.Core.Options.Inputs;

namespace Stryker.Core.UnitTest.Options.Inputs;

[TestClass]
public class PathToUnityInputTests : TestBase
{
    private MockFileSystem _fileSystem;

    [TestInitialize]
    public void Setup()
    {
        _fileSystem = new MockFileSystem();
    }

    [TestMethod]
    public void ShouldHaveHelpText()
    {
        var target = new PathToUnityInput();
        target.HelpText.ShouldBe("Override path to Unity instance for running tests");
    }

    [TestMethod]
    public void ShouldHaveCorrectDefaultValue()
    {
        var target = new PathToUnityInput();
        target.Default.ShouldBeNull();
    }

    [TestMethod]
    public void ShouldReturnNullWhenSuppliedInputIsNull()
    {
        var target = new PathToUnityInput { SuppliedInput = null };
        var result = target.Validate(_fileSystem);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void ShouldThrowExceptionWhenSuppliedInputIsWhitespace()
    {
        var target = new PathToUnityInput { SuppliedInput = "   " };

        var ex = Should.Throw<InputException>(() => target.Validate(_fileSystem));
        ex.Message.ShouldBe("Path to unity cannot be empty");
    }

    [TestMethod]
    public void ShouldThrowExceptionWhenFileDoesNotExist()
    {
        var target = new PathToUnityInput { SuppliedInput = "C:\\Unity\\Unity.exe" };

        var ex = Should.Throw<InputException>(() => target.Validate(_fileSystem));
        ex.Message.ShouldBe("File on this path doesn't exist 'C:\\Unity\\Unity.exe'");
    }

    [TestMethod]
    public void ShouldReturnPathWhenFileExists()
    {
        var unityPath = "C:\\Unity\\Unity.exe";
        _fileSystem.AddFile(unityPath, "fake unity executable");

        var target = new PathToUnityInput { SuppliedInput = unityPath };
        var result = target.Validate(_fileSystem);

        result.ShouldBe(unityPath);
    }

    [TestMethod]
    public void ShouldHandleRelativePaths()
    {
        var unityPath = "Unity.exe";
        _fileSystem.AddFile(unityPath, "fake unity executable");

        var target = new PathToUnityInput { SuppliedInput = unityPath };
        var result = target.Validate(_fileSystem);

        result.ShouldBe(unityPath);
    }

    [TestMethod]
    public void ShouldHandlePathsWithSpaces()
    {
        var unityPath = "C:\\Program Files\\Unity\\Unity.exe";
        _fileSystem.AddFile(unityPath, "fake unity executable");

        var target = new PathToUnityInput { SuppliedInput = unityPath };
        var result = target.Validate(_fileSystem);

        result.ShouldBe(unityPath);
    }

    [TestMethod]
    public void ShouldHandleUnixStylePaths()
    {
        var unityPath = "/Applications/Unity/Unity.app/Contents/MacOS/Unity";
        _fileSystem.AddFile(unityPath, "fake unity executable");

        var target = new PathToUnityInput { SuppliedInput = unityPath };
        var result = target.Validate(_fileSystem);

        result.ShouldBe(unityPath);
    }
}
