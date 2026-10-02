using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains the exact canonical decision alongside all post-completion source provenance.</summary>
public sealed class NasdaqPostCompletionCandidateLifecycleResult
{
    internal NasdaqPostCompletionCandidateLifecycleResult(NasdaqPostCompletionCandidateState sourceState,
        NasdaqCandidateLifecycleDecision decision)
    {
        SourceState = sourceState;
        Decision = decision;
    }

    public NasdaqPostCompletionCandidateState SourceState { get; }
    public NasdaqCandidateLifecycleDecision Decision { get; }
    public Candle MarketCursor => Decision.MarketCursor;
    public NasdaqPostCompletionEpisode Episode => SourceState.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceState.ActivePair;
}
