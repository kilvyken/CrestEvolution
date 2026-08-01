using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkCrestOverhaul.Core.Combat;

public enum AttackKind { Normal, Dash, Dive, Charge, Skill, Tool, Projectile, Additional }

public sealed record AttackEvent(
    long AttackId,
    int AttackerInstanceId,
    int TargetInstanceId,
    AttackKind Kind,
    string SourceTag,
    double BaseDamage,
    int HitIndex = 0,
    bool IsAdditionalDamage = false);

public sealed class DamageContext
{
    public DamageContext(AttackEvent attack)
    {
        Attack = attack;
        Damage = attack.BaseDamage;
    }

    public AttackEvent Attack { get; }
    public double Damage { get; set; }
    public bool IsCritical { get; set; }
    public double CriticalMultiplier { get; set; } = 2.0;
    public List<AdditionalDamageCommand> AdditionalDamage { get; } = new();
    public List<DamageAuditEntry> Audit { get; } = new();
}

public interface IDamageModifier
{
    string Id { get; }
    int Order { get; }
    bool Applies(DamageContext context);
    void Apply(DamageContext context);
}

public sealed class DamagePipeline
{
    private readonly List<IDamageModifier> _modifiers = new();

    public void Register(IDamageModifier modifier)
    {
        if (_modifiers.Any(x => x.Id == modifier.Id))
            throw new InvalidOperationException($"Duplicate damage modifier: {modifier.Id}");
        _modifiers.Add(modifier);
        _modifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public DamageResult Evaluate(AttackEvent attack)
    {
        var context = new DamageContext(attack);
        foreach (var modifier in _modifiers)
        {
            if (!modifier.Applies(context)) continue;
            var before = context.Damage;
            modifier.Apply(context);
            context.Audit.Add(new DamageAuditEntry(modifier.Id, before, context.Damage));
        }

        if (context.IsCritical)
        {
            var before = context.Damage;
            context.Damage *= context.CriticalMultiplier;
            context.Audit.Add(new DamageAuditEntry("critical", before, context.Damage));
        }

        var final = Math.Max(0, Math.Round(context.Damage, MidpointRounding.AwayFromZero));
        return new DamageResult(final, context.IsCritical, context.AdditionalDamage, context.Audit);
    }
}

public sealed record AdditionalDamageCommand(
    int TargetInstanceId,
    double Damage,
    string SourceTag,
    bool ReenterPipeline = false);
public sealed record DamageAuditEntry(string ModifierId, double Before, double After);
public sealed record DamageResult(
    double FinalDamage,
    bool IsCritical,
    IReadOnlyList<AdditionalDamageCommand> AdditionalDamage,
    IReadOnlyList<DamageAuditEntry> Audit);
