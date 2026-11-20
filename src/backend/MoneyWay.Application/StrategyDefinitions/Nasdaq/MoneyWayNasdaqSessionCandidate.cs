using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;
using MoneyWay.Application.Backtesting;

namespace MoneyWay.Application.StrategyDefinitions.Nasdaq;

/// <summary>Explicit opt-in research composition. Never registered/activated by a default catalog.</summary>
public static class MoneyWayNasdaqSessionCandidate
{
    public static StrategyVersion Version { get; } = new("nasdaq-0.3.0-session-candidate");
    public const string LifecycleStatus = "Candidate_NotActivated";
    private const string Source = "docs/strategies/nasdaq/explicit-session-candidate.md";
    public static StrategyDefinition Definition { get; } = Create();
    private static StrategyDefinition Create()
    {
        var baseline = MoneyWayNasdaqM5StrictCloseCandidate.Definition;
        return new(baseline.StrategyId, Version, "MoneyWay Nasdaq explicit-session candidate", Source,
            baseline.Rules.Select(r => r.RuleId.Value != "NQ-LIQ-001" ? r : new StrategyRuleDefinition(r.RuleId, r.Name, r.Stage, r.Sequence, r.IsRequired,
                r.DefinitionStatus, "Exact H1 Asia [D-1 17:00,D 02:00), London [D 02:00,D 08:00), America/Bogota. Complete observable intervals, exact extremes with all ties. User-configured candidate only; relevance and takes remain separate.", Source)));
    }
    public static IReadOnlyList<IReplayRuleEvaluator> Evaluators() => Array.AsReadOnly<IReplayRuleEvaluator>([
        new MoneyWayNasdaqSessionPreparationCompletionEvaluator(Version), new MoneyWayNasdaqHumanH4ContextEvaluator(Version),
        new MoneyWayNasdaqSessionLiquidityEvaluator(new NasdaqSessionLiquidityCalculator(), Version), new MoneyWayNasdaqHumanStructuralLiquidityEvaluator(Version),
        new MoneyWayNasdaqTradingWindowStartEvaluator(Version), new MoneyWayNasdaqTradingWindowEndEvaluator(Version),
        new MoneyWayNasdaqHumanLiquidityTakeEvaluator(Version), new MoneyWayNasdaqHumanWickfillScenario2ReversalEvaluator(Version),
        new MoneyWayNasdaqWickfillScenario2PreEntryEligibilityEvaluator(Version), new MoneyWayNasdaqHumanM5TriggerEvaluator(Version),
        new MoneyWayNasdaqHumanM5FvgEvaluator(Version), new MoneyWayNasdaqHumanM5FvgQualityEvaluator(Version),
        new MoneyWayNasdaqHumanM1CorrectiveRetracementEvaluator(Version), new MoneyWayNasdaqHumanM1RealignmentEvaluator(Version),
        new MoneyWayNasdaqPreEntryEligibilityEvaluator(Version), new MoneyWayNasdaqHumanStructuralStopLossEvaluator(Version),
        new MoneyWayNasdaqRiskExposureEvaluator(Version), new MoneyWayNasdaqHumanTakeProfitEvaluator(Version)]);
    public static StrategyReplayWorkflowDefinition Workflow() => new(Definition.StrategyId, Version,
        MoneyWayReplayWorkflowDefinitions.GetAll().Single(d => d.StrategyId == Definition.StrategyId).RulePrerequisites);
    public static IStrategyReplayLifecyclePolicy Lifecycle() => new MoneyWayNasdaqReplayLifecyclePolicy(Version);
    public static GenerateCanonicalMultiTimeframeBacktestUseCase CreateBacktest()
    {
        var definitions = new[] { Definition };
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(Evaluators()),
            new(definitions, [Workflow()]), new(), new(definitions, [Lifecycle()]), new(new()));
        return new(new(run, new()), new());
    }
}
