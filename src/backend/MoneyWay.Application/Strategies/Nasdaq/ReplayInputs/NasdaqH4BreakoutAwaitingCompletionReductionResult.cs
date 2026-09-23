namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>One typed lifecycle outcome for a frozen H4 breakout awaiting completion.</summary>
public abstract record NasdaqH4BreakoutAwaitingCompletionReductionResult
{
    private NasdaqH4BreakoutAwaitingCompletionReductionResult() { }

    public abstract NasdaqH4ReconstructionSnapshot Snapshot { get; }

    public abstract record Missing : NasdaqH4BreakoutAwaitingCompletionReductionResult
    {
        private Missing() { }

        public sealed record HumanStructuralPrice008(
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Reduction) : Missing
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }

        public sealed record OrdinaryRebuilt009(
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Reduction) : Missing
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }
    }

    public abstract record Conflict : NasdaqH4BreakoutAwaitingCompletionReductionResult
    {
        private Conflict() { }

        public sealed record HumanStructuralPrice008(
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Reduction) : Conflict
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }

        public sealed record OrdinaryRebuilt009(
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Reduction) : Conflict
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }
    }

    public abstract record Completed : NasdaqH4BreakoutAwaitingCompletionReductionResult
    {
        private Completed() { }

        public sealed record Directional007(NasdaqH4ReconstructionSnapshot.Completed.Directional Result) : Completed
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Result;
        }

        public sealed record HumanStructuralPrice008(
            NasdaqH4HumanStructuralPriceBreakoutEvidenceReductionResult Reduction) : Completed
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }

        public sealed record OrdinaryRebuilt009(
            NasdaqH4OrdinaryRebuiltBreakoutEvidenceReductionResult Reduction) : Completed
        {
            public override NasdaqH4ReconstructionSnapshot Snapshot => Reduction.Snapshot;
        }
    }
}
