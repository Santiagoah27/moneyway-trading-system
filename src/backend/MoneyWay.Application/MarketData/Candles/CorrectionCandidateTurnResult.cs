using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.MarketData.Candles;

/// <summary>Generic correction facts after consuming one candle, independent of structural episode provenance.</summary>
public abstract class CorrectionCandidateTurnResult
{
    private CorrectionCandidateTurnResult(IReadOnlyList<Candle> correctionTurnCandles,
        StructuralTurnGeometryResult geometry, StructuralCandidateExtremeSide candidateSide,
        Candle marketCursor, CandleBodyDirection bodyDirection)
    {
        CorrectionTurnCandles = new ReadOnlyCollection<Candle>(correctionTurnCandles.ToArray());
        Geometry = geometry;
        CandidateSide = candidateSide;
        MarketCursor = marketCursor;
        BodyDirection = bodyDirection;
    }

    public IReadOnlyList<Candle> CorrectionTurnCandles { get; }
    public StructuralTurnGeometryResult Geometry { get; }
    public StructuralCandidateExtremeSide CandidateSide { get; }
    public Candle MarketCursor { get; }
    public CandleBodyDirection BodyDirection { get; }

    public sealed class ContinuingCorrection : CorrectionCandidateTurnResult
    {
        internal ContinuingCorrection(IReadOnlyList<Candle> members, StructuralTurnGeometryResult geometry,
            StructuralCandidateExtremeSide candidateSide, Candle candle, CandleBodyDirection body)
            : base(members, geometry, candidateSide, candle, body) { }
    }

    public sealed class CandidateProvisional : CorrectionCandidateTurnResult
    {
        internal CandidateProvisional(IReadOnlyList<Candle> members, StructuralTurnGeometryResult geometry,
            StructuralCandidateExtremeSide candidateSide, Candle candle, CandleBodyDirection body)
            : base(members, geometry, candidateSide, candle, body) { }

        /// <summary>The first opposite impulse candle contributes its wick, not its body, to the candidate.</summary>
        public Candle TerminalCandle => MarketCursor;
    }
}
