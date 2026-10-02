using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Correction consumption facts with the unchanged active structural pair and complete source provenance.</summary>
public abstract class NasdaqPostCompletionCorrectionTurnResult
{
    private NasdaqPostCompletionCorrectionTurnResult(
        NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange observation)
    {
        Observation = observation;
    }

    public NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange Observation { get; }
    public NasdaqPostCompletionActiveCorrectionState SourceState => Observation.SourceState;
    public NasdaqPostCompletionEpisode Episode => SourceState.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceState.ActivePair;
    public Candle MarketCursor => Observation.IncomingCandle;

    public sealed class ContinuingCorrection : NasdaqPostCompletionCorrectionTurnResult
    {
        internal ContinuingCorrection(NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange observation,
            CorrectionCandidateTurnResult.ContinuingCorrection turnResult) : base(observation) => TurnResult = turnResult;

        public CorrectionCandidateTurnResult.ContinuingCorrection TurnResult { get; }
    }

    public sealed class CandidateProvisional : NasdaqPostCompletionCorrectionTurnResult
    {
        internal CandidateProvisional(NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange observation,
            CorrectionCandidateTurnResult.CandidateProvisional turnResult) : base(observation) => TurnResult = turnResult;

        public CorrectionCandidateTurnResult.CandidateProvisional TurnResult { get; }
    }
}
