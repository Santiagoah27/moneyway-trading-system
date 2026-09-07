using MoneyWay.Domain.MarketData;

namespace MoneyWay.Domain.UnitTests.MarketData;

public sealed class CandleSeriesTests
{
    private static readonly MarketDataProviderId ProviderId = new("historical-fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptySeriesIsValidAndPreservesMetadata()
    {
        var series = new CandleSeries(ProviderId, Symbol, Timeframe, []);

        Assert.Same(ProviderId, series.ProviderId);
        Assert.Same(Symbol, series.Symbol);
        Assert.Same(Timeframe, series.Timeframe);
        Assert.Empty(series.Candles);
        Assert.Equal(0, series.Count);
        Assert.Null(series.StartTimeUtc);
        Assert.Null(series.EndTimeUtc);
    }

    [Fact]
    public void ChronologicalSeriesPreservesOrderAndBoundaries()
    {
        var first = CreateCandle(StartUtc);
        var second = CreateCandle(StartUtc.AddMinutes(5));
        var third = CreateCandle(StartUtc.AddMinutes(10));

        var series = new CandleSeries(ProviderId, Symbol, Timeframe, [first, second, third]);

        Assert.Equal([first, second, third], series.Candles);
        Assert.Equal(3, series.Count);
        Assert.Equal(first.OpenTimeUtc, series.StartTimeUtc);
        Assert.Equal(third.CloseTimeUtc, series.EndTimeUtc);
    }

    [Fact]
    public void SourceIsDefensivelyCopiedAndExposedCollectionIsReadOnly()
    {
        var first = CreateCandle(StartUtc);
        var source = new List<Candle> { first };
        var series = new CandleSeries(ProviderId, Symbol, Timeframe, source);
        source.Clear();

        Assert.Equal([first], series.Candles);
        Assert.False(series.Candles is List<Candle>);
        var collection = Assert.IsAssignableFrom<ICollection<Candle>>(series.Candles);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Fact]
    public void NullConstructorValuesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CandleSeries(null!, Symbol, Timeframe, []));
        Assert.Throws<ArgumentNullException>(() => new CandleSeries(ProviderId, null!, Timeframe, []));
        Assert.Throws<ArgumentNullException>(() => new CandleSeries(ProviderId, Symbol, null!, []));
        Assert.Throws<ArgumentNullException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, null!));
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [null!]));
    }

    [Fact]
    public void DifferentProviderIsRejected()
    {
        var candle = CreateCandle(StartUtc, providerId: new MarketDataProviderId("other-provider"));
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [candle]));
    }

    [Fact]
    public void DifferentSymbolIsRejected()
    {
        var candle = CreateCandle(StartUtc, symbol: new MarketSymbol("NAS100"));
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [candle]));
    }

    [Fact]
    public void DifferentTimeframeIsRejected()
    {
        var candle = CreateCandle(StartUtc, timeframe: new Timeframe(1, TimeframeUnit.Minute));
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [candle]));
    }

    [Fact]
    public void UnorderedInputIsRejected()
    {
        var later = CreateCandle(StartUtc.AddMinutes(5));
        var earlier = CreateCandle(StartUtc);
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [later, earlier]));
    }

    [Fact]
    public void DuplicateOpenTimeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new CandleSeries(
            ProviderId, Symbol, Timeframe, [CreateCandle(StartUtc), CreateCandle(StartUtc)]));
    }

    [Fact]
    public void AdjacentCandlesAreAccepted()
    {
        var series = new CandleSeries(
            ProviderId, Symbol, Timeframe, [CreateCandle(StartUtc), CreateCandle(StartUtc.AddMinutes(5))]);
        Assert.Equal(2, series.Count);
    }

    [Fact]
    public void GapsAreAcceptedWithoutSyntheticCandles()
    {
        var series = new CandleSeries(
            ProviderId, Symbol, Timeframe, [CreateCandle(StartUtc), CreateCandle(StartUtc.AddMinutes(15))]);
        Assert.Equal(2, series.Count);
    }

    [Fact]
    public void OverlapIsRejected()
    {
        var first = CreateCandle(StartUtc, closeTimeUtc: StartUtc.AddMinutes(6));
        var overlapping = CreateCandle(StartUtc.AddMinutes(5));
        Assert.Throws<ArgumentException>(() => new CandleSeries(ProviderId, Symbol, Timeframe, [first, overlapping]));
    }

    private static Candle CreateCandle(
        DateTimeOffset openTimeUtc,
        DateTimeOffset? closeTimeUtc = null,
        MarketDataProviderId? providerId = null,
        MarketSymbol? symbol = null,
        Timeframe? timeframe = null) =>
        new(providerId ?? ProviderId, symbol ?? Symbol, timeframe ?? Timeframe,
            openTimeUtc, closeTimeUtc ?? openTimeUtc.AddMinutes(5), 100m, 110m, 90m, 105m, 10m);
}
