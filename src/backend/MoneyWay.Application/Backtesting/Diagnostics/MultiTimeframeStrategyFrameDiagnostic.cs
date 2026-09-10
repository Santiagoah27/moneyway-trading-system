using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Represents a compact diagnostic projection of one synchronized canonical strategy outcome. It includes global
/// replay timing and timeframe state, but no candle-history copies or trade information.
/// </summary>
public sealed class MultiTimeframeStrategyFrameDiagnostic
{
    public MultiTimeframeStrategyFrameDiagnostic(
        int step,
        DateTimeOffset asOfUtc,
        StrategyVerdict verdict,
        bool hasCompleteRequiredCoverage,
        string reason,
        RuleId? blockingRuleId,
        int? blockingSequence,
        RuleEvaluationResult? blockingResult,
        IEnumerable<RuleId> missingRequiredRuleIds,
        IEnumerable<Timeframe> updatedTimeframes,
        IEnumerable<Timeframe> availableTimeframes,
        StrategyReplayProgressionSnapshot? workflowProgression = null)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));
        ArgumentNullException.ThrowIfNull(missingRequiredRuleIds);
        ArgumentNullException.ThrowIfNull(updatedTimeframes);
        ArgumentNullException.ThrowIfNull(availableTimeframes);

        var missing = missingRequiredRuleIds.ToArray();
        var updated = updatedTimeframes.ToArray();
        var available = availableTimeframes.ToArray();
        ValidateUniqueItems(missing, nameof(missingRequiredRuleIds));
        ValidateUniqueItems(updated, nameof(updatedTimeframes));
        ValidateUniqueItems(available, nameof(availableTimeframes));
        if (updated.Any(timeframe => !available.Contains(timeframe)))
            throw new ArgumentException("Updated timeframes must be available.", nameof(updatedTimeframes));

        var hasAnyBlockingValue = blockingRuleId is not null || blockingSequence is not null || blockingResult is not null;
        var hasAllBlockingValues = blockingRuleId is not null && blockingSequence is not null && blockingResult is not null;
        if (hasAnyBlockingValue != hasAllBlockingValues)
            throw new ArgumentException("Blocking metadata must be supplied together.");
        if (blockingSequence <= 0) throw new ArgumentOutOfRangeException(nameof(blockingSequence));
        if (hasCompleteRequiredCoverage && missing.Length != 0)
            throw new ArgumentException("Complete coverage cannot contain missing rules.", nameof(missingRequiredRuleIds));
        if (!hasCompleteRequiredCoverage && (verdict != StrategyVerdict.DataUnavailable || missing.Length == 0 || hasAnyBlockingValue))
            throw new ArgumentException("Incomplete coverage requires missing rules and a blocker-free data-unavailable verdict.");
        if (workflowProgression is not null
            && (workflowProgression.Step != step || workflowProgression.AsOfUtc != asOfUtc))
            throw new ArgumentException("Workflow progression must match the frame step and timestamp.", nameof(workflowProgression));

        Step = step;
        AsOfUtc = asOfUtc;
        Verdict = verdict;
        HasCompleteRequiredCoverage = hasCompleteRequiredCoverage;
        Reason = reason;
        BlockingRuleId = blockingRuleId;
        BlockingSequence = blockingSequence;
        BlockingResult = blockingResult;
        MissingRequiredRuleIds = new ReadOnlyCollection<RuleId>(missing);
        UpdatedTimeframes = new ReadOnlyCollection<Timeframe>(updated);
        AvailableTimeframes = new ReadOnlyCollection<Timeframe>(available);
        WorkflowProgression = workflowProgression;
    }

    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public StrategyVerdict Verdict { get; }
    public bool HasCompleteRequiredCoverage { get; }
    public string Reason { get; }
    public RuleId? BlockingRuleId { get; }
    public int? BlockingSequence { get; }
    public RuleEvaluationResult? BlockingResult { get; }
    public IReadOnlyList<RuleId> MissingRequiredRuleIds { get; }
    public IReadOnlyList<Timeframe> UpdatedTimeframes { get; }
    public IReadOnlyList<Timeframe> AvailableTimeframes { get; }
    public StrategyReplayProgressionSnapshot? WorkflowProgression { get; }

    private static void ValidateUniqueItems<T>(IReadOnlyList<T> values, string parameterName) where T : class
    {
        if (values.Any(value => value is null) || values.Distinct().Count() != values.Count)
            throw new ArgumentException("Collection items must be non-null and unique.", parameterName);
    }
}
