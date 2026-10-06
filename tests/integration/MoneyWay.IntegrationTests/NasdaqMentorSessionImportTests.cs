using System.Globalization;
using System.Text;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqMentorSessionImportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "moneyway-synthetic-session-" + Guid.NewGuid());
    private const string Header = "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume";
    internal NasdaqMentorSessionReplayInput Input()
    {
        Directory.CreateDirectory(directory);
        var files = new Dictionary<Timeframe, string>();
        foreach (var series in M5Fixture.Series())
        {
            var path = Path.Combine(directory, $"{series.Timeframe.Amount}-{series.Timeframe.Unit}.csv");
            var rows = series.Candles.Where(c => c.CloseTimeUtc <= M5Fixture.At(15, 10)).Select(c =>
                FormattableString.Invariant($"{c.ProviderId.Value},{c.Symbol.Value},{c.Timeframe.Amount},{c.Timeframe.Unit},{c.OpenTimeUtc:O},{c.CloseTimeUtc:O},{c.Open},{c.High},{c.Low},{c.Close},{c.Volume}"));
            File.WriteAllText(path, Header + "\n" + string.Join("\n", rows), new UTF8Encoding(false));
            files.Add(series.Timeframe, path);
        }
        return new("synthetic-csv-test-only", LiquidityFixture.Session(M5Fixture.At(13)), files,
            SyntheticNasdaqMentorSessionFixture.Records());
    }

    [Fact]
    public void FourImportedFilesReachCanonicalAndFactualArtifactsInOneRun()
    {
        var input = Input();
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(input);
        Assert.Empty(report.InputDiagnostics);
        Assert.Equal(NasdaqMentorSessionReplayInput.RequiredTimeframes, report.Imports.Select(i => i.Timeframe));
        Assert.All(report.Imports, i => Assert.True(i.CandleCount > 0));
        Assert.NotNull(report.Canonical);
        Assert.Equal(report.Frames.Count, report.Canonical!.Frames.Count);
        Assert.Equal(4, report.Canonical.OutcomeRun.StrategyRun.ConfiguredTimeframes.Count);
        var last = report.Frames.Last();
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(last.Snapshot).Snapshot;
        Assert.Same(snapshot, Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(last.FactualEvaluation).Snapshot);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(last.DocumentedExit);
        Assert.NotEmpty(last.ContactResolution!.Contacts);
        var json = NasdaqMentorSessionReportJson.Serialize(report);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Contains("Available", json);
        Assert.Contains("synthetic:exit:record", json);
        Assert.Contains("RuleFacts", json);
        Assert.Contains("ContactResolution", json);
        Assert.Contains("StopPrice", json);
        Assert.DoesNotContain("PnL", json);
        Assert.Equal(input.SessionId, report.SessionId);
        Assert.Equal(report.Frames.First().Strategy.AsOfUtc, report.ReplayStartUtc);
        Assert.Equal(report.Frames.Last().Strategy.AsOfUtc, report.ReplayEndUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachMissingTimeframeStopsBeforeReplayWithoutSynthesis(int index)
    {
        var input = Input(); var files = input.CsvFiles.ToDictionary();
        var absent = NasdaqMentorSessionReplayInput.RequiredTimeframes[index]; files.Remove(absent);
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, input.Session, files, input.Evidence));
        Assert.Equal(absent, Assert.Single(report.InputDiagnostics).Timeframe);
        Assert.Equal("MissingTimeframe", report.InputDiagnostics[0].Code);
        Assert.Null(report.Canonical); Assert.Empty(report.Frames); Assert.Equal(3, report.Imports.Count);
    }

    [Fact]
    public void ImporterCodeLineAndColumnAreRetainedWithoutPartialReplay()
    {
        var input = Input(); var path = input.CsvFiles[M5Fixture.Minute];
        File.WriteAllText(path, Header + "\nfixture,NASDAQ,1,Minute,invalid,2026-10-03T15:00:00Z,100,110,90,100,\n");
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(input);
        var error = Assert.Single(report.InputDiagnostics);
        Assert.Equal(path, error.SourceReference); Assert.Equal(2, error.LineNumber); Assert.Equal("open_time_utc", error.ColumnName);
        Assert.Equal("invalid_value", error.Code);
        Assert.Null(report.Canonical); Assert.Empty(report.Frames); Assert.Equal(3, report.Imports.Count);
    }

    [Fact]
    public void WrongTimeframeAndUnreadableFileRemainSessionInputFailures()
    {
        var input = Input(); var files = input.CsvFiles.ToDictionary();
        files[M5Fixture.Minute] = input.CsvFiles[new(1, TimeframeUnit.Hour)];
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, input.Session, files, input.Evidence));
        Assert.Equal("SeriesIdentityMismatch", Assert.Single(report.InputDiagnostics).Code); Assert.Null(report.Canonical);
        files[M5Fixture.Minute] = Path.Combine(directory, "absent.csv");
        report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, input.Session, files, input.Evidence));
        Assert.Equal("FileUnavailable", Assert.Single(report.InputDiagnostics).Code); Assert.Null(report.Canonical);
    }

    [Fact]
    public void NoClosesOnSelectedTradingDayStopsBeforeReplay()
    {
        var input = Input();
        var session = new NasdaqDemoSessionIdentity(input.Session.StrategyId, input.Session.StrategyVersion,
            input.Session.ProviderId, input.Session.Symbol, input.Session.TradingDay.AddDays(2));
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, session, input.CsvFiles, []));
        Assert.Equal("SelectedSessionDataUnavailable", Assert.Single(report.InputDiagnostics).Code);
        Assert.Null(report.Canonical); Assert.Empty(report.Frames);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
