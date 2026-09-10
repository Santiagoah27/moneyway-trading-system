using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyReplay;

/// <summary>
/// Records rule evaluations produced for one synchronized strategy replay context. It contains no single global
/// current candle and no strategy verdict.
/// </summary>
public sealed class StrategyReplayContextObservation
{
    public StrategyReplayContextObservation(
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        MarketDataProviderId providerId,
        MarketSymbol symbol,
        int step,
        DateTimeOffset asOfUtc,
        IEnumerable<RuleEvaluation> evaluations,
        StrategyReplayProgressionSnapshot? workflowProgression = null,
        StrategyReplayLifecycleSnapshot? lifecycleProgression = null,
        IEnumerable<ReplayMarketDataObservabilityAssessment>? marketDataObservability = null)
    {
        ArgumentNullException.ThrowIfNull(strategyId);
        ArgumentNullException.ThrowIfNull(strategyVersion);
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(evaluations);
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC.", nameof(asOfUtc));
        var snapshot = evaluations.ToArray();
        var observabilityInput = (marketDataObservability ?? []).ToArray();
        if (snapshot.Any(x => x is null)) throw new ArgumentException("Evaluations cannot contain null.", nameof(evaluations));
        if (observabilityInput.Any(item => item is null))
            throw new ArgumentException("Market-data observability assessments cannot contain null.", nameof(marketDataObservability));
        var observability = observabilityInput.OrderBy(item => item.RuleId.Value, StringComparer.Ordinal).ToArray();
        if (snapshot.GroupBy(x => x.RuleId).Any(x => x.Count() > 1) || snapshot.GroupBy(x => x.Sequence).Any(x => x.Count() > 1))
            throw new ArgumentException("Evaluation identifiers and sequences must be unique.", nameof(evaluations));
        if (snapshot.Where((x, i) => i > 0 && x.Sequence <= snapshot[i - 1].Sequence).Any())
            throw new ArgumentException("Evaluations must be ordered by sequence.", nameof(evaluations));
        if (snapshot.Any(x => x.EvaluatedAtUtc != asOfUtc))
            throw new ArgumentException("Evaluation timestamps must match the context.", nameof(evaluations));
        if (observability.GroupBy(item => item.RuleId).Any(group => group.Count() > 1)
            || observability.Any(item =>
                item.StrategyId != strategyId
                || item.StrategyVersion != strategyVersion
                || item.ProviderId != providerId
                || item.Symbol != symbol
                || item.Step != step
                || item.AsOfUtc != asOfUtc))
            throw new ArgumentException("Market-data observability assessments must be non-null, unique by rule, and match the observation identity.", nameof(marketDataObservability));
        if (workflowProgression is not null
            && (workflowProgression.StrategyId != strategyId
                || workflowProgression.StrategyVersion != strategyVersion
                || workflowProgression.ProviderId != providerId
                || workflowProgression.Symbol != symbol
                || workflowProgression.Step != step
                || workflowProgression.AsOfUtc != asOfUtc
                || !workflowProgression.RuleEligibility.Select(item => item.RuleId).SequenceEqual(snapshot.Select(item => item.RuleId))
                || workflowProgression.RuleEligibility.Zip(snapshot).Any(pair =>
                    pair.First.EstablishesProgression != (pair.First.IsEligible && pair.Second.Result == RuleEvaluationResult.Passed))))
            throw new ArgumentException("Workflow progression must match the observation identity and timestamp.", nameof(workflowProgression));
        if (lifecycleProgression is not null
            && (workflowProgression is null
                || lifecycleProgression.StrategyId != strategyId
                || lifecycleProgression.StrategyVersion != strategyVersion
                || lifecycleProgression.ProviderId != providerId
                || lifecycleProgression.Symbol != symbol
                || lifecycleProgression.Step != step
                || lifecycleProgression.AsOfUtc != asOfUtc))
            throw new ArgumentException("Lifecycle progression must match the observation identity and timestamp.", nameof(lifecycleProgression));
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        ProviderId = providerId;
        Symbol = symbol;
        Step = step;
        AsOfUtc = asOfUtc;
        Evaluations = new ReadOnlyCollection<RuleEvaluation>(snapshot);
        WorkflowProgression = workflowProgression;
        LifecycleProgression = lifecycleProgression;
        MarketDataObservability = new ReadOnlyCollection<ReplayMarketDataObservabilityAssessment>(observability);
    }

    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public MarketDataProviderId ProviderId { get; }
    public MarketSymbol Symbol { get; }
    public int Step { get; }
    public DateTimeOffset AsOfUtc { get; }
    public IReadOnlyList<RuleEvaluation> Evaluations { get; }
    public StrategyReplayProgressionSnapshot? WorkflowProgression { get; }
    public StrategyReplayLifecycleSnapshot? LifecycleProgression { get; }
    public IReadOnlyList<ReplayMarketDataObservabilityAssessment> MarketDataObservability { get; }
    public int EvaluationCount => Evaluations.Count;

    public StrategyReplayContextObservation WithWorkflowProgression(StrategyReplayProgressionSnapshot workflowProgression)
    {
        ArgumentNullException.ThrowIfNull(workflowProgression);
        if (WorkflowProgression is not null)
            throw new InvalidOperationException("Workflow progression is already attached to this observation.");
        return new(StrategyId, StrategyVersion, ProviderId, Symbol, Step, AsOfUtc, Evaluations, workflowProgression, marketDataObservability: MarketDataObservability);
    }

    public StrategyReplayContextObservation WithProgressions(
        StrategyReplayProgressionSnapshot workflowProgression,
        StrategyReplayLifecycleSnapshot lifecycleProgression)
    {
        ArgumentNullException.ThrowIfNull(workflowProgression);
        ArgumentNullException.ThrowIfNull(lifecycleProgression);
        if (WorkflowProgression is not null || LifecycleProgression is not null)
            throw new InvalidOperationException("Replay progression is already attached to this observation.");
        return new(
            StrategyId,
            StrategyVersion,
            ProviderId,
            Symbol,
            Step,
            AsOfUtc,
            Evaluations,
            workflowProgression,
            lifecycleProgression,
            MarketDataObservability);
    }

    public StrategyReplayContextObservation WithMarketDataObservability(
        IEnumerable<ReplayMarketDataObservabilityAssessment> marketDataObservability)
    {
        ArgumentNullException.ThrowIfNull(marketDataObservability);
        if (MarketDataObservability.Count != 0)
            throw new InvalidOperationException("Market-data observability is already attached to this observation.");
        return new(
            StrategyId,
            StrategyVersion,
            ProviderId,
            Symbol,
            Step,
            AsOfUtc,
            Evaluations,
            WorkflowProgression,
            LifecycleProgression,
            marketDataObservability);
    }
}
