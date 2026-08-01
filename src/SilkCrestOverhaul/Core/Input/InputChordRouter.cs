using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkCrestOverhaul.Core.Input;

public sealed record InputSnapshot(
    bool Up,
    bool Down,
    bool HealPressed,
    bool AttackPressed,
    bool AttackHeld,
    int ChargeStage,
    bool InAir,
    bool HasControl,
    bool InMenuOrCutscene);

public sealed record ChordResult(string ActionId, bool ConsumeHeal, bool ConsumeAttack);

public sealed class InputChordRouter
{
    private readonly List<Registration> _registrations = new();

    public void Register(
        string actionId,
        int priority,
        Func<InputSnapshot, bool> predicate,
        Func<bool> statePredicate,
        bool consumeHeal = false,
        bool consumeAttack = false)
    {
        _registrations.Add(new Registration(actionId, priority, predicate, statePredicate, consumeHeal, consumeAttack));
        _registrations.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public ChordResult? Resolve(InputSnapshot snapshot)
    {
        if (!snapshot.HasControl || snapshot.InMenuOrCutscene) return null;
        var match = _registrations.FirstOrDefault(x => x.StatePredicate() && x.Predicate(snapshot));
        return match is null ? null : new ChordResult(match.ActionId, match.ConsumeHeal, match.ConsumeAttack);
    }

    private sealed record Registration(
        string ActionId,
        int Priority,
        Func<InputSnapshot, bool> Predicate,
        Func<bool> StatePredicate,
        bool ConsumeHeal,
        bool ConsumeAttack);
}
