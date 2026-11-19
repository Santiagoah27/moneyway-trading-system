using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Establishes the human-selected target for an exact canonical SL without price derivation or execution.</summary>
public sealed class MoneyWayNasdaqHumanTakeProfitEvaluator : IReplayRuleEvaluator
{
    private readonly StrategyVersion boundVersion;
    public MoneyWayNasdaqHumanTakeProfitEvaluator() : this(MoneyWayNasdaqStrategyDefinition.Instance.Version) { }
    internal MoneyWayNasdaqHumanTakeProfitEvaluator(StrategyVersion version) { boundVersion = version; }

    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => boundVersion;
    public RuleId RuleId { get; } = new("NQ-TP-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        // The canonical terminal gate owns setup ancestry; TP cannot revive a blocked pre-entry setup.
        var blocked = NasdaqFvgReplayPrerequisites.CheckVersion(context, RuleId, StrategyVersion);
        if (blocked is not null) return blocked;
        var latest = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-SL-001"));
        if (latest?.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result == RuleEvaluationResult.Passed
            && NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-SL-001")
            && latest.RuleFacts.SingleOrDefault(f => f.RuleId.Value == "NQ-SL-001")?.Fact is NasdaqHumanWickfillScenario2StopLossRuleFact wf2
            && wf2.Session.Matches(context))
            return EvaluateScenario2(context, wf2);
        var stop = latest?.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
            .OfType<NasdaqHumanStructuralStopLossRuleFact>().SingleOrDefault();
        if (stop is null || !stop.Session.Matches(context)
            || latest!.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Result != RuleEvaluationResult.Passed
            || !NasdaqFvgReplayPrerequisites.Eligible(context, "NQ-SL-001"))
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-SL-001");

        var selection = new NasdaqHumanTakeProfitObservationSelector().Select(context, stop);
        var support = selection switch
        {
            NasdaqHumanTakeProfitSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanTakeProfitSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanTakeProfitObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            StopLoss = stop,
            TakeProfitEvidence = support.Select(Evidence),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations.Select(Evidence),
        });
        if (selection is NasdaqHumanTakeProfitSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "Human-selected Take Profit targets conflict; no target or source ranking is defined.", proof);
        if (selection is NasdaqHumanTakeProfitSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable,
                    "A competing selected Take Profit target has unavailable exact sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The source-backed human Take Profit target is established for the exact canonical SL and setup.",
                proof, new NasdaqHumanTakeProfitRuleFact(stop, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human Take Profit selection is available for the exact eligible setup and selected SL.", proof);
    }

    private static ReplayRuleEvaluationDecision EvaluateScenario2(StrategyReplayContext context, NasdaqHumanWickfillScenario2StopLossRuleFact stop)
    {
        var selection = new NasdaqWickfillScenario2TakeProfitObservationSelector().Select(context, stop);
        object Evidence(NasdaqWickfillScenario2TakeProfitObservation o) => new
        {
            o.Session,
            o.Direction,
            o.Target.TargetReferencePrice,
            o.Target.TakeProfitPrice,
            o.EffectiveAtUtc,
            o.ObservedAtUtc,
            o.SourceReference,
            StopOwnerStep = o.StopOwner.Step,
            StopOwnerAsOfUtc = o.StopOwner.AsOfUtc,
            TargetSource = JsonSerializer.SerializeToElement(o.Target.Reference, o.Target.Reference.GetType())
        };
        var proof = JsonSerializer.Serialize(new
        {
            Path = "WickfillScenario2",
            SlFactReceived = true,
            stop.StopPrice,
            TakeProfitEvidence = selection.SupportingObservations.Select(Evidence),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations.Select(Evidence)
        });
        if (selection.IsConflict) return new(RuleEvaluationResult.HumanValidationRequired,
            "Scenario 2 human targets conflict; no importance, target or source ranking is defined.", proof);
        if (selection.IsUnique && !selection.UnavailableSourceObservations.Any(o => !o.SameFact(selection.SupportingObservations[0])))
            return new(RuleEvaluationResult.Passed,
                "Human-selected Scenario 2 SELL Low establishes the exact native TP for the prior canonical SL; no execution or outcome.",
                proof, new NasdaqWickfillScenario2TakeProfitRuleFact(selection));
        return new(selection.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "Canonical Scenario 2 SL received; a source-backed human-selected native important Low, exact TP/reference price equality and original selection/availability are required. No target is inferred.", proof);
    }

    private static object Evidence(NasdaqHumanTakeProfitObservation observation) => new
    {
        observation.Direction,
        observation.Target.TargetReferencePrice,
        observation.Target.TakeProfitPrice,
        observation.EffectiveAtUtc,
        observation.ObservedAtUtc,
        observation.SourceReference,
        // Preserve the concrete endpoint/model and exact source candles, rather than serializing only the base reference.
        TargetSource = JsonSerializer.SerializeToElement(observation.Target.Reference, observation.Target.Reference.GetType()),
    };
}
