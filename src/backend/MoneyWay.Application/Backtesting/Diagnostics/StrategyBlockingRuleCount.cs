using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Counts frames where a real strategy evaluation outcome identified a specific blocking rule.
/// It does not represent trade losses or strategy profitability.
/// </summary>
public sealed class StrategyBlockingRuleCount
{
    public StrategyBlockingRuleCount(RuleId ruleId, int sequence, StrategyVerdict verdict, RuleEvaluationResult blockingResult, int count)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        RuleId = ruleId; Sequence = sequence; Verdict = verdict; BlockingResult = blockingResult; Count = count;
    }
    public RuleId RuleId { get; }
    public int Sequence { get; }
    public StrategyVerdict Verdict { get; }
    public RuleEvaluationResult BlockingResult { get; }
    public int Count { get; }
}
