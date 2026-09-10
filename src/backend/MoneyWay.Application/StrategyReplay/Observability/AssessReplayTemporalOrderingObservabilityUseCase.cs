using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay.Observability;

/// <summary>Determines whether two evidence windows can be chronologically ordered from data visible at the current replay boundary.</summary>
public sealed class AssessReplayTemporalOrderingObservabilityUseCase
{
    public const string SufficientReason = "The supplied evidence windows are disjoint, so their chronological order is observable.";
    public const string ResolutionInsufficientReason = "The supplied evidence windows overlap, so available market-data resolution cannot establish their chronological order.";

    public ReplayMarketDataObservabilityAssessment Execute(
        StrategyDefinition strategyDefinition,
        StrategyReplayContext context,
        RuleId ruleId,
        ReplayTemporalEvidenceWindow leftEvidence,
        ReplayTemporalEvidenceWindow rightEvidence)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ruleId);
        ArgumentNullException.ThrowIfNull(leftEvidence);
        ArgumentNullException.ThrowIfNull(rightEvidence);
        if (context.StrategyId != strategyDefinition.StrategyId || context.StrategyVersion != strategyDefinition.Version)
            throw new InvalidOperationException("Strategy context identity must exactly match the strategy definition.");
        if (strategyDefinition.Rules.All(rule => rule.RuleId != ruleId))
            throw new InvalidOperationException("Observability assessment references an unknown strategy rule.");

        var status = AreDisjoint(leftEvidence, rightEvidence)
            ? ReplayMarketDataObservabilityStatus.Sufficient
            : ReplayMarketDataObservabilityStatus.ResolutionInsufficient;
        var reason = status == ReplayMarketDataObservabilityStatus.Sufficient
            ? SufficientReason
            : ResolutionInsufficientReason;

        return new(
            context.StrategyId,
            context.StrategyVersion,
            context.ProviderId,
            context.Symbol,
            ruleId,
            context.Step,
            context.AsOfUtc,
            leftEvidence,
            rightEvidence,
            status,
            reason);
    }

    private static bool AreDisjoint(ReplayTemporalEvidenceWindow left, ReplayTemporalEvidenceWindow right) =>
        left.LatestPossibleUtc < right.EarliestPossibleUtc || right.LatestPossibleUtc < left.EarliestPossibleUtc;
}
