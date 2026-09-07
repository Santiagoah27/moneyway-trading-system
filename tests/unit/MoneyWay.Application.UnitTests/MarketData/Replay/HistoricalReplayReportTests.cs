using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.MarketData.Replay;

public sealed class HistoricalReplayReportTests
{
    private static readonly MarketDataProviderId ProviderId = new("historical-fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly Timeframe Timeframe = new(5, TimeframeUnit.Minute);
    private static readonly DateTimeOffset DatasetStart = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstFrame = new(2026, 1, 1, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastFrame = new(2026, 1, 1, 10, 15, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyReportIsValidAndPreservesMetadata()
    {
        var report = Create(0, null, null, 0, null, null);

        Assert.Same(ProviderId, report.ProviderId);
        Assert.Same(Symbol, report.Symbol);
        Assert.Same(Timeframe, report.Timeframe);
        Assert.Equal(0, report.CandleCount);
        Assert.Equal(0, report.FramesProcessed);
        Assert.Null(report.DatasetStartTimeUtc);
        Assert.Null(report.DatasetEndTimeUtc);
        Assert.Null(report.FirstFrameAsOfUtc);
        Assert.Null(report.LastFrameAsOfUtc);
    }

    [Fact]
    public void NonEmptyReportIsValidAndPreservesAllValues()
    {
        var report = Create(3, DatasetStart, LastFrame, 3, FirstFrame, LastFrame);

        Assert.Same(ProviderId, report.ProviderId);
        Assert.Same(Symbol, report.Symbol);
        Assert.Same(Timeframe, report.Timeframe);
        Assert.Equal(3, report.CandleCount);
        Assert.Equal(DatasetStart, report.DatasetStartTimeUtc);
        Assert.Equal(LastFrame, report.DatasetEndTimeUtc);
        Assert.Equal(3, report.FramesProcessed);
        Assert.Equal(FirstFrame, report.FirstFrameAsOfUtc);
        Assert.Equal(LastFrame, report.LastFrameAsOfUtc);
    }

    [Fact]
    public void NullMetadataIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HistoricalReplayReport(null!, Symbol, Timeframe, 0, null, null, 0, null, null));
        Assert.Throws<ArgumentNullException>(() =>
            new HistoricalReplayReport(ProviderId, null!, Timeframe, 0, null, null, 0, null, null));
        Assert.Throws<ArgumentNullException>(() =>
            new HistoricalReplayReport(ProviderId, Symbol, null!, 0, null, null, 0, null, null));
    }

    [Fact]
    public void NegativeCandleCountIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(-1, null, null, -1, null, null));

    [Fact]
    public void NegativeFrameCountIsRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(0, null, null, -1, null, null));

    [Fact]
    public void CountMismatchIsRejected() =>
        Assert.Throws<ArgumentException>(() => Create(2, DatasetStart, LastFrame, 1, FirstFrame, LastFrame));

    [Theory]
    [InlineData("dataset-start")]
    [InlineData("dataset-end")]
    [InlineData("first-frame")]
    [InlineData("last-frame")]
    public void EmptyReportRejectsEveryTimestamp(string populatedTimestamp)
    {
        Assert.Throws<ArgumentException>(() => Create(
            0,
            populatedTimestamp == "dataset-start" ? DatasetStart : null,
            populatedTimestamp == "dataset-end" ? LastFrame : null,
            0,
            populatedTimestamp == "first-frame" ? FirstFrame : null,
            populatedTimestamp == "last-frame" ? LastFrame : null));
    }

    [Theory]
    [InlineData("dataset-start")]
    [InlineData("dataset-end")]
    [InlineData("first-frame")]
    [InlineData("last-frame")]
    public void NonEmptyReportRequiresEveryTimestamp(string missingTimestamp)
    {
        Assert.Throws<ArgumentException>(() => Create(
            1,
            missingTimestamp == "dataset-start" ? null : DatasetStart,
            missingTimestamp == "dataset-end" ? null : LastFrame,
            1,
            missingTimestamp == "first-frame" ? null : FirstFrame,
            missingTimestamp == "last-frame" ? null : LastFrame));
    }

    [Theory]
    [InlineData("dataset-start")]
    [InlineData("dataset-end")]
    [InlineData("first-frame")]
    [InlineData("last-frame")]
    public void NonUtcTimestampIsRejected(string timestamp)
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<ArgumentException>(() => Create(
            1,
            timestamp == "dataset-start" ? nonUtc : DatasetStart,
            timestamp == "dataset-end" ? nonUtc : LastFrame,
            1,
            timestamp == "first-frame" ? nonUtc : FirstFrame,
            timestamp == "last-frame" ? nonUtc : LastFrame));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DatasetEndMustBeAfterStart(bool equal)
    {
        var end = equal ? DatasetStart : DatasetStart.AddMinutes(-1);
        Assert.Throws<ArgumentException>(() => Create(1, DatasetStart, end, 1, FirstFrame, LastFrame));
    }

    [Fact]
    public void LastFrameBeforeFirstIsRejected() =>
        Assert.Throws<ArgumentException>(() => Create(2, DatasetStart, LastFrame, 2, LastFrame, FirstFrame));

    private static HistoricalReplayReport Create(
        int candleCount,
        DateTimeOffset? datasetStart,
        DateTimeOffset? datasetEnd,
        int framesProcessed,
        DateTimeOffset? firstFrame,
        DateTimeOffset? lastFrame) =>
        new(
            ProviderId,
            Symbol,
            Timeframe,
            candleCount,
            datasetStart,
            datasetEnd,
            framesProcessed,
            firstFrame,
            lastFrame);
}
