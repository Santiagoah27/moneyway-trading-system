using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Maps typed human trigger selection to Step 4 only; implements no SC/IFVG detector or FVG confirmation.</summary>
public sealed class MoneyWayNasdaqHumanM5TriggerEvaluator : IReplayRuleEvaluator
{
    private readonly StrategyVersion boundVersion;
    public MoneyWayNasdaqHumanM5TriggerEvaluator() : this(MoneyWayNasdaqStrategyDefinition.Instance.Version) { }
    internal MoneyWayNasdaqHumanM5TriggerEvaluator(StrategyVersion version) { boundVersion = version; }

    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private readonly NasdaqHumanM5TriggerObservationSelector selector = new();
    public StrategyId StrategyId => Definition.StrategyId;
    public StrategyVersion StrategyVersion => boundVersion;
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
        var applicability = NasdaqStandardBranchApplicability.Check(context, "NQ-LIQ-003", preserveM5WindowSelection: true);
        if (applicability is not null) return applicability;

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

        NasdaqM5StrictCloseDetector.Detection? detection = null;
        if (StrategyVersion == MoneyWayNasdaqM5StrictCloseCandidate.Version || MoneyWayNasdaqWindow1130Candidate.UsesExplicitSessions(StrategyVersion))
        {
            var retained = context.PriorObservations.SelectMany(o => o.RuleFacts).Where(f => f.RuleId == RuleId)
                .SelectMany(f => f.Fact switch
                {
                    NasdaqHumanM5TriggerRuleFact trigger => trigger.Selection.SupportingObservations,
                    NasdaqM5StrictCloseDetectionFact computed => computed.Observations,
                    _ => Array.Empty<NasdaqHumanM5TriggerObservation>()
                }).Where(o => o.DecisiveTake.SameFact(take.Take)).Distinct().ToArray();
            // Existing confirmed events cannot be replaced by a later automatic detection.
            if (retained.Length == 0) detection = new NasdaqM5StrictCloseDetector().Evaluate(context, take);
            var inputs = context.InputObservations.Where(o => o is not NasdaqHumanM5TriggerObservation trigger || NasdaqM5StrictCloseDetector.QualifiesReviewedTrigger(trigger))
                .Concat(retained.Where(o => o.Origin == "DeterministicStrictClose"));
            if (detection?.Observation is { } automatic) inputs = inputs.Append(automatic);
            context = context.WithInputObservations(inputs);
        }
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
        var automaticSupport = supporting.Where(o => o.Origin == "DeterministicStrictClose").ToArray();
        var computedEvidence = automaticSupport.Length > 0 ? new NasdaqM5StrictCloseDetectionFact(automaticSupport) : null;
        if (selection is NasdaqHumanM5TriggerSelection.Conflict)
            return new(RuleEvaluationResult.HumanValidationRequired, "First-trigger evidence conflicts; no SC/IFVG priority is defined.", proof, computedEvidence);
        if (selection is NasdaqHumanM5TriggerSelection.Unique unique)
        {
            // Only an unresolved earlier/equally-first different event can defeat a trustworthy first selection.
            var requiredUnavailable = selection.UnavailableSourceObservations.Any(o => o.EffectiveAtUtc <= unique.Fact.EffectiveAtUtc
                && !o.SameFact(unique.Fact));
            if (requiredUnavailable)
                return new(RuleEvaluationResult.DataUnavailable, "A potentially first competing trigger has unavailable named sources.", proof, computedEvidence);
            return new(RuleEvaluationResult.Passed, unique.Fact.Origin == "DeterministicStrictClose"
                ? "The first Standard SC is established deterministically by a strict closed M5 break of the validated swing; no separate human SC classification is required."
                : "The first human-reviewed 5M trigger is established; optional continuation FVG remains separately audited supporting evidence.",
                proof, new NasdaqHumanM5TriggerRuleFact(unique));
        }
        if (selection.UnavailableSourceObservations.Count > 0)
            return new(RuleEvaluationResult.DataUnavailable, "Required named trigger source material is unavailable.", proof);
        if (detection is not null)
            return new(detection.UnconfirmedResult, "Candidate SC requires an available validated swing and a strict newly closed M5 break; no separate SC approval is requested.", detection.Evidence);
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
