namespace SilkCrestOverhaul.Core.Health;

public sealed record HealTransaction(
    int Requested,
    int WhiteHealthApplied,
    int Overflow,
    int ConvertedUnits,
    MarkedHealthKind? ConvertedTo);

public sealed class HealthConversionService
{
    public HealTransaction Apply(int requested, int missingWhiteHealth, MarkedHealthPool? overflowPool)
    {
        var white = System.Math.Min(System.Math.Max(requested, 0), System.Math.Max(missingWhiteHealth, 0));
        var overflow = System.Math.Max(0, requested - white);
        var converted = overflowPool?.AddRawHealing(overflow) ?? 0;
        return new HealTransaction(requested, white, overflow, converted, overflowPool?.Kind);
    }
}
