using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Canonical candidate routing extracted from the reconstruction dispatcher; observes migration before breakout routing.</summary>
public sealed class NasdaqCandidateLifecycleDecisionCalculator
{
    private readonly StructuralCandidateExtremeCalculator migrationCalculator = new();
    private readonly StructuralBodyCloseBreakCalculator breakoutCalculator = new();
    private readonly CandleBodyDirectionCalculator bodyCalculator = new();

    public NasdaqCandidateLifecycleDecision Evaluate(StructuralCandidateExtremeSide side,
        StructuralTurnGeometryResult geometry, StructuralTurnGeometryResult frozenTerminal,
        Candle previousCursor, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(frozenTerminal);
        ArgumentNullException.ThrowIfNull(previousCursor);
        ArgumentNullException.ThrowIfNull(candle);
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        var lower = side == StructuralCandidateExtremeSide.Lower;
        if (geometry.Side != (lower ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper)
            || frozenTerminal.Side != (lower ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower))
            throw new ArgumentException("The candidate and frozen terminal must have matching opposite sides.");
        if (candle.ProviderId != previousCursor.ProviderId || candle.Symbol != previousCursor.Symbol
            || candle.Timeframe != NasdaqHumanOriginVertexObservation.H4 || candle.Timeframe != previousCursor.Timeframe
            || candle.OpenTimeUtc <= previousCursor.OpenTimeUtc || candle.OpenTimeUtc < previousCursor.CloseTimeUtc
            || candle.CloseTimeUtc <= previousCursor.CloseTimeUtc)
            throw new ArgumentException("The next candle must follow the candidate in the same H4 series.", nameof(candle));

        var migration = migrationCalculator.Evaluate(geometry.ProtectionAnchor, lower ? candle.Low : candle.High, side);
        var breakout = breakoutCalculator.Evaluate(candle, frozenTerminal.StructuralPrice,
            lower ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower);
        var body = bodyCalculator.Evaluate(candle);
        if (!migration.WasReplaced)
            return breakout.IsConfirmed
                ? new NasdaqCandidateLifecycleDecision.DirectCompleted(geometry, frozenTerminal, previousCursor, migration, breakout, body)
                : new NasdaqCandidateLifecycleDecision.CandidateContinues(geometry, frozenTerminal, previousCursor, migration, breakout, body);
        var directional = body == (lower ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish);
        if (breakout.IsConfirmed)
            return new NasdaqCandidateLifecycleDecision.CollisionBreakout(geometry, frozenTerminal, previousCursor, migration, breakout, body,
                directional ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                    : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired);
        return directional
            ? new NasdaqCandidateLifecycleDecision.RebuiltTracking(geometry, frozenTerminal, previousCursor, migration, breakout, body)
            : new NasdaqCandidateLifecycleDecision.RebuildPending(geometry, frozenTerminal, previousCursor, migration, breakout, body);
    }
}
