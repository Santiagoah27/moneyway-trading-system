using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A consumed 008 collision frozen pending human StructuralPrice ownership; no definitive candidate geometry exists.</summary>
public sealed class NasdaqPostCompletionBreakoutAwaitingCompletionState
{
    internal NasdaqPostCompletionBreakoutAwaitingCompletionState(NasdaqPostCompletionCandidateLifecycleResult lifecycleResult,
        NasdaqCandidateLifecycleDecision.CollisionBreakout collision,
        NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired resolution)
    {
        LifecycleResult = lifecycleResult;
        Collision = collision;
        Resolution = resolution;
        EvidenceContext = new(Episode, CollisionCandle.OpenTimeUtc, collision.CandidateSide);
    }

    public NasdaqPostCompletionCandidateLifecycleResult LifecycleResult { get; }
    public NasdaqCandidateLifecycleDecision.CollisionBreakout Collision { get; }
    /// <summary>Final StructuralPrice remains human-assisted; the source candidate is provenance only.</summary>
    public NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired Resolution { get; }
    public NasdaqPostCompletionCollisionStructuralPriceContext EvidenceContext { get; }
    public NasdaqPostCompletionCandidateState SourceCandidate => LifecycleResult.SourceState;
    public NasdaqPostCompletionEpisode Episode => SourceCandidate.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => SourceCandidate.ActivePair;
    public StructuralCandidateValidationResult PreviousProtectedTurn => ActivePair.ProtectedTurn;
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => Collision.FrozenTerminal;
    public StructuralCandidateExtremeResult Migration => Collision.Migration;
    /// <summary>The known migrated wick anchor does not establish final StructuralPrice.</summary>
    public decimal EffectiveProtectionAnchor => Collision.EffectiveProtectionAnchor;
    public StructuralBodyCloseBreakResult Breakout => Collision.Breakout;
    public Candle CollisionCandle => Collision.MarketCursor;
    public CandleBodyDirection BodyDirection => Collision.BodyDirection;
    /// <summary>No additional market input is consumed while materializing this boundary.</summary>
    public Candle MarketCursor => LifecycleResult.MarketCursor;
}
