namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence readiness only; never validates geometry, completes structure or changes the frozen market step.</summary>
public abstract class NasdaqHumanPostCompletionCollisionStructuralPriceSelection
{
    private NasdaqHumanPostCompletionCollisionStructuralPriceSelection(NasdaqPostCompletionBreakoutAwaitingCompletionState awaitingState,
        DateTimeOffset asOfUtc, IReadOnlyList<NasdaqHumanPostCompletionCollisionStructuralPriceObservation> supportingObservations)
    {
        AwaitingState = awaitingState;
        AsOfUtc = asOfUtc;
        SupportingObservations = supportingObservations;
    }

    public NasdaqPostCompletionBreakoutAwaitingCompletionState AwaitingState { get; }
    public NasdaqPostCompletionCollisionStructuralPriceContext Context => AwaitingState.EvidenceContext;
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<NasdaqHumanPostCompletionCollisionStructuralPriceObservation> SupportingObservations { get; }

    public sealed class Missing : NasdaqHumanPostCompletionCollisionStructuralPriceSelection
    {
        internal Missing(NasdaqPostCompletionBreakoutAwaitingCompletionState state, DateTimeOffset asOfUtc)
            : base(state, asOfUtc, Array.AsReadOnly(Array.Empty<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>())) { }
    }

    public sealed class UniqueEvidenceReady : NasdaqHumanPostCompletionCollisionStructuralPriceSelection
    {
        internal UniqueEvidenceReady(NasdaqPostCompletionBreakoutAwaitingCompletionState state, DateTimeOffset asOfUtc,
            IReadOnlyList<NasdaqHumanPostCompletionCollisionStructuralPriceObservation> observations, decimal structuralPrice)
            : base(state, asOfUtc, observations) => StructuralPrice = structuralPrice;
        public decimal StructuralPrice { get; }
    }

    public sealed class Conflict : NasdaqHumanPostCompletionCollisionStructuralPriceSelection
    {
        internal Conflict(NasdaqPostCompletionBreakoutAwaitingCompletionState state, DateTimeOffset asOfUtc,
            IReadOnlyList<NasdaqHumanPostCompletionCollisionStructuralPriceObservation> observations, IReadOnlyList<decimal> conflictingPrices)
            : base(state, asOfUtc, observations) => ConflictingPrices = conflictingPrices;
        public IReadOnlyList<decimal> ConflictingPrices { get; }
    }
}
