using MoneyWay.Application.Backtesting;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class MultiTimeframeStrategyBacktestRunTests
{
    private static readonly StrategyId Strategy = new("synthetic"); private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture"); private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute); private static readonly Timeframe Five = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyRunIsValidAndDefensivelyCopiesAllCollections()
    {
        var configured = new List<Timeframe> { Minute, Five }; var market = new List<MultiTimeframeBacktestObservation>(); var strategy = new List<StrategyReplayContextObservation>();
        var run = new MultiTimeframeStrategyBacktestRun(Strategy, Version, Provider, Symbol, configured, market, strategy);
        configured.Clear(); market.Add(Market(1, 1)); strategy.Add(Observation(1, 1));
        Assert.Equal(Strategy, run.StrategyId); Assert.Equal(Version, run.StrategyVersion); Assert.Equal(Provider, run.ProviderId); Assert.Equal(Symbol, run.Symbol);
        Assert.Equal([Minute, Five], run.ConfiguredTimeframes); Assert.Empty(run.MarketObservations); Assert.Empty(run.StrategyObservations); Assert.Equal(0, run.ObservationCount); Assert.Null(run.FirstAsOfUtc); Assert.Null(run.LastAsOfUtc);
    }

    [Fact]
    public void PopulatedRunPreservesAlignedChronology()
    {
        var run = Create([Market(1, 1), Market(2, 5, [Minute, Five], [Minute, Five])], [Observation(1, 1), Observation(2, 5)]);
        Assert.Equal(2, run.ObservationCount); Assert.Equal(Start.AddMinutes(1), run.FirstAsOfUtc); Assert.Equal(Start.AddMinutes(5), run.LastAsOfUtc);
    }

    [Fact]
    public void NullCollectionsItemsCountsAndAlignmentAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestRun(Strategy, Version, Provider, Symbol, [Minute], null!, []));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBacktestRun(Strategy, Version, Provider, Symbol, [Minute], [], null!));
        Assert.Throws<ArgumentException>(() => Create(new MultiTimeframeBacktestObservation[] { null! }, [Observation(1, 1)]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], new StrategyReplayContextObservation[] { null! }));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], []));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(2, 1)]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(1, 2)]));
    }

    [Fact]
    public void StrategyIdentityMismatchesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(1, 1, strategyId: new("other"))]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(1, 1, version: new("v2"))]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(1, 1, provider: new("other"))]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1)], [Observation(1, 1, symbol: new("OTHER"))]));
    }

    [Fact]
    public void NonConsecutiveOrNonIncreasingGlobalEventsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1), Market(3, 2)], [Observation(1, 1), Observation(3, 2)]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1), Market(2, 1)], [Observation(1, 1), Observation(2, 1)]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 2), Market(2, 1)], [Observation(1, 2), Observation(2, 1)]));
    }

    [Fact]
    public void UnknownOrIncorrectlyOrderedTimeframesAreRejected()
    {
        var hour = new Timeframe(1, TimeframeUnit.Hour);
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1, [hour], [hour])], [Observation(1, 1)]));
        Assert.Throws<ArgumentException>(() => Create([Market(1, 1, [], [hour])], [Observation(1, 1)]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeBacktestObservation(1, Start.AddMinutes(1), [Five, Minute], [Five, Minute]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyBacktestRun(Strategy, Version, Provider, Symbol, [Five, Minute], [], []));
    }

    private static MultiTimeframeStrategyBacktestRun Create(IEnumerable<MultiTimeframeBacktestObservation> market, IEnumerable<StrategyReplayContextObservation> strategy) => new(Strategy, Version, Provider, Symbol, [Minute, Five], market, strategy);
    private static MultiTimeframeBacktestObservation Market(int step, int minute, Timeframe[]? updated = null, Timeframe[]? available = null) => new(step, Start.AddMinutes(minute), updated ?? [Minute], available ?? [Minute]);
    private static StrategyReplayContextObservation Observation(int step, int minute, StrategyId? strategyId = null, StrategyVersion? version = null, MarketDataProviderId? provider = null, MarketSymbol? symbol = null) =>
        new(strategyId ?? Strategy, version ?? Version, provider ?? Provider, symbol ?? Symbol, step, Start.AddMinutes(minute), []);
}
