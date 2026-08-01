namespace SilkCrestOverhaul.UI;

public sealed record UiProjection(
    string CrestId,
    int EnhancementStacks,
    double RemainingSeconds,
    double NormalAttackDamage,
    int MarkedHealth,
    string MarkedHealthKind,
    bool Rage,
    bool SuperRage,
    bool SpecialMode);

public interface IUiPresenter
{
    void Present(UiProjection projection);
    void Hide();
}
