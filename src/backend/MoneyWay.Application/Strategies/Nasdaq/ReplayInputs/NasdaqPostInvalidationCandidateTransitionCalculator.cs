using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Classifies one candidate H4 candle and delegates to exactly one ADR 0013 transition primitive.</summary>
public sealed class NasdaqPostInvalidationCandidateTransitionCalculator
{
    private readonly NasdaqPostInvalidationCandidateContinuationCalculator continuationCalculator = new();
    private readonly NasdaqDirectCandidateBreakoutCompletionCalculator directBreakoutCalculator = new();
    private readonly NasdaqPostInvalidationCandidateRebuildTransitionCalculator rebuildCalculator = new();
    private readonly NasdaqPostInvalidationCandidateTrackingStartCalculator trackingStartCalculator = new();
    private readonly NasdaqCandidateLifecycleDecisionCalculator decisionCalculator = new();

    public NasdaqPostInvalidationCandidateTransitionResult Evaluate(
        NasdaqPostInvalidationCandidateState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        var decision = decisionCalculator.Evaluate(current.CandidateSide, current.CandidateGeometry,
            current.FrozenImpulseTerminal, current.LastProcessedCandle, candle);
        return decision switch
        {
            NasdaqCandidateLifecycleDecision.CandidateContinues =>
                new NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues(continuationCalculator.Evaluate(current, candle)),
            NasdaqCandidateLifecycleDecision.DirectCompleted =>
                new NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted(directBreakoutCalculator.Evaluate(current, candle)),
            NasdaqCandidateLifecycleDecision.CollisionBreakout collision =>
                new NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout(
                    NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator.Materialize(current, collision)),
            NasdaqCandidateLifecycleDecision.RebuiltTracking =>
                new NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking(trackingStartCalculator.Evaluate(current, candle)),
            NasdaqCandidateLifecycleDecision.RebuildPending =>
                new NasdaqPostInvalidationCandidateTransitionResult.RebuildPending(rebuildCalculator.Evaluate(current, candle)),
            _ => throw new InvalidOperationException("The candidate decision is not supported."),
        };
    }
}
