using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Counts synchronized replay steps where a real strategy evaluation outcome identified one exact blocking rule. It
/// does not represent losses or profitability.
/// </summary>
public sealed class MultiTimeframeStrategyBlockingRuleCount
{
    public MultiTimeframeStrategyBlockingRuleCount(
        RuleId ruleId,
        int sequence,
        StrategyVerdict verdict,
        RuleEvaluationResult blockingResult,
        int count)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        RuleId = ruleId;
        Sequence = sequence;
        Verdict = verdict;
        BlockingResult = blockingResult;
        Count = count;
    }

    public RuleId RuleId { get; }
    public int Sequence { get; }
    public StrategyVerdict Verdict { get; }
    public RuleEvaluationResult BlockingResult { get; }
    public int Count { get; }
}
