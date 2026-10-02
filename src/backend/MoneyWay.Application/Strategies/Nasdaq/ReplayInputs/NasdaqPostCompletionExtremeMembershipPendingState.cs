using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>
/// A consumed correction-start boundary awaiting final active-extreme membership.
/// Normalizes the event independently of the path that established it; no later market candle is consumed.
/// </summary>
public sealed class NasdaqPostCompletionExtremeMembershipPendingState
{
    internal NasdaqPostCompletionExtremeMembershipPendingState(NasdaqPostCompletionActiveExtremeMembershipEvent membershipEvent)
    {
        ArgumentNullException.ThrowIfNull(membershipEvent);
        MembershipEvent = membershipEvent;
    }

    public NasdaqPostCompletionActiveExtremeMembershipEvent MembershipEvent { get; }
    public NasdaqPostCompletionEpisode Episode => MembershipEvent.Episode;
    public NasdaqH4ReconstructionSnapshot.Completed Completion => Episode.Completion;
    public StructuralTurnBodyCoordinateSide ActiveExtremeSide => Episode.ActiveExtremeSide;
    public StructuralCandidateValidationResult ValidatedProtectedTurn => Completion.ValidatedCandidate;
    public StructuralTurnGeometryResult ProtectedTurnGeometry => ValidatedProtectedTurn.CandidateGeometry;
    /// <summary>The already-consumed first correction member, retained for future correction initialization.</summary>
    public Candle CorrectionStartCandle => MembershipEvent.CorrectionStartCandle;
    public Candle MarketCursor => CorrectionStartCandle;
}
