namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exactly one outcome of advancing an active post-invalidation correction.</summary>
public sealed class NasdaqPostInvalidationCorrectionTransitionResult
{
    private NasdaqPostInvalidationCorrectionTransitionResult(
        NasdaqPostInvalidationCorrectionTransitionKind kind,
        NasdaqPostInvalidationCorrectionState? continuingCorrection,
        NasdaqPostInvalidationCandidateState? candidate)
    {
        Kind = kind;
        ContinuingCorrection = continuingCorrection;
        Candidate = candidate;
    }

    public NasdaqPostInvalidationCorrectionTransitionKind Kind { get; }

    public NasdaqPostInvalidationCorrectionState? ContinuingCorrection { get; }

    public NasdaqPostInvalidationCandidateState? Candidate { get; }

    internal static NasdaqPostInvalidationCorrectionTransitionResult Continue(
        NasdaqPostInvalidationCorrectionState state) =>
        new(NasdaqPostInvalidationCorrectionTransitionKind.ContinuingCorrection, state, null);

    internal static NasdaqPostInvalidationCorrectionTransitionResult FormCandidate(
        NasdaqPostInvalidationCandidateState state) =>
        new(NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional, null, state);
}
