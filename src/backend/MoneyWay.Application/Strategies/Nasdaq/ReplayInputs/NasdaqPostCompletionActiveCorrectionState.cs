using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A resolved active pair whose correction has consumed exactly its first candle.</summary>
public sealed class NasdaqPostCompletionActiveCorrectionState
{
    internal NasdaqPostCompletionActiveCorrectionState(NasdaqPostCompletionExtremeGeometryReady geometryReady)
    {
        ArgumentNullException.ThrowIfNull(geometryReady);
        if (!ReferenceEquals(geometryReady.ExtremeGeometry.MemberResolution, geometryReady.MembersResolved.Resolution))
            throw new ArgumentException("The extreme geometry must retain the exact resolved membership.", nameof(geometryReady));
        GeometryReady = geometryReady;
        ActivePair = new(geometryReady.ExtremeGeometry, geometryReady.PendingState.ValidatedProtectedTurn);
        CorrectionTurnCandles = new ReadOnlyCollection<Candle>([CorrectionStartCandle]);
    }

    public NasdaqPostCompletionExtremeGeometryReady GeometryReady { get; }
    public NasdaqPostCompletionStructuralPair ActivePair { get; }
    public NasdaqPostCompletionEpisode Episode => GeometryReady.PendingState.Episode;
    public NasdaqH4ReconstructionSnapshot.Completed Completion => Episode.Completion;
    public Candle CorrectionStartCandle => GeometryReady.PendingState.CorrectionStartCandle;
    public IReadOnlyList<Candle> CorrectionTurnCandles { get; }
    public Candle MarketCursor => CorrectionStartCandle;
}
