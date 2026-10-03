using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Exact preparation-session scope; it is not a liquidity-take setup or an H4 reconstruction episode.</summary>
public sealed record NasdaqDemoSessionIdentity
{
    private static readonly TimeZoneInfo StrategyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public NasdaqDemoSessionIdentity(StrategyId strategyId, StrategyVersion strategyVersion,
        MarketDataProviderId providerId, MarketSymbol symbol, DateOnly tradingDay)
    {
        StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        StrategyVersion = strategyVersion ?? throw new ArgumentNullException(nameof(strategyVersion));
        ProviderId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        TradingDay = tradingDay;
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public DateOnly TradingDay { get; }

    internal static NasdaqDemoSessionIdentity FromContext(StrategyReplayContext context) => new(
        context.StrategyId, context.StrategyVersion, context.ProviderId, context.Symbol,
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone).DateTime));

    internal bool Matches(StrategyReplayContext context) => this == FromContext(context);
}
