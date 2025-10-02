namespace Stryker.Abstractions.Options.Inputs;

public class UnityMemoryConsumptionLimitInMbInput : Input<int>
{
    public override int Default => 4000;

    protected override string Description => "Maximum memory consumption limit in MB for Unity process before restart";

    public int Validate()
    {
        if (SuppliedInput <= 0)
        {
            return Default;
        }

        return SuppliedInput;
    }
}
