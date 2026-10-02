using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Observes a later closed candle against the active pair without consuming it or changing correction state.</summary>
public sealed class NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator
{
    private readonly StructuralBodyCloseBreakCalculator breakCalculator = new();
    private readonly CandleBodyDirectionCalculator bodyCalculator = new();

    public NasdaqPostCompletionActiveCorrectionBoundaryObservationResult Evaluate(
        NasdaqPostCompletionActiveCorrectionState current, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        var previous = current.MarketCursor;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4 || candle.Timeframe != previous.Timeframe)
            throw new ArgumentException("The incoming candle must belong to the same H4 series.", nameof(candle));
        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
            throw new ArgumentException("The incoming closed candle must follow the market cursor without overlap.", nameof(candle));

        var (continuationDirection, invalidationDirection) = current.ActivePair.ActiveExtremeSide switch
        {
            StructuralTurnBodyCoordinateSide.Upper => (StructuralBreakDirection.Upper, StructuralBreakDirection.Lower),
            StructuralTurnBodyCoordinateSide.Lower => (StructuralBreakDirection.Lower, StructuralBreakDirection.Upper),
            _ => throw new ArgumentOutOfRangeException(nameof(current)),
        };
        var continuation = breakCalculator.Evaluate(candle, current.ActivePair.ActiveExtremeGeometry.StructuralPrice, continuationDirection);
        var invalidation = breakCalculator.Evaluate(candle, current.ActivePair.ProtectedTurnGeometry.StructuralPrice, invalidationDirection);
        if (continuation.IsConfirmed && invalidation.IsConfirmed)
            throw new InvalidOperationException("An ordered active pair cannot confirm both structural breaks for one close.");
        var body = bodyCalculator.Evaluate(candle);
        if (continuation.IsConfirmed)
            return new NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.ContinuationBreak(current, continuation, invalidation, body);
        if (invalidation.IsConfirmed)
            return new NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.ProtectedTurnInvalidated(current, continuation, invalidation, body);
        return new NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange(current, continuation, invalidation, body);
    }
}
