using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class CanonicalMultiTimeframeReplayCursorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OptionalEmptyInputMatchesCandleBoundariesWithoutChangingCandles()
    {
        var candles = Series(1, 2);
        var before = candles.Candles.ToArray();
        var candleOnly = Capture(new([candles]));
        var withEmptyInput = Capture(new([candles], Observations()));

        Assert.Equal(candleOnly.Select(Signature), withEmptyInput.Select(Signature));
        Assert.Equal(before, candles.Candles);
        Assert.All(withEmptyInput, frame => Assert.True(frame.MarketDataAvailability.HighResolutionInputConfigured));
        Assert.All(withEmptyInput, frame => Assert.Equal(0, frame.MarketPriceObservations.ObservationCount));
    }

    [Fact]
    public void EventsCreateCanonicalBoundariesAndVisibilityIsInclusiveAndFutureSafe()
    {
        var events = Observations(
            Observation(Start.AddSeconds(10), 100),
            Observation(Start.AddMinutes(1), 101),
            Observation(Start.AddMinutes(1).AddSeconds(1), 102));
        var frames = Capture(new([Series(1, 2)], events));
        var atOneMinute = frames.Single(frame => frame.AsOfUtc == Start.AddMinutes(1));

        Assert.Equal([Start.AddSeconds(10), Start.AddMinutes(1), Start.AddMinutes(1).AddSeconds(1), Start.AddMinutes(2)],
            frames.Select(frame => frame.AsOfUtc));
        Assert.Equal(2, atOneMinute.MarketPriceObservations.ObservationCount);
        Assert.Equal([100m, 101m], atOneMinute.MarketPriceObservations.Groups.SelectMany(group => group.Observations).Select(item => item.ObservedPrice));
        Assert.DoesNotContain(atOneMinute.MarketPriceObservations.Groups.SelectMany(group => group.Observations), item => item.ObservedPrice == 102m);
        Assert.Equal([Minute], atOneMinute.UpdatedTimeframes);
        Assert.Equal(101m, Assert.Single(atOneMinute.CurrentMarketPriceObservations!.Observations).ObservedPrice);
    }

    [Fact]
    public void AdvancingDoesNotMutatePriorSnapshotOrDuplicateVisibleEvents()
    {
        var cursor = new CanonicalMultiTimeframeReplayCursor(
            [Series(1)],
            Observations(Observation(Start.AddSeconds(10), 100), Observation(Start.AddSeconds(20), 101)));
        Assert.True(cursor.TryAdvance(out var first));
        var firstSnapshot = first!.MarketPriceObservations;
        Assert.True(cursor.TryAdvance(out var second));

        Assert.Equal(1, firstSnapshot.ObservationCount);
        Assert.Single(firstSnapshot.Groups);
        Assert.Equal(2, second!.MarketPriceObservations.ObservationCount);
        Assert.Equal(2, second.MarketPriceObservations.Groups.Count);
    }

    [Fact]
    public void ReplayPrefixIsEquivalentWhenAnotherInputContainsFutureEvents()
    {
        var throughBoundary = Observations(Observation(Start.AddSeconds(10), 100), Observation(Start.AddMinutes(1), 101));
        var withFuture = Observations(
            Observation(Start.AddSeconds(10), 100),
            Observation(Start.AddMinutes(1), 101),
            Observation(Start.AddMinutes(1).AddSeconds(1), 999));

        var shortFrame = Capture(new([Series(1)], throughBoundary)).Single(frame => frame.AsOfUtc == Start.AddMinutes(1));
        var longFrame = Capture(new([Series(1)], withFuture)).Single(frame => frame.AsOfUtc == Start.AddMinutes(1));

        Assert.Equal(SnapshotSignature(shortFrame), SnapshotSignature(longFrame));
    }

    [Fact]
    public void ProviderAndSymbolMismatchAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalMultiTimeframeReplayCursor(
            [Series(1)], new(new("other"), Symbol, [])));
        Assert.Throws<ArgumentException>(() => new CanonicalMultiTimeframeReplayCursor(
            [Series(1)], new(Provider, new("OTHER"), [])));
    }

    private static List<CanonicalMultiTimeframeReplayFrame> Capture(CanonicalMultiTimeframeReplayCursor cursor)
    {
        var frames = new List<CanonicalMultiTimeframeReplayFrame>();
        while (cursor.TryAdvance(out var frame)) frames.Add(frame!);
        return frames;
    }

    private static object Signature(CanonicalMultiTimeframeReplayFrame frame) =>
        (frame.Step, frame.AsOfUtc, string.Join(',', frame.UpdatedTimeframes), frame.FramesByTimeframe.Count);

    private static object SnapshotSignature(CanonicalMultiTimeframeReplayFrame frame) =>
        (Signature(frame), frame.MarketPriceObservations.ObservationCount,
            string.Join(',', frame.MarketPriceObservations.Groups.SelectMany(group => group.Observations).Select(item => item.ObservedPrice)));

    private static HistoricalMarketPriceObservation Observation(DateTimeOffset at, decimal price) =>
        new(Provider, Symbol, at, price, "source-price", "100ms");

    private static HistoricalMarketPriceObservationSeries Observations(params HistoricalMarketPriceObservation[] observations) =>
        new(Provider, Symbol, observations);

    private static CandleSeries Series(params int[] closes) => new(
        Provider,
        Symbol,
        Minute,
        closes.Select(close => new Candle(
            Provider, Symbol, Minute, Start.AddMinutes(close - 1), Start.AddMinutes(close), 100, 101, 99, 100, null)));
}
