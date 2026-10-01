using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves visible origin evidence without consuming any H4 market candle.</summary>
public sealed class NasdaqH4InvalidatedOriginEvidenceSnapshotReducer
{
    private readonly NasdaqHumanOriginVertexObservationSelector selector = new();
    private readonly NasdaqHumanOriginVertexMemberResolver memberResolver = new();
    private readonly NasdaqHumanOriginVertexGeometryCalculator geometryCalculator = new();
    private readonly NasdaqPostInvalidationOppositeImpulseInitializer impulseInitializer = new();

    public NasdaqH4InvalidatedOriginEvidenceReductionResult Reduce(
        NasdaqH4ReconstructionSnapshot snapshot,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin awaiting)
            throw new ArgumentException("Only an InvalidatedAwaitingOrigin snapshot can be reduced.", nameof(snapshot));
        if (snapshot.StrategyId != context.StrategyId || snapshot.StrategyVersion != context.StrategyVersion
            || snapshot.ProviderId != context.ProviderId || snapshot.Symbol != context.Symbol
            || context.AsOfUtc < awaiting.MarketCursor.CloseTimeUtc)
            throw new ArgumentException("Replay context must match the closed invalidation and its strategy/market identity.", nameof(context));

        var selection = selector.Select(context, awaiting.Episode);
        return selection.Kind switch
        {
            NasdaqHumanOriginVertexObservationSelectionKind.Missing =>
                new NasdaqH4InvalidatedOriginEvidenceReductionResult.Missing(awaiting, selection),
            NasdaqHumanOriginVertexObservationSelectionKind.Conflict =>
                new NasdaqH4InvalidatedOriginEvidenceReductionResult.Conflict(awaiting, selection),
            NasdaqHumanOriginVertexObservationSelectionKind.Unique => Resolve(awaiting, context, selection),
            _ => throw new ArgumentOutOfRangeException(nameof(selection), "Unsupported origin evidence selection."),
        };
    }

    private NasdaqH4InvalidatedOriginEvidenceReductionResult.Resolved Resolve(
        NasdaqH4ReconstructionSnapshot.InvalidatedAwaitingOrigin awaiting,
        StrategyReplayContext context,
        NasdaqHumanOriginVertexObservationSelection selection)
    {
        // All Unique supporters assert the same semantic member set. The resolver accepts one
        // observation as transport; the selection retains every supporter without assigning authority.
        var members = memberResolver.Evaluate(selection.SupportingObservations[0], context);
        var geometry = geometryCalculator.Evaluate(members, awaiting.State.Invalidation);
        var impulse = impulseInitializer.Initialize(members, awaiting.State.Invalidation, geometry);
        return new(new NasdaqH4ReconstructionSnapshot.OppositeImpulse(impulse), selection, members, geometry);
    }
}
