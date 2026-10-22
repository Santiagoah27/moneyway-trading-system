using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Backtesting;

/// <summary>
/// Runs one synchronized multi-timeframe replay, creates and evaluates one strategy context per global step, and records
/// aligned observations. It performs no strategy-verdict aggregation or trading simulation.
/// </summary>
public sealed class GenerateMultiTimeframeStrategyBacktestRunUseCase
{
    private readonly RunMultiTimeframeReplayUseCase replayUseCase;
    private readonly CreateStrategyReplayContextUseCase contextUseCase;
    private readonly EvaluateStrategyReplayContextUseCase evaluationUseCase;
    private readonly StrategyReplayWorkflowCatalog workflowCatalog;
    private readonly AdvanceStrategyReplayProgressionUseCase progressionUseCase;
    private readonly StrategyReplayLifecyclePolicyCatalog lifecyclePolicyCatalog;
    private readonly AdvanceStrategyReplayLifecycleUseCase lifecycleUseCase;

    public GenerateMultiTimeframeStrategyBacktestRunUseCase(
        RunMultiTimeframeReplayUseCase replayUseCase,
        CreateStrategyReplayContextUseCase contextUseCase,
        EvaluateStrategyReplayContextUseCase evaluationUseCase)
        : this(
            replayUseCase,
            contextUseCase,
            evaluationUseCase,
            StrategyReplayWorkflowCatalog.Empty,
            new(),
            StrategyReplayLifecyclePolicyCatalog.Empty,
            new(new()))
    {
    }

    public GenerateMultiTimeframeStrategyBacktestRunUseCase(
        RunMultiTimeframeReplayUseCase replayUseCase,
        CreateStrategyReplayContextUseCase contextUseCase,
        EvaluateStrategyReplayContextUseCase evaluationUseCase,
        StrategyReplayWorkflowCatalog workflowCatalog,
        AdvanceStrategyReplayProgressionUseCase progressionUseCase)
        : this(
            replayUseCase,
            contextUseCase,
            evaluationUseCase,
            workflowCatalog,
            progressionUseCase,
            StrategyReplayLifecyclePolicyCatalog.Empty,
            new(progressionUseCase))
    {
    }

    public GenerateMultiTimeframeStrategyBacktestRunUseCase(
        RunMultiTimeframeReplayUseCase replayUseCase,
        CreateStrategyReplayContextUseCase contextUseCase,
        EvaluateStrategyReplayContextUseCase evaluationUseCase,
        StrategyReplayWorkflowCatalog workflowCatalog,
        AdvanceStrategyReplayProgressionUseCase progressionUseCase,
        StrategyReplayLifecyclePolicyCatalog lifecyclePolicyCatalog,
        AdvanceStrategyReplayLifecycleUseCase lifecycleUseCase)
    {
        this.replayUseCase = replayUseCase ?? throw new ArgumentNullException(nameof(replayUseCase));
        this.contextUseCase = contextUseCase ?? throw new ArgumentNullException(nameof(contextUseCase));
        this.evaluationUseCase = evaluationUseCase ?? throw new ArgumentNullException(nameof(evaluationUseCase));
        this.workflowCatalog = workflowCatalog ?? throw new ArgumentNullException(nameof(workflowCatalog));
        this.progressionUseCase = progressionUseCase ?? throw new ArgumentNullException(nameof(progressionUseCase));
        this.lifecyclePolicyCatalog = lifecyclePolicyCatalog ?? throw new ArgumentNullException(nameof(lifecyclePolicyCatalog));
        this.lifecycleUseCase = lifecycleUseCase ?? throw new ArgumentNullException(nameof(lifecycleUseCase));
    }

    public MultiTimeframeStrategyBacktestRun Execute(StrategyDefinition strategyDefinition, IEnumerable<CandleSeries> series)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                frame => consumeContext(contextUseCase.Execute(strategyDefinition, frame))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(preparationObservations);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                frame => consumeContext(contextUseCase.Execute(strategyDefinition, frame, preparationObservations))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        IEnumerable<IStrategyReplayInputObservation> inputObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(inputObservations);
        var inputSnapshot = inputObservations.ToArray();
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                frame => consumeContext(contextUseCase.Execute(strategyDefinition, frame, inputSnapshot))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        IStrategyReplayLifecycleEvidenceProducer evidenceProducer)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series); ArgumentNullException.ThrowIfNull(evidenceProducer);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                frame => consumeContext(contextUseCase.Execute(strategyDefinition, frame))),
            evidenceProducer);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series); ArgumentNullException.ThrowIfNull(marketPriceObservations);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                marketPriceObservations,
                frame => consumeContext(contextUseCase.ExecuteCanonical(strategyDefinition, frame))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        NasdaqPreparationCompletionObservationSeries preparationObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations); ArgumentNullException.ThrowIfNull(preparationObservations);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                marketPriceObservations,
                frame => consumeContext(contextUseCase.ExecuteCanonical(strategyDefinition, frame, preparationObservations))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        IEnumerable<IStrategyReplayInputObservation> inputObservations)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations); ArgumentNullException.ThrowIfNull(inputObservations);
        var inputSnapshot = inputObservations.ToArray();
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                marketPriceObservations,
                frame => consumeContext(contextUseCase.ExecuteCanonical(strategyDefinition, frame, inputSnapshot))),
            null);
    }

    public MultiTimeframeStrategyBacktestRun Execute(
        StrategyDefinition strategyDefinition,
        IEnumerable<CandleSeries> series,
        HistoricalMarketPriceObservationSeries marketPriceObservations,
        IStrategyReplayLifecycleEvidenceProducer evidenceProducer)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(marketPriceObservations); ArgumentNullException.ThrowIfNull(evidenceProducer);
        return ExecuteCore(
            strategyDefinition,
            consumeContext => replayUseCase.Execute(
                series,
                marketPriceObservations,
                frame => consumeContext(contextUseCase.ExecuteCanonical(strategyDefinition, frame))),
            evidenceProducer);
    }

    public MultiTimeframeStrategyBacktestRun ExecuteMentorSession(StrategyDefinition strategyDefinition, IEnumerable<CandleSeries> series,
        NasdaqMentorSessionReplay session, ReplayPerformanceProfile? performance = null)
    {
        ArgumentNullException.ThrowIfNull(strategyDefinition); ArgumentNullException.ThrowIfNull(series); ArgumentNullException.ThrowIfNull(session);
        using var timing = performance?.Measure("replay.synchronized-inclusive");
        session.Start(strategyDefinition);
        return ExecuteCore(strategyDefinition,
            consume => replayUseCase.Execute(series, frame =>
            {
                StrategyReplayContext context;
                using (performance?.Measure("replay.context-create")) context = contextUseCase.Execute(strategyDefinition, frame);
                consume(context);
            }),
            null, session, performance);
    }

    private MultiTimeframeStrategyBacktestRun ExecuteCore(
        StrategyDefinition strategyDefinition,
        Func<Action<StrategyReplayContext>, MultiTimeframeReplayRunResult> runReplay,
        IStrategyReplayLifecycleEvidenceProducer? evidenceProducer,
        NasdaqMentorSessionReplay? session = null, ReplayPerformanceProfile? performance = null)
    {
        var workflow = workflowCatalog.Find(strategyDefinition.StrategyId, strategyDefinition.Version);
        var lifecyclePolicy = lifecyclePolicyCatalog.Find(strategyDefinition.StrategyId, strategyDefinition.Version);
        if (evidenceProducer is not null && (workflow is null || lifecyclePolicy is null))
            throw new InvalidOperationException("Lifecycle evidence requires a registered workflow and lifecycle policy.");
        StrategyReplayProgressionSnapshot? progression = null;
        StrategyReplayLifecycleSnapshot? lifecycle = null;
        var marketObservations = new List<MultiTimeframeBacktestObservation>();
        var strategyObservations = new List<StrategyReplayContextObservation>();
        var nasdaq = StrategyDefinitions.Nasdaq.MoneyWayNasdaqStrategyDefinition.Instance;
        // Run-local references only: retain every gate, in emission order, including repetitions
        // and non-terminal candidates. Other strategies keep the original history lookup.
        var priorGateCandidates = strategyDefinition.StrategyId == nasdaq.StrategyId && strategyDefinition.Version == nasdaq.Version
            ? new List<IReplayRuleGateFact>() : null;
        var replayResult = runReplay(context =>
        {
            StrategyReplayContext evaluationContext;
            using (performance?.Measure("replay.history-context")) evaluationContext = context.WithPriorObservations(strategyObservations);
            using (performance?.Measure("session.bind-inclusive"))
                if (session is not null) context = evaluationContext = session.Bind(evaluationContext);
            var marketObservation = new MultiTimeframeBacktestObservation(
                context.Step,
                context.AsOfUtc,
                context.UpdatedTimeframes,
                context.AvailableTimeframes,
                context.MarketDataAvailability);
            StrategyReplayContextObservation strategyObservation;
            using (performance?.Measure("evaluation.observation-inclusive"))
                strategyObservation = priorGateCandidates is null
                    ? evaluationUseCase.Execute(strategyDefinition, evaluationContext, performance)
                    : evaluationUseCase.ExecuteWithPriorGateCandidates(strategyDefinition, evaluationContext, priorGateCandidates, performance);
            using (performance?.Measure("replay.progression-lifecycle"))
            {
                if (workflow is not null)
                {
                    if (lifecyclePolicy is null)
                    {
                        progression = progressionUseCase.Execute(workflow, strategyObservation, progression);
                        strategyObservation = strategyObservation.WithWorkflowProgression(progression);
                    }
                    else
                    {
                        var producer = evidenceProducer ?? lifecyclePolicy as IStrategyReplayLifecycleEvidenceProducer;
                        var advanced = producer is null
                            ? lifecycleUseCase.Execute(workflow, lifecyclePolicy, strategyObservation, lifecycle, progression)
                            : lifecycleUseCase.Execute(workflow, lifecyclePolicy, evaluationContext, strategyObservation, lifecycle, progression, producer);
                        progression = advanced.WorkflowProgression;
                        lifecycle = advanced.LifecycleProgression;
                        strategyObservation = strategyObservation.WithProgressions(progression, lifecycle);
                    }
                }
            }
            if (strategyObservation.StrategyId != strategyDefinition.StrategyId || strategyObservation.StrategyVersion != strategyDefinition.Version
                || strategyObservation.ProviderId != context.ProviderId || strategyObservation.Symbol != context.Symbol
                || strategyObservation.Step != context.Step || strategyObservation.AsOfUtc != context.AsOfUtc)
                throw new InvalidOperationException("Strategy observation is inconsistent with the replay context.");
            using (performance?.Measure("session.capture-historical-inclusive")) session?.Capture(context, strategyObservation);
            marketObservations.Add(marketObservation); strategyObservations.Add(strategyObservation);
            if (priorGateCandidates is not null)
                foreach (var fact in strategyObservation.RuleFacts)
                    if (fact.Fact is IReplayRuleGateFact gateFact) priorGateCandidates.Add(gateFact);
            performance?.FrameProcessed();
        });
        if (replayResult.GlobalFramesProcessed != marketObservations.Count || replayResult.GlobalFramesProcessed != strategyObservations.Count)
            throw new InvalidOperationException("Replay output is inconsistent with recorded observations.");
        return new(strategyDefinition.StrategyId, strategyDefinition.Version, replayResult.ProviderId, replayResult.Symbol,
            replayResult.ConfiguredTimeframes, marketObservations, strategyObservations);
    }
}
