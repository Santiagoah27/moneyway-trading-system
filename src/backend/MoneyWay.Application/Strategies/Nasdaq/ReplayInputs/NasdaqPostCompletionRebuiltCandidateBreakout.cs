using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>A frozen rebuild breakout with truthful origin; no definitive rebuilt vertex or structural completion exists yet.</summary>
/// <remarks>CollisionKind is retained unchanged. Only an ordinary non-migrating breakout belongs to downstream 009 membership handling.</remarks>
public sealed class NasdaqPostCompletionRebuiltCandidateBreakout
{
    internal NasdaqPostCompletionRebuiltCandidateBreakout(NasdaqPostCompletionRebuildBreakoutSource source)
        => Source = source ?? throw new ArgumentNullException(nameof(source));

    public NasdaqPostCompletionRebuildBreakoutSource Source { get; }
    public NasdaqPostCompletionEpisode Episode => Source.Episode;
    public NasdaqPostCompletionStructuralPair ActivePair => Source.ActivePair;
    public StructuralCandidateExtremeSide CandidateSide => Source.CandidateSide;
    public StructuralTurnGeometryResult FrozenBreakoutTerminal => Source.FrozenBreakoutTerminal;
    public StructuralCandidateExtremeResult Migration => Source.Migration;
    public StructuralBodyCloseBreakResult Breakout => Source.Breakout;
    public CandleBodyDirection BodyDirection => Source.BodyDirection;
    public Candle BreakoutCandle => Source.BreakoutCandle;
    public Candle MarketCursor => Source.MarketCursor;
    public Candle PriorMigrationCandle => Source.PriorMigrationCandle;
    public Candle EffectiveMigrationCandle => Source.EffectiveMigrationCandle;
    public decimal PreviousProtectionAnchor => Source.PreviousProtectionAnchor;
    public decimal EffectiveProtectionAnchor => Source.EffectiveProtectionAnchor;
    public NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind CollisionKind => Source.CollisionKind;
}
