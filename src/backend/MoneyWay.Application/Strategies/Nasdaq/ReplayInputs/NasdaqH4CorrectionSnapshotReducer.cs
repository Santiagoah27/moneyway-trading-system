using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Reduces exactly one Correction snapshot through one closed H4 candle.</summary>
public sealed class NasdaqH4CorrectionSnapshotReducer
{
    private readonly NasdaqPostInvalidationCorrectionTransitionCalculator transitionCalculator = new();

    public NasdaqH4ReconstructionSnapshot Reduce(NasdaqH4ReconstructionSnapshot snapshot, Candle candle)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candle);
        if (snapshot is not NasdaqH4ReconstructionSnapshot.Correction correction)
            throw new ArgumentException("Only a Correction snapshot can be reduced by this reducer.", nameof(snapshot));

        var result = transitionCalculator.Evaluate(correction.State, candle);
        return result.Kind switch
        {
            NasdaqPostInvalidationCorrectionTransitionKind.ContinuingCorrection =>
                new NasdaqH4ReconstructionSnapshot.Correction(
                    result.ContinuingCorrection ?? throw new InvalidOperationException("The correction transition requires a correction state.")),
            NasdaqPostInvalidationCorrectionTransitionKind.CandidateProvisional =>
                new NasdaqH4ReconstructionSnapshot.Candidate(
                    result.Candidate ?? throw new InvalidOperationException("The correction transition requires a candidate state.")),
            _ => throw new InvalidOperationException("The correction transition result is not supported."),
        };
    }
}
