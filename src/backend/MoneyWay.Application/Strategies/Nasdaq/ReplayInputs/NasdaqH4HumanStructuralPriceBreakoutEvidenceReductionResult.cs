namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One evidence-lifecycle outcome for an already-detected NQ-Q-H4-008 breakout.</summary>
public sealed class NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult
{
    private NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult(
        NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind kind,
        NasdaqH4ReconstructionSnapshot snapshot,
        NasdaqHumanCollisionStructuralPriceObservationSelection selection,
        NasdaqHumanStructuralPriceBreakoutCompletionResult? completionResult)
    {
        Kind = kind;
        Snapshot = snapshot;
        Selection = selection;
        CompletionResult = completionResult;
    }

    public NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind Kind { get; }
    public NasdaqH4ReconstructionSnapshot Snapshot { get; }
    public NasdaqHumanCollisionStructuralPriceObservationSelection Selection { get; }
    public NasdaqHumanStructuralPriceBreakoutCompletionResult? CompletionResult { get; }

    internal static NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Pending(
        NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion snapshot,
        NasdaqHumanCollisionStructuralPriceObservationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(selection);
        var kind = selection.Kind switch
        {
            NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Missing =>
                NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Missing,
            NasdaqHumanCollisionStructuralPriceObservationSelectionKind.Conflict =>
                NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Conflict,
            _ => throw new ArgumentException("Only missing or conflicting evidence can remain pending.", nameof(selection)),
        };
        return new(kind, snapshot, selection, null);
    }

    internal static NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Completed(
        NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind.Completed,
            snapshot, snapshot.Result.HumanPriceSelection, snapshot.Result);
    }
}

public enum NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionKind
{
    Missing,
    Conflict,
    Completed,
}
