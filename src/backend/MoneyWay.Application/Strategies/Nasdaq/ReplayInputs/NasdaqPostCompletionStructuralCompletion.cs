using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A completed current episode, validated turn and consumed confirmation; no successor extreme or episode is established.</summary>
public sealed class NasdaqPostCompletionStructuralCompletion
{
    internal NasdaqPostCompletionStructuralCompletion(NasdaqPostCompletionStructuralCompletionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lifecycleResult = source.LifecycleResult;
        var validatedTurn = source.ValidatedTurn;
        if (!validatedTurn.IsValidated || validatedTurn.CandidateSide != lifecycleResult.SourceState.CandidateSide
            || !ReferenceEquals(validatedTurn.BreakObservation, lifecycleResult.Decision.Breakout)
            || !ReferenceEquals(lifecycleResult.Decision.CandidateGeometry, lifecycleResult.SourceState.CandidateGeometry)
            || !ReferenceEquals(lifecycleResult.Decision.FrozenTerminal, lifecycleResult.SourceState.ActivePair.ActiveExtremeGeometry)
            || !ReferenceEquals(lifecycleResult.Decision.PreviousCursor, lifecycleResult.SourceState.MarketCursor))
            throw new ArgumentException("Completion must retain the canonical validated turn and confirming breakout.", nameof(source));
        Source = source;
    }

    public NasdaqPostCompletionStructuralCompletionSource Source { get; }
    public NasdaqPostCompletionCandidateLifecycleResult LifecycleResult => Source.LifecycleResult;
    /// <summary>The truthful Candidate breakout origin, including correction and initial completion provenance.</summary>
    public NasdaqPostCompletionCandidateState SourceCandidate => LifecycleResult.SourceState;
    public NasdaqPostCompletionEpisode Episode => SourceCandidate.Episode;
    public StructuralCandidateValidationResult ValidatedTurn => Source.ValidatedTurn;
    public StructuralCandidateValidationResult PreviousProtectedTurn => SourceCandidate.ActivePair.ProtectedTurn;
    /// <summary>The prior HH/LL broken by confirmation, not a finalized successor extreme.</summary>
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => LifecycleResult.Decision.FrozenTerminal;
    public Candle ConfirmingCandle => LifecycleResult.MarketCursor;
    public CandleBodyDirection BodyDirection => LifecycleResult.Decision.BodyDirection;
    public Candle MarketCursor => ConfirmingCandle;
}
