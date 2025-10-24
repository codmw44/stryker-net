using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using Stryker.Abstractions.Options;
using Stryker.Core.Initialisation;
using System.IO.Abstractions.TestingHelpers;

namespace Stryker.Core.UnitTest.Initialisation;

[TestClass]
public class UnityStrykerOptionsExtensionTests : TestBase
{
    private MockFileSystem _fileSystem;
    private Mock<IStrykerOptions> _strykerOptionsMock;

    [TestInitialize]
    public void Setup()
    {
        _fileSystem = new MockFileSystem();
        _strykerOptionsMock = new Mock<IStrykerOptions>();
    }

    [TestMethod]
    public void IsUnityProject_WithNullOptions_ShouldReturnFalse()
    {
        IStrykerOptions options = null;

        var result = options.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithNullProjectPath_ShouldReturnFalse()
    {
        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns((string)null);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithEmptyProjectPath_ShouldReturnFalse()
    {
        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns("");
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithNonExistentPath_ShouldReturnFalse()
    {
        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns("/NonExistentPath");
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithValidUnityProject_ShouldReturnTrue()
    {
        var projectPath = "/UnityProject";
        SetupUnityProjectStructure(projectPath);

        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns(projectPath);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsUnityProject_WithSolutionPath_ShouldUseSolutionPath()
    {
        var solutionPath = "/UnityProject/MyProject.sln";
        var projectPath = "/UnityProject";
        _fileSystem.AddDirectory(projectPath);
        _fileSystem.AddFile(solutionPath, new MockFileData(""));
        SetupUnityProjectStructure(projectPath);

        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns((string)null);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns(solutionPath);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsUnityProject_WithMissingAssetsFolder_ShouldReturnFalse()
    {
        var projectPath = "/NotUnityProject";
        _fileSystem.AddDirectory(projectPath);
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Packages"));
        _fileSystem.AddDirectory(Path.Combine(projectPath, "ProjectSettings"));
        // Missing Assets folder

        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns(projectPath);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithMissingPackagesFolder_ShouldReturnFalse()
    {
        var projectPath = "/NotUnityProject";
        _fileSystem.AddDirectory(projectPath);
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Assets"));
        _fileSystem.AddDirectory(Path.Combine(projectPath, "ProjectSettings"));
        // Missing Packages folder

        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns(projectPath);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithMissingProjectSettingsFolder_ShouldReturnFalse()
    {
        var projectPath = "/NotUnityProject";
        _fileSystem.AddDirectory(projectPath);
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Assets"));
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Packages"));
        // Missing ProjectSettings folder

        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns(projectPath);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithStringPath_ShouldWork()
    {
        var projectPath = "/UnityProject";
        SetupUnityProjectStructure(projectPath);

        var result = projectPath.IsUnityProject(_fileSystem);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsUnityProject_WithEmptyStringPath_ShouldReturnFalse()
    {
        var result = "".IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityProject_WithNullStringPath_ShouldReturnFalse()
    {
        var result = ((string)null).IsUnityProject(_fileSystem);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithNullOptions_ShouldReturnNull()
    {
        IStrykerOptions options = null;

        var result = options.GetUnityProjectDirectory(_fileSystem);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithProjectPath_ShouldReturnProjectPath()
    {
        var projectPath = "/UnityProject";
        _fileSystem.AddDirectory(projectPath);
        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns(projectPath);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns((string)null);

        var result = _strykerOptionsMock.Object.GetUnityProjectDirectory(_fileSystem);

        result.ShouldBe(projectPath);
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithSolutionPath_ShouldReturnSolutionDirectory()
    {
        var solutionPath = "/UnityProject/MyProject.sln";
        var expectedDirectory = "/UnityProject";

        _fileSystem.AddDirectory(expectedDirectory);
        _fileSystem.AddFile(solutionPath, new MockFileData(""));
        _strykerOptionsMock.Setup(x => x.ProjectPath).Returns((string)null);
        _strykerOptionsMock.Setup(x => x.SolutionPath).Returns(solutionPath);

        var result = _strykerOptionsMock.Object.GetUnityProjectDirectory(_fileSystem);

        result.ShouldBe(expectedDirectory);
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithStringPath_ShouldReturnPath()
    {
        var projectPath = "/UnityProject";
        _fileSystem.AddDirectory(projectPath);

        var result = projectPath.GetUnityProjectDirectory(_fileSystem);

        result.ShouldBe(projectPath);
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithFile_ShouldReturnParentDirectory()
    {
        var filePath = "/UnityProject/MyProject.sln";
        var expectedDirectory = "/UnityProject";
        _fileSystem.AddDirectory(expectedDirectory);
        _fileSystem.AddFile(filePath, new MockFileData(""));

        var result = filePath.GetUnityProjectDirectory(_fileSystem);

        result.ShouldBe(expectedDirectory);
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithEmptyString_ShouldReturnNull()
    {
        var result = "".GetUnityProjectDirectory(_fileSystem);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithNullString_ShouldReturnNull()
    {
        var result = ((string)null).GetUnityProjectDirectory(_fileSystem);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetUnityProjectDirectory_WithNonExistentPath_ShouldReturnNull()
    {
        var result = "/NonExistentPath".GetUnityProjectDirectory(_fileSystem);

        result.ShouldBeNull();
    }


    private void SetupUnityProjectStructure(string projectPath)
    {
        _fileSystem.AddDirectory(projectPath);
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Assets"));
        _fileSystem.AddDirectory(Path.Combine(projectPath, "Packages"));
        _fileSystem.AddDirectory(Path.Combine(projectPath, "ProjectSettings"));
    }
}
