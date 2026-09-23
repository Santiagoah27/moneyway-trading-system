using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Starts rebuilt tracking when one candidate candle is strict migration, no breakout, and the first valid turn.</summary>
public sealed class NasdaqPostInvalidationCandidateTrackingStartCalculator
{
    private readonly NasdaqPostInvalidationRebuiltCandidateTrackingInitializer trackingInitializer = new();

    public NasdaqPostInvalidationRebuiltCandidateTrackingState Evaluate(
        NasdaqPostInvalidationCandidateState current,
        Candle candle)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(candle);

        return trackingInitializer.Initialize(current, candle);
    }
}
