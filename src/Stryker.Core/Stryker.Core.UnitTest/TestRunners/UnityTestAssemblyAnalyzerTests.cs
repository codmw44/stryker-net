using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.Abstractions;
using Stryker.Abstractions.Options;
using Stryker.TestRunner.Unity;
using System.Linq;
using Buildalyzer;
using Moq;

namespace Stryker.Core.UnitTest.TestRunners;

// Simple mock implementation of IProjectItem for testing
public class MockProjectItem : IProjectItem
{
    public string ItemSpec { get; set; }
    public string ItemType { get; set; }
    public IReadOnlyDictionary<string, string> Metadata { get; set; }

    public MockProjectItem(string itemSpec, string itemType = "None", IReadOnlyDictionary<string, string> metadata = null)
    {
        ItemSpec = itemSpec;
        ItemType = itemType;
        Metadata = metadata ?? new Dictionary<string, string>();
    }
}

[TestClass]
public class UnityTestAssemblyAnalyzerTests : TestBase
{
    private MockFileSystem _fileSystem;
    private UnityTestAssemblyAnalyzer _analyzer;

    [TestInitialize]
    public void Setup()
    {
        _fileSystem = new MockFileSystem();
        _analyzer = new UnityTestAssemblyAnalyzer(_fileSystem);
    }

    [TestMethod]
    public void UnityTestAssemblyInfo_ShouldInitializeCorrectly()
    {
        var info = new UnityTestAssemblyInfo
        {
            AssemblyName = "TestAssembly",
            ProjectPath = "C:\\Test\\TestAssembly.csproj",
            SupportedModes = UnityTestMode.EditMode,
            ReferencedAssemblies = new List<string> { "UnityEngine", "UnityEditor" }
        };

        info.AssemblyName.ShouldBe("TestAssembly");
        info.ProjectPath.ShouldBe("C:\\Test\\TestAssembly.csproj");
        info.SupportedModes.ShouldBe(UnityTestMode.EditMode);
        info.ReferencedAssemblies.ShouldContain("UnityEngine");
        info.ReferencedAssemblies.ShouldContain("UnityEditor");
    }

    [TestMethod]
    public void TryGetTestAssemblyInfo_WithNonExistentAssembly_ShouldReturnFalse()
    {
        var result = _analyzer.TryGetTestAssemblyInfo("NonExistentAssembly", out var assemblyInfo);

        result.ShouldBeFalse();
        assemblyInfo.ShouldBeNull();
    }

    [TestMethod]
    public void TryGetTestAssemblyInfo_WithExistingAssembly_ShouldReturnTrueAndAssemblyInfo()
    {
        // Manually add an assembly to test the retrieval logic
        AddTestAssembly("TestAssembly", UnityTestMode.EditMode);

        var result = _analyzer.TryGetTestAssemblyInfo("TestAssembly", out var assemblyInfo);

        result.ShouldBeTrue();
        assemblyInfo.ShouldNotBeNull();
        assemblyInfo.AssemblyName.ShouldBe("TestAssembly");
        assemblyInfo.SupportedModes.ShouldBe(UnityTestMode.EditMode);
    }

    [TestMethod]
    public void GetTestAssembliesForMode_WithEditMode_ShouldReturnOnlyEditModeAssemblies()
    {
        // Add test assemblies with different modes
        AddTestAssembly("EditModeAssembly", UnityTestMode.EditMode);
        AddTestAssembly("PlayModeAssembly", UnityTestMode.PlayMode);
        AddTestAssembly("AllModeAssembly", UnityTestMode.All);

        var editModeAssemblies = _analyzer.GetTestAssembliesForMode(UnityTestMode.EditMode).ToList();

        editModeAssemblies.Count.ShouldBe(2); // EditMode and All
        editModeAssemblies.ShouldContain(ta => ta.AssemblyName == "EditModeAssembly");
        editModeAssemblies.ShouldContain(ta => ta.AssemblyName == "AllModeAssembly");
        editModeAssemblies.ShouldNotContain(ta => ta.AssemblyName == "PlayModeAssembly");
    }

    [TestMethod]
    public void GetTestAssembliesForMode_WithPlayMode_ShouldReturnOnlyPlayModeAssemblies()
    {
        // Add test assemblies with different modes
        AddTestAssembly("EditModeAssembly", UnityTestMode.EditMode);
        AddTestAssembly("PlayModeAssembly", UnityTestMode.PlayMode);
        AddTestAssembly("AllModeAssembly", UnityTestMode.All);

        var playModeAssemblies = _analyzer.GetTestAssembliesForMode(UnityTestMode.PlayMode).ToList();

        playModeAssemblies.Count.ShouldBe(2); // PlayMode and All
        playModeAssemblies.ShouldContain(ta => ta.AssemblyName == "PlayModeAssembly");
        playModeAssemblies.ShouldContain(ta => ta.AssemblyName == "AllModeAssembly");
        playModeAssemblies.ShouldNotContain(ta => ta.AssemblyName == "EditModeAssembly");
    }

    [TestMethod]
    public void GetTestAssembliesForMode_WithAllMode_ShouldReturnAllAssemblies()
    {
        // Add test assemblies with different modes
        AddTestAssembly("EditModeAssembly", UnityTestMode.EditMode);
        AddTestAssembly("PlayModeAssembly", UnityTestMode.PlayMode);
        AddTestAssembly("AllModeAssembly", UnityTestMode.All);

        var allAssemblies = _analyzer.GetTestAssembliesForMode(UnityTestMode.All).ToList();

        allAssemblies.Count.ShouldBe(3);
        allAssemblies.ShouldContain(ta => ta.AssemblyName == "EditModeAssembly");
        allAssemblies.ShouldContain(ta => ta.AssemblyName == "PlayModeAssembly");
        allAssemblies.ShouldContain(ta => ta.AssemblyName == "AllModeAssembly");
    }

    [TestMethod]
    public void GetTestAssembliesForMutants_WithRelevantMutants_ShouldReturnTestAssemblies()
    {
        // Add test assemblies with different referenced assemblies
        AddTestAssemblyWithReferences("TestAssembly1", new[] { "TargetAssembly", "UnityEngine" });
        AddTestAssemblyWithReferences("TestAssembly2", new[] { "OtherAssembly", "UnityEditor" });
        AddTestAssemblyWithReferences("TestAssembly3", new[] { "TargetAssembly", "UnityEngine" });

        // Create mock mutants that target "TargetAssembly"
        var mockMutant1 = CreateMockMutant("C:\\TargetAssembly\\File1.cs");
        var mockMutant2 = CreateMockMutant("C:\\TargetAssembly\\File2.cs");

        // Set up file system to return "TargetAssembly" for the mutant files
        _fileSystem.AddFile("C:\\TargetAssembly\\File1.cs", "class TestClass1 { }");
        _fileSystem.AddFile("C:\\TargetAssembly\\File2.cs", "class TestClass2 { }");

        // Create a project file that maps to "TargetAssembly"
        _fileSystem.AddFile("C:\\TargetAssembly\\TargetAssembly.csproj",
            @"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup>
                    <AssemblyName>TargetAssembly</AssemblyName>
                </PropertyGroup>
            </Project>");

        var mutants = new[] { mockMutant1.Object, mockMutant2.Object };
        var relevantAssemblies = _analyzer.GetTestAssembliesForMutants(mutants).ToList();

        relevantAssemblies.Count.ShouldBe(2); // TestAssembly1 and TestAssembly3
        relevantAssemblies.ShouldContain(ta => ta.AssemblyName == "TestAssembly1");
        relevantAssemblies.ShouldContain(ta => ta.AssemblyName == "TestAssembly3");
        relevantAssemblies.ShouldNotContain(ta => ta.AssemblyName == "TestAssembly2");
    }

    [TestMethod]
    public void GetFilteredTestAssemblies_ShouldReturnIntersectionOfMutantAndModeAssemblies()
    {
        // Add test assemblies
        AddTestAssemblyWithReferences("EditModeAssembly", new[] { "TargetAssembly" }, UnityTestMode.EditMode);
        AddTestAssemblyWithReferences("PlayModeAssembly", new[] { "TargetAssembly" }, UnityTestMode.PlayMode);
        AddTestAssemblyWithReferences("AllModeAssembly", new[] { "TargetAssembly" }, UnityTestMode.All);
        AddTestAssemblyWithReferences("OtherAssembly", new[] { "OtherTarget" }, UnityTestMode.EditMode);

        // Create mock mutants
        var mockMutant = CreateMockMutant("C:\\TargetAssembly\\File.cs");

        // Set up file system to return "TargetAssembly" for the mutant file
        _fileSystem.AddFile("C:\\TargetAssembly\\File.cs", "class TestClass { }");

        // Create a project file that maps to "TargetAssembly"
        _fileSystem.AddFile("C:\\TargetAssembly\\TargetAssembly.csproj",
            @"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup>
                    <AssemblyName>TargetAssembly</AssemblyName>
                </PropertyGroup>
            </Project>");

        var mutants = new[] { mockMutant.Object };
        var filteredAssemblies = _analyzer.GetFilteredTestAssemblies(mutants, UnityTestMode.EditMode).ToList();

        filteredAssemblies.Count.ShouldBe(2); // EditModeAssembly and AllModeAssembly
        filteredAssemblies.ShouldContain(ta => ta.AssemblyName == "EditModeAssembly");
        filteredAssemblies.ShouldContain(ta => ta.AssemblyName == "AllModeAssembly");
        filteredAssemblies.ShouldNotContain(ta => ta.AssemblyName == "PlayModeAssembly");
        filteredAssemblies.ShouldNotContain(ta => ta.AssemblyName == "OtherAssembly");
    }

    [TestMethod]
    public void IsUnityTestProject_WithTestAsmdef_ShouldReturnTrue()
    {
        // Create a test asmdef file with test runner references
        var asmdefContent = @"{
            ""name"": ""TestAssembly"",
            ""references"": [""UnityEngine.TestRunner"", ""UnityEditor.TestRunner""]
        }";
        _fileSystem.AddFile("C:\\Test\\TestAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result with the correct path
        var mockAnalyzerResult = CreateMockAnalyzerResult("TestAssembly", "C:\\Test\\TestAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\TestAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        var result = _analyzer.IsUnityTestProject(mockAnalyzerResult.Object);

        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsUnityTestProject_WithNonTestAsmdef_ShouldReturnFalse()
    {
        // Create a non-test asmdef file
        var asmdefContent = @"{
            ""name"": ""RegularAssembly"",
            ""references"": [""UnityEngine""]
        }";
        _fileSystem.AddFile("C:\\Test\\RegularAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result
        var mockAnalyzerResult = CreateMockAnalyzerResult("RegularAssembly", "C:\\Test\\RegularAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\RegularAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        var result = _analyzer.IsUnityTestProject(mockAnalyzerResult.Object);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsUnityTestProject_WithNoAsmdef_ShouldReturnFalse()
    {
        // Create a mock analyzer result without asmdef files
        var mockAnalyzerResult = CreateMockAnalyzerResult("RegularAssembly", "C:\\Test\\RegularAssembly.csproj",
            new Dictionary<string, IProjectItem[]>(),
            new[] { "System" });

        var result = _analyzer.IsUnityTestProject(mockAnalyzerResult.Object);

        result.ShouldBeFalse();
    }

    [TestMethod]
    public void AnalyzeProject_WithEditModeAsmdef_ShouldSetCorrectTestMode()
    {
        // Create EditMode asmdef file
        var asmdefContent = @"{
            ""name"": ""EditModeAssembly"",
            ""references"": [""UnityEditor.TestRunner""],
            ""includePlatforms"": [""Editor""]
        }";
        _fileSystem.AddFile("C:\\Test\\EditModeAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result
        var mockAnalyzerResult = CreateMockAnalyzerResult("EditModeAssembly", "C:\\Test\\EditModeAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\EditModeAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        _analyzer.AnalyzeProject(mockAnalyzerResult.Object);

        var result = _analyzer.TryGetTestAssemblyInfo("EditModeAssembly", out var assemblyInfo);
        result.ShouldBeTrue();
        assemblyInfo.SupportedModes.ShouldBe(UnityTestMode.EditMode);
    }

    [TestMethod]
    public void AnalyzeProject_WithPlayModeAsmdef_ShouldSetCorrectTestMode()
    {
        // Create PlayMode asmdef file (no includePlatforms means PlayMode)
        var asmdefContent = @"{
            ""name"": ""PlayModeAssembly"",
            ""references"": [""UnityEngine.TestRunner""]
        }";
        _fileSystem.AddFile("C:\\Test\\PlayModeAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result
        var mockAnalyzerResult = CreateMockAnalyzerResult("PlayModeAssembly", "C:\\Test\\PlayModeAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\PlayModeAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        _analyzer.AnalyzeProject(mockAnalyzerResult.Object);

        var result = _analyzer.TryGetTestAssemblyInfo("PlayModeAssembly", out var assemblyInfo);
        result.ShouldBeTrue();
        assemblyInfo.SupportedModes.ShouldBe(UnityTestMode.PlayMode);
    }

    [TestMethod]
    public void AnalyzeProject_WithAllModeAsmdef_ShouldSetCorrectTestMode()
    {
        // Create All mode asmdef file (no includePlatforms and both test runners)
        var asmdefContent = @"{
            ""name"": ""AllModeAssembly"",
            ""references"": [""UnityEngine.TestRunner"", ""UnityEditor.TestRunner""]
        }";
        _fileSystem.AddFile("C:\\Test\\AllModeAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result
        var mockAnalyzerResult = CreateMockAnalyzerResult("AllModeAssembly", "C:\\Test\\AllModeAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\AllModeAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        _analyzer.AnalyzeProject(mockAnalyzerResult.Object);

        var result = _analyzer.TryGetTestAssemblyInfo("AllModeAssembly", out var assemblyInfo);
        result.ShouldBeTrue();
        assemblyInfo.SupportedModes.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void AnalyzeProject_WithInvalidAsmdef_ShouldDefaultToAllMode()
    {
        // Create invalid asmdef file
        var asmdefContent = @"{
            ""name"": ""InvalidAssembly"",
            ""invalid_json"":
        }";
        _fileSystem.AddFile("C:\\Test\\InvalidAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result
        var mockAnalyzerResult = CreateMockAnalyzerResult("InvalidAssembly", "C:\\Test\\InvalidAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\InvalidAssembly.asmdef")
                }
            },
            new[] { "UnityEngine" });

        _analyzer.AnalyzeProject(mockAnalyzerResult.Object);

        var result = _analyzer.TryGetTestAssemblyInfo("InvalidAssembly", out var assemblyInfo);
        result.ShouldBeTrue();
        assemblyInfo.SupportedModes.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void AnalyzeProject_WithProjectReferences_ShouldIncludeThemInReferencedAssemblies()
    {
        // Create test asmdef file
        var asmdefContent = @"{
            ""name"": ""TestAssembly"",
            ""references"": [""UnityEngine.TestRunner""]
        }";
        _fileSystem.AddFile("C:\\Test\\TestAssembly.asmdef", asmdefContent);

        // Create a mock analyzer result with project references
        var mockAnalyzerResult = CreateMockAnalyzerResult("TestAssembly", "C:\\Test\\TestAssembly.csproj",
            new Dictionary<string, IProjectItem[]>
            {
                ["None"] = new IProjectItem[]
                {
                    new MockProjectItem("C:\\Test\\TestAssembly.asmdef")
                },
                ["ProjectReference"] = new IProjectItem[]
                {
                    new MockProjectItem("ReferencedProject.csproj")
                }
            },
            new[] { "UnityEngine", "System" });

        _analyzer.AnalyzeProject(mockAnalyzerResult.Object);

        var result = _analyzer.TryGetTestAssemblyInfo("TestAssembly", out var assemblyInfo);
        result.ShouldBeTrue();
        assemblyInfo.ReferencedAssemblies.ShouldContain("ReferencedProject");
        assemblyInfo.ReferencedAssemblies.ShouldContain("UnityEngine");
        assemblyInfo.ReferencedAssemblies.ShouldContain("System");
    }

    [TestMethod]
    public void UnityAssemblyMapper_WithValidProjectFile_ShouldReturnCorrectAssemblyName()
    {
        // Create a project file
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <AssemblyName>TestAssembly</AssemblyName>
            </PropertyGroup>
        </Project>";
        _fileSystem.AddFile("C:\\Test\\TestAssembly.csproj", projectContent);

        // Create a source file in the project directory
        _fileSystem.AddFile("C:\\Test\\TestClass.cs", "class TestClass { }");

        // Create a mutant that references the source file
        var mockMutant = CreateMockMutant("C:\\Test\\TestClass.cs");

        // Create a new UnityAssemblyMapper instance
        var assemblyMapper = new UnityAssemblyMapper(_fileSystem);

        // Test the GetAssemblyForMutant method
        var assemblyName = assemblyMapper.GetAssemblyForMutant(mockMutant.Object);

        assemblyName.ShouldBe("TestAssembly");
    }

    [TestMethod]
    public void UnityAssemblyMapper_WithInvalidProjectFile_ShouldReturnNull()
    {
        // Create an invalid project file
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <!-- Missing AssemblyName -->
            </PropertyGroup>
        </Project>";
        _fileSystem.AddFile("C:\\Test\\InvalidAssembly.csproj", projectContent);

        // Create a source file in the project directory
        _fileSystem.AddFile("C:\\Test\\TestClass.cs", "class TestClass { }");

        // Create a mutant that references the source file
        var mockMutant = CreateMockMutant("C:\\Test\\TestClass.cs");

        // Create a new UnityAssemblyMapper instance
        var assemblyMapper = new UnityAssemblyMapper(_fileSystem);

        // Test the GetAssemblyForMutant method
        var assemblyName = assemblyMapper.GetAssemblyForMutant(mockMutant.Object);

        assemblyName.ShouldBeNull();
    }

    // Helper methods
    private Mock<IAnalyzerResult> CreateMockAnalyzerResult(string assemblyName, string projectPath,
        Dictionary<string, IProjectItem[]> items, string[] references)
    {
        var mockAnalyzerResult = new Mock<IAnalyzerResult>();

        // Mock the Properties dictionary to return assembly name
        var properties = new Dictionary<string, string> { ["AssemblyName"] = assemblyName };
        mockAnalyzerResult.Setup(x => x.Properties).Returns(properties);
        mockAnalyzerResult.Setup(x => x.ProjectFilePath).Returns(projectPath);
        mockAnalyzerResult.Setup(x => x.Items).Returns(items);
        mockAnalyzerResult.Setup(x => x.References).Returns(references);

        return mockAnalyzerResult;
    }

    private void AddTestAssembly(string assemblyName, UnityTestMode supportedModes)
    {
        var assemblyInfo = new UnityTestAssemblyInfo
        {
            AssemblyName = assemblyName,
            ProjectPath = $"C:\\Test\\{assemblyName}.csproj",
            SupportedModes = supportedModes,
            ReferencedAssemblies = new List<string> { "UnityEngine" }
        };

        // Use reflection to add to private dictionary
        var field = typeof(UnityTestAssemblyAnalyzer).GetField("_testAssemblies",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var testAssemblies = (Dictionary<string, UnityTestAssemblyInfo>)field.GetValue(_analyzer);
        testAssemblies[assemblyName] = assemblyInfo;
    }

    private void AddTestAssemblyWithReferences(string assemblyName, string[] referencedAssemblies, UnityTestMode supportedModes = UnityTestMode.All)
    {
        var assemblyInfo = new UnityTestAssemblyInfo
        {
            AssemblyName = assemblyName,
            ProjectPath = $"C:\\Test\\{assemblyName}.csproj",
            SupportedModes = supportedModes,
            ReferencedAssemblies = referencedAssemblies.ToList()
        };

        // Use reflection to add to private dictionaries
        var testAssembliesField = typeof(UnityTestAssemblyAnalyzer).GetField("_testAssemblies",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var testAssemblies = (Dictionary<string, UnityTestAssemblyInfo>)testAssembliesField.GetValue(_analyzer);
        testAssemblies[assemblyName] = assemblyInfo;

        var assemblyReferencesField = typeof(UnityTestAssemblyAnalyzer).GetField("_assemblyReferences",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var assemblyReferences = (Dictionary<string, List<string>>)assemblyReferencesField.GetValue(_analyzer);
        assemblyReferences[assemblyName] = referencedAssemblies.ToList();
    }

    private Mock<IMutant> CreateMockMutant(string filePath)
    {
        var mockMutant = new Mock<IMutant>();

        // Create a real SyntaxTree and SyntaxNode instead of mocking them
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("class TestClass { }", path: filePath);
        var root = syntaxTree.GetRoot();
        var classDeclaration = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();

        var mutation = new Mutation
        {
            OriginalNode = classDeclaration
        };

        mockMutant.Setup(m => m.Mutation).Returns(mutation);

        return mockMutant;
    }
}
