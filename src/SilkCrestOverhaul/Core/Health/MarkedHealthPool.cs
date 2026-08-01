using System;

namespace SilkCrestOverhaul.Core.Health;

public enum MarkedHealthKind { BeastRed, WitchGreen, ArchitectPurple, Blue, ShardRepair }

public sealed class MarkedHealthPool
{
    public MarkedHealthPool(MarkedHealthKind kind, int capacity, int conversionUnit = 1)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (conversionUnit <= 0) throw new ArgumentOutOfRangeException(nameof(conversionUnit));
        Kind = kind;
        Capacity = capacity;
        ConversionUnit = conversionUnit;
    }

    public MarkedHealthKind Kind { get; }
    public int Capacity { get; }
    public int ConversionUnit { get; }
    public int Value { get; private set; }
    public int Remainder { get; private set; }

    public int AddRawHealing(int rawHealing)
    {
        if (rawHealing <= 0) return 0;
        var total = Remainder + rawHealing;
        var units = total / ConversionUnit;
        Remainder = total % ConversionUnit;
        var accepted = Math.Min(units, Capacity - Value);
        Value += accepted;
        return accepted;
    }

    public int Consume(int amount)
    {
        var consumed = Math.Min(Math.Max(amount, 0), Value);
        Value -= consumed;
        return consumed;
    }

    public void Clear() { Value = 0; Remainder = 0; }
}
