using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Stateless selection of native human SL evidence under actual prior WF2-002 ancestry.
/// No evaluator, peak calculation, source lookup, timestamp rewrite or rule fact creation.</summary>
public sealed class NasdaqHumanWickfillScenario2StopLossObservationSelector
{
    public NasdaqHumanWickfillScenario2StopLossSelection Select(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-002"));
        if (latest is null || !NasdaqHumanM5FvgObservationSelector.InWindow(context, context.AsOfUtc))
            return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]);
        NasdaqWickfillScenario2PreEntryEligibilityRuleFact current;
        try { current = NasdaqHumanWickfillScenario2StopLossObservation.RequireEligibility(latest); }
        catch (ArgumentException) { return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]); }
        var start = current.Reversal.Applicability.Fact.InitialTakeObservation;
        var active = context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression?.ActiveInstance;
        if (!current.Session.Matches(context) || !HasCanonicalReversalOwner(context, latest, current)
            || !context.PriorObservations.Contains(start)
            || active is null || active.InstanceId != latest.LifecycleProgression!.ActiveInstance!.InstanceId
            || active.StartedAtUtc != start.AsOfUtc
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<IReplayRuleGateFact>()
                .Any(g => g.Blocks(context, new RuleId("NQ-WF2-002"))))
            return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]);

        var latestTurn = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        var latestTurnFact = latestTurn?.RuleFacts.Where(f => f.RuleId.Value == "NQ-WF2-001").Select(f => f.Fact)
            .OfType<NasdaqHumanWickfillScenario2ReversalRuleFact>().SingleOrDefault();
        if (latestTurnFact is null || latestTurn!.Evaluations.Single(e => e.RuleId.Value == "NQ-WF2-001").Result != RuleEvaluationResult.Passed
            || latestTurn.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-001")?.EstablishesProgression != true
            || !ReferenceEquals(latestTurnFact.Applicability.Fact.InitialTakeObservation, start)
            || !latestTurnFact.Reversal.Fact.SameFact(current.Reversal.Reversal.Fact))
            return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]);

        // Revalidate current source selections without invoking the WF2 evaluator or reviving an old favorable review.
        var applicability = new NasdaqHumanBranchApplicabilityObservationSelector().Select(context, start);
        if (applicability is not NasdaqHumanBranchApplicabilitySelection.Unique selected
            || selected.Fact.Path != NasdaqStrategyPath.WickfillScenario2
            || selected.SupportingObservations.Any(o => !ReferenceEquals(o.InitialTakeObservation, start)))
            return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]);
        try
        {
            var sweep = NasdaqWickfillScenario2OpeningSweepContext.From(context, current.Reversal.Reversal.Fact.OpeningSweep.PreOpenContext, start);
            var reversal = new NasdaqHumanWickfillScenario2PostSweepReversalObservationSelector().Select(context, sweep);
            if (reversal is not NasdaqHumanWickfillScenario2PostSweepReversalSelection.Unique turn
                || !turn.Fact.SameFact(current.Reversal.Reversal.Fact)
                || turn.SupportingObservations.Any(o => !ReferenceEquals(o.OpeningSweep.TakeObservation, start))
                || turn.UnavailableSourceObservations.Any(o => !o.SameFact(turn.Fact)))
                return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]);
        }
        catch (ArgumentException) { return new NasdaqHumanWickfillScenario2StopLossSelection.Missing([]); }

        var valid = new List<NasdaqHumanWickfillScenario2StopLossObservation>();
        var unavailable = new List<NasdaqHumanWickfillScenario2StopLossObservation>();
        foreach (var observation in NasdaqHumanM5FvgObservationSelector.Ordered(
            context.InputObservations.OfType<NasdaqHumanWickfillScenario2StopLossObservation>()))
        {
            if (!observation.Session.Matches(context) || !ReferenceEquals(observation.StartOwner, start)
                || !context.PriorObservations.Contains(observation.EligibilityOwner)
                || !HasCanonicalReversalOwner(context, observation.EligibilityOwner, observation.PreEntryEligibility)
                || observation.EligibilityOwner.AsOfUtc >= context.AsOfUtc
                || !observation.PreEntryEligibility.Reversal.Reversal.Fact.SameFact(current.Reversal.Reversal.Fact)
                || observation.ObservedAtUtc > context.AsOfUtc) continue;
            var missing = false;
            foreach (var source in observation.ProtectiveReference.SupportingCandles)
            {
                if (!context.TryGetFrame(source.Timeframe, out var frame)) { missing = true; continue; }
                var actual = frame!.AvailableCandles.SingleOrDefault(c => c.OpenTimeUtc == source.OpenTimeUtc);
                if (actual is null || !NasdaqStructuralLiquidityReference.SameCandle(actual, source)) missing = true;
            }
            if (missing) unavailable.Add(observation);
            else valid.Add(observation);
        }
        if (valid.Count == 0) return new NasdaqHumanWickfillScenario2StopLossSelection.Missing(unavailable);
        return valid.All(o => o.SameFact(valid[0]))
            ? new NasdaqHumanWickfillScenario2StopLossSelection.Unique(valid, unavailable)
            : new NasdaqHumanWickfillScenario2StopLossSelection.Conflict(valid, unavailable);
    }

    private static bool HasCanonicalReversalOwner(StrategyReplayContext context, StrategyReplayContextObservation eligibilityOwner,
        NasdaqWickfillScenario2PreEntryEligibilityRuleFact fact)
    {
        var owner = context.PriorObservations.LastOrDefault(o => o.AsOfUtc < eligibilityOwner.AsOfUtc
            && o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-001"));
        return owner is not null && owner.Evaluations.Single(e => e.RuleId.Value == "NQ-WF2-001").Result == RuleEvaluationResult.Passed
            && owner.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-WF2-001")?.EstablishesProgression == true
            && owner.RuleFacts.Any(f => f.RuleId.Value == "NQ-WF2-001" && ReferenceEquals(f.Fact, fact.Reversal));
    }

}
