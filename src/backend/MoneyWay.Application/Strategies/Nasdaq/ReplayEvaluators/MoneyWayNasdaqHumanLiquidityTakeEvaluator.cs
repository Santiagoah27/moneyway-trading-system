using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Consumes explicit human relevant selection and prior canonical facts; discovers no market events.</summary>
public sealed class MoneyWayNasdaqHumanLiquidityTakeEvaluator : IReplayRuleEvaluator
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly RuleId TakeRule = new("NQ-LIQ-003");
    private static readonly string[] Prerequisites = ["NQ-TIME-003", "NQ-H4-001", "NQ-LIQ-002", "NQ-TIME-001"];
    public StrategyId StrategyId => Definition.StrategyId;
    public StrategyVersion StrategyVersion => Definition.Version;
    public RuleId RuleId => TakeRule;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");
        var session = NasdaqDemoSessionIdentity.FromContext(context);
        var terminal = context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>().FirstOrDefault(f => f.Session == session && f.IsSessionInvalidated);
        if (terminal is not null)
            return new(RuleEvaluationResult.Failed, "The Nasdaq session remains terminally invalidated; later takes cannot revive it.", terminal.EvidenceReference, terminal);
        var initiating = context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>().FirstOrDefault(f => f.Session == session && !f.IsSessionInvalidated);
        var candidates = context.InputObservations.OfType<NasdaqHumanRelevantLiquidityTakeObservation>().Where(o => o.Session == session).ToArray();
        var later = initiating is null ? [] : candidates.Where(o => o.InitiatingTake is not null && o.InitiatingTake.SameFact(initiating.InitiatingTake)).ToArray();
        // Sequential lifecycle events have exact initiating lineage; they are not competing initial claims.
        var selections = (later.Length > 0 ? later : candidates.Where(o => o.InitiatingTake is null))
            .OrderBy(o => o.ObservedAtUtc).ThenBy(o => o.SourceReference, StringComparer.Ordinal)
            .ThenBy(o => JsonSerializer.Serialize(Describe(o.Take)), StringComparer.Ordinal).ToArray();
        var selector = new NasdaqHumanLiquidityTakeObservationSelector();
        var resolved = selections.Select(o => (Selection: o, Result: selector.Select(context, o.Take.Reference))).ToArray();
        var valid = resolved.Where(p => p.Result is NasdaqHumanLiquidityTakeSelection.Unique u && u.Fact.SameFact(p.Selection.Take)).ToArray();
        var conflicts = resolved.Where(p => p.Result is NasdaqHumanLiquidityTakeSelection.Conflict).ToArray();
        var unavailable = resolved.SelectMany(p => p.Result.UnavailableSourceObservations).Distinct().ToArray();
        var proof = JsonSerializer.Serialize(new
        {
            Session = session,
            RelevantSelections = selections.Select(o => new { o.SourceReference, o.ObservedAtUtc, Take = Describe(o.Take) }),
            Conflicts = conflicts.SelectMany(p => ((NasdaqHumanLiquidityTakeSelection.Conflict)p.Result).SupportingObservations).Select(Describe),
            Supporting = valid.SelectMany(p => ((NasdaqHumanLiquidityTakeSelection.Unique)p.Result).SupportingObservations).Distinct().Select(Describe),
            Unavailable = unavailable.Select(Describe),
        });
        if (conflicts.Length > 0 || (valid.Length > 0 && valid.Any(p => !p.Selection.Take.SameFact(valid[0].Selection.Take))))
            return new(RuleEvaluationResult.HumanValidationRequired, "Relevant initiating-take assertions conflict; no winner is selected.", proof);
        if (unavailable.Any(o => valid.Length == 0 || !o.SameFact(valid[0].Selection.Take)))
            return new(RuleEvaluationResult.DataUnavailable, "A source for a competing claim in the required relevant-take context is unavailable.", proof);
        if (valid.Length == 0)
            return new(RuleEvaluationResult.HumanValidationRequired, "No usable explicit human relevant initiating-take selection is observable.", proof);
        var take = valid[0].Selection.Take;
        var history = context.PriorObservations.Where(o => o.AsOfUtc <= take.EffectiveAtUtc).ToArray();
        var window = history.SelectMany(o => o.RuleFacts).Select(f => f.Fact).OfType<NasdaqTradingWindowFact>()
            .LastOrDefault(f => f.Contains(take.EffectiveAtUtc));
        var prior = Prerequisites.Select(id => new
        {
            RuleId = id,
            Evaluation = history.SelectMany(o => o.Evaluations).LastOrDefault(e => e.RuleId.Value == id
                && window is not null && e.EvaluatedAtUtc >= window.DayStartUtc),
        }).ToArray();
        var evidence = JsonSerializer.Serialize(new
        {
            TakeEvidence = JsonSerializer.Deserialize<JsonElement>(proof),
            Prerequisites = prior.Select(p => new { p.RuleId, p.Evaluation?.Result, p.Evaluation?.EvaluatedAtUtc }),
            Window = window
        });
        // Local projection uses the same definition sequence as canonical aggregation, not a global status ranking.
        var blocker = prior.FirstOrDefault(p => p.Evaluation?.Result != RuleEvaluationResult.Passed);
        if (blocker is not null)
        {
            var result = blocker.Evaluation?.Result ?? RuleEvaluationResult.Waiting;
            if (result == RuleEvaluationResult.NotApplicable) result = RuleEvaluationResult.Waiting;
            return new(result, "A required prior canonical prerequisite has not passed for this event/session.", evidence);
        }
        if (window is not null && context.AsOfUtc >= window.EndUtc)
            return new(RuleEvaluationResult.Failed, "The canonical TIME-owned operational cutoff has been reached; no pre-entry continuation is allowed.", evidence);
        if (window is null || !window.Contains(context.AsOfUtc))
            return new(RuleEvaluationResult.Waiting, "No canonical operational-window fact permits this event/current frame.", evidence);
        if (context.PriorObservations.Any(o => o.AsOfUtc >= window.StartUtc && o.AsOfUtc < window.EndUtc
            && o.WorkflowProgression?.EstablishedRuleIds.Any(r => r.Value == "NQ-M1-003") == true))
            return new(RuleEvaluationResult.NotApplicable, "Pre-entry take evaluation does not manage a completed entry sequence.", evidence);
        var h4 = history.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-H4-001").Select(f => f.Fact)
            .OfType<NasdaqHumanH4ContextRuleFact>().LastOrDefault(f => f.Selection.SupportingObservations.Any(o => o.Session == session
                && o.ObservedAtUtc <= take.EffectiveAtUtc && o.EffectiveAtUtc <= take.EffectiveAtUtc));
        if (h4 is null || h4.Selection.Fact.PermittedDirection == NasdaqHumanH4PermittedDirection.Unresolved
            || h4.Selection.Fact.ContextKind == NasdaqHumanH4ContextKind.Unresolved)
            return new(RuleEvaluationResult.HumanValidationRequired, "No resolved canonical H4 direction was available at the take event.", evidence);
        var direction = h4.Selection.Fact.PermittedDirection;
        var aligned = take.Side == NasdaqStructuralLiquiditySide.Low
            ? direction == NasdaqHumanH4PermittedDirection.Buy : direction == NasdaqHumanH4PermittedDirection.Sell;
        evidence = JsonSerializer.Serialize(new
        {
            Evidence = JsonSerializer.Deserialize<JsonElement>(evidence),
            H4Direction = direction,
            H4Sources = h4.Selection.SupportingObservations.Select(o => new { o.SourceReference, o.ObservedAtUtc, o.EffectiveAtUtc }),
            TakeEffectiveAtUtc = take.EffectiveAtUtc,
            SessionInvalidated = !aligned
        });
        return new(aligned ? RuleEvaluationResult.Passed : RuleEvaluationResult.Failed,
            aligned ? "An eligible relevant take aligns with canonical H4 direction; only later 5M progression is enabled."
                : "A relevant misaligned pre-entry take terminally invalidates the Nasdaq session.",
            evidence, new NasdaqLiquidityTakeRuleFact(take, direction, !aligned, evidence, initiating?.InitiatingTake));
    }

    private static object Describe(NasdaqHumanLiquidityTakeObservation o) => new
    {
        ReferenceKind = o.Reference.GetType().Name,
        EventKind = o.Event.GetType().Name,
        EventIdentity = o.Event.SortKey,
        ReferenceIdentity = o.Reference switch
        {
            NasdaqLiquidityTakeReference.SessionLevel r => JsonSerializer.Serialize(new { r.Endpoint, r.SelectedAtUtc, r.SourceReference }),
            NasdaqLiquidityTakeReference.Structural r => JsonSerializer.Serialize(new
            {
                r.Member.Role,
                r.Member.Model,
                r.Member.PriceOwnership,
                r.Member.StructuralPrice,
                r.Member.Timeframe,
                Members = r.Member.Members.Select(c => c.OpenTimeUtc)
            }),
            _ => throw new InvalidOperationException("Unknown reference branch."),
        },
        SourceBindings = o.Reference.Sources.Select(c => new { c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc }),
        o.Side,
        o.ReferencePrice,
        o.ObservedPrice,
        TakeEffectiveAtUtc = o.EffectiveAtUtc,
        o.ReferenceEligibleAtUtc,
        o.ObservedAtUtc,
        o.SourceReference,
        Provenance = JsonSerializer.Deserialize<JsonElement>(o.AuditKey),
    };
}
