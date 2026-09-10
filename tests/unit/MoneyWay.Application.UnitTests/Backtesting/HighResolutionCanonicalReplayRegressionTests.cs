using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class HighResolutionCanonicalReplayRegressionTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActualEvaluatorsWorkflowAndLifecycleAreUnchangedWhenEventsShareExistingBoundaries()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var candles = new[] { Series(1, 2) };
        var baseline = UseCase().Execute(definition, candles);
        var events = new HistoricalMarketPriceObservationSeries(
            Provider,
            Symbol,
            [Observation(1, 100, 10), Observation(2, 101, 20)]);

        var enriched = UseCase().Execute(definition, candles, events);

        Assert.Equal(baseline.ObservationCount, enriched.ObservationCount);
        Assert.Equal(baseline.StrategyObservations.Select(ObservationSignature),
            enriched.StrategyObservations.Select(ObservationSignature));
        Assert.Equal(baseline.StrategyObservations.Select(WorkflowSignature),
            enriched.StrategyObservations.Select(WorkflowSignature));
        Assert.Equal(baseline.StrategyObservations.Select(LifecycleSignature),
            enriched.StrategyObservations.Select(LifecycleSignature));
    }

    private static GenerateMultiTimeframeStrategyBacktestRunUseCase UseCase()
    {
        var catalog = new StrategyDefinitionCatalog();
        var definitions = catalog.GetAll();
        var progression = new AdvanceStrategyReplayProgressionUseCase();
        return new(
            new(),
            new(),
            new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()),
            progression,
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()),
            new(progression));
    }

    private static object ObservationSignature(StrategyReplayContextObservation observation) =>
        (observation.Step, observation.AsOfUtc,
            string.Join('|', observation.Evaluations.Select(item =>
                $"{item.RuleId}:{item.Result}:{item.Sequence}:{item.Reason}")));

    private static object WorkflowSignature(StrategyReplayContextObservation observation) =>
        observation.WorkflowProgression is null
            ? string.Empty
            : string.Join('|', observation.WorkflowProgression.RuleEligibility.Select(item =>
                $"{item.RuleId}:{item.IsEligible}:{item.EstablishesProgression}:{string.Join(',', item.MissingPrerequisiteRuleIds)}"));

    private static object LifecycleSignature(StrategyReplayContextObservation observation) =>
        observation.LifecycleProgression is null
            ? string.Empty
            : $"{observation.LifecycleProgression.Instances.Count}:{observation.LifecycleProgression.TransitionHistory.Count}:{observation.LifecycleProgression.ActiveInstance?.InstanceId}";

    private static HistoricalMarketPriceObservation Observation(int minute, decimal price, long sequence) =>
        new(Provider, Symbol, Start.AddMinutes(minute), price, "source-price", "100ms", sequence);

    private static CandleSeries Series(params int[] closes) => new(
        Provider,
        Symbol,
        Minute,
        closes.Select(close => new Candle(
            Provider, Symbol, Minute, Start.AddMinutes(close - 1), Start.AddMinutes(close), 100, 101, 99, 100, null)));
}
