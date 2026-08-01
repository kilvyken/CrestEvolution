using System;

namespace SilkCrestOverhaul.Core.Combat;

public sealed class SilkShieldPolicy
{
    public SilkShieldResult Resolve(int incomingMaskDamage, int currentSilk, int silkPerMask = 3)
    {
        if (incomingMaskDamage < 0) throw new ArgumentOutOfRangeException(nameof(incomingMaskDamage));
        if (currentSilk < 0) throw new ArgumentOutOfRangeException(nameof(currentSilk));
        if (silkPerMask <= 0) throw new ArgumentOutOfRangeException(nameof(silkPerMask));

        var absorbableMasks = Math.Min(incomingMaskDamage, currentSilk / silkPerMask);
        var silkSpent = absorbableMasks * silkPerMask;
        var remainingDamage = incomingMaskDamage - absorbableMasks;
        var fullyAbsorbed = remainingDamage == 0;
        return new SilkShieldResult(absorbableMasks, silkSpent, remainingDamage, !fullyAbsorbed);
    }
}

public sealed record SilkShieldResult(
    int AbsorbedMaskDamage,
    int SilkSpent,
    int RemainingMaskDamage,
    bool ShouldExitEnhancement);
