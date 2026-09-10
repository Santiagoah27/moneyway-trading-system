using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Provides an auditable diagnostic projection over a completed canonical multi-timeframe outcome run. It preserves
/// canonical verdict and coverage counts and does not recompute strategy outcomes.
/// </summary>
public sealed class MultiTimeframeStrategyBacktestDiagnosticsReport
{
    public MultiTimeframeStrategyBacktestDiagnosticsReport(
        MultiTimeframeStrategyOutcomeBacktestRun outcomeRun,
        IEnumerable<MultiTimeframeStrategyFrameDiagnostic> frames,
        IEnumerable<MultiTimeframeStrategyBlockingRuleCount> blockingRules,
        IEnumerable<MultiTimeframeMissingRequiredRuleCount> missingRequiredRules)
    {
        ArgumentNullException.ThrowIfNull(outcomeRun);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(blockingRules);
        ArgumentNullException.ThrowIfNull(missingRequiredRules);
        var frameSnapshot = frames.ToArray();
        var blockerSnapshot = blockingRules.ToArray();
        var missingSnapshot = missingRequiredRules.ToArray();
        if (frameSnapshot.Any(item => item is null) || blockerSnapshot.Any(item => item is null) || missingSnapshot.Any(item => item is null))
            throw new ArgumentException("Report collections cannot contain null elements.");
        if (frameSnapshot.Length != outcomeRun.OutcomeCount)
            throw new ArgumentException("Every global outcome must have one frame diagnostic.", nameof(frames));

        for (var index = 0; index < frameSnapshot.Length; index++)
            ValidateFrame(frameSnapshot[index], outcomeRun.Outcomes[index], outcomeRun.StrategyRun.MarketObservations[index], outcomeRun.ConfiguredTimeframes);
        ValidateBlockingCounts(frameSnapshot, blockerSnapshot);
        ValidateMissingCounts(frameSnapshot, missingSnapshot);

        OutcomeRun = outcomeRun;
        Frames = new ReadOnlyCollection<MultiTimeframeStrategyFrameDiagnostic>(frameSnapshot);
        BlockingRules = new ReadOnlyCollection<MultiTimeframeStrategyBlockingRuleCount>(blockerSnapshot);
        MissingRequiredRules = new ReadOnlyCollection<MultiTimeframeMissingRequiredRuleCount>(missingSnapshot);
    }

    public MultiTimeframeStrategyOutcomeBacktestRun OutcomeRun { get; }
    public IReadOnlyList<MultiTimeframeStrategyFrameDiagnostic> Frames { get; }
    public IReadOnlyList<MultiTimeframeStrategyBlockingRuleCount> BlockingRules { get; }
    public IReadOnlyList<MultiTimeframeMissingRequiredRuleCount> MissingRequiredRules { get; }
    public StrategyId StrategyId => OutcomeRun.StrategyId;
    public StrategyVersion StrategyVersion => OutcomeRun.StrategyVersion;
    public MarketDataProviderId ProviderId => OutcomeRun.ProviderId;
    public MarketSymbol Symbol => OutcomeRun.Symbol;
    public IReadOnlyList<Timeframe> ConfiguredTimeframes => OutcomeRun.ConfiguredTimeframes;
    public int FrameCount => Frames.Count;
    public DateTimeOffset? FirstAsOfUtc => OutcomeRun.FirstAsOfUtc;
    public DateTimeOffset? LastAsOfUtc => OutcomeRun.LastAsOfUtc;
    public int ReadyCount => OutcomeRun.ReadyCount;
    public int WaitCount => OutcomeRun.WaitCount;
    public int NoTradeCount => OutcomeRun.NoTradeCount;
    public int HumanValidationRequiredCount => OutcomeRun.HumanValidationRequiredCount;
    public int DataUnavailableCount => OutcomeRun.DataUnavailableCount;
    public int CompleteRequiredCoverageCount => OutcomeRun.CompleteRequiredCoverageCount;
    public int IncompleteRequiredCoverageCount => OutcomeRun.IncompleteRequiredCoverageCount;
    public int CompleteCoverageDataUnavailableCount => OutcomeRun.CompleteCoverageDataUnavailableCount;

    private static void ValidateFrame(
        MultiTimeframeStrategyFrameDiagnostic frame,
        StrategyReplayContextOutcome outcome,
        MultiTimeframeBacktestObservation market,
        IReadOnlyList<Timeframe> configured)
    {
        var evaluation = outcome.EvaluationOutcome;
        if (frame.Step != outcome.Step || frame.Step != market.Step || frame.AsOfUtc != outcome.AsOfUtc || frame.AsOfUtc != market.AsOfUtc
            || frame.Verdict != outcome.Verdict || frame.HasCompleteRequiredCoverage != outcome.HasCompleteRequiredCoverage
            || frame.Reason != outcome.Reason || frame.BlockingRuleId != evaluation?.BlockingRuleId
            || frame.BlockingSequence != evaluation?.BlockingSequence || frame.BlockingResult != evaluation?.BlockingResult
            || !frame.MissingRequiredRuleIds.SequenceEqual(outcome.MissingRequiredRuleIds)
            || !frame.UpdatedTimeframes.SequenceEqual(market.UpdatedTimeframes)
            || !frame.AvailableTimeframes.SequenceEqual(market.AvailableTimeframes)
            || !ReferenceEquals(frame.WorkflowProgression, outcome.Observation.WorkflowProgression)
            || !ReferenceEquals(frame.LifecycleProgression, outcome.Observation.LifecycleProgression)
            || !frame.MarketDataObservability.SequenceEqual(outcome.Observation.MarketDataObservability))
            throw new ArgumentException("Frame diagnostics must exactly match their global outcome and market observation.", nameof(frame));
        ValidateConfiguredOrder(frame.UpdatedTimeframes, configured);
        ValidateConfiguredOrder(frame.AvailableTimeframes, configured);
    }

    private static void ValidateConfiguredOrder(IReadOnlyList<Timeframe> values, IReadOnlyList<Timeframe> configured)
    {
        var positions = configured.Select((timeframe, index) => (timeframe, index)).ToDictionary(item => item.timeframe, item => item.index);
        var previous = -1;
        foreach (var value in values)
        {
            if (!positions.TryGetValue(value, out var position) || position <= previous)
                throw new ArgumentException("Frame timeframes must be configured and preserve configured order.");
            previous = position;
        }
    }

    private static void ValidateBlockingCounts(
        IReadOnlyList<MultiTimeframeStrategyFrameDiagnostic> frames,
        IReadOnlyList<MultiTimeframeStrategyBlockingRuleCount> counts)
    {
        var expected = frames.Where(frame => frame.BlockingRuleId is not null)
            .GroupBy(frame => (RuleId: frame.BlockingRuleId!, Sequence: frame.BlockingSequence!.Value, frame.Verdict, Result: frame.BlockingResult!.Value))
            .Select(group => (group.Key.RuleId, group.Key.Sequence, group.Key.Verdict, group.Key.Result, Count: group.Count()))
            .OrderBy(item => VerdictOrder(item.Verdict)).ThenBy(item => item.Sequence).ThenBy(item => item.Result)
            .ToArray();
        var actual = counts.Select(item => (item.RuleId, item.Sequence, item.Verdict, Result: item.BlockingResult, item.Count)).ToArray();
        if (!actual.SequenceEqual(expected))
            throw new ArgumentException("Blocking rule counts must exactly aggregate frame blockers in stable order.", nameof(counts));
    }

    private static void ValidateMissingCounts(
        IReadOnlyList<MultiTimeframeStrategyFrameDiagnostic> frames,
        IReadOnlyList<MultiTimeframeMissingRequiredRuleCount> counts)
    {
        var expectedCounts = frames.SelectMany(frame => frame.MissingRequiredRuleIds).GroupBy(ruleId => ruleId)
            .ToDictionary(group => group.Key, group => group.Count());
        if (counts.Count != expectedCounts.Count || counts.Select(item => item.RuleId).Distinct().Count() != counts.Count
            || counts.Select(item => item.Sequence).Where((value, index) => index > 0 && value <= counts[index - 1].Sequence).Any()
            || counts.Any(item => !expectedCounts.TryGetValue(item.RuleId, out var count) || item.Count != count))
            throw new ArgumentException("Missing required rule counts must exactly aggregate frame coverage in sequence order.", nameof(counts));
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
