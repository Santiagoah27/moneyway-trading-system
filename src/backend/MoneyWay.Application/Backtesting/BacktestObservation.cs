using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Represents the auditable record of one completed replay step.
/// It contains only the candle observable at that step and no strategy outcome.
/// </summary>
public sealed class BacktestObservation
{
    public BacktestObservation(int step, DateTimeOffset asOfUtc, Candle currentCandle)
    {
        ArgumentNullException.ThrowIfNull(currentCandle);
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Step must be greater than zero.");
        }

        if (asOfUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Observation timestamp must have a UTC offset.", nameof(asOfUtc));
        }

        if (asOfUtc != currentCandle.CloseTimeUtc)
        {
            throw new ArgumentException("Observation timestamp must equal the candle close timestamp.", nameof(asOfUtc));
        }

        Step = step;
        AsOfUtc = asOfUtc;
        CurrentCandle = currentCandle;
    }

    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public Candle CurrentCandle { get; }
}
