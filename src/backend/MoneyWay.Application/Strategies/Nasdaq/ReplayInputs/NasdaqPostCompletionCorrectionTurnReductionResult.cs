namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exactly one state materialized from an already-consumed correction turn result.</summary>
public abstract class NasdaqPostCompletionCorrectionTurnReductionResult
{
    private NasdaqPostCompletionCorrectionTurnReductionResult() { }

    public sealed class ActiveCorrection : NasdaqPostCompletionCorrectionTurnReductionResult
    {
        internal ActiveCorrection(NasdaqPostCompletionActiveCorrectionState state) => State = state;
        public NasdaqPostCompletionActiveCorrectionState State { get; }
    }

    public sealed class Candidate : NasdaqPostCompletionCorrectionTurnReductionResult
    {
        internal Candidate(NasdaqPostCompletionCandidateState state) => State = state;
        public NasdaqPostCompletionCandidateState State { get; }
    }
}
