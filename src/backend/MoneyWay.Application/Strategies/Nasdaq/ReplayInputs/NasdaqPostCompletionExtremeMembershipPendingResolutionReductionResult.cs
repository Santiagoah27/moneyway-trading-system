using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed resolution-only outcomes retaining the frozen pending lifecycle and exact resolver payload.</summary>
public abstract class NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult
{
    private NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult(
        NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady evidenceReady)
        => EvidenceReady = evidenceReady;

    public NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady EvidenceReady { get; }
    public NasdaqPostCompletionExtremeMembershipPendingState PendingState => EvidenceReady.PendingState;
    public Candle MarketCursor => PendingState.MarketCursor;

    public sealed class DataUnavailable : NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult
    {
        internal DataUnavailable(NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady evidenceReady,
            NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable resolution) : base(evidenceReady) => Resolution = resolution;

        public NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable Resolution { get; }
    }

    public sealed class MembersResolved : NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult
    {
        internal MembersResolved(NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady evidenceReady,
            NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved resolution) : base(evidenceReady) => Resolution = resolution;

        public NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved Resolution { get; }
    }
}
