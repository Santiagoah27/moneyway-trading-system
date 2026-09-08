using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>
/// Evaluates MoneyWay Nasdaq rule NQ-LIQ-001 by delegating completed session-extrema availability to
/// <see cref="NasdaqSessionLiquidityCalculator"/>. Unavailable levels map to <see cref="RuleEvaluationResult.DataUnavailable"/>
/// and available levels map to <see cref="RuleEvaluationResult.Passed"/>. It produces no strategy verdict, trade,
/// or downstream liquidity-sweep evaluation.
/// </summary>
public sealed class MoneyWayNasdaqSessionLiquidityEvaluator
    : IReplayRuleEvaluator
{
    private static readonly StrategyDefinition StrategyDefinition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition RuleDefinition =
        StrategyDefinition.Rules.Single(rule => rule.RuleId.Value == "NQ-LIQ-001");
    private readonly NasdaqSessionLiquidityCalculator calculator;

    public MoneyWayNasdaqSessionLiquidityEvaluator(NasdaqSessionLiquidityCalculator calculator)
    {
        this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public StrategyId StrategyId => StrategyDefinition.StrategyId;

    public StrategyVersion StrategyVersion => StrategyDefinition.Version;

    public RuleId RuleId => RuleDefinition.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
        {
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");
        }

        var calculation = calculator.Calculate(context);
        var result = calculation.IsAvailable
            ? RuleEvaluationResult.Passed
            : RuleEvaluationResult.DataUnavailable;

        return new ReplayRuleEvaluationDecision(result, calculation.Reason, null);
    }
}
