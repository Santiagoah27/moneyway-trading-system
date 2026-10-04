using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;

internal static class NasdaqFvgReplayPrerequisites
{
    internal static ReplayRuleEvaluationDecision? Check(StrategyReplayContext context, RuleId rule)
    {
        ArgumentNullException.ThrowIfNull(context);
        var definition = StrategyDefinitions.Nasdaq.MoneyWayNasdaqStrategyDefinition.Instance;
        if (context.StrategyId != definition.StrategyId || context.StrategyVersion != definition.Version)
            throw new InvalidOperationException("Strategy context identity must exactly match the evaluator identity.");
        var gate = context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => f.Fact)
            .OfType<IReplayRuleGateFact>().FirstOrDefault(f => f.Blocks(context, rule));
        if (gate is not null) return new(RuleEvaluationResult.Failed, "Canonical terminal session gate blocks FVG progression.", gate.EvidenceReference);
        var windows = context.PriorObservations.SelectMany(o => o.RuleFacts).Where(f => f.RuleId.Value is "NQ-TIME-001" or "NQ-TIME-002")
            .Select(f => f.Fact).OfType<NasdaqTradingWindowFact>().ToArray();
        var sameSession = windows.Where(w => context.AsOfUtc >= w.DayStartUtc && context.AsOfUtc < w.DayStartUtc.AddDays(1)).ToArray();
        if (sameSession.Any(w => context.AsOfUtc >= w.EndUtc))
            return new(RuleEvaluationResult.Failed, "The canonical TIME-owned entry-acquisition cutoff has been reached.", System.Text.Json.JsonSerializer.Serialize(sameSession));
        if (context.PriorObservations.LastOrDefault(o => o.LifecycleProgression is not null)?.LifecycleProgression is { ActiveInstance: null })
            return new(RuleEvaluationResult.Waiting, "No surviving canonical setup is active.", null);
        return null;
    }
    internal static RuleEvaluation? Latest(StrategyReplayContext context, string rule) => context.PriorObservations
        .LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == rule))?.Evaluations.Single(e => e.RuleId.Value == rule);
    internal static bool Eligible(StrategyReplayContext context, string rule) => context.PriorObservations
        .LastOrDefault(o => o.Evaluations.Any(e => e.RuleId.Value == rule))?.WorkflowProgression?.RuleEligibility
        .SingleOrDefault(e => e.RuleId.Value == rule)?.EstablishesProgression == true;
    internal static ReplayRuleEvaluationDecision Waiting(StrategyReplayContext context, string rule) => new(
        Latest(context, rule)?.Result == RuleEvaluationResult.DataUnavailable ? RuleEvaluationResult.DataUnavailable : RuleEvaluationResult.Waiting,
        "The exact canonical prerequisite has not established eligible progression.", Latest(context, rule)?.EvidenceReference);
}
