using MoneyWay.Application.Backtesting;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class StrategyBacktestRunTests
{
    private static readonly StrategyId Strategy = new("test-strategy");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyRunIsValidAndPreservesIdentityAndMetadata()
    {
        var market = new BacktestRun(Provider, Symbol, Timeframe, []);
        var run = new StrategyBacktestRun(Strategy, Version, market, []);
        Assert.Same(Strategy, run.StrategyId); Assert.Same(Version, run.StrategyVersion); Assert.Same(market, run.MarketReplay);
        Assert.Equal(0, run.ObservationCount); Assert.Null(run.FirstAsOfUtc); Assert.Null(run.LastAsOfUtc);
        Assert.Same(Provider, run.ProviderId); Assert.Same(Symbol, run.Symbol); Assert.Same(Timeframe, run.Timeframe);
    }

    [Fact]
    public void ValidRunAlignsObservationsAndDefensivelyCopies()
    {
        var candles = Candles(2); var market = MarketRun(candles); var source = StrategyObservations(candles);
        var run = new StrategyBacktestRun(Strategy, Version, market, source); source.Clear();
        Assert.Equal(2, run.ObservationCount); Assert.Equal(market.FirstAsOfUtc, run.FirstAsOfUtc); Assert.Equal(market.LastAsOfUtc, run.LastAsOfUtc);
        Assert.All(run.StrategyObservations.Select((item, index) => (item, index)), pair => Assert.Same(market.Observations[pair.index].CurrentCandle, pair.item.CurrentCandle));
    }

    [Fact]
    public void ConstructorRejectsNullsAndCountMismatch()
    {
        var market = new BacktestRun(Provider, Symbol, Timeframe, []);
        Assert.Throws<ArgumentNullException>(() => new StrategyBacktestRun(null!, Version, market, []));
        Assert.Throws<ArgumentNullException>(() => new StrategyBacktestRun(Strategy, null!, market, []));
        Assert.Throws<ArgumentNullException>(() => new StrategyBacktestRun(Strategy, Version, null!, []));
        Assert.Throws<ArgumentNullException>(() => new StrategyBacktestRun(Strategy, Version, market, null!));
        Assert.Throws<ArgumentException>(() => new StrategyBacktestRun(Strategy, Version, market, new StrategyReplayFrameObservation[] { null! }));
    }

    [Theory]
    [InlineData("step")]
    [InlineData("time")]
    [InlineData("candle")]
    [InlineData("strategy")]
    [InlineData("version")]
    public void ConstructorRejectsMisalignment(string mismatch)
    {
        var candles = Candles(2); var market = MarketRun(candles); var observations = StrategyObservations(candles);
        var replacement = mismatch switch
        {
            "step" => Observation(2, candles[0], Strategy, Version),
            "time" => Observation(1, candles[1], Strategy, Version),
            "candle" => Observation(1, Clone(candles[0]), Strategy, Version),
            "strategy" => Observation(1, candles[0], new("other"), Version),
            _ => Observation(1, candles[0], Strategy, new("v2")),
        };
        observations[0] = replacement;
        Assert.Throws<ArgumentException>(() => new StrategyBacktestRun(Strategy, Version, market, observations));
    }

    [Fact]
    public void ZeroEvaluatorsStillProducesOneObservationPerCandle()
    {
        var series = new CandleSeries(Provider, Symbol, Timeframe, Candles(3));
        var run = UseCase([]).Execute(Definition(), series);
        Assert.Equal(3, run.MarketReplay.ObservationCount); Assert.Equal(3, run.StrategyObservations.Count);
        Assert.Equal([1, 2, 3], run.StrategyObservations.Select(x => x.Step)); Assert.All(run.StrategyObservations, x => Assert.Equal(0, x.EvaluationCount));
    }

    [Fact]
    public void EvaluatorReceivesGrowingHistoryAndRunsAreReproducible()
    {
        var evaluator = new FakeEvaluator(); var useCase = UseCase([evaluator]); var series = new CandleSeries(Provider, Symbol, Timeframe, Candles(3));
        var first = useCase.Execute(Definition(), series); var second = useCase.Execute(Definition(), series);
        Assert.Equal(["Observed candles: 1", "Observed candles: 2", "Observed candles: 3"], first.StrategyObservations.Select(x => x.Evaluations[0].Reason));
        Assert.Equal(first.StrategyObservations.Select(x => x.AsOfUtc), second.StrategyObservations.Select(x => x.AsOfUtc));
        Assert.Equal(6, evaluator.Invocations);
    }

    [Fact]
    public void GapIsPreservedWithoutSyntheticObservations()
    {
        var candles = new[] { CandleAt(Start), CandleAt(Start.AddMinutes(15)) };
        var run = UseCase([]).Execute(Definition(), new(Provider, Symbol, Timeframe, candles));
        Assert.Equal(2, run.ObservationCount); Assert.Equal(candles.Select(x => x.CloseTimeUtc), run.StrategyObservations.Select(x => x.AsOfUtc));
    }

    [Fact]
    public void NullInputsAndDependenciesAreRejected()
    {
        var evaluatorUseCase = new EvaluateStrategyReplayFrameUseCase([]);
        Assert.Throws<ArgumentNullException>(() => new GenerateStrategyBacktestRunUseCase(null!, evaluatorUseCase));
        Assert.Throws<ArgumentNullException>(() => new GenerateStrategyBacktestRunUseCase(new(), null!));
        var useCase = new GenerateStrategyBacktestRunUseCase(new(), evaluatorUseCase);
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(null!, new(Provider, Symbol, Timeframe, [])));
        Assert.Throws<ArgumentNullException>(() => useCase.Execute(Definition(), null!));
    }

    private static GenerateStrategyBacktestRunUseCase UseCase(IEnumerable<IReplayRuleEvaluator> evaluators) => new(new(), new(evaluators));
    private static StrategyDefinition Definition() => new(Strategy, Version, "Test strategy", "test", [new(FakeEvaluator.Rule, "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "d", "s")]);
    private static BacktestRun MarketRun(Candle[] candles) => new(Provider, Symbol, Timeframe, candles.Select((c, i) => new BacktestObservation(i + 1, c.CloseTimeUtc, c)));
    private static List<StrategyReplayFrameObservation> StrategyObservations(Candle[] candles) => candles.Select((c, i) => Observation(i + 1, c, Strategy, Version)).ToList();
    private static StrategyReplayFrameObservation Observation(int step, Candle candle, StrategyId strategy, StrategyVersion version) => new(strategy, version, step, candle.CloseTimeUtc, candle, []);
    private static Candle[] Candles(int count) => Enumerable.Range(0, count).Select(i => CandleAt(Start.AddMinutes(i * 5))).ToArray();
    private static Candle CandleAt(DateTimeOffset open) => new(Provider, Symbol, Timeframe, open, open.AddMinutes(5), 100, 101, 99, 100, null);
    private static Candle Clone(Candle c) => new(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume);
    private sealed class FakeEvaluator : IReplayRuleEvaluator
    {
        public static readonly RuleId Rule = new("R-1"); public StrategyId StrategyId => Strategy; public StrategyVersion StrategyVersion => Version; public RuleId RuleId => Rule; public int Invocations { get; private set; }
        public ReplayRuleEvaluationDecision Evaluate(MoneyWay.Domain.MarketData.Replay.ReplayFrame frame) { Invocations++; return new(RuleEvaluationResult.Passed, $"Observed candles: {frame.AvailableCandles.Count}", null); }
    }
}
