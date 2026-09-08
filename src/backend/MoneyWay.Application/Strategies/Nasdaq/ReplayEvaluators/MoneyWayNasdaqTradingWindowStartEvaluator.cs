using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>
/// Deterministically evaluates MoneyWay Nasdaq <c>nasdaq-0.1.0-draft</c> rule <c>NQ-TIME-001</c> against the
/// 08:30 <c>America/Bogota</c> trading-window start using only <see cref="StrategyReplayContext.AsOfUtc"/>. It does
/// not evaluate the complete trading window, produce a strategy verdict, or execute a trade. The audited source is
/// <c>docs/strategies/nasdaq/rule-catalog.md</c>.
/// </summary>
public sealed class MoneyWayNasdaqTradingWindowStartEvaluator : IReplayRuleEvaluator
{
    private const string TimeZoneId = "America/Bogota";
    private static readonly TimeOnly TradingWindowStart = new(8, 30);
    private static readonly TimeZoneInfo StrategyTimeZone = ResolveStrategyTimeZone();
    private static readonly StrategyDefinition StrategyDefinition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition RuleDefinition = StrategyDefinition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-001");

    public StrategyId StrategyId => StrategyDefinition.StrategyId;
    public StrategyVersion StrategyVersion => StrategyDefinition.Version;
    public RuleId RuleId => RuleDefinition.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");

        var localTime = TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone);
        var result = TimeOnly.FromDateTime(localTime.DateTime) < TradingWindowStart
            ? RuleEvaluationResult.Waiting
            : RuleEvaluationResult.Passed;
        var reason = result == RuleEvaluationResult.Waiting
            ? "The replay context is before the 08:30 America/Bogota trading-window start."
            : "The replay context is at or after the 08:30 America/Bogota trading-window start.";

        return new ReplayRuleEvaluationDecision(result, reason, null);
    }

    private static TimeZoneInfo ResolveStrategyTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(TimeZoneId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }
}
