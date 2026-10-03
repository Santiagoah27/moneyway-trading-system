using MoneyWay.Application.MarketData.StructuralBreaks;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed, lossless canonical source paths for the common structural-completion boundary.</summary>
public abstract class NasdaqPostCompletionStructuralCompletionSource
{
    private NasdaqPostCompletionStructuralCompletionSource() { }

    public abstract NasdaqPostCompletionCandidateLifecycleResult LifecycleResult { get; }
    public abstract StructuralCandidateValidationResult ValidatedTurn { get; }

    public sealed class Direct : NasdaqPostCompletionStructuralCompletionSource
    {
        internal Direct(NasdaqPostCompletionCandidateLifecycleResult lifecycleResult)
        {
            ArgumentNullException.ThrowIfNull(lifecycleResult);
            if (lifecycleResult.Decision is not NasdaqCandidateLifecycleDecision.DirectCompleted direct)
                throw new ArgumentException("Direct provenance requires a canonical direct completion.", nameof(lifecycleResult));
            LifecycleResult = lifecycleResult;
            Decision = direct;
        }

        public override NasdaqPostCompletionCandidateLifecycleResult LifecycleResult { get; }
        public NasdaqCandidateLifecycleDecision.DirectCompleted Decision { get; }
        public override StructuralCandidateValidationResult ValidatedTurn => Decision.Validation;
    }

    public sealed class DirectionalCollision : NasdaqPostCompletionStructuralCompletionSource
    {
        internal DirectionalCollision(NasdaqPostCompletionCandidateLifecycleResult lifecycleResult)
        {
            ArgumentNullException.ThrowIfNull(lifecycleResult);
            if (lifecycleResult.Decision is not NasdaqCandidateLifecycleDecision.CollisionBreakout collision
                || collision.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                || collision.CandidateResolution is not NasdaqCollisionCandidateResolution.Directional resolution)
                throw new ArgumentException("Directional provenance requires a canonical 007 collision.", nameof(lifecycleResult));
            LifecycleResult = lifecycleResult;
            Resolution = resolution;
        }

        public override NasdaqPostCompletionCandidateLifecycleResult LifecycleResult { get; }
        public NasdaqCollisionCandidateResolution.Directional Resolution { get; }
        public override StructuralCandidateValidationResult ValidatedTurn => Resolution.Validation;
    }

    public sealed class HumanStructuralPriceResolved : NasdaqPostCompletionStructuralCompletionSource
    {
        internal HumanStructuralPriceResolved(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate resolvedCandidate)
        {
            ArgumentNullException.ThrowIfNull(resolvedCandidate);
            var awaiting = resolvedCandidate.AwaitingState;
            if (awaiting.Collision.CollisionKind != NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired
                || !ReferenceEquals(awaiting.LifecycleResult.Decision, awaiting.Collision)
                || !ReferenceEquals(awaiting.Collision.CandidateResolution, awaiting.Resolution)
                || !resolvedCandidate.ValidatedCandidate.IsValidated
                || resolvedCandidate.CandidateSide != awaiting.Collision.CandidateSide
                || !ReferenceEquals(resolvedCandidate.ValidatedCandidate.BreakObservation, awaiting.Breakout)
                || !ReferenceEquals(resolvedCandidate.Selection.AwaitingState, awaiting)
                || resolvedCandidate.Selection.Context != awaiting.EvidenceContext
                || resolvedCandidate.Selection.SupportingObservations.Count == 0
                || resolvedCandidate.Selection.SupportingObservations.Any(item => item.Context != awaiting.EvidenceContext
                    || item.StructuralPrice != resolvedCandidate.Selection.StructuralPrice)
                || resolvedCandidate.CandidateGeometry.StructuralPrice != resolvedCandidate.Selection.StructuralPrice
                || resolvedCandidate.CandidateGeometry.ProtectionAnchor != awaiting.EffectiveProtectionAnchor)
                throw new ArgumentException("Human-price provenance requires a fully resolved canonical 008 candidate and its exact evidence.", nameof(resolvedCandidate));
            ResolvedCandidate = resolvedCandidate;
        }

        public NasdaqPostCompletionResolvedHumanStructuralPriceCandidate ResolvedCandidate { get; }
        public override NasdaqPostCompletionCandidateLifecycleResult LifecycleResult => ResolvedCandidate.AwaitingState.LifecycleResult;
        public override StructuralCandidateValidationResult ValidatedTurn => ResolvedCandidate.ValidatedCandidate;
    }
}
