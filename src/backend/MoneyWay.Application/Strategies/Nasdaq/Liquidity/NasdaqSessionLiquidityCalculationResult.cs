using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.Liquidity;

/// <summary>
/// Reports whether the closed-candle-only MoneyWay Nasdaq NQ-LIQ-001 primitive could calculate completed session
/// levels. This result is not a rule evaluation, strategy verdict, or trading-execution decision.
/// </summary>
public sealed record NasdaqSessionLiquidityCalculationResult
{
    public NasdaqSessionLiquidityCalculationResult(NasdaqSessionLiquidityLevels? levels, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
        {
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));
        }

        Levels = levels;
        Reason = reason;
    }

    // Only the existing calculator can bind an available output to its actual bounded source context.
    internal NasdaqSessionLiquidityCalculationResult(NasdaqSessionLiquidityLevels levels, string reason, StrategyReplayContext sourceContext)
        : this(levels, reason) => SourceContext = sourceContext;

    internal StrategyReplayContext? SourceContext { get; }

    public bool IsAvailable => Levels is not null;

    public NasdaqSessionLiquidityLevels? Levels { get; }

    public string Reason { get; }
}
