using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Maps typed human trigger selection to Step 4 only; implements no SC/IFVG detector or FVG confirmation.</summary>
public sealed class MoneyWayNasdaqHumanM5TriggerEvaluator : IReplayRuleEvaluator
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private readonly NasdaqHumanM5TriggerObservationSelector selector = new();
    public StrategyId StrategyId => Definition.StrategyId;
    public StrategyVersion StrategyVersion => Definition.Version;
    public RuleId RuleId { get; } = new("NQ-M5-001");

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");
        // The canonical use case applies this gate before calling us. Preserve the same safety for direct calls.
        var gate = context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<IReplayRuleGateFact>().FirstOrDefault(f => f.Blocks(context, RuleId));
        if (gate is not null)
            return new(RuleEvaluationResult.Failed, "A canonical terminal session fact blocks this downstream rule.", gate.EvidenceReference);
        var session = NasdaqDemoSessionIdentity.FromContext(context);
        var take = context.PriorObservations.SelectMany(o => o.RuleFacts)
            .Where(f => f.RuleId.Value == "NQ-LIQ-003").Select(f => f.Fact).OfType<NasdaqLiquidityTakeRuleFact>()
            .LastOrDefault(f => f.Session == session);
        if (take is null)
            return new(RuleEvaluationResult.Waiting, "No canonical same-session decisive take has established the prerequisite stage.", null);
        var prior = context.PriorObservations.LastOrDefault(o => o.RuleFacts.Any(f => f.RuleId.Value == "NQ-LIQ-003"
            && f.Fact is NasdaqLiquidityTakeRuleFact t && t.Session == session));
        var latest = context.PriorObservations.Where(o => o.AsOfUtc >= prior!.AsOfUtc)
            .LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == "NQ-LIQ-003"));
        var prerequisite = latest?.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-003");
        var eligibility = latest?.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-LIQ-003");
        if (prerequisite?.Result != RuleEvaluationResult.Passed || eligibility?.EstablishesProgression != true)
            return new(prerequisite?.Result == RuleEvaluationResult.DataUnavailable ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.Waiting,
                "The canonical take prerequisite has not established eligible progression; no trigger review is due yet.",
                JsonSerializer.Serialize(new { Session = session, prerequisite?.Result, Eligibility = eligibility, prerequisite?.EvidenceReference }));

        var selection = selector.Select(context, take);
        var supporting = selection switch
        {
            NasdaqHumanM5TriggerSelection.Unique u => u.SupportingObservations,
            NasdaqHumanM5TriggerSelection.Conflict c => c.SupportingObservations,
            _ => Array.Empty<NasdaqHumanM5TriggerObservation>(),
        };
        var proof = JsonSerializer.Serialize(new
        {
            Session = session,
            CanonicalTakeEvidence = take.EvidenceReference,
            PrerequisiteEvaluatedAtUtc = prerequisite.EvaluatedAtUtc,
            TriggerEvidence = supporting.Select(Describe),
            UnavailableSourceEvidence = selection.UnavailableSourceObservations.Select(Describe),
        });
        if (selection is NasdaqHumanM5TriggerSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "First-trigger evidence conflicts; no SC/IFVG priority is defined.", proof);
        if (selection is NasdaqHumanM5TriggerSelection.Unique unique)
        {
            // Only an unresolved earlier/equally-first different event can defeat a trustworthy first selection.
            var requiredUnavailable = selection.UnavailableSourceObservations.Any(o => o.EffectiveAtUtc <= unique.Fact.EffectiveAtUtc
                && !o.SameFact(unique.Fact));
            if (requiredUnavailable)
                return new(RuleEvaluationResult.DataUnavailable, "A potentially first competing trigger has unavailable named sources.", proof);
            return new(RuleEvaluationResult.Passed, "The first human-reviewed 5M trigger is established; mandatory FVG remains a separate next stage.",
                proof, new NasdaqHumanM5TriggerRuleFact(unique));
        }
        if (selection.UnavailableSourceObservations.Count > 0)
            return new(RuleEvaluationResult.DataUnavailable, "Required named trigger source material is unavailable.", proof);
        return new(RuleEvaluationResult.HumanValidationRequired, "The eligible setup has no usable human-reviewed first 5M trigger evidence.", proof);
    }

    private static object Describe(NasdaqHumanM5TriggerObservation observation) => new
    {
        TriggerKind = observation.Event.Kind.ToString(),
        TriggerEffectiveAtUtc = observation.EffectiveAtUtc,
        observation.ObservedAtUtc,
        SameCandleOrder = observation.SameCandleOrder.ToString(),
        Observation = observation,
    };
}
