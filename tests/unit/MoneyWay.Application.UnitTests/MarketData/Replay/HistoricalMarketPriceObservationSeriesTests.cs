using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class HistoricalMarketPriceObservationSeriesTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly DateTimeOffset At = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObservationPreservesNeutralAuditableSourceData()
    {
        var observation = Observation(At, 100.25m, "source-price", "100ms", 42);

        Assert.Equal((Provider, Symbol, At, 100.25m, "source-price", "100ms", 42L),
            (observation.ProviderId, observation.Symbol, observation.ObservedAtUtc, observation.ObservedPrice,
                observation.ObservationKind, observation.SourceResolution, observation.SourceSequence));
    }

    [Fact]
    public void ObservationRejectsInvalidTimestampAndDescriptors()
    {
        Assert.Throws<ArgumentException>(() => Observation(At.ToOffset(TimeSpan.FromHours(-5)), 100, "source-price", "100ms"));
        Assert.Throws<ArgumentException>(() => Observation(At, 100, " ", "100ms"));
        Assert.Throws<ArgumentException>(() => Observation(At, 100, "source-price", " 100ms"));
    }

    [Fact]
    public void SeriesValidatesIdentityChronologyAndDefensivelyCopies()
    {
        var values = new List<HistoricalMarketPriceObservation>
        {
            Observation(At, 100),
            Observation(At.AddMilliseconds(1), 101),
        };
        var series = new HistoricalMarketPriceObservationSeries(Provider, Symbol, values);
        values.Clear();

        Assert.Equal(2, series.Count);
        Assert.Equal(2, series.Groups.Count);
        Assert.Throws<ArgumentException>(() => new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At.AddMilliseconds(1), 100), Observation(At, 101)]));
        Assert.Throws<ArgumentException>(() => new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [new(new("other"), Symbol, At, 100, "source-price", "100ms")]));
        Assert.Throws<ArgumentException>(() => new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [new(Provider, new("OTHER"), At, 100, "source-price", "100ms")]));
    }

    [Fact]
    public void EqualTimestampUsesOnlyCompleteUniqueAscendingSourceSequenceAsAuthoritativeOrder()
    {
        var singleton = new HistoricalMarketPriceObservationSeries(Provider, Symbol, [Observation(At, 100)]);
        var ordered = new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100, sequence: 10), Observation(At, 101, sequence: 11)]);
        var unordered = new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100), Observation(At, 101)]);
        var partiallySequenced = new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100, sequence: 10), Observation(At, 101)]);
        var unsequencedDuplicates = new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100), Observation(At, 100)]);

        Assert.True(Assert.Single(singleton.Groups).HasAuthoritativeOrder);
        Assert.True(Assert.Single(ordered.Groups).HasAuthoritativeOrder);
        Assert.Equal([10L, 11L], ordered.Groups[0].Observations.Select(item => item.SourceSequence!.Value));
        Assert.False(Assert.Single(unordered.Groups).HasAuthoritativeOrder);
        Assert.False(Assert.Single(partiallySequenced.Groups).HasAuthoritativeOrder);
        Assert.Equal(2, Assert.Single(unsequencedDuplicates.Groups).Observations.Count);
        Assert.Throws<ArgumentException>(() => new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100, sequence: 11), Observation(At, 101, sequence: 10)]));
        Assert.Throws<ArgumentException>(() => new HistoricalMarketPriceObservationSeries(
            Provider, Symbol, [Observation(At, 100, sequence: 10), Observation(At, 101, sequence: 10)]));
    }

    private static HistoricalMarketPriceObservation Observation(
        DateTimeOffset at,
        decimal price,
        string kind = "source-price",
        string resolution = "100ms",
        long? sequence = null) =>
        new(Provider, Symbol, at, price, kind, resolution, sequence);
}
