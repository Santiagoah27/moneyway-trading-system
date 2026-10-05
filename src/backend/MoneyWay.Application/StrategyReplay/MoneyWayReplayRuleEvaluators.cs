using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Provides the immutable canonical set of real MoneyWay replay rule evaluators.</summary>
public static class MoneyWayReplayRuleEvaluators
{
    private static readonly IReadOnlyList<IReplayRuleEvaluator> Evaluators =
        new ReadOnlyCollection<IReplayRuleEvaluator>(
        [
            new MoneyWayNasdaqSessionPreparationCompletionEvaluator(),
            new MoneyWayNasdaqHumanH4ContextEvaluator(),
            new MoneyWayNasdaqSessionLiquidityEvaluator(new NasdaqSessionLiquidityCalculator()),
            new MoneyWayNasdaqHumanStructuralLiquidityEvaluator(),
            new MoneyWayNasdaqTradingWindowStartEvaluator(),
            new MoneyWayNasdaqTradingWindowEndEvaluator(),
            new MoneyWayNasdaqHumanLiquidityTakeEvaluator(),
            new MoneyWayNasdaqHumanM5TriggerEvaluator(),
            new MoneyWayNasdaqHumanM5FvgEvaluator(),
            new MoneyWayNasdaqHumanM5FvgQualityEvaluator(),
            new MoneyWayNasdaqHumanM1CorrectiveRetracementEvaluator(),
            new MoneyWayNasdaqHumanM1RealignmentEvaluator(),
            new MoneyWayNasdaqPreEntryEligibilityEvaluator(),
        ]);

    public static IReadOnlyList<IReplayRuleEvaluator> GetAll() => Evaluators;
}
