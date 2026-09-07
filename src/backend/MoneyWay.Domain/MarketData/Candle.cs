namespace MoneyWay.Domain.MarketData;

/// <summary>
/// Represents immutable OHLC market data for a closed time interval.
/// </summary>
public sealed class Candle
{
    public Candle(
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        Timeframe timeframe,
        DateTimeOffset openTimeUtc,
        DateTimeOffset closeTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal? volume)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(timeframe);

        if (openTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Open timestamp must have a UTC offset.", nameof(openTimeUtc));
        }

        if (closeTimeUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Close timestamp must have a UTC offset.", nameof(closeTimeUtc));
        }

        if (closeTimeUtc <= openTimeUtc)
        {
            throw new ArgumentException("Close timestamp must be after open timestamp.", nameof(closeTimeUtc));
        }

        if (high < open || high < close || high < low)
        {
            throw new ArgumentException("High must be greater than or equal to Open, Close, and Low.", nameof(high));
        }

        if (low > open || low > close)
        {
            throw new ArgumentException("Low must be less than or equal to Open and Close.", nameof(low));
        }

        if (volume < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume cannot be negative.");
        }

        ProviderId = providerId;
        Symbol = symbol;
        Timeframe = timeframe;
        OpenTimeUtc = openTimeUtc;
        CloseTimeUtc = closeTimeUtc;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    public MarketDataProviderId ProviderId { get; }

    public MarketSymbol Symbol { get; }

    public Timeframe Timeframe { get; }

    public DateTimeOffset OpenTimeUtc { get; }

    public DateTimeOffset CloseTimeUtc { get; }

    public decimal Open { get; }

    public decimal High { get; }

    public decimal Low { get; }

    public decimal Close { get; }

    public decimal? Volume { get; }
}
