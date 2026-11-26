using System.Globalization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

/// <summary>Applies the exclusive pre-entry cutoff for the replay's exact version and reviewed path.</summary>
public sealed class MoneyWayNasdaqTradingWindowEndEvaluator : IReplayRuleEvaluator
{
    private readonly StrategyVersion boundVersion;
    public MoneyWayNasdaqTradingWindowEndEvaluator() : this(MoneyWayNasdaqStrategyDefinition.Instance.Version) { }
    internal MoneyWayNasdaqTradingWindowEndEvaluator(StrategyVersion version) { boundVersion = version; }

    private static readonly StrategyDefinition StrategyDefinition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly StrategyRuleDefinition RuleDefinition = StrategyDefinition.Rules.Single(rule => rule.RuleId.Value == "NQ-TIME-002");

    public StrategyId StrategyId => StrategyDefinition.StrategyId;
    public StrategyVersion StrategyVersion => boundVersion;
    public RuleId RuleId => RuleDefinition.RuleId;

    public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.StrategyId != StrategyId || context.StrategyVersion != StrategyVersion)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");

        var window = NasdaqTradingWindowFact.ForReplay(context);
        var result = context.AsOfUtc >= window.EndUtc
            ? RuleEvaluationResult.Failed
            : RuleEvaluationResult.Passed;
        var cutoff = TimeZoneInfo.ConvertTime(window.EndUtc, NasdaqSupervisedSessionPolicy.Zone)
            .ToString("HH:mm", CultureInfo.InvariantCulture);
        var reason = result == RuleEvaluationResult.Failed
            ? $"The replay context is at or after the {cutoff} America/Bogota pre-entry operational cutoff."
            : $"The replay context is before the {cutoff} America/Bogota pre-entry operational cutoff.";

        return new ReplayRuleEvaluationDecision(result, reason, null, window);
    }
}
