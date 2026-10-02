using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed structural observations retaining both comparisons; no lifecycle transition is performed.</summary>
public abstract class NasdaqPostCompletionActiveCorrectionBoundaryObservationResult
{
    private NasdaqPostCompletionActiveCorrectionBoundaryObservationResult(
        NasdaqPostCompletionActiveCorrectionState sourceState,
        StructuralBodyCloseBreakResult activeExtremeBreak,
        StructuralBodyCloseBreakResult protectedTurnBreak,
        CandleBodyDirection bodyDirection)
    {
        SourceState = sourceState;
        ActiveExtremeBreak = activeExtremeBreak;
        ProtectedTurnBreak = protectedTurnBreak;
        BodyDirection = bodyDirection;
    }

    public NasdaqPostCompletionActiveCorrectionState SourceState { get; }
    public Candle IncomingCandle => ActiveExtremeBreak.Candle!;
    public StructuralTurnBodyCoordinateSide ActiveExtremeSide => SourceState.ActivePair.ActiveExtremeSide;
    public StructuralBodyCloseBreakResult ActiveExtremeBreak { get; }
    public StructuralBodyCloseBreakResult ProtectedTurnBreak { get; }
    public CandleBodyDirection BodyDirection { get; }

    public sealed class ContinuationBreak : NasdaqPostCompletionActiveCorrectionBoundaryObservationResult
    {
        internal ContinuationBreak(NasdaqPostCompletionActiveCorrectionState sourceState,
            StructuralBodyCloseBreakResult activeExtremeBreak, StructuralBodyCloseBreakResult protectedTurnBreak,
            CandleBodyDirection bodyDirection)
            : base(sourceState, activeExtremeBreak, protectedTurnBreak, bodyDirection) { }
    }

    public sealed class ProtectedTurnInvalidated : NasdaqPostCompletionActiveCorrectionBoundaryObservationResult
    {
        internal ProtectedTurnInvalidated(NasdaqPostCompletionActiveCorrectionState sourceState,
            StructuralBodyCloseBreakResult activeExtremeBreak, StructuralBodyCloseBreakResult protectedTurnBreak,
            CandleBodyDirection bodyDirection)
            : base(sourceState, activeExtremeBreak, protectedTurnBreak, bodyDirection) { }
    }

    public sealed class InsideStructuralRange : NasdaqPostCompletionActiveCorrectionBoundaryObservationResult
    {
        internal InsideStructuralRange(NasdaqPostCompletionActiveCorrectionState sourceState,
            StructuralBodyCloseBreakResult activeExtremeBreak, StructuralBodyCloseBreakResult protectedTurnBreak,
            CandleBodyDirection bodyDirection)
            : base(sourceState, activeExtremeBreak, protectedTurnBreak, bodyDirection) { }
    }
}
