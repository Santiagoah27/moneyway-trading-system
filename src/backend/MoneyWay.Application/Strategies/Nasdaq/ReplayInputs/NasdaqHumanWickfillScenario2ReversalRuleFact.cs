using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical human reversal evidence only. It supplies no entry, order or Standard-path exemption.</summary>
public sealed class NasdaqHumanWickfillScenario2ReversalRuleFact : IReplayRuleFact
{
    private NasdaqHumanWickfillScenario2ReversalRuleFact(NasdaqHumanBranchApplicabilitySelection.Unique applicability,
        NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique reversal)
    { Applicability = applicability; Reversal = reversal; }

    public NasdaqHumanBranchApplicabilitySelection.Unique Applicability { get; }
    public NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique Reversal { get; }
    public DateTimeOffset EffectiveAtUtc => Reversal.Fact.EffectiveAtUtc;

    internal static NasdaqHumanWickfillScenario2ReversalRuleFact Create(StrategyReplayContext context,
        NasdaqHumanBranchApplicabilitySelection.Unique applicability, NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique reversal)
    {
        if (applicability.Fact.Path != NasdaqStrategyPath.WickfillScenario2
            || !NasdaqHumanBranchApplicabilityObservation.SameSetup(applicability.Fact.InitialTake, reversal.Fact.OpeningSweep.Take)
            || !applicability.Fact.Session.Matches(context)
            || applicability.SupportingObservations.Any(o => o.ObservedAtUtc > context.AsOfUtc)
            || reversal.SupportingObservations.Any(o => o.ObservedAtUtc > context.AsOfUtc)
            || reversal.UnavailableSourceObservations.Any(o => !o.SameFact(reversal.Fact)))
            throw new ArgumentException("Only observable compatible Unique Scenario 2 applicability and reversal can form a canonical fact.");
        return new(applicability, reversal);
    }
}
