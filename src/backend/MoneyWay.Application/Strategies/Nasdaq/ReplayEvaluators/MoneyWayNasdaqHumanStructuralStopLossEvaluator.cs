using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Evaluates only documented structural stop evidence for an established pre-entry setup.</summary>
public sealed class MoneyWayNasdaqHumanStructuralStopLossEvaluator : IReplayRuleEvaluator
{
    private readonly StrategyVersion boundVersion;
    public MoneyWayNasdaqHumanStructuralStopLossEvaluator() : this(MoneyWayNasdaqStrategyDefinition.Instance.Version) { }
    internal MoneyWayNasdaqHumanStructuralStopLossEvaluator(StrategyVersion version) { boundVersion = version; }

    public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
    public StrategyVersion StrategyVersion => boundVersion;
    public RuleId RuleId { get; } = new("NQ-SL-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        var blocked = NasdaqFvgReplayPrerequisites.CheckVersion(context, RuleId, StrategyVersion);
        if (blocked is not null) return blocked;

        if (NasdaqHumanWickfillScenario2StopLossRuleFact.HasScenario2Ancestry(context))
            return EvaluateScenario2(context);

        var established = context.PriorObservations.LastOrDefault(o => o.WorkflowProgression?.RuleEligibility
            .SingleOrDefault(e => e.RuleId.Value == "NQ-M1-003")?.EstablishesProgression == true);
        var preEntry = established?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-003").Select(f => f.Fact)
            .OfType<NasdaqPreEntryEligibilityRuleFact>().SingleOrDefault();
        if (preEntry is null || !preEntry.Session.Matches(context)
            || established!.Evaluations.Single(e => e.RuleId.Value == "NQ-M1-003").Result != RuleEvaluationResult.Passed)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-M1-003");

        var selection = new NasdaqHumanStructuralStopLossObservationSelector().Select(context, preEntry);
        var support = selection switch
        {
            NasdaqHumanStructuralStopLossSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanStructuralStopLossSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanStructuralStopLossObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            PreEntryEligibility = preEntry,
            StructuralStopLossEvidence = support,
            UnavailableSourceEvidence = selection.UnavailableSourceObservations,
        });
        if (selection is NasdaqHumanStructuralStopLossSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired,
                "Selected structural Stop Loss observations conflict; no anchor or price ranking is defined.", proof);
        if (selection is NasdaqHumanStructuralStopLossSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable,
                    "A competing selected structural Stop Loss has unavailable exact 5M sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The documented 5M structural protection anchor and distinct Stop Price are established for the exact pre-entry setup.",
                proof, new NasdaqHumanStructuralStopLossRuleFact(preEntry, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0
                ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "No usable human structural Stop Loss is available for the exact pre-entry setup.", proof);
    }

    private static ReplayRuleEvaluationDecision EvaluateScenario2(StrategyReplayContext context)
    {
        var owner = context.PriorObservations.LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-WF2-002"));
        if (owner is null) return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-002");
        NasdaqWickfillScenario2PreEntryEligibilityRuleFact eligibility;
        try { eligibility = NasdaqHumanWickfillScenario2StopLossObservation.RequireEligibility(owner!); }
        catch (ArgumentException) { return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-002"); }
        var active = context.PriorObservations.Last(o => o.LifecycleProgression is not null).LifecycleProgression!.ActiveInstance!;
        if (owner!.AsOfUtc >= context.AsOfUtc || !eligibility.Session.Matches(context)
            || owner.LifecycleProgression!.ActiveInstance!.InstanceId != active.InstanceId
            || active.StartedAtUtc != eligibility.Reversal.Applicability.Fact.InitialTakeObservation.AsOfUtc)
            return NasdaqFvgReplayPrerequisites.Waiting(context, "NQ-WF2-002");

        var selection = new NasdaqHumanWickfillScenario2StopLossObservationSelector().Select(context);
        var support = selection switch
        {
            NasdaqHumanWickfillScenario2StopLossSelection.Unique unique => unique.SupportingObservations,
            NasdaqHumanWickfillScenario2StopLossSelection.Conflict conflict => conflict.Alternatives,
            _ => Array.Empty<NasdaqHumanWickfillScenario2StopLossObservation>()
        };
        object Evidence(NasdaqHumanWickfillScenario2StopLossObservation o) => new
        {
            o.Session,
            o.Direction,
            o.ProtectiveReference,
            o.StopPrice,
            o.EffectiveAtUtc,
            o.ObservedAtUtc,
            o.SourceReference,
            EligibilityOwnerStep = o.EligibilityOwner.Step,
            EligibilityOwnerAsOfUtc = o.EligibilityOwner.AsOfUtc,
            StartStep = o.StartOwner.Step,
            StartAsOfUtc = o.StartOwner.AsOfUtc
        };
        var proof = JsonSerializer.Serialize(new
        {
            Path = "WickfillScenario2",
            SelectorEvaluated = true,
            owner.Step,
            EligibilityEvaluatedAtUtc = owner.AsOfUtc,
            eligibility.Session,
            eligibility.EligibilityEffectiveAtUtc,
            StructuralStopLossEvidence = support.Select(Evidence),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations.Select(Evidence),
            RequiredEvidence = new[] { "Exact native protective reference and price", "Independently approved initial StopPrice strictly above that reference",
                "Original effective and available times", "Human provenance and verified native source applicability" }
        });
        if (selection is NasdaqHumanWickfillScenario2StopLossSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "Scenario 2 native SL reviews conflict; no reference or stop is ranked.", proof);
        if (selection is NasdaqHumanWickfillScenario2StopLossSelection.Unique selected)
        {
            if (selection.UnavailableSourceObservations.Any(o => !o.SameFact(selected.Fact)))
                return new(RuleEvaluationResult.DataUnavailable, "A competing Scenario 2 native SL has unavailable exact sources.", proof);
            return new(RuleEvaluationResult.Passed,
                "The Scenario 2 SL selector established the human-selected native protective reference and independently documented initial StopPrice; no execution or risk approval is established.",
                proof, new NasdaqHumanWickfillScenario2StopLossRuleFact(eligibility, selected));
        }
        return new(selection.UnavailableSourceObservations.Count > 0 ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.HumanValidationRequired,
            "Scenario 2 SL selector evaluated: no usable native protective reference/price and independently approved initial StopPrice with original availability/provenance; current source ancestry must remain valid. USTEC testimony alone cannot supply US100 evidence.", proof);
    }
}
