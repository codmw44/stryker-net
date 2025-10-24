using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.Abstractions.Options;
using Stryker.TestRunner.Unity;
using System.IO.Abstractions.TestingHelpers;

namespace Stryker.Core.UnitTest.TestRunners;

[TestClass]
public class AsmdefParserTests : TestBase
{
    private MockFileSystem _fileSystem;
    private AsmdefParser _asmdefParser;

    [TestInitialize]
    public void Setup()
    {
        _fileSystem = new MockFileSystem();
        _asmdefParser = new AsmdefParser(_fileSystem);
    }

    [TestMethod]
    public void GetAssemblyName_WithValidAsmdef_ShouldReturnAssemblyName()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"name\": \"TestAssembly\"}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetAssemblyName(asmdefPath);

        result.ShouldBe("TestAssembly");
    }

    [TestMethod]
    public void GetAssemblyName_WithMissingNameProperty_ShouldReturnNull()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"version\": \"1.0.0\"}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetAssemblyName(asmdefPath);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAssemblyName_WithEmptyNameProperty_ShouldReturnEmptyString()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"name\": \"\"}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetAssemblyName(asmdefPath);

        result.ShouldBe("");
    }

    [TestMethod]
    public void GetAssemblyName_WithNullNameProperty_ShouldReturnNull()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"name\": null}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetAssemblyName(asmdefPath);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAssemblyName_WithInvalidJson_ShouldThrowException()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "invalid json";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        Should.Throw<System.Text.Json.JsonException>(() => _asmdefParser.GetAssemblyName(asmdefPath));
    }

    [TestMethod]
    public void GetAssemblyName_WithNonExistentFile_ShouldThrowException()
    {
        var asmdefPath = "C:\\Test\\NonExistent.asmdef";

        Should.Throw<FileNotFoundException>(() => _asmdefParser.GetAssemblyName(asmdefPath));
    }

    [TestMethod]
    public void GetTestMode_WithEditorPlatform_ShouldReturnEditMode()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"includePlatforms\": [\"Editor\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.EditMode);
    }

    [TestMethod]
    public void GetTestMode_WithPlayerPlatform_ShouldReturnPlayMode()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"includePlatforms\": [\"Player\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.PlayMode);
    }

    [TestMethod]
    public void GetTestMode_WithMultiplePlatformsIncludingEditor_ShouldReturnEditMode()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"includePlatforms\": [\"Editor\", \"Player\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.EditMode);
    }

    [TestMethod]
    public void GetTestMode_WithMultiplePlatformsExcludingEditor_ShouldReturnPlayMode()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"includePlatforms\": [\"Player\", \"WebGL\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.PlayMode);
    }

    [TestMethod]
    public void GetTestMode_WithNoIncludePlatforms_ShouldReturnAll()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"name\": \"TestAssembly\"}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void GetTestMode_WithEmptyIncludePlatforms_ShouldReturnAll()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"includePlatforms\": []}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.PlayMode);
    }

    [TestMethod]
    public void GetTestMode_WithInvalidJson_ShouldReturnAll()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "invalid json";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void GetTestMode_WithNonExistentFile_ShouldReturnAll()
    {
        var asmdefPath = "C:\\Test\\NonExistent.asmdef";

        var result = _asmdefParser.GetTestMode(asmdefPath);

        result.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void IsTestAssembly_WithUnityEngineTestRunnerReference_ShouldReturnTrue()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": [\"UnityEngine.TestRunner\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsTestAssembly_WithUnityEditorTestRunnerReference_ShouldReturnTrue()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": [\"UnityEditor.TestRunner\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsTestAssembly_WithBothTestRunnerReferences_ShouldReturnTrue()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": [\"UnityEngine.TestRunner\", \"UnityEditor.TestRunner\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsTestAssembly_WithOtherReferences_ShouldReturnFalse()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": [\"UnityEngine\", \"UnityEditor\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsTestAssembly_WithNoReferences_ShouldReturnFalse()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"name\": \"TestAssembly\"}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsTestAssembly_WithEmptyReferences_ShouldReturnFalse()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": []}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsTestAssembly_WithInvalidJson_ShouldReturnFalse()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "invalid json";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsTestAssembly_WithNonExistentFile_ShouldReturnFalse()
    {
        var asmdefPath = "C:\\Test\\NonExistent.asmdef";

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsTestAssembly_WithPartialTestRunnerName_ShouldReturnTrue()
    {
        var asmdefPath = "C:\\Test\\TestAssembly.asmdef";
        var asmdefContent = "{\"references\": [\"UnityEngine.TestRunner.AdditionalPackage\"]}";
        _fileSystem.AddFile(asmdefPath, asmdefContent);

        var result = _asmdefParser.IsTestAssembly(asmdefPath);

        result.ShouldBeTrue();
    }
}
