using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// A new post-completion episode with an open impulse and no correction or final active-extreme membership.
/// Retains the validated protected HL/LH and exact predecessor completion without recalculating geometry.
/// </summary>
public sealed class NasdaqPostCompletionActiveImpulseState
{
    internal NasdaqPostCompletionActiveImpulseState(NasdaqPostCompletionEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        Episode = episode;
        MarketCursor = episode.ConfirmingCandle;
    }

    internal NasdaqPostCompletionActiveImpulseState(NasdaqPostCompletionActiveImpulseState previous, Candle marketCursor)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(marketCursor);
        Episode = previous.Episode;
        MarketCursor = marketCursor;
    }

    public NasdaqPostCompletionEpisode Episode { get; }
    public NasdaqH4ReconstructionSnapshot.Completed Completion => Episode.Completion;
    public StructuralTurnBodyCoordinateSide ActiveExtremeSide => Episode.ActiveExtremeSide;
    public StructuralCandidateValidationResult ValidatedProtectedTurn => Completion.ValidatedCandidate;
    public StructuralTurnGeometryResult ProtectedTurnGeometry => ValidatedProtectedTurn.CandidateGeometry;
    public Candle ConfirmingCandle => Episode.ConfirmingCandle;
    public Candle MarketCursor { get; }
}
