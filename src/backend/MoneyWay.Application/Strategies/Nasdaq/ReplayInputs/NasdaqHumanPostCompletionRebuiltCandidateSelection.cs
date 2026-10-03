namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed evidence readiness outcomes; membership identities are not resolved and market time remains frozen.</summary>
public abstract class NasdaqHumanPostCompletionRebuiltCandidateSelection
{
    private NasdaqHumanPostCompletionRebuiltCandidateSelection(NasdaqPostCompletionRebuildPendingState pendingState,
        DateTimeOffset asOfUtc, IReadOnlyList<NasdaqHumanPostCompletionRebuiltCandidateObservation> observations)
    {
        PendingState = pendingState;
        AsOfUtc = asOfUtc;
        SupportingObservations = observations;
    }

    public NasdaqPostCompletionRebuildPendingState PendingState { get; }
    public NasdaqPostCompletionRebuildContext Context => PendingState.EvidenceContext;
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<NasdaqHumanPostCompletionRebuiltCandidateObservation> SupportingObservations { get; }

    public sealed class Missing : NasdaqHumanPostCompletionRebuiltCandidateSelection
    {
        internal Missing(NasdaqPostCompletionRebuildPendingState state, DateTimeOffset asOfUtc)
            : base(state, asOfUtc, Array.AsReadOnly(Array.Empty<NasdaqHumanPostCompletionRebuiltCandidateObservation>())) { }
    }

    public sealed class UniqueEvidenceReady : NasdaqHumanPostCompletionRebuiltCandidateSelection
    {
        internal UniqueEvidenceReady(NasdaqPostCompletionRebuildPendingState state, DateTimeOffset asOfUtc,
            IReadOnlyList<NasdaqHumanPostCompletionRebuiltCandidateObservation> observations, IReadOnlyList<DateTimeOffset> members)
            : base(state, asOfUtc, observations) => SemanticMemberOpenTimesUtc = members;
        public IReadOnlyList<DateTimeOffset> SemanticMemberOpenTimesUtc { get; }
    }

    public sealed class Conflict : NasdaqHumanPostCompletionRebuiltCandidateSelection
    {
        internal Conflict(NasdaqPostCompletionRebuildPendingState state, DateTimeOffset asOfUtc,
            IReadOnlyList<NasdaqHumanPostCompletionRebuiltCandidateObservation> observations,
            IReadOnlyList<IReadOnlyList<DateTimeOffset>> memberships)
            : base(state, asOfUtc, observations) => ConflictingMemberships = memberships;
        public IReadOnlyList<IReadOnlyList<DateTimeOffset>> ConflictingMemberships { get; }
    }
}
