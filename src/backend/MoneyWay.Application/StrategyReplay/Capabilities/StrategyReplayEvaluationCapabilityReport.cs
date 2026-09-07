using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Describes evaluator implementation coverage for one exact strategy version. It does not represent a strategy verdict or trading result.</summary>
public sealed class StrategyReplayEvaluationCapabilityReport
{
    public StrategyReplayEvaluationCapabilityReport(StrategyId strategyId, StrategyVersion strategyVersion, string displayName, IEnumerable<ReplayRuleEvaluationCapability> rules)
    {
        ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(strategyVersion); ArgumentNullException.ThrowIfNull(displayName); ArgumentNullException.ThrowIfNull(rules);
        if (string.IsNullOrWhiteSpace(displayName) || displayName != displayName.Trim()) throw new ArgumentException("Display name must be non-empty and trimmed.", nameof(displayName));
        var snapshot = rules.ToArray(); if (snapshot.Any(x => x is null)) throw new ArgumentException("Rules cannot contain null.", nameof(rules));
        if (snapshot.Any(x => x.StrategyId != strategyId || x.StrategyVersion != strategyVersion)) throw new ArgumentException("Rule identity must match report.", nameof(rules));
        if (snapshot.GroupBy(x => x.RuleId).Any(x => x.Count() > 1) || snapshot.GroupBy(x => x.Sequence).Any(x => x.Count() > 1)) throw new ArgumentException("Rule IDs and sequences must be unique.", nameof(rules));
        if (snapshot.Where((x, i) => i > 0 && x.Sequence <= snapshot[i - 1].Sequence).Any()) throw new ArgumentException("Rules must be ordered by sequence.", nameof(rules));
        StrategyId = strategyId; StrategyVersion = strategyVersion; DisplayName = displayName; Rules = new ReadOnlyCollection<ReplayRuleEvaluationCapability>(snapshot);
    }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public string DisplayName { get; }
    public IReadOnlyList<ReplayRuleEvaluationCapability> Rules { get; }
    public int TotalRuleCount => Rules.Count; public int RequiredRuleCount => Rules.Count(x => x.IsRequired);
    public int ImplementedCount => Count(ReplayRuleEvaluationCapabilityStatus.Implemented); public int HumanOnlyCount => Count(ReplayRuleEvaluationCapabilityStatus.HumanOnly);
    public int NotImplementedCount => Count(ReplayRuleEvaluationCapabilityStatus.NotImplemented); public int BlockedByUnresolvedSpecificationCount => Count(ReplayRuleEvaluationCapabilityStatus.BlockedByUnresolvedSpecification);
    public int RequiredImplementedCount => Rules.Count(x => x.IsRequired && x.CapabilityStatus == ReplayRuleEvaluationCapabilityStatus.Implemented);
    public int RequiredEvaluatorGapCount => RequiredRuleCount - RequiredImplementedCount;
    /// <summary>True only when every required rule has a registered evaluator; it does not indicate readiness, approval, or profitability.</summary>
    public bool HasFullRequiredEvaluatorRegistration => RequiredEvaluatorGapCount == 0;
    private int Count(ReplayRuleEvaluationCapabilityStatus status) => Rules.Count(x => x.CapabilityStatus == status);
}
