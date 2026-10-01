namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One bounded H4 fold outcome, including its causal evidence stop boundary.</summary>
public abstract record NasdaqH4ReconstructionCatchUpResult
{
    private NasdaqH4ReconstructionCatchUpResult() { }

    public abstract NasdaqH4ReconstructionSnapshot Snapshot { get; }
    public abstract NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution { get; }

    public sealed record UpToDate(
        NasdaqH4ReconstructionSnapshot Current,
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? Origin = null) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => Current;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => Origin;
    }

    public sealed record OriginEvidenceMissing(
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing Reduction) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => null;
    }

    public sealed record OriginEvidenceConflict(
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict Reduction) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => null;
    }

    public sealed record BreakoutEvidenceMissing(
        NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing Reduction,
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? Origin = null) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => Origin;
    }

    public sealed record BreakoutEvidenceConflict(
        NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict Reduction,
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? Origin = null) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => Origin;
    }

    public sealed record Completed(
        NasdaqH4ReconstructionSnapshot.Completed TerminalSnapshot,
        NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed? BreakoutCompletion = null,
        NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? Origin = null) : NasdaqH4ReconstructionCatchUpResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => TerminalSnapshot;
        public override NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved? OriginResolution => Origin;
    }
}
