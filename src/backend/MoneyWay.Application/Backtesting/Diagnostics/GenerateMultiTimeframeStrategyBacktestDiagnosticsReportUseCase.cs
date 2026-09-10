using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Projects and aggregates an already completed canonical multi-timeframe outcome run. It performs no replay,
/// market-data evaluation, strategy-verdict calculation, or trading simulation.
/// </summary>
public sealed class GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase
{
    public MultiTimeframeStrategyBacktestDiagnosticsReport Execute(
        StrategyDefinition strategyDefinition,
        MultiTimeframeStrategyOutcomeBacktestRun outcomeRun)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(outcomeRun);
        if (strategyDefinition.StrategyId != outcomeRun.StrategyId || strategyDefinition.Version != outcomeRun.StrategyVersion)
            throw new InvalidOperationException("Strategy identity does not match the outcome run.");

        var definitions = strategyDefinition.Rules.ToDictionary(rule => rule.RuleId);
        var frames = new List<MultiTimeframeStrategyFrameDiagnostic>(outcomeRun.OutcomeCount);
        var blockers = new Dictionary<(RuleId RuleId, StrategyVerdict Verdict, RuleEvaluationResult Result), int>();
        var missing = new Dictionary<RuleId, int>();
        for (var index = 0; index < outcomeRun.OutcomeCount; index++)
        {
            var outcome = outcomeRun.Outcomes[index];
            var market = outcomeRun.StrategyRun.MarketObservations[index];
            if (outcome.Step != market.Step || outcome.AsOfUtc != market.AsOfUtc)
                throw new InvalidOperationException("Outcome and market observation are not aligned.");

            var evaluation = outcome.EvaluationOutcome;
            if (evaluation?.BlockingRuleId is { } blockerId)
            {
                if (!definitions.TryGetValue(blockerId, out var definition) || evaluation.BlockingSequence != definition.Sequence)
                    throw new InvalidOperationException("Blocking rule metadata is inconsistent with the strategy definition.");
                var key = (blockerId, outcome.Verdict, evaluation.BlockingResult!.Value);
                blockers[key] = blockers.GetValueOrDefault(key) + 1;
            }
            foreach (var missingId in outcome.MissingRequiredRuleIds)
            {
                if (!definitions.TryGetValue(missingId, out var definition) || !definition.IsRequired)
                    throw new InvalidOperationException("Missing rule metadata is inconsistent with the strategy definition.");
                missing[missingId] = missing.GetValueOrDefault(missingId) + 1;
            }
            frames.Add(new(
                outcome.Step,
                outcome.AsOfUtc,
                outcome.Verdict,
                outcome.HasCompleteRequiredCoverage,
                outcome.Reason,
                evaluation?.BlockingRuleId,
                evaluation?.BlockingSequence,
                evaluation?.BlockingResult,
                outcome.MissingRequiredRuleIds,
                market.UpdatedTimeframes,
                market.AvailableTimeframes,
                outcome.Observation.WorkflowProgression,
                outcome.Observation.LifecycleProgression,
                outcome.Observation.MarketDataObservability));
        }

        var blockingCounts = blockers.Select(pair => new MultiTimeframeStrategyBlockingRuleCount(
                pair.Key.RuleId,
                definitions[pair.Key.RuleId].Sequence,
                pair.Key.Verdict,
                pair.Key.Result,
                pair.Value))
            .OrderBy(item => VerdictOrder(item.Verdict)).ThenBy(item => item.Sequence).ThenBy(item => item.BlockingResult)
            .ToArray();
        var missingCounts = missing.Select(pair => new MultiTimeframeMissingRequiredRuleCount(
                pair.Key,
                definitions[pair.Key].Sequence,
                pair.Value))
            .OrderBy(item => item.Sequence)
            .ToArray();
        return new(outcomeRun, frames, blockingCounts, missingCounts);
    }

    private static int VerdictOrder(StrategyVerdict verdict) => verdict switch
    {
        StrategyVerdict.Wait => 0,
        StrategyVerdict.NoTrade => 1,
        StrategyVerdict.HumanValidationRequired => 2,
        StrategyVerdict.DataUnavailable => 3,
        StrategyVerdict.Ready => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(verdict)),
    };
}
