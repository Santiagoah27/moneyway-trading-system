using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Counts synchronized replay steps where runtime required-rule evaluation coverage lacked one exact required rule.
/// </summary>
public sealed class MultiTimeframeMissingRequiredRuleCount
{
    public MultiTimeframeMissingRequiredRuleCount(RuleId ruleId, int sequence, int count)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        RuleId = ruleId;
        Sequence = sequence;
        Count = count;
    }

    public RuleId RuleId { get; }
    public int Sequence { get; }
    public int Count { get; }
}
