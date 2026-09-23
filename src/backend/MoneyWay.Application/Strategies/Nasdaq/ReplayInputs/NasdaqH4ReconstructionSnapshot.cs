using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exactly one immutable, episode-bound post-invalidation H4 reconstruction state.</summary>
public abstract record NasdaqH4ReconstructionSnapshot : IStrategyReplayPreEvaluationState
{
    private NasdaqH4ReconstructionSnapshot() { }

    public abstract NasdaqH4ReconstructionSnapshotKind Kind { get; }
    public abstract NasdaqHumanOriginVertexEpisode Episode { get; }
    public abstract Candle MarketCursor { get; }
    public StrategyId StrategyId => Episode.StrategyId;
    public StrategyVersion StrategyVersion => Episode.StrategyVersion;
    public MarketDataProviderId ProviderId => Episode.ProviderId;
    public MarketSymbol Symbol => Episode.Symbol;

    public sealed record InvalidatedAwaitingOrigin : NasdaqH4ReconstructionSnapshot
    {
        public InvalidatedAwaitingOrigin(NasdaqH4InvalidatedAwaitingOriginState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqH4InvalidatedAwaitingOriginState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.InvalidatedAwaitingOrigin;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.InvalidatingCandle;
    }

    public sealed record OppositeImpulse : NasdaqH4ReconstructionSnapshot
    {
        public OppositeImpulse(NasdaqPostInvalidationOppositeImpulseState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationOppositeImpulseState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.OppositeImpulse;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.LastProcessedCandle;
    }

    public sealed record Correction : NasdaqH4ReconstructionSnapshot
    {
        public Correction(NasdaqPostInvalidationCorrectionState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationCorrectionState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.Correction;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.LastProcessedCandle;
    }

    public sealed record Candidate : NasdaqH4ReconstructionSnapshot
    {
        public Candidate(NasdaqPostInvalidationCandidateState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationCandidateState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.Candidate;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.TerminalCandle;
    }

    public sealed record RebuildPending : NasdaqH4ReconstructionSnapshot
    {
        public RebuildPending(NasdaqPostInvalidationCandidateRebuildPendingState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationCandidateRebuildPendingState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.RebuildPending;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.LastProcessedCandle;
    }

    public sealed record RebuiltTracking : NasdaqH4ReconstructionSnapshot
    {
        public RebuiltTracking(NasdaqPostInvalidationRebuiltCandidateTrackingState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationRebuiltCandidateTrackingState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.RebuiltTracking;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.LastProcessedCandle;
    }

    public sealed record BreakoutAwaitingCompletion : NasdaqH4ReconstructionSnapshot
    {
        public BreakoutAwaitingCompletion(NasdaqPostInvalidationCandidateRebuildBreakoutState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        public NasdaqPostInvalidationCandidateRebuildBreakoutState State { get; }
        public override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.BreakoutAwaitingCompletion;
        public override NasdaqHumanOriginVertexEpisode Episode => State.Episode;
        public override Candle MarketCursor => State.ValidatingCandle;
    }

    public abstract record Completed : NasdaqH4ReconstructionSnapshot
    {
        private Completed() { }
        public sealed override NasdaqH4ReconstructionSnapshotKind Kind => NasdaqH4ReconstructionSnapshotKind.Completed;
        public abstract StructuralCandidateValidationResult ValidatedCandidate { get; }

        public sealed record Ordinary : Completed
        {
            public Ordinary(NasdaqOrdinaryRebuiltBreakoutCompletionResult result) =>
                Result = result ?? throw new ArgumentNullException(nameof(result));
            public NasdaqOrdinaryRebuiltBreakoutCompletionResult Result { get; }
            public override NasdaqHumanOriginVertexEpisode Episode => Result.Breakout.Episode;
            public override Candle MarketCursor => Result.Breakout.ValidatingCandle;
            public override StructuralCandidateValidationResult ValidatedCandidate => Result.ValidatedCandidate;
        }

        public sealed record Directional : Completed
        {
            public Directional(NasdaqDirectionalMigrationBreakoutCompletionResult result) =>
                Result = result ?? throw new ArgumentNullException(nameof(result));
            public NasdaqDirectionalMigrationBreakoutCompletionResult Result { get; }
            public override NasdaqHumanOriginVertexEpisode Episode => Result.Breakout.Episode;
            public override Candle MarketCursor => Result.Breakout.ValidatingCandle;
            public override StructuralCandidateValidationResult ValidatedCandidate => Result.ValidatedCandidate;
        }

        public sealed record HumanStructuralPrice : Completed
        {
            public HumanStructuralPrice(NasdaqHumanStructuralPriceBreakoutCompletionResult result) =>
                Result = result ?? throw new ArgumentNullException(nameof(result));
            public NasdaqHumanStructuralPriceBreakoutCompletionResult Result { get; }
            public override NasdaqHumanOriginVertexEpisode Episode => Result.Breakout.Episode;
            public override Candle MarketCursor => Result.Breakout.ValidatingCandle;
            public override StructuralCandidateValidationResult ValidatedCandidate => Result.ValidatedCandidate;
        }
    }
}

public enum NasdaqH4ReconstructionSnapshotKind
{
    InvalidatedAwaitingOrigin,
    OppositeImpulse,
    Correction,
    Candidate,
    RebuildPending,
    RebuiltTracking,
    BreakoutAwaitingCompletion,
    Completed,
}
