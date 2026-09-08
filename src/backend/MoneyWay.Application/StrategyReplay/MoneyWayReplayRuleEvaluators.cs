using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>Provides the immutable canonical set of real MoneyWay replay rule evaluators.</summary>
public static class MoneyWayReplayRuleEvaluators
{
    private static readonly IReadOnlyList<IReplayRuleEvaluator> Evaluators =
        new ReadOnlyCollection<IReplayRuleEvaluator>(
        [
            new MoneyWayNasdaqTradingWindowStartEvaluator(),
            new MoneyWayNasdaqTradingWindowEndEvaluator(),
        ]);

    public static IReadOnlyList<IReplayRuleEvaluator> GetAll() => Evaluators;
}
