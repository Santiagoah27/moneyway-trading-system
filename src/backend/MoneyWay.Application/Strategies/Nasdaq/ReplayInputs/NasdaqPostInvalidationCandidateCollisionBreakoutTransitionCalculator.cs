using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Detects one same-candle collision and retains the canonical candidate resolution.</summary>
public sealed class NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator
{
    private readonly NasdaqCandidateLifecycleDecisionCalculator decisionCalculator = new();

    public NasdaqPostInvalidationCandidateRebuildBreakoutState Evaluate(
        NasdaqPostInvalidationCandidateState current, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        var decision = decisionCalculator.Evaluate(current.CandidateSide, current.CandidateGeometry,
            current.FrozenImpulseTerminal, current.LastProcessedCandle, candle);
        if (decision is not NasdaqCandidateLifecycleDecision.CollisionBreakout collision)
            throw new ArgumentException("The candle must strictly migrate the candidate and break the frozen terminal.", nameof(candle));
        return Materialize(current, collision);
    }

    internal static NasdaqPostInvalidationCandidateRebuildBreakoutState Materialize(
        NasdaqPostInvalidationCandidateState current, NasdaqCandidateLifecycleDecision.CollisionBreakout collision) =>
        new(collision, current);
}
