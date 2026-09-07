using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.IntegrationTests;

public sealed class HistoricalReplayWorkflowTests
{
    private const string Header =
        "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume";
    private readonly CsvMarketDataImporter importer = new();
    private readonly GenerateHistoricalReplayReportUseCase useCase = new();

    [Fact]
    public void CanonicalCsvFlowsThroughImportReplayAndReport()
    {
        var csv = Csv(
            Row("10:00", "10:05"),
            Row("10:05", "10:10"),
            Row("10:10", "10:15"));

        var report = useCase.Execute(importer.Import(new StringReader(csv)));

        Assert.Equal("historical-fixture", report.ProviderId.Value);
        Assert.Equal("DEMO", report.Symbol.Value);
        Assert.Equal("5m", report.Timeframe.ToString());
        Assert.Equal(3, report.CandleCount);
        Assert.Equal(Utc(10, 0), report.DatasetStartTimeUtc);
        Assert.Equal(Utc(10, 15), report.DatasetEndTimeUtc);
        Assert.Equal(3, report.FramesProcessed);
        Assert.Equal(Utc(10, 5), report.FirstFrameAsOfUtc);
        Assert.Equal(Utc(10, 15), report.LastFrameAsOfUtc);
    }

    [Fact]
    public void InvalidCandleIsRejectedBeforeReportGeneration()
    {
        var csv = Csv(Row("10:00", "10:05", open: "100", high: "99", low: "98", close: "98.5"));

        var exception = Assert.Throws<MarketDataCsvImportException>(() =>
            useCase.Execute(importer.Import(new StringReader(csv))));

        Assert.Equal("invalid_candle", exception.Code);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void UnorderedCsvIsRejectedBeforeReplay()
    {
        var csv = Csv(Row("10:05", "10:10"), Row("10:00", "10:05"));

        var exception = Assert.Throws<MarketDataCsvImportException>(() =>
            useCase.Execute(importer.Import(new StringReader(csv))));

        Assert.Equal("invalid_series", exception.Code);
    }

    private static string Csv(params string[] rows) =>
        string.Join('\n', new[] { Header }.Concat(rows));

    private static string Row(
        string openTime,
        string closeTime,
        string open = "100",
        string high = "101",
        string low = "99",
        string close = "100.5") =>
        $"historical-fixture,DEMO,5,Minute,2026-01-01T{openTime}:00Z,2026-01-01T{closeTime}:00Z,{open},{high},{low},{close},1000";

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);
}
