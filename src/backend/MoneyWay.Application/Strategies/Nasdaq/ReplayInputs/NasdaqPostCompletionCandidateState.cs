using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A provisional HL/LH inside the same episode; the validated reference pair remains unchanged.</summary>
public sealed class NasdaqPostCompletionCandidateState
{
    internal NasdaqPostCompletionCandidateState(NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional provisional)
    {
        ArgumentNullException.ThrowIfNull(provisional);
        Provisional = provisional;
    }

    internal NasdaqPostCompletionCandidateState(NasdaqPostCompletionCandidateLifecycleResult continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        Provisional = continuation.SourceState.Provisional;
        Continuation = continuation;
    }

    public NasdaqPostCompletionCorrectionTurnResult.CandidateProvisional Provisional { get; }
    /// <summary>Absent only at candidate formation; retains the exact decision and previous candidate state.</summary>
    public NasdaqPostCompletionCandidateLifecycleResult? Continuation { get; }
    public CorrectionCandidateTurnResult.CandidateProvisional CandidateFacts => Provisional.TurnResult;
    public NasdaqPostCompletionActiveCorrectionState SourceCorrection => Provisional.SourceState;
    public NasdaqPostCompletionEpisode Episode => Provisional.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => Provisional.ActivePair;
    public StructuralCandidateExtremeSide CandidateSide => CandidateFacts.CandidateSide;
    public StructuralTurnGeometryResult CandidateGeometry => Continuation?.Decision.CandidateGeometry ?? CandidateFacts.Geometry;
    public Candle CorrectionStartCandle => SourceCorrection.CorrectionStartCandle;
    public IReadOnlyList<Candle> CorrectionTurnCandles => CandidateFacts.CorrectionTurnCandles;
    public Candle TerminalCandle => CandidateFacts.TerminalCandle;
    public Candle MarketCursor => Continuation?.MarketCursor ?? Provisional.MarketCursor;
}
