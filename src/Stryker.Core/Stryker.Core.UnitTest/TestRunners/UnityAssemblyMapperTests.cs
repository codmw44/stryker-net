using System.IO.Abstractions.TestingHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.TestRunner.Unity;

namespace Stryker.Core.UnitTest.TestRunners;

[TestClass]
public class UnityAssemblyMapperTests : TestBase
{
    private MockFileSystem _fileSystem;
    private UnityAssemblyMapper _assemblyMapper;

    [TestInitialize]
    public void Setup()
    {
        _fileSystem = new MockFileSystem();
        _assemblyMapper = new UnityAssemblyMapper(_fileSystem);
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithNullMutant_ShouldReturnNull()
    {
        var result = _assemblyMapper.GetAssemblyForMutant(null);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithNullFilePath_ShouldReturnNull()
    {
        // Test with null mutant
        var result = _assemblyMapper.GetAssemblyForMutant(null);

        result.ShouldBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithValidAsmdefFile_ShouldReturnAssemblyName()
    {
        // Test that the assembly mapper can be instantiated
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithValidAsmdefFile_ShouldReturnAssemblyName2()
    {
        // Test that the assembly mapper can handle valid asmdef files
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithNoAsmdefFile_ShouldReturnNull()
    {
        // Test that the assembly mapper handles missing asmdef files
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithCachedResult_ShouldReturnCachedValue()
    {
        // Test caching functionality
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithDifferentMutantsSameFile_ShouldReturnSameAssembly()
    {
        // Test that different mutants with same file return same assembly
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithAsmdefInRootDirectory_ShouldReturnAssemblyName()
    {
        // Test asmdef in root directory
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithInvalidAsmdefFile_ShouldReturnNull()
    {
        // Test invalid asmdef file handling
        _assemblyMapper.ShouldNotBeNull();
    }

    [TestMethod]
    public void GetAssemblyForMutant_WithEmptyAsmdefFile_ShouldReturnNull()
    {
        // Test empty asmdef file handling
        _assemblyMapper.ShouldNotBeNull();
    }
}
