using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical human-selected structural stop evidence; no order or execution is established.</summary>
public sealed record NasdaqHumanStructuralStopLossRuleFact : IReplayRuleFact
{
    internal NasdaqHumanStructuralStopLossRuleFact(NasdaqPreEntryEligibilityRuleFact preEntryEligibility,
        NasdaqHumanStructuralStopLossSelection.Unique selection)
    {
        PreEntryEligibility = preEntryEligibility ?? throw new ArgumentNullException(nameof(preEntryEligibility));
        Selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    public NasdaqPreEntryEligibilityRuleFact PreEntryEligibility { get; }
    public NasdaqHumanStructuralStopLossSelection.Unique Selection { get; }
    public NasdaqDemoSessionIdentity Session => PreEntryEligibility.Session;
    public NasdaqHumanH4PermittedDirection Direction => PreEntryEligibility.Direction;
    public NasdaqM5ProtectionAnchorKind ProtectionAnchorKind => Selection.Fact.ProtectionAnchor.Kind;
    public decimal StructuralAnchorPrice => Selection.Fact.ProtectionAnchor.ProtectionAnchorPrice;
    public decimal StopPrice => Selection.Fact.StopPrice;
    public DateTimeOffset EffectiveAtUtc => Selection.Fact.EffectiveAtUtc;
}
