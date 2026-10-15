using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects only explicit classifications for an exact canonical setup; absence never implies Standard.</summary>
public sealed class NasdaqHumanBranchApplicabilityObservationSelector
{
    public NasdaqHumanBranchApplicabilitySelection Select(StrategyReplayContext context, StrategyReplayContextObservation initialTakeObservation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(initialTakeObservation);
        NasdaqLiquidityTakeRuleFact initialTake;
        try { initialTake = NasdaqHumanBranchApplicabilityObservation.RequireInitialTake(initialTakeObservation); }
        catch (ArgumentException) { return new NasdaqHumanBranchApplicabilitySelection.Missing(); }
        var currentOwner = initialTakeObservation.AsOfUtc == context.AsOfUtc && initialTakeObservation.Step == context.Step;
        if (!initialTake.Session.Matches(context) || initialTakeObservation.AsOfUtc > context.AsOfUtc
            || !currentOwner && !context.PriorObservations.Contains(initialTakeObservation))
            return new NasdaqHumanBranchApplicabilitySelection.Missing();
        // The already established owner is the source for setup identity; no OHLC reclassification or latest-take substitution.
        var support = NasdaqHumanM5FvgObservationSelector.Ordered(context.InputObservations.OfType<NasdaqHumanBranchApplicabilityObservation>())
            .Where(o => o.ObservedAtUtc <= context.AsOfUtc
                && NasdaqHumanBranchApplicabilityObservation.SameSetup(o.InitialTake, initialTake)).ToArray();
        if (support.Length == 0) return new NasdaqHumanBranchApplicabilitySelection.Missing();
        return support.All(o => o.SameFact(support[0]))
            ? new NasdaqHumanBranchApplicabilitySelection.Unique(support)
            : new NasdaqHumanBranchApplicabilitySelection.Conflict(support);
    }
}
