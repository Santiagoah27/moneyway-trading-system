using System.Collections.ObjectModel;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.Strategies.Evaluation;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Represents the safe strategy outcome for one synchronized multi-timeframe replay observation. Incomplete required
/// coverage produces DataUnavailable without a domain outcome; complete coverage delegates to the sequential evaluator.
/// It contains no trade simulation.
/// </summary>
public sealed class StrategyReplayContextOutcome
{
    public const string IncompleteCoverageReason = "Required rule evaluation coverage is incomplete for this strategy replay context.";

    public StrategyReplayContextOutcome(
        StrategyReplayContextObservation observation,
        bool hasCompleteRequiredCoverage,
        IEnumerable<RuleId> missingRequiredRuleIds,
        StrategyVerdict verdict,
        string reason,
        StrategyEvaluationOutcome? evaluationOutcome)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(missingRequiredRuleIds);
        ArgumentNullException.ThrowIfNull(reason);
        if (string.IsNullOrWhiteSpace(reason) || reason != reason.Trim())
            throw new ArgumentException("Reason must be non-empty and have no surrounding whitespace.", nameof(reason));
        var missing = missingRequiredRuleIds.ToArray();
        if (missing.Any(x => x is null) || missing.Distinct().Count() != missing.Length)
            throw new ArgumentException("Missing rule identifiers must be non-null and unique.", nameof(missingRequiredRuleIds));
        if (hasCompleteRequiredCoverage)
        {
            if (missing.Length != 0 || evaluationOutcome is null || verdict != evaluationOutcome.Verdict || reason != evaluationOutcome.Reason)
                throw new ArgumentException("Complete coverage must match a sequential evaluation outcome.");
        }
        else if (missing.Length == 0 || evaluationOutcome is not null || verdict != StrategyVerdict.DataUnavailable || reason != IncompleteCoverageReason)
        {
            throw new ArgumentException("Incomplete coverage must use the safe data-unavailable outcome.");
        }
        Observation = observation;
        HasCompleteRequiredCoverage = hasCompleteRequiredCoverage;
        MissingRequiredRuleIds = new ReadOnlyCollection<RuleId>(missing);
        Verdict = verdict;
        Reason = reason;
        EvaluationOutcome = evaluationOutcome;
    }

    public StrategyReplayContextObservation Observation { get; }
    public bool HasCompleteRequiredCoverage { get; }
    public IReadOnlyList<RuleId> MissingRequiredRuleIds { get; }
    public StrategyVerdict Verdict { get; }
    public string Reason { get; }
    public StrategyEvaluationOutcome? EvaluationOutcome { get; }
    public StrategyId StrategyId => Observation.StrategyId;
    public StrategyVersion StrategyVersion => Observation.StrategyVersion;
    public MarketDataProviderId ProviderId => Observation.ProviderId;
    public MarketSymbol Symbol => Observation.Symbol;
    public int Step => Observation.Step;
    public DateTimeOffset AsOfUtc => Observation.AsOfUtc;
}
