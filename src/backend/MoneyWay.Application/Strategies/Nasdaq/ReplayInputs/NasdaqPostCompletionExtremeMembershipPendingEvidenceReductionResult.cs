using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed evidence-only lifecycle outcomes retaining the unchanged pending state and typed selector payload.</summary>
public abstract class NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult
{
    private NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult(NasdaqPostCompletionExtremeMembershipPendingState pendingState)
        => PendingState = pendingState;

    public NasdaqPostCompletionExtremeMembershipPendingState PendingState { get; }
    public Candle MarketCursor => PendingState.MarketCursor;

    public sealed class Missing : NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult
    {
        internal Missing(NasdaqPostCompletionExtremeMembershipPendingState pendingState,
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing selection) : base(pendingState) => Selection = selection;

        public NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing Selection { get; }
    }

    public sealed class Conflict : NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult
    {
        internal Conflict(NasdaqPostCompletionExtremeMembershipPendingState pendingState,
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict selection) : base(pendingState) => Selection = selection;

        public NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict Selection { get; }
    }

    public sealed class UniqueEvidenceReady : NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult
    {
        internal UniqueEvidenceReady(NasdaqPostCompletionExtremeMembershipPendingState pendingState,
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique selection) : base(pendingState) => Selection = selection;

        public NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique Selection { get; }
    }
}
