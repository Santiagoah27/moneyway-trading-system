using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Projects and aggregates an already completed outcome run.
/// It performs no replay, strategy evaluation, or trading simulation.
/// </summary>
public sealed class GenerateStrategyBacktestDiagnosticsReportUseCase
{
    public StrategyBacktestDiagnosticsReport Execute(StrategyDefinition strategyDefinition, StrategyOutcomeBacktestRun outcomeRun)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(outcomeRun);
        if (strategyDefinition.StrategyId != outcomeRun.StrategyId || strategyDefinition.Version != outcomeRun.StrategyVersion) throw new InvalidOperationException("Strategy identity does not match the outcome run.");
        var definitions = strategyDefinition.Rules.ToDictionary(rule => rule.RuleId);
        var frames = new List<StrategyFrameDiagnostic>(outcomeRun.OutcomeCount);
        var blockers = new Dictionary<(RuleId RuleId, StrategyVerdict Verdict, RuleEvaluationResult Result), int>();
        var missing = new Dictionary<RuleId, int>();
        foreach (var outcome in outcomeRun.Outcomes)
        {
            var evaluation = outcome.EvaluationOutcome;
            if (evaluation?.BlockingRuleId is { } blockerId)
            {
                if (!definitions.TryGetValue(blockerId, out var definition) || evaluation.BlockingSequence != definition.Sequence) throw new InvalidOperationException("Blocking rule metadata is inconsistent with the strategy definition.");
                var key = (blockerId, outcome.Verdict, evaluation.BlockingResult!.Value); blockers[key] = blockers.GetValueOrDefault(key) + 1;
            }
            foreach (var missingId in outcome.MissingRequiredRuleIds)
            {
                if (!definitions.TryGetValue(missingId, out var definition) || !definition.IsRequired) throw new InvalidOperationException("Missing rule metadata is inconsistent with the strategy definition.");
                missing[missingId] = missing.GetValueOrDefault(missingId) + 1;
            }
            frames.Add(new(outcome.Step, outcome.AsOfUtc, outcome.Verdict, outcome.HasCompleteRequiredCoverage, outcome.Reason,
                evaluation?.BlockingRuleId, evaluation?.BlockingSequence, evaluation?.BlockingResult, outcome.MissingRequiredRuleIds));
        }
        var verdictOrder = new Dictionary<StrategyVerdict, int> { [StrategyVerdict.Wait] = 0, [StrategyVerdict.NoTrade] = 1, [StrategyVerdict.HumanValidationRequired] = 2, [StrategyVerdict.DataUnavailable] = 3, [StrategyVerdict.Ready] = 4 };
        var blockerCounts = blockers.Select(pair => new StrategyBlockingRuleCount(pair.Key.RuleId, definitions[pair.Key.RuleId].Sequence, pair.Key.Verdict, pair.Key.Result, pair.Value))
            .OrderBy(item => verdictOrder[item.Verdict]).ThenBy(item => item.Sequence).ThenBy(item => item.BlockingResult.ToString(), StringComparer.Ordinal).ToArray();
        var missingCounts = missing.Select(pair => new MissingRequiredRuleCount(pair.Key, definitions[pair.Key].Sequence, pair.Value)).OrderBy(item => item.Sequence).ToArray();
        return new(outcomeRun, frames, blockerCounts, missingCounts);
    }
}
