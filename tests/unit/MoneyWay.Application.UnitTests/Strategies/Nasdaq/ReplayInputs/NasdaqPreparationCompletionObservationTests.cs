using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPreparationCompletionObservationTests
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateOnly Day = new(2026, 9, 17);
    private static readonly DateTimeOffset Start = new(2026, 9, 17, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PositiveObservationPreservesIdentityCausalTimeAndSourceWithoutRuleVerdict()
    {
        var at = Start.AddMinutes(20);
        var observation = Observation(Day, at, "review:session-D");

        Assert.Equal(Day, observation.TradingDay);
        Assert.Equal(at, observation.ObservedAtUtc);
        Assert.Equal("review:session-D", observation.SourceReference);
        Assert.Equal(Definition.StrategyId, observation.StrategyId);
        Assert.Equal(Definition.Version, observation.StrategyVersion);
        Assert.Equal(Provider, observation.ProviderId);
        Assert.Equal(Symbol, observation.Symbol);
        Assert.Null(typeof(NasdaqPreparationCompletionObservation).GetProperty("Completed"));
        Assert.Null(typeof(NasdaqPreparationCompletionObservation).GetProperty("Result"));
        Assert.Null(typeof(NasdaqPreparationCompletionObservation).GetProperty("UpstreamAnalysisCorrect"));
        Assert.All(typeof(NasdaqPreparationCompletionObservation).GetProperties(), property => Assert.False(property.CanWrite));
        Assert.Throws<ArgumentException>(() => Observation(Day, at.ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => Observation(Day, at, " "));
    }

    [Fact]
    public void SeriesAcceptsOnePerDayAndRejectsEveryDuplicateBySessionRatherThanValue()
    {
        var first = Observation(Day, Start.AddMinutes(20));
        var next = Observation(Day.AddDays(1), Start.AddDays(1).AddMinutes(20));
        var accepted = Series(first, next);
        Assert.Equal([Day, Day.AddDays(1)], accepted.Observations.Select(item => item.TradingDay));

        Assert.Throws<ArgumentException>(() => Series(first, first));
        Assert.Throws<ArgumentException>(() => Series(first, Observation(Day, first.ObservedAtUtc)));
        Assert.Throws<ArgumentException>(() => Series(first, Observation(Day, first.ObservedAtUtc.AddMinutes(1))));
        Assert.Throws<ArgumentException>(() => Series(first, Observation(Day, first.ObservedAtUtc, "other-source")));
        Assert.Throws<ArgumentException>(() => new NasdaqPreparationCompletionObservationSeries(
            Definition.StrategyId, Definition.Version, Provider, Symbol,
            [new(Definition.StrategyId, Definition.Version, Provider, new("OTHER"), Day, first.ObservedAtUtc, "source")]));
        Assert.Throws<ArgumentException>(() => new NasdaqPreparationCompletionObservation(
            new("moneyway-forex"), Definition.Version, Provider, Symbol, Day, first.ObservedAtUtc, "source"));
    }

    [Fact]
    public void SeriesAndContextsDefensivelyCopyAndExposeReadOnlyEvidence()
    {
        var input = new List<NasdaqPreparationCompletionObservation> { Observation(Day, Start.AddMinutes(20)) };
        var series = new NasdaqPreparationCompletionObservationSeries(Definition.StrategyId, Definition.Version, Provider, Symbol, input);
        input.Clear();
        Assert.Single(series.Observations);

        var context = ContextAt(Start.AddMinutes(25), series);
        Assert.Single(context.InputObservations);
        Assert.Throws<NotSupportedException>(() => ((ICollection<IStrategyReplayInputObservation>)context.InputObservations)
            .Add(Observation(Day.AddDays(1), Start.AddMinutes(25))));
        Assert.Single(context.InputObservations);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqPreparationCompletionObservation>)series.Observations)
            .Clear());
    }

    [Fact]
    public void ContextRejectsPreparationInputForAnotherReplayIdentity()
    {
        var observation = Observation(Day, Start.AddMinutes(20));
        var cursor = new MultiTimeframeCandleReplayCursor([CandleSeries(25)]);
        Assert.True(cursor.TryAdvance(out var frame));
        var otherProvider = new NasdaqPreparationCompletionObservationSeries(
            Definition.StrategyId, Definition.Version, new("other"), Symbol,
            [new(Definition.StrategyId, Definition.Version, new("other"), Symbol, Day, observation.ObservedAtUtc, "source")]);
        var otherVersion = new NasdaqPreparationCompletionObservationSeries(
            Definition.StrategyId, new("other"), Provider, Symbol, []);
        var factory = new CreateStrategyReplayContextUseCase();

        Assert.Throws<ArgumentException>(() => factory.Execute(Definition, frame!, otherProvider));
        Assert.Throws<ArgumentException>(() => factory.Execute(Definition, frame!, otherVersion));
    }

    [Fact]
    public void ContextBoundsFutureEvidenceWithoutRewritingEarlierSnapshot()
    {
        var completion = Observation(Day, Start.AddMinutes(20));
        var series = Series(completion);
        var before = ContextAt(Start.AddMinutes(10), series);
        var at = ContextAt(Start.AddMinutes(20), series);
        var after = ContextAt(Start.AddMinutes(25), series);

        Assert.Empty(before.InputObservations);
        Assert.Same(completion, Assert.Single(at.InputObservations));
        Assert.Same(completion, Assert.Single(after.InputObservations));
        Assert.Empty(before.InputObservations);

        var noFutureDataset = ContextAt(Start.AddMinutes(10), Series());
        Assert.Equal(noFutureDataset.InputObservations.Count, before.InputObservations.Count);
        Assert.Equal(noFutureDataset.AsOfUtc, before.AsOfUtc);
    }

    [Fact]
    public void SessionLookupCannotUsePreviousOrNextDayEvidence()
    {
        var series = Series(
            Observation(Day.AddDays(-1), Start.AddDays(-1).AddMinutes(20)),
            Observation(Day.AddDays(1), Start.AddDays(1).AddMinutes(20)));
        var context = ContextAt(Start.AddDays(1).AddMinutes(25), series);

        Assert.Equal(2, context.InputObservations.Count);
        Assert.Null(ForDay(context, Day));
        Assert.NotNull(ForDay(context, Day.AddDays(-1)));
        Assert.NotNull(ForDay(context, Day.AddDays(1)));
    }

    [Fact]
    public void LateCompletionRemainsObservableWithoutInputLayerTimingVerdict()
    {
        var late = Observation(Day, Start.AddMinutes(30));
        var series = Series(late);

        Assert.Empty(ContextAt(Start.AddMinutes(29), series).InputObservations);
        Assert.Same(late, ForDay(ContextAt(Start.AddMinutes(30), series), Day));
        Assert.Same(late, ForDay(ContextAt(Start.AddMinutes(35), series), Day));
    }

    [Fact]
    public void CanonicalRunnerDeliversBoundedInputWithoutChangingCurrentEvaluations()
    {
        var completion = Observation(Day, Start.AddMinutes(20));
        var series = CandleSeries(10, 20, 25);
        var captured = new List<StrategyReplayContext>();
        var evaluator = new CaptureEvaluator(captured);
        var useCase = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new([evaluator]));

        var result = useCase.Execute(Definition, [series], Series(completion));
        Assert.Equal([Start.AddMinutes(10), Start.AddMinutes(20), Start.AddMinutes(25)],
            captured.Select(item => item.AsOfUtc));
        Assert.Empty(captured[0].InputObservations);
        Assert.Same(completion, Assert.Single(captured[1].InputObservations));
        Assert.Same(completion, Assert.Single(captured[2].InputObservations));
        Assert.Empty(captured[0].InputObservations);
        Assert.Equal(3, result.ObservationCount);

        var baseline = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()))
            .Execute(Definition, [series]);
        var empty = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()))
            .Execute(Definition, [series], Series());
        var populated = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()))
            .Execute(Definition, [series], Series(completion));
        static IEnumerable<object> Evaluations(MultiTimeframeStrategyBacktestRun run) =>
            run.StrategyObservations.SelectMany(item => item.Evaluations.Select(evaluation =>
                (object)(item.AsOfUtc, evaluation.RuleId, evaluation.Result, evaluation.Reason)));
        Assert.Equal(Evaluations(baseline), Evaluations(empty));
        Assert.Equal(Evaluations(baseline), Evaluations(populated));
    }

    [Fact]
    public void CanonicalPriceEnhancedContextAlsoCarriesBoundedPreparationInput()
    {
        var completion = Observation(Day, Start.AddMinutes(20));
        var priceInput = new HistoricalMarketPriceObservationSeries(Provider, Symbol, []);
        var cursor = new CanonicalMultiTimeframeReplayCursor([CandleSeries(10, 20)], priceInput);
        var factory = new CreateStrategyReplayContextUseCase();
        Assert.True(cursor.TryAdvance(out var early));
        Assert.True(cursor.TryAdvance(out var current));

        Assert.Empty(factory.ExecuteCanonical(Definition, early!, Series(completion)).InputObservations);
        Assert.Same(completion, Assert.Single(factory.ExecuteCanonical(Definition, current!, Series(completion)).InputObservations));
    }

    [Fact]
    public void CanonicalBacktestEntryPointTransportsPreparationWithOrWithoutPriceInput()
    {
        var completion = Observation(Day, Start.AddMinutes(20));
        var captured = new List<StrategyReplayContext>();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new([new CaptureEvaluator(captured)]));
        var facade = new GenerateCanonicalMultiTimeframeBacktestUseCase(
            new GenerateMultiTimeframeStrategyOutcomeBacktestRunUseCase(run, new()),
            new GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase());

        var candleOnly = facade.Execute(Definition, [CandleSeries(10, 20)], Series(completion));
        Assert.Equal(2, candleOnly.FrameCount);
        Assert.Empty(captured[0].InputObservations);
        Assert.Same(completion, Assert.Single(captured[1].InputObservations));

        captured.Clear();
        var withPrice = facade.Execute(Definition, [CandleSeries(10, 20)],
            new HistoricalMarketPriceObservationSeries(Provider, Symbol, []), Series(completion));
        Assert.Equal(2, withPrice.FrameCount);
        Assert.Empty(captured[0].InputObservations);
        Assert.Same(completion, Assert.Single(captured[1].InputObservations));
    }

    private static NasdaqPreparationCompletionObservation? ForDay(StrategyReplayContext context, DateOnly day) =>
        context.InputObservations.OfType<NasdaqPreparationCompletionObservation>()
            .SingleOrDefault(item => item.TradingDay == day);

    private static StrategyReplayContext ContextAt(DateTimeOffset at, NasdaqPreparationCompletionObservationSeries observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([CandleSeries((int)(at - Start).TotalMinutes)]);
        Assert.True(cursor.TryAdvance(out var frame));
        return new CreateStrategyReplayContextUseCase().Execute(Definition, frame!, observations);
    }

    private static NasdaqPreparationCompletionObservation Observation(DateOnly day, DateTimeOffset at, string source = "review:prepared") =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, day, at, source);

    private static NasdaqPreparationCompletionObservationSeries Series(params NasdaqPreparationCompletionObservation[] observations) =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, observations);

    private static CandleSeries CandleSeries(params int[] closes) => new(
        Provider, Symbol, Minute,
        closes.Select(close => new Candle(Provider, Symbol, Minute, Start.AddMinutes(close - 1), Start.AddMinutes(close),
            100, 101, 99, 100, null)));

    private sealed class CaptureEvaluator(List<StrategyReplayContext> captured) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => Definition.StrategyId;
        public StrategyVersion StrategyVersion => Definition.Version;
        public RuleId RuleId { get; } = new("NQ-TIME-001");
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context)
        {
            captured.Add(context);
            return new(RuleEvaluationResult.Waiting, "Input transport probe.", null);
        }
    }
}
