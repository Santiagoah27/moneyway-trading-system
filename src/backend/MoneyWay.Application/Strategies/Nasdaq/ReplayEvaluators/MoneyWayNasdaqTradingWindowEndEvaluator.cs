using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>
/// Deterministically evaluates MoneyWay Nasdaq <c>nasdaq-0.1.0-draft</c> rule <c>NQ-TIME-002</c> against the
/// exclusive 11:00 <c>America/Bogota</c> pre-entry operational cutoff using only
/// <see cref="StrategyReplayContext.AsOfUtc"/>. Contexts at or after 11:00 fail; the evaluator does not manage a
/// complete trade lifecycle, close positions, or execute trades. The audited source is
/// <c>docs/strategies/nasdaq/rule-catalog.md</c>.
/// </summary>
public sealed class MoneyWayNasdaqTradingWindowEndEvaluator : IReplayRuleEvaluator
{
    private const string TimeZoneId = "America/Bogota";
    private static readonly TimeOnly TradingWindowEnd = new(11, 0);
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
        var result = TimeOnly.FromDateTime(localTime.DateTime) >= TradingWindowEnd
            ? RuleEvaluationResult.Failed
            : RuleEvaluationResult.Passed;
        var reason = result == RuleEvaluationResult.Failed
            ? "The replay context is at or after the 11:00 America/Bogota pre-entry operational cutoff."
            : "The replay context is before the 11:00 America/Bogota pre-entry operational cutoff.";

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
