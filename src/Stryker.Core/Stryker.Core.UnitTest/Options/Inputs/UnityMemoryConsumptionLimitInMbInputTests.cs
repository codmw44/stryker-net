using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.Abstractions.Options.Inputs;

namespace Stryker.Core.UnitTest.Options.Inputs;

[TestClass]
public class UnityMemoryConsumptionLimitInMbInputTests : TestBase
{
    [TestMethod]
    public void ShouldHaveCorrectDefaultValue()
    {
        var target = new UnityMemoryConsumptionLimitInMbInput();
        target.Default.ShouldBe(4000);
    }

    [TestMethod]
    [DataRow(1000, 1000)]
    [DataRow(2000, 2000)]
    [DataRow(4000, 4000)]
    [DataRow(8000, 8000)]
    [DataRow(16000, 16000)]
    public void ShouldReturnSuppliedValueWhenValid(int input, int expected)
    {
        var target = new UnityMemoryConsumptionLimitInMbInput { SuppliedInput = input };
        var result = target.Validate();

        result.ShouldBe(expected);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(-100)]
    public void ShouldReturnDefaultWhenZeroOrNegativeValue(int input)
    {
        var target = new UnityMemoryConsumptionLimitInMbInput { SuppliedInput = input };
        var result = target.Validate();

        result.ShouldBe(4000);
    }

    [TestMethod]
    public void ShouldReturnDefaultWhenNullInput()
    {
        var target = new UnityMemoryConsumptionLimitInMbInput { SuppliedInput = 0 };
        var result = target.Validate();

        result.ShouldBe(4000);
    }

    [TestMethod]
    public void ShouldHandleVeryLargeValues()
    {
        var target = new UnityMemoryConsumptionLimitInMbInput { SuppliedInput = int.MaxValue };
        var result = target.Validate();

        result.ShouldBe(int.MaxValue);
    }
}
