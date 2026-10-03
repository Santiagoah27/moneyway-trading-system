using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Definitive 008 candidate facts and selected human provenance; no structural completion or successor is materialized.</summary>
public sealed class NasdaqPostCompletionResolvedHumanStructuralPriceCandidate
{
    internal NasdaqPostCompletionResolvedHumanStructuralPriceCandidate(NasdaqPostCompletionBreakoutAwaitingCompletionState awaitingState,
        NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady selection,
        StructuralCandidateValidationResult validatedCandidate)
    {
        AwaitingState = awaitingState;
        Selection = selection;
        ValidatedCandidate = validatedCandidate;
    }

    public NasdaqPostCompletionBreakoutAwaitingCompletionState AwaitingState { get; }
    public NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady Selection { get; }
    public StructuralCandidateValidationResult ValidatedCandidate { get; }
    public StructuralTurnGeometryResult CandidateGeometry => ValidatedCandidate.CandidateGeometry;
    public StructuralCandidateExtremeSide CandidateSide => ValidatedCandidate.CandidateSide;
    public NasdaqPostCompletionEpisode Episode => AwaitingState.Episode;
    public Candle MarketCursor => AwaitingState.MarketCursor;
}
