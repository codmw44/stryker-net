using System.Collections.Generic;
using Stryker.Abstractions.Exceptions;

namespace Stryker.Abstractions.Options.Inputs;

public class UnityTestModeInput : Input<string>
{
    public override string Default => UnityTestMode.All.ToString();

    protected override string Description => "Specify which Unity test mode to run: All (runs both EditMode and PlayMode in sequence), PlayMode, or EditMode";
    protected override IEnumerable<string> AllowedOptions => EnumToStrings(typeof(UnityTestMode));

    public UnityTestMode Validate()
    {
        if (System.Enum.TryParse(SuppliedInput, true, out UnityTestMode mode))
        {
            return mode;
        }
        else
        {
            return UnityTestMode.All;
        }
    }
}
