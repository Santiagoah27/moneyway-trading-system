using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Capabilities;

/// <summary>Builds deterministic capability reports from versioned definitions, exact evaluator registrations, and explicit audited declarations.</summary>
public sealed class StrategyReplayEvaluationCapabilityCatalog
{
    public const string ImplementedReason = "An exact replay rule evaluator is registered for this strategy rule version.";
    public const string DefaultNotImplementedReason = "No replay rule evaluator is registered and no explicit evaluation limitation is declared.";
    private readonly IReadOnlyList<StrategyReplayEvaluationCapabilityReport> reports;
    public StrategyReplayEvaluationCapabilityCatalog(StrategyDefinitionCatalog strategyDefinitions, IEnumerable<IReplayRuleEvaluator> evaluators, IEnumerable<ReplayRuleEvaluationCapabilityDeclaration> declarations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinitions); ArgumentNullException.ThrowIfNull(evaluators); ArgumentNullException.ThrowIfNull(declarations);
        var definitions = strategyDefinitions.GetAll(); var evaluatorSnapshot = evaluators.ToArray(); var declarationSnapshot = declarations.ToArray();
        if (evaluatorSnapshot.Any(x => x is null)) throw new ArgumentException("Evaluators cannot contain null.", nameof(evaluators));
        if (declarationSnapshot.Any(x => x is null)) throw new ArgumentException("Declarations cannot contain null.", nameof(declarations));
        var evaluatorGroups = evaluatorSnapshot.GroupBy(Key).ToArray(); var declarationGroups = declarationSnapshot.GroupBy(Key).ToArray();
        if (evaluatorGroups.Any(x => x.Count() > 1)) throw new ArgumentException("Evaluator keys must be unique.", nameof(evaluators));
        if (declarationGroups.Any(x => x.Count() > 1)) throw new ArgumentException("Declaration keys must be unique.", nameof(declarations));
        var evaluatorMap = evaluatorGroups.ToDictionary(x => x.Key, x => x.Single()); var declarationMap = declarationGroups.ToDictionary(x => x.Key, x => x.Single());
        if (evaluatorMap.Keys.Any(declarationMap.ContainsKey)) throw new InvalidOperationException("An evaluator and declaration cannot target the same rule version.");
        foreach (var key in evaluatorMap.Keys.Concat(declarationMap.Keys))
        {
            var definition = definitions.SingleOrDefault(x => x.StrategyId == key.StrategyId && x.Version == key.Version);
            if (definition is null || definition.Rules.All(x => x.RuleId != key.RuleId)) throw new InvalidOperationException("Capability configuration references an unknown strategy rule version.");
        }
        reports = new ReadOnlyCollection<StrategyReplayEvaluationCapabilityReport>(definitions.Select(definition => new StrategyReplayEvaluationCapabilityReport(definition.StrategyId, definition.Version, definition.DisplayName,
            definition.Rules.Select(rule => Create(definition, rule, evaluatorMap, declarationMap)))).ToArray());
    }
    public StrategyReplayEvaluationCapabilityReport? Find(StrategyId strategyId, StrategyVersion version) { ArgumentNullException.ThrowIfNull(strategyId); ArgumentNullException.ThrowIfNull(version); return reports.SingleOrDefault(x => x.StrategyId == strategyId && x.StrategyVersion == version); }
    public IReadOnlyList<StrategyReplayEvaluationCapabilityReport> GetAll() => reports;
    private static ReplayRuleEvaluationCapability Create(StrategyDefinition definition, StrategyRuleDefinition rule, IReadOnlyDictionary<KeyValue, IReplayRuleEvaluator> evaluators, IReadOnlyDictionary<KeyValue, ReplayRuleEvaluationCapabilityDeclaration> declarations)
    {
        var key = new KeyValue(definition.StrategyId, definition.Version, rule.RuleId);
        if (evaluators.ContainsKey(key)) return new(definition.StrategyId, definition.Version, rule, ReplayRuleEvaluationCapabilityStatus.Implemented, ImplementedReason, null);
        if (declarations.TryGetValue(key, out var declaration)) return new(definition.StrategyId, definition.Version, rule, declaration.Status, declaration.Reason, declaration.SourceReference);
        return new(definition.StrategyId, definition.Version, rule, ReplayRuleEvaluationCapabilityStatus.NotImplemented, DefaultNotImplementedReason, null);
    }
    private static KeyValue Key(IReplayRuleEvaluator value) => new(value.StrategyId, value.StrategyVersion, value.RuleId);
    private static KeyValue Key(ReplayRuleEvaluationCapabilityDeclaration value) => new(value.StrategyId, value.StrategyVersion, value.RuleId);
    private sealed record KeyValue(StrategyId StrategyId, StrategyVersion Version, RuleId RuleId);
}
