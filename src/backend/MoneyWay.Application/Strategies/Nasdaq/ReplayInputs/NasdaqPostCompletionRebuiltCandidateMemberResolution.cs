using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact historical member availability, retaining selected human evidence and the frozen pending lineage.</summary>
public abstract class NasdaqPostCompletionRebuiltCandidateMemberResolution
{
    private NasdaqPostCompletionRebuiltCandidateMemberResolution(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection)
        => Selection = selection;
    public NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady Selection { get; }
    public NasdaqPostCompletionRebuildPendingState PendingState => Selection.PendingState;
    public NasdaqPostCompletionRebuildContext Context => Selection.Context;
    public Candle MarketCursor => PendingState.MarketCursor;

    public sealed class DataUnavailable : NasdaqPostCompletionRebuiltCandidateMemberResolution
    {
        internal DataUnavailable(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection,
            IReadOnlyList<DateTimeOffset> unavailableMemberOpenTimesUtc) : base(selection)
            => UnavailableMemberOpenTimesUtc = unavailableMemberOpenTimesUtc;
        public IReadOnlyList<DateTimeOffset> UnavailableMemberOpenTimesUtc { get; }
    }
    public sealed class MembersResolved : NasdaqPostCompletionRebuiltCandidateMemberResolution
    {
        internal MembersResolved(NasdaqHumanPostCompletionRebuiltCandidateSelection.UniqueEvidenceReady selection,
            IReadOnlyList<Candle> selectedMembers) : base(selection) => SelectedMembers = selectedMembers;
        public IReadOnlyList<Candle> SelectedMembers { get; }
    }
}
