using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Calculates the source-backed single-candle vertex from established directional collision facts.</summary>
public sealed class NasdaqDirectionalMigrationBreakoutCandidateCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public NasdaqCollisionCandidateResolution.Directional Evaluate(StructuralCandidateExtremeResult migration,
        StructuralBodyCloseBreakResult breakout, CandleBodyDirection bodyDirection)
    {
        ArgumentNullException.ThrowIfNull(migration);
        ArgumentNullException.ThrowIfNull(breakout);
        var lower = migration.Side == StructuralCandidateExtremeSide.Lower;
        var candle = breakout.Candle;
        if (!migration.WasReplaced || !breakout.IsConfirmed || candle is null
            || breakout.Direction != (lower ? StructuralBreakDirection.Upper : StructuralBreakDirection.Lower)
            || bodyDirection != (lower ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish))
            throw new ArgumentException("Only established directional migration and breakout facts define this vertex.");
        var geometry = geometryCalculator.Evaluate([candle], lower
            ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper);
        if (geometry.ProtectionAnchor != migration.ResultingExtreme)
            throw new ArgumentException("The same-candle protection anchor must equal the effective migrated anchor.", nameof(migration));
        return new(new StructuralCandidateValidationResult(migration.Side, geometry, breakout));
    }
}
