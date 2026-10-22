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
        var seen = new HashSet<NasdaqTradingWindowFact>();
        var windows = new List<NasdaqTradingWindowFact>();
        // One ordered traversal avoids repeated SelectMany/OfType iterator dispatch. Retain
        // every gate check and the first original representative of each exact TIME value.
        for (var i = 0; i < context.PriorObservations.Count; i++)
        {
            var facts = context.PriorObservations[i].RuleFacts;
            for (var j = 0; j < facts.Count; j++)
            {
                var fact = facts[j];
                if (fact.Fact is IReplayRuleGateFact gate && gate.Blocks(context, rule))
                    return new(RuleEvaluationResult.Failed, "Canonical terminal session gate blocks FVG progression.", gate.EvidenceReference);
                if (fact.RuleId.Value is "NQ-TIME-001" or "NQ-TIME-002"
                    && fact.Fact is NasdaqTradingWindowFact window && seen.Add(window))
                    windows.Add(window);
            }
        }
        // Keep session arithmetic/cutoff evaluation after all terminal gates, as before.
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
