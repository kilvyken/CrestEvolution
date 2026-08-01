using UnityEngine;
namespace SilkCrestOverhaul.Core.Time;
public sealed class UnityClock : IClock
{
    public double Now => Time.timeAsDouble;
}
