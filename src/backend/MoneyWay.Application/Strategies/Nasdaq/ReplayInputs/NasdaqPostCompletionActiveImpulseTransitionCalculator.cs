using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Detects protected-turn invalidation, correction start or open impulse from one later closed H4 candle.</summary>
public sealed class NasdaqPostCompletionActiveImpulseTransitionCalculator
{
    private readonly StructuralBodyCloseBreakCalculator breakCalculator = new();
    private readonly CandleBodyDirectionCalculator bodyCalculator = new();
    private readonly CorrectionBodyDirectionCalculator correctionBodyCalculator = new();

    public NasdaqPostCompletionActiveImpulseTransitionResult Evaluate(NasdaqPostCompletionActiveImpulseState current, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        var previous = current.MarketCursor;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4 || candle.Timeframe != previous.Timeframe)
            throw new ArgumentException("The next candle must belong to the same H4 series.", nameof(candle));
        if (candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
            throw new ArgumentException("The next closed candle must follow the market cursor without overlap.", nameof(candle));

        var (breakDirection, extremeSide) = current.ActiveExtremeSide switch
        {
            StructuralTurnBodyCoordinateSide.Upper => (StructuralBreakDirection.Lower, CorrectionOriginExtremeSide.Ceiling),
            StructuralTurnBodyCoordinateSide.Lower => (StructuralBreakDirection.Upper, CorrectionOriginExtremeSide.Floor),
            _ => throw new ArgumentOutOfRangeException(nameof(current)),
        };
        var protectedTurnBreak = breakCalculator.Evaluate(candle, current.ValidatedProtectedTurn.StructuralPrice, breakDirection);
        var body = bodyCalculator.Evaluate(candle);
        if (protectedTurnBreak.IsConfirmed)
            return new NasdaqPostCompletionActiveImpulseTransitionResult.ProtectedTurnInvalidated(current, protectedTurnBreak, body);
        if (correctionBodyCalculator.Evaluate(extremeSide, body))
            return new NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted(current, protectedTurnBreak, body,
                new NasdaqPostCompletionActiveExtremeMembershipEvent(current.Episode, candle));

        return new NasdaqPostCompletionActiveImpulseTransitionResult.ImpulseRemainsOpen(current, protectedTurnBreak, body,
            new NasdaqPostCompletionActiveImpulseState(current, candle));
    }
}
