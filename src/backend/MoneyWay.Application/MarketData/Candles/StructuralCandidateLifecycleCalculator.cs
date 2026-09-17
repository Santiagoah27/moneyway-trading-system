using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Advances an active candidate lifecycle using a previously resolved closed-candle boundary.</summary>
public sealed class StructuralCandidateLifecycleCalculator
{
    public StructuralCandidateLifecycleResult Evaluate(
        CorrectionTurnLifecycleResult activeCandidateLifecycle,
        StructuralCandidateTurnBoundaryResult boundary)
    {
        ArgumentNullException.ThrowIfNull(activeCandidateLifecycle);
        ArgumentNullException.ThrowIfNull(boundary);

        var previousTurn = activeCandidateLifecycle.ResultingTurnCandles;
        if (activeCandidateLifecycle.Disposition != CorrectionTurnLifecycleDisposition.ActiveCorrection
            || previousTurn.Count == 0)
        {
            throw new ArgumentException("The structural candidate must have an active correction turn.", nameof(activeCandidateLifecycle));
        }

        if (activeCandidateLifecycle.LastProcessedCandle is { } lastProcessed
            && boundary.CurrentCandle.CloseTimeUtc <= lastProcessed.CloseTimeUtc)
        {
            throw new ArgumentException("The boundary candle must follow the active candidate lifecycle.", nameof(boundary));
        }

        if (!SameCandles(previousTurn, boundary.PreviousCandidateTurnCandles))
            throw new ArgumentException("The boundary must belong to the active candidate turn.", nameof(boundary));

        if (boundary.Kind == StructuralCandidateTurnBoundaryKind.ContinueCandidateTurn
            && !ContinuesTurn(previousTurn, boundary.Lifecycle.ResultingTurnCandles, boundary.CurrentCandle))
        {
            throw new ArgumentException("The boundary continuation must preserve the active turn and append the current candle once.", nameof(boundary));
        }

        return new StructuralCandidateLifecycleResult(boundary);
    }

    private static bool SameCandles(IReadOnlyList<Candle> expected, IReadOnlyList<Candle> actual)
    {
        if (expected.Count != actual.Count) return false;
        for (var index = 0; index < expected.Count; index++)
        {
            if (!ReferenceEquals(expected[index], actual[index])) return false;
        }

        return true;
    }

    private static bool ContinuesTurn(
        IReadOnlyList<Candle> previousTurn,
        IReadOnlyList<Candle> resultingTurn,
        Candle currentCandle)
    {
        if (resultingTurn.Count != previousTurn.Count + 1
            || !ReferenceEquals(resultingTurn[^1], currentCandle))
        {
            return false;
        }

        for (var index = 0; index < previousTurn.Count; index++)
        {
            if (!ReferenceEquals(resultingTurn[index], previousTurn[index])) return false;
        }

        return true;
    }
}
