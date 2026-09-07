namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Represents current replay-evaluation implementation capability. It is independent from rule definition status.</summary>
public enum ReplayRuleEvaluationCapabilityStatus { Implemented, HumanOnly, NotImplemented, BlockedByUnresolvedSpecification }
