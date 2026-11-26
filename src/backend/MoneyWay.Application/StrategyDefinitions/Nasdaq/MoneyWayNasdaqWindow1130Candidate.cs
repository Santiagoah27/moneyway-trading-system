using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.Strategies;
using MoneyWay.Application.Backtesting;

namespace MoneyWay.Application.StrategyDefinitions.Nasdaq;

/// <summary>Explicit opt-in research composition. Never registered/activated by a default catalog.</summary>
public static class MoneyWayNasdaqWindow1130Candidate
{
    public static StrategyVersion Version { get; } = new("nasdaq-0.4.0-window-1130-candidate");
    public const string LifecycleStatus = "Candidate_NotActivated";
    private const string Source = "docs/strategies/nasdaq/trading-window-1130-candidate.md";
    public static StrategyDefinition Definition { get; } = Create();
    private static StrategyDefinition Create()
    {
        var parent = MoneyWayNasdaqSessionCandidate.Definition;
        return new(parent.StrategyId, Version, "MoneyWay Nasdaq 11:30 trading-window candidate", Source,
            parent.Rules.Select(rule => rule.RuleId.Value != "NQ-TIME-002" ? rule :
                new StrategyRuleDefinition(rule.RuleId, rule.Name, rule.Stage, rule.Sequence, rule.IsRequired,
                    rule.DefinitionStatus, "Standard entry acquisition ends at 11:30 exclusive America/Bogota. An explicitly reviewed Wickfill Scenario 2 setup retains 11:00 exclusive.", Source)));
    }
    internal static bool UsesExplicitSessions(StrategyVersion version) =>
        version == MoneyWayNasdaqSessionCandidate.Version || version == Version;

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
