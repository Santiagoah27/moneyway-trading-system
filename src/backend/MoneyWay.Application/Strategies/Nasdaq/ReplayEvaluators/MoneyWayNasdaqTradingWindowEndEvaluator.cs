using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>
/// Deterministically evaluates MoneyWay Nasdaq <c>nasdaq-0.1.0-draft</c> rule <c>NQ-TIME-002</c> against the
/// inclusive 11:30 <c>America/Bogota</c> trading-window end using only <see cref="StrategyReplayContext.AsOfUtc"/>.
/// Contexts after 11:30 fail; the evaluator does not manage a complete trade lifecycle, close positions, or execute
/// trades. The audited source is <c>docs/strategies/nasdaq/rule-catalog.md</c>.
/// </summary>
public sealed class MoneyWayNasdaqTradingWindowEndEvaluator : IReplayRuleEvaluator
{
    private const string TimeZoneId = "America/Bogota";
    private static readonly TimeOnly TradingWindowEnd = new(11, 30);
    private static readonly TimeZoneInfo StrategyTimeZone = ResolveStrategyTimeZone();
    private static readonly StrategyDefinition StrategyDefinition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition RuleDefinition = StrategyDefinition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-002");

    public StrategyId StrategyId => StrategyDefinition.StrategyId;
    public StrategyVersion StrategyVersion => StrategyDefinition.Version;
    public RuleId RuleId => RuleDefinition.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");

        var localTime = TimeZoneInfo.ConvertTime(context.AsOfUtc, StrategyTimeZone);
        var result = TimeOnly.FromDateTime(localTime.DateTime) > TradingWindowEnd
            ? RuleEvaluationResult.Failed
            : RuleEvaluationResult.Passed;
        var reason = result == RuleEvaluationResult.Failed
            ? "The replay context is after the 11:30 America/Bogota trading-window end."
            : "The replay context is not after the 11:30 America/Bogota trading-window end.";

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
