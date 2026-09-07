using System.Collections.ObjectModel;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting.Diagnostics;

/// <summary>
/// Represents a compact diagnostic projection of one strategy replay frame outcome.
/// It does not contain full replay history or trade information.
/// </summary>
public sealed class StrategyFrameDiagnostic
{
    public StrategyFrameDiagnostic(int step, DateTimeOffset asOfUtc, StrategyVerdict verdict,
        bool hasCompleteRequiredCoverage, string reason, RuleId? blockingRuleId, int? blockingSequence,
        RuleEvaluationResult? blockingResult, IEnumerable<RuleId> missingRequiredRuleIds)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim()) throw new ArgumentException("Reason must be non-empty and trimmed.", nameof(reason));
        ArgumentNullException.ThrowIfNull(missingRequiredRuleIds);
        var missing = missingRequiredRuleIds.ToArray();
        if (missing.Any(id => id is null) || missing.Distinct().Count() != missing.Length) throw new ArgumentException("Missing rule identifiers must be non-null and unique.", nameof(missingRequiredRuleIds));
        var anyBlocking = blockingRuleId is not null || blockingSequence is not null || blockingResult is not null;
        var allBlocking = blockingRuleId is not null && blockingSequence is not null && blockingResult is not null;
        if (anyBlocking != allBlocking) throw new ArgumentException("Blocking metadata must be supplied together.");
        if (blockingSequence <= 0) throw new ArgumentOutOfRangeException(nameof(blockingSequence));
        if (hasCompleteRequiredCoverage && missing.Length != 0) throw new ArgumentException("Complete coverage cannot contain missing rules.", nameof(missingRequiredRuleIds));
        if (!hasCompleteRequiredCoverage && (verdict != StrategyVerdict.DataUnavailable || missing.Length == 0 || anyBlocking)) throw new ArgumentException("Incomplete coverage requires missing rules and a blocker-free data-unavailable verdict.");
        Step = step; AsOfUtc = asOfUtc; Verdict = verdict; HasCompleteRequiredCoverage = hasCompleteRequiredCoverage; Reason = reason;
        BlockingRuleId = blockingRuleId; BlockingSequence = blockingSequence; BlockingResult = blockingResult;
        MissingRequiredRuleIds = new ReadOnlyCollection<RuleId>(missing);
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
}
