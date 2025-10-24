using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stryker.Abstractions.Options;
using Stryker.Abstractions.Options.Inputs;

namespace Stryker.Core.UnitTest.Options.Inputs;

[TestClass]
public class UnityTestModeInputTests : TestBase
{

    [TestMethod]
    public void ShouldHaveCorrectDefaultValue()
    {
        var target = new UnityTestModeInput();
        target.Default.ShouldBe("All");
    }

    [TestMethod]
    public void ShouldHaveCorrectAllowedOptions()
    {
        var target = new UnityTestModeInput();
        // Test that the input can parse the expected values
        target.SuppliedInput = "None";
        target.Validate().ShouldBe(UnityTestMode.None);

        target.SuppliedInput = "EditMode";
        target.Validate().ShouldBe(UnityTestMode.EditMode);

        target.SuppliedInput = "PlayMode";
        target.Validate().ShouldBe(UnityTestMode.PlayMode);

        target.SuppliedInput = "All";
        target.Validate().ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    [DataRow("All", UnityTestMode.All)]
    [DataRow("all", UnityTestMode.All)]
    [DataRow("ALL", UnityTestMode.All)]
    [DataRow("EditMode", UnityTestMode.EditMode)]
    [DataRow("editmode", UnityTestMode.EditMode)]
    [DataRow("EDITMODE", UnityTestMode.EditMode)]
    [DataRow("PlayMode", UnityTestMode.PlayMode)]
    [DataRow("playmode", UnityTestMode.PlayMode)]
    [DataRow("PLAYMODE", UnityTestMode.PlayMode)]
    [DataRow("None", UnityTestMode.None)]
    [DataRow("none", UnityTestMode.None)]
    [DataRow("NONE", UnityTestMode.None)]
    public void ShouldParseValidUnityTestModeValues(string input, UnityTestMode expected)
    {
        var target = new UnityTestModeInput { SuppliedInput = input };
        var result = target.Validate();

        result.ShouldBe(expected);
    }

    [TestMethod]
    [DataRow("InvalidMode")]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Edit")]
    [DataRow("Play")]
    public void ShouldReturnDefaultWhenInvalidValueProvided(string input)
    {
        var target = new UnityTestModeInput { SuppliedInput = input };
        var result = target.Validate();

        result.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void ShouldReturnDefaultWhenNullInput()
    {
        var target = new UnityTestModeInput { SuppliedInput = null };
        var result = target.Validate();

        result.ShouldBe(UnityTestMode.All);
    }

    [TestMethod]
    public void ShouldHandleCombinedFlags()
    {
        var target = new UnityTestModeInput { SuppliedInput = "EditMode,PlayMode" };
        var result = target.Validate();

        result.ShouldBe(UnityTestMode.All);
    }
}
