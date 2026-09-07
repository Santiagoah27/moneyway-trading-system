using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Provides an auditable diagnostic view over a completed <see cref="StrategyOutcomeBacktestRun"/>.
/// It does not recompute strategy outcomes.
/// </summary>
public sealed class StrategyBacktestDiagnosticsReport
{
    public StrategyBacktestDiagnosticsReport(StrategyOutcomeBacktestRun outcomeRun, IEnumerable<StrategyFrameDiagnostic> frames,
        IEnumerable<StrategyBlockingRuleCount> blockingRules, IEnumerable<MissingRequiredRuleCount> missingRequiredRules)
    {
        ArgumentNullException.ThrowIfNull(outcomeRun); ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(blockingRules); ArgumentNullException.ThrowIfNull(missingRequiredRules);
        var frameSnapshot = frames.ToArray(); var blockerSnapshot = blockingRules.ToArray(); var missingSnapshot = missingRequiredRules.ToArray();
        if (frameSnapshot.Any(x => x is null) || blockerSnapshot.Any(x => x is null) || missingSnapshot.Any(x => x is null)) throw new ArgumentException("Report collections cannot contain null elements.");
        if (frameSnapshot.Length != outcomeRun.OutcomeCount) throw new ArgumentException("Every outcome must have one frame diagnostic.", nameof(frames));
        for (var index = 0; index < frameSnapshot.Length; index++)
        {
            var frame = frameSnapshot[index]; var outcome = outcomeRun.Outcomes[index]; var evaluation = outcome.EvaluationOutcome;
            if (frame.Step != outcome.Step || frame.AsOfUtc != outcome.AsOfUtc || frame.Verdict != outcome.Verdict
                || frame.HasCompleteRequiredCoverage != outcome.HasCompleteRequiredCoverage || frame.Reason != outcome.Reason
                || frame.BlockingRuleId != evaluation?.BlockingRuleId || frame.BlockingSequence != evaluation?.BlockingSequence
                || frame.BlockingResult != evaluation?.BlockingResult || !frame.MissingRequiredRuleIds.SequenceEqual(outcome.MissingRequiredRuleIds))
                throw new ArgumentException("Frame diagnostics must exactly match source outcomes.", nameof(frames));
        }
        var expectedBlockers = frameSnapshot.Where(x => x.BlockingRuleId is not null)
            .GroupBy(x => (RuleId: x.BlockingRuleId!, Sequence: x.BlockingSequence!.Value, x.Verdict, Result: x.BlockingResult!.Value))
            .Select(group => (group.Key.RuleId, group.Key.Sequence, group.Key.Verdict, group.Key.Result, Count: group.Count()))
            .ToArray();
        if (blockerSnapshot.Length != expectedBlockers.Length || blockerSnapshot.Any(item =>
            !expectedBlockers.Contains((item.RuleId, item.Sequence, item.Verdict, item.BlockingResult, item.Count))))
            throw new ArgumentException("Blocking rule counts must exactly aggregate frame blockers.", nameof(blockingRules));
        var expectedMissing = frameSnapshot.SelectMany(x => x.MissingRequiredRuleIds).GroupBy(x => x)
            .Select(group => (RuleId: group.Key, Count: group.Count())).ToArray();
        if (missingSnapshot.Length != expectedMissing.Length || missingSnapshot.Any(item =>
            !expectedMissing.Contains((item.RuleId, item.Count))))
            throw new ArgumentException("Missing required rule counts must exactly aggregate frame coverage.", nameof(missingRequiredRules));
        OutcomeRun = outcomeRun; Frames = new ReadOnlyCollection<StrategyFrameDiagnostic>(frameSnapshot);
        BlockingRules = new ReadOnlyCollection<StrategyBlockingRuleCount>(blockerSnapshot);
        MissingRequiredRules = new ReadOnlyCollection<MissingRequiredRuleCount>(missingSnapshot);
    }

    public StrategyOutcomeBacktestRun OutcomeRun { get; }
    public IReadOnlyList<StrategyFrameDiagnostic> Frames { get; }
    public IReadOnlyList<StrategyBlockingRuleCount> BlockingRules { get; }
    public IReadOnlyList<MissingRequiredRuleCount> MissingRequiredRules { get; }
    public StrategyId StrategyId => OutcomeRun.StrategyId;
    public StrategyVersion StrategyVersion => OutcomeRun.StrategyVersion;
    public MarketDataProviderId ProviderId => OutcomeRun.ProviderId;
    public MarketSymbol Symbol => OutcomeRun.Symbol;
    public Timeframe Timeframe => OutcomeRun.Timeframe;
    public int FrameCount => Frames.Count;
    public int ReadyCount => OutcomeRun.ReadyCount;
    public int WaitCount => OutcomeRun.WaitCount;
    public int NoTradeCount => OutcomeRun.NoTradeCount;
    public int HumanValidationRequiredCount => OutcomeRun.HumanValidationRequiredCount;
    public int DataUnavailableCount => OutcomeRun.DataUnavailableCount;
    public int CompleteRequiredCoverageCount => OutcomeRun.CompleteRequiredCoverageCount;
    public int IncompleteRequiredCoverageCount => OutcomeRun.IncompleteRequiredCoverageCount;
    public int CompleteCoverageDataUnavailableCount => OutcomeRun.CompleteCoverageDataUnavailableCount;
}
