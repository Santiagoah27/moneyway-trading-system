using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Immutable semantic output of an owning canonical rule evaluator.</summary>
public interface IReplayRuleFact { }

/// <summary>A strategy-owned terminal fact can block later dependent evaluations without evaluator state.</summary>
public interface IReplayRuleGateFact : IReplayRuleFact
{
    string EvidenceReference { get; }
    bool Blocks(StrategyReplayContext context, RuleId ruleId);
}

public sealed record StrategyReplayRuleFact
{
    internal StrategyReplayRuleFact(RuleId ruleId, IReplayRuleFact fact) { RuleId = ruleId; Fact = fact; }
    public RuleId RuleId { get; }
    public IReplayRuleFact Fact { get; }
}
