namespace MoneyWay.Application.Strategies.Nasdaq.Liquidity;

/// <summary>
/// Represents the four completed MoneyWay Nasdaq session extrema for one <c>America/Bogota</c> trading day.
/// It contains no strategy verdict, signal, or trading-execution instruction.
/// </summary>
public sealed record NasdaqSessionLiquidityLevels
{
    public NasdaqSessionLiquidityLevels(
        DateOnly tradingDay,
        decimal asiaHigh,
        decimal asiaLow,
        decimal londonHigh,
        decimal londonLow)
    {
        TradingDay = tradingDay;
        AsiaHigh = asiaHigh;
        AsiaLow = asiaLow;
        LondonHigh = londonHigh;
        LondonLow = londonLow;
    }

    public DateOnly TradingDay { get; }

    public decimal AsiaHigh { get; }

    public decimal AsiaLow { get; }

    public decimal LondonHigh { get; }

    public decimal LondonLow { get; }
}
