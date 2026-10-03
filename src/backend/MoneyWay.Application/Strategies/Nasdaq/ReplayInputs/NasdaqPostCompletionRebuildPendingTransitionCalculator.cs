using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Observes one later closed H4 candle without materializing the next rebuild lifecycle state.</summary>
public sealed class NasdaqPostCompletionRebuildPendingTransitionCalculator
{
    private readonly StructuralCandidateExtremeCalculator migrationCalculator = new();
    private readonly StructuralBodyCloseBreakCalculator breakoutCalculator = new();
    private readonly CandleBodyDirectionCalculator bodyCalculator = new();

    /// <summary>Replay visibility of the supplied closed interval remains the caller's responsibility.</summary>
    public NasdaqPostCompletionRebuildPendingTransitionResult Evaluate(NasdaqPostCompletionRebuildPendingState current, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);
        var previous = current.MarketCursor;
        if (candle.ProviderId != previous.ProviderId || candle.Symbol != previous.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4 || candle.Timeframe != previous.Timeframe
            || candle.OpenTimeUtc <= previous.OpenTimeUtc || candle.OpenTimeUtc < previous.CloseTimeUtc
            || candle.CloseTimeUtc <= previous.CloseTimeUtc)
            throw new ArgumentException("The next closed candle must follow the pending cursor in the same H4 series without overlap.", nameof(candle));

        var lower = current.CandidateSide == StructuralCandidateExtremeSide.Lower;
        var migration = migrationCalculator.Evaluate(current.KnownProtectionAnchor, lower ? candle.Low : candle.High, current.CandidateSide);
        var breakout = breakoutCalculator.Evaluate(candle, current.FrozenBreakoutTerminal.StructuralPrice,
            lower ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower);
        var body = bodyCalculator.Evaluate(candle);
        var startsTurn = body == (lower ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish);
        if (breakout.IsConfirmed)
            return new NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected(current, migration, breakout, body,
                !migration.WasReplaced ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None
                    : startsTurn ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                    : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired);
        if (startsTurn)
            return new NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted(current, migration, breakout, body);
        return migration.WasReplaced
            ? new NasdaqPostCompletionRebuildPendingTransitionResult.PendingReset(current, migration, breakout, body)
            : new NasdaqPostCompletionRebuildPendingTransitionResult.PendingContinues(current, migration, breakout, body);
    }
}
