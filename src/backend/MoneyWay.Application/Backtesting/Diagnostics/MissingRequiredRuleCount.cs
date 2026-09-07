using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>Counts frames where required evaluation coverage lacked a specific rule.</summary>
public sealed class MissingRequiredRuleCount
{
    public MissingRequiredRuleCount(RuleId ruleId, int sequence, int count)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        RuleId = ruleId; Sequence = sequence; Count = count;
    }
    public RuleId RuleId { get; }
    public int Sequence { get; }
    public int Count { get; }
}
