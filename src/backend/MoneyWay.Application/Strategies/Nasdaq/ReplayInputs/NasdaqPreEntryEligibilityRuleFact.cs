using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Bounded eligibility derived from one canonical realignment; no entry or order is established.</summary>
public sealed record NasdaqPreEntryEligibilityRuleFact : IReplayRuleFact
{
    internal NasdaqPreEntryEligibilityRuleFact(NasdaqHumanM1RealignmentRuleFact realignment)
    {
        Realignment = realignment ?? throw new ArgumentNullException(nameof(realignment));
    }

    public NasdaqHumanM1RealignmentRuleFact Realignment { get; }
    public NasdaqDemoSessionIdentity Session => Realignment.Selection.Fact.Session;
    public NasdaqHumanH4PermittedDirection Direction => Realignment.Selection.Fact.SetupDirection;
    public DateTimeOffset EligibilityEffectiveAtUtc => Realignment.Selection.Fact.RealignmentEffectiveAtUtc;
}
