using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces exactly one OppositeImpulse snapshot through one closed H4 candle.</summary>
public sealed class NasdaqH4OppositeImpulseSnapshotReducer
{
    private readonly NasdaqPostInvalidationOppositeImpulseTransitionCalculator transitionCalculator = new();
    private readonly NasdaqPostInvalidationCorrectionInitializer correctionInitializer = new();

    public NasdaqH4ReconstructionSnapshot Reduce(NasdaqH4ReconstructionSnapshot snapshot, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candle);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.OppositeImpulse impulse)
            throw new ArgumentException("Only an OppositeImpulse snapshot can be reduced by this reducer.", nameof(snapshot));

        var result = transitionCalculator.Evaluate(impulse.State, candle);
        return result.Kind switch
        {
            NasdaqPostInvalidationOppositeImpulseTransitionKind.ContinuingImpulse =>
                new NasdaqH4ReconstructionSnapshot.OppositeImpulse(result.ResultingState),
            NasdaqPostInvalidationOppositeImpulseTransitionKind.CorrectionStarted =>
                new NasdaqH4ReconstructionSnapshot.Correction(correctionInitializer.Initialize(result)),
            _ => throw new InvalidOperationException("The opposite impulse transition result is not supported."),
        };
    }
}
