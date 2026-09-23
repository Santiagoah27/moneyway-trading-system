namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One evidence-lifecycle outcome for an already-detected ordinary NQ-Q-H4-009 breakout.</summary>
public sealed class NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult
{
    private NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult(
        NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind kind,
        NasdaqH4ReconstructionSnapshot snapshot,
        NasdaqHumanRebuiltCandidateVertexObservationSelection selection,
        NasdaqOrdinaryRebuiltBreakoutCompletionResult? completionResult)
    {
        Kind = kind;
        Snapshot = snapshot;
        Selection = selection;
        CompletionResult = completionResult;
    }

    public NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind Kind { get; }
    public NasdaqH4ReconstructionSnapshot Snapshot { get; }
    public NasdaqHumanRebuiltCandidateVertexObservationSelection Selection { get; }
    public NasdaqOrdinaryRebuiltBreakoutCompletionResult? CompletionResult { get; }

    internal static NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Pending(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion snapshot,
        NasdaqHumanRebuiltCandidateVertexObservationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selection);
        var kind = selection.Kind switch
        {
            NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Missing =>
                NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Missing,
            NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Conflict =>
                NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Conflict,
            _ => throw new ArgumentException("Only missing or conflicting evidence can remain pending.", nameof(selection)),
        };
        return new(kind, snapshot, selection, null);
    }

    internal static NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Completed(
        NasdaqH4ReconstructionSnapshot.Completed.Ordinary snapshot,
        NasdaqHumanRebuiltCandidateVertexObservationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Kind != NasdaqHumanRebuiltCandidateVertexObservationSelectionKind.Unique)
            throw new ArgumentException("Ordinary completion requires unique rebuilt membership.", nameof(selection));
        return new(NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind.Completed,
            snapshot, selection, snapshot.Result);
    }
}

public enum NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionKind
{
    Missing,
    Conflict,
    Completed,
}
