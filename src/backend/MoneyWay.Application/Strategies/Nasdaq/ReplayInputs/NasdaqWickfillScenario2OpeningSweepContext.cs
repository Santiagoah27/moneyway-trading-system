using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Immutable evidence join of reviewed pre-open context and an established canonical initiating London take.
/// It is not a rule fact, eligibility, branch progression, signal or order (ADR 0026 section 2.1).</summary>
public sealed class NasdaqWickfillScenario2OpeningSweepContext
{
    private NasdaqWickfillScenario2OpeningSweepContext(NasdaqHumanWickfillScenario2ContextSelection.Unique preOpenContext,
        StrategyReplayContextObservation takeObservation, NasdaqLiquidityTakeRuleFact take,
        NasdaqHumanH4ContextRuleFact h4, NasdaqHumanLiquidityTakeSelection.Unique takeSelection)
    {
        PreOpenContext = preOpenContext;
        TakeObservation = takeObservation;
        Take = take;
        CanonicalH4Context = h4;
        TakeSelection = takeSelection;
        ObservedAtUtc = preOpenContext.SupportingObservations.Concat(preOpenContext.UnavailableSourceObservations)
            .Select(o => o.ObservedAtUtc).Concat(takeSelection.SupportingObservations.Concat(takeSelection.UnavailableSourceObservations)
                .Select(o => o.ObservedAtUtc)).Append(takeObservation.AsOfUtc).Max();
    }

    public NasdaqHumanWickfillScenario2ContextSelection.Unique PreOpenContext { get; }
    public StrategyReplayContextObservation TakeObservation { get; }
    public NasdaqLiquidityTakeRuleFact Take { get; }
    public NasdaqHumanH4ContextRuleFact CanonicalH4Context { get; }
    public NasdaqHumanLiquidityTakeSelection.Unique TakeSelection { get; }
    public NasdaqDemoSessionIdentity Session => Take.Session;
    public DateTimeOffset EffectiveAtUtc => Take.Take.EffectiveAtUtc;
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary>Joins already selected/evaluated evidence visible in the bounded replay context.
    /// No evaluator is invoked and no trading-window boundary is calculated.</summary>
    public static NasdaqWickfillScenario2OpeningSweepContext From(StrategyReplayContext context,
        NasdaqHumanWickfillScenario2ContextSelection.Unique preOpenContext, StrategyReplayContextObservation takeObservation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(preOpenContext);
        ArgumentNullException.ThrowIfNull(takeObservation);
        var session = preOpenContext.Fact.Session;
        var currentOwner = takeObservation.AsOfUtc == context.AsOfUtc && takeObservation.Step == context.Step
            && takeObservation.StrategyId == context.StrategyId && takeObservation.StrategyVersion == context.StrategyVersion
            && takeObservation.ProviderId == context.ProviderId && takeObservation.Symbol == context.Symbol;
        if (!session.Matches(context) || !currentOwner && !context.PriorObservations.Contains(takeObservation))
            throw new ArgumentException("The same-session canonical take observation must belong to this frame or its visible history.", nameof(takeObservation));
        var visible = new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(context, session);
        if (visible is not NasdaqHumanWickfillScenario2ContextSelection.Unique unique
            || !SameObservations(unique.SupportingObservations, preOpenContext.SupportingObservations)
            || !SameObservations(unique.UnavailableSourceObservations, preOpenContext.UnavailableSourceObservations))
            throw new ArgumentException("The selected context must remain the exact usable Unique selection at this frame.", nameof(preOpenContext));
        var take = takeObservation.RuleFacts.Where(f => f.RuleId.Value == "NQ-LIQ-003").Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>().SingleOrDefault();
        if (take is null || take.Session != session || take.IsSessionInvalidated
            || takeObservation.Evaluations.SingleOrDefault(e => e.RuleId.Value == "NQ-LIQ-003")?.Result != RuleEvaluationResult.Passed
            || takeObservation.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-LIQ-003")?.EstablishesProgression != true)
            throw new ArgumentException("An established successful same-session canonical NQ-LIQ-003 fact is required.", nameof(takeObservation));
        if (take.Take.Reference is not NasdaqLiquidityTakeReference.SessionLevel reference
            || reference.Endpoint is not (NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh or NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow))
            throw new ArgumentException("Only a reviewed LondonHigh or LondonLow SessionLevel reference is admissible.", nameof(takeObservation));
        if (take.Direction != preOpenContext.Fact.Direction)
            throw new ArgumentException("The canonical take direction must match the reviewed Scenario 2 direction.", nameof(takeObservation));
        if (!take.Take.SameFact(take.InitiatingTake))
            throw new ArgumentException("A later extension cannot replace the initiating opening-sweep event.", nameof(takeObservation));
        if (preOpenContext.Fact.EffectiveAtUtc >= take.Take.EffectiveAtUtc)
            throw new ArgumentException("Reviewed pre-open context must strictly precede the opening sweep.", nameof(preOpenContext));
        if (take.Take.ObservedAtUtc > takeObservation.AsOfUtc
            || context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
                .OfType<NasdaqLiquidityTakeRuleFact>().Any(f => f.Session == session && f.IsSessionInvalidated))
            throw new ArgumentException("Unavailable or terminally invalidated take evidence cannot be composed.", nameof(takeObservation));
        // Resolve the same causal H4 history consumed by NQ-LIQ-003; never parse its diagnostic prose/JSON.
        var h4 = context.PriorObservations.Where(o => o.AsOfUtc < takeObservation.AsOfUtc && o.AsOfUtc <= take.Take.EffectiveAtUtc)
            .SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value == "NQ-H4-001").Select(f => f.Fact)
            .OfType<NasdaqHumanH4ContextRuleFact>().LastOrDefault(f => f.Selection.SupportingObservations
                .Any(o => o.Session == session && o.ObservedAtUtc <= take.Take.EffectiveAtUtc && o.EffectiveAtUtc <= take.Take.EffectiveAtUtc));
        if (h4 is null || !h4.Selection.Fact.Equals(preOpenContext.Fact.H4Context.Fact))
            throw new ArgumentException("The reviewed context must match the canonical H4 fact used at the take event.", nameof(preOpenContext));
        var takeSelection = new NasdaqHumanLiquidityTakeObservationSelector().Select(context, reference);
        if (takeSelection is not NasdaqHumanLiquidityTakeSelection.Unique selected || !selected.Fact.SameFact(take.Take)
            || selected.UnavailableSourceObservations.Any(o => !o.SameFact(take.Take)))
            throw new ArgumentException("The exact canonical take sources must remain observable and compatible.", nameof(takeObservation));
        return new(preOpenContext, takeObservation, take, h4, selected);
    }

    private static bool SameObservations(IReadOnlyList<NasdaqHumanWickfillScenario2ContextObservation> one,
        IReadOnlyList<NasdaqHumanWickfillScenario2ContextObservation> other) => one.Count == other.Count
        && one.Zip(other).All(pair => pair.First.SameFact(pair.Second)
            && pair.First.ObservedAtUtc == pair.Second.ObservedAtUtc && pair.First.SourceReference == pair.Second.SourceReference
            && pair.First.H4Context.ObservedAtUtc == pair.Second.H4Context.ObservedAtUtc
            && pair.First.H4Context.SourceReference == pair.Second.H4Context.SourceReference);
}
