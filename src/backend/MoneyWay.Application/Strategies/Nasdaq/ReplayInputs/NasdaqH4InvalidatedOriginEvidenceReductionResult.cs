using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Evidence-only outcome for one frozen H4 invalidation episode.</summary>
public abstract record NasdaqH4InvalidatedOriginEvidenceReductionResult
{
    private NasdaqH4InvalidatedOriginEvidenceReductionResult() { }

    public abstract NasdaqH4ReconstructionSnapshot Snapshot { get; }
    public abstract NasdaqHumanOriginVertexObservationSelection Selection { get; }

    public sealed record Missing(
        NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin PendingSnapshot,
        NasdaqHumanOriginVertexObservationSelection Evidence) : NasdaqH4InvalidatedOriginEvidenceReductionResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => PendingSnapshot;
        public override NasdaqHumanOriginVertexObservationSelection Selection => Evidence;
    }

    public sealed record Conflict(
        NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin PendingSnapshot,
        NasdaqHumanOriginVertexObservationSelection Evidence) : NasdaqH4InvalidatedOriginEvidenceReductionResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => PendingSnapshot;
        public override NasdaqHumanOriginVertexObservationSelection Selection => Evidence;
    }

    public sealed record Resolved(
        NasdaqH4ReconstructionSnapshot.OppositeImpulse ImpulseSnapshot,
        NasdaqHumanOriginVertexObservationSelection Evidence,
        NasdaqHumanOriginVertexMemberResolution Members,
        StructuralTurnGeometryResult OriginGeometry) : NasdaqH4InvalidatedOriginEvidenceReductionResult
    {
        public override NasdaqH4ReconstructionSnapshot Snapshot => ImpulseSnapshot;
        public override NasdaqHumanOriginVertexObservationSelection Selection => Evidence;
        public NasdaqPostInvalidationOppositeImpulseState State => ImpulseSnapshot.State;
    }
}
