using System.Globalization;
using System.Text;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.IntegrationTests;

public sealed class CsvMarketDataImporterTests
{
    private const string Header =
        "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume";
    private readonly CsvMarketDataImporter importer = new();

    [Fact]
    public void CanonicalHeaderAndOneRowProduceExactCandle()
    {
        var series = Import(Row());
        var candle = Assert.Single(series.Candles);

        Assert.Equal("historical-fixture", series.ProviderId.Value);
        Assert.Equal("EUR/USD", series.Symbol.Value);
        Assert.Equal(new Timeframe(5, TimeframeUnit.Minute), series.Timeframe);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.Zero), candle.OpenTimeUtc);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 10, 5, 0, TimeSpan.Zero), candle.CloseTimeUtc);
        Assert.Equal(1.1000m, candle.Open);
        Assert.Equal(1.1010m, candle.High);
        Assert.Equal(1.0990m, candle.Low);
        Assert.Equal(1.1005m, candle.Close);
        Assert.Equal(100m, candle.Volume);
    }

    [Fact]
    public void ReorderedColumnsAreAccepted()
    {
        const string header =
            "symbol,provider_id,timeframe_unit,timeframe_amount,close_time_utc,open_time_utc,close,low,high,open,volume";
        const string row =
            "EUR/USD,historical-fixture,Minute,5,2026-01-05T10:05:00Z,2026-01-05T10:00:00Z,1.1005,1.0990,1.1010,1.1000,100";

        var series = importer.Import(new StringReader($"{header}\n{row}"));

        Assert.Equal("historical-fixture", series.ProviderId.Value);
        Assert.Equal(1.1005m, series.Candles[0].Close);
    }

    [Fact]
    public void MultiRowImportPreservesChronologyAndBoundaries()
    {
        var series = Import(
            Row(),
            Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z", open: "1.1005", high: "1.1020", close: "1.1015"),
            Row("2026-01-05T10:10:00Z", "2026-01-05T10:15:00Z", open: "1.1015", high: "1.1025", close: "1.1020"));

        Assert.Equal(3, series.Count);
        Assert.Equal(series.Candles.OrderBy(static candle => candle.OpenTimeUtc), series.Candles);
        Assert.Equal(series.Candles[0].OpenTimeUtc, series.StartTimeUtc);
        Assert.Equal(series.Candles[2].CloseTimeUtc, series.EndTimeUtc);
    }

    [Fact]
    public void EmptyAndZeroVolumesRemainDistinct()
    {
        var series = Import(
            Row(volume: ""),
            Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z", volume: "0"));

        Assert.Null(series.Candles[0].Volume);
        Assert.Equal(0m, series.Candles[1].Volume);
    }

    [Fact]
    public void DecimalParsingUsesInvariantCultureAndPreservesPrecision()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("es-CO");
            var candle = Import(Row(open: "1.12345", high: "1.22345", low: "1.02345", close: "+1.12346")).Candles[0];
            Assert.Equal(1.12345m, candle.Open);
            Assert.Equal(1.12346m, candle.Close);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(1, "Minute")]
    [InlineData(2, "Hour")]
    [InlineData(3, "Day")]
    [InlineData(4, "Week")]
    public void CanonicalTimeframeUnitsAreAccepted(int amount, string unit)
    {
        Assert.Equal(new Timeframe(amount, Enum.Parse<TimeframeUnit>(unit)), Import(Row(amount: amount.ToString(), unit: unit)).Timeframe);
    }

    [Theory]
    [InlineData("minute")]
    [InlineData("HOUR")]
    [InlineData("1")]
    [InlineData("Minutes")]
    public void NonCanonicalTimeframeUnitIsRejected(string unit)
    {
        AssertError(Row(unit: unit), "invalid_value", 2, "timeframe_unit");
    }

    [Theory]
    [InlineData("5.0")]
    [InlineData("five")]
    [InlineData("-1")]
    [InlineData("0")]
    public void InvalidTimeframeAmountIsRejected(string amount)
    {
        AssertError(Row(amount: amount), "invalid_value", 2, "timeframe_amount");
    }

    [Theory]
    [InlineData("2026-01-05T10:00:00Z")]
    [InlineData("2026-01-05T10:00:00+00:00")]
    public void ExplicitUtcTimestampIsAccepted(string timestamp)
    {
        Assert.Equal(TimeSpan.Zero, Import(Row(openTime: timestamp)).Candles[0].OpenTimeUtc.Offset);
    }

    [Theory]
    [InlineData("2026-01-05T05:00:00-05:00")]
    [InlineData("2026-01-05T10:00:00")]
    [InlineData("not-a-timestamp")]
    public void InvalidTimestampIsContextualized(string timestamp)
    {
        AssertError(Row(openTime: timestamp), "invalid_value", 2, "open_time_utc");
    }

    [Theory]
    [InlineData("100", "99", "90", "95")]
    [InlineData("100", "110", "106", "105")]
    public void InvalidOhlcIsContextualized(string open, string high, string low, string close)
    {
        AssertError(Row(open: open, high: high, low: low, close: close), "invalid_candle", 2, null);
    }

    [Fact]
    public void NegativeVolumeIsInvalidCandle() =>
        AssertError(Row(volume: "-1"), "invalid_candle", 2, null);

    [Theory]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("timeframe")]
    public void MixedSeriesMetadataIsRejected(string changedField)
    {
        var second = changedField switch
        {
            "provider" => Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z", provider: "provider-b"),
            "symbol" => Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z", symbol: "USDCHF"),
            _ => Row("2026-01-05T10:05:00Z", "2026-01-05T10:20:00Z", amount: "15"),
        };

        AssertError([Row(), second], "mixed_series_metadata", 3, null);
    }

    [Theory]
    [InlineData("unordered")]
    [InlineData("duplicate")]
    [InlineData("overlap")]
    public void InvalidSeriesChronologyIsNotRepaired(string scenario)
    {
        var rows = scenario switch
        {
            "unordered" =>
                new[] { Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z"), Row() },
            "duplicate" =>
                new[] { Row(), Row(open: "1.1005", close: "1.1006") },
            _ =>
                new[] { Row("2026-01-05T10:00:00Z", "2026-01-05T10:06:00Z"), Row("2026-01-05T10:05:00Z", "2026-01-05T10:10:00Z") },
        };

        AssertError(rows, "invalid_series", null, null);
    }

    [Fact]
    public void GapIsAcceptedWithoutGeneratedCandles()
    {
        var series = Import(Row(), Row("2026-01-05T10:15:00Z", "2026-01-05T10:20:00Z"));
        Assert.Equal(2, series.Count);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 10, 15, 0, TimeSpan.Zero), series.Candles[1].OpenTimeUtc);
    }

    [Fact]
    public void QuotedCommasAndEscapedQuotesArePreserved()
    {
        var quotedSeries = Import(Row(provider: "\"historical,fixture\"", symbol: "\"EUR/USD\""));
        var escapedQuoteSeries = Import(Row(symbol: "\"EUR/\"\"USD\"\"\""));

        Assert.Equal("historical,fixture", quotedSeries.ProviderId.Value);
        Assert.Equal("EUR/USD", quotedSeries.Symbol.Value);
        Assert.Equal("EUR/\"USD\"", escapedQuoteSeries.Symbol.Value);
    }

    [Theory]
    [InlineData("\"unterminated,EUR/USD,5,Minute,2026-01-05T10:00:00Z,2026-01-05T10:05:00Z,1,2,0,1,1")]
    [InlineData("\"provider\"x,EUR/USD,5,Minute,2026-01-05T10:00:00Z,2026-01-05T10:05:00Z,1,2,0,1,1")]
    [InlineData("provider,EUR/USD,5,Minute,2026-01-05T10:00:00Z")]
    public void MalformedRecordsAreRejected(string row)
    {
        var expectedCode = row.StartsWith("provider,") ? "field_count_mismatch" : "malformed_csv";
        AssertError(row, expectedCode, 2, null);
    }

    [Fact]
    public void EmptyFileHasMissingHeader() =>
        AssertException("", "missing_header", 1, null);

    [Fact]
    public void HeaderOnlyHasMissingData() =>
        AssertException(Header, "missing_data", null, null);

    [Theory]
    [InlineData("missing", "missing_column")]
    [InlineData("duplicate", "duplicate_column")]
    [InlineData("unknown", "unknown_column")]
    [InlineData("leading-space", "invalid_header")]
    [InlineData("trailing-space", "invalid_header")]
    [InlineData("wrong-case", "unknown_column")]
    [InlineData("empty", "invalid_header")]
    public void InvalidHeadersAreRejected(string scenario, string code)
    {
        var header = scenario switch
        {
            "missing" => string.Join(',', Header.Split(',').Where(column => column != "volume")),
            "duplicate" => $"{Header},volume",
            "unknown" => $"{Header},extra",
            "leading-space" => Header.Replace("provider_id", " provider_id", StringComparison.Ordinal),
            "trailing-space" => Header.Replace("provider_id", "provider_id ", StringComparison.Ordinal),
            "wrong-case" => Header.Replace("provider_id", "Provider_Id", StringComparison.Ordinal),
            _ => Header.Replace("provider_id", "", StringComparison.Ordinal),
        };

        AssertException($"{header}\n{Row()}", code, 1, scenario == "missing" ? "volume" : null);
    }

    [Fact]
    public void BomBeforeHeaderIsAccepted()
    {
        Assert.Equal(1, importer.Import(new StringReader($"\uFEFF{Header}\n{Row()}")).Count);
    }

    [Fact]
    public void EmptyPhysicalLinesAreIgnoredButPhysicalLineNumberIsPreserved()
    {
        var text = $"{Header}\n{Row()}\n\n{Row("bad-time", "2026-01-05T10:10:00Z")}";
        AssertException(text, "invalid_value", 4, "open_time_utc");
    }

    [Fact]
    public void WhitespaceOnlyLineIsNotIgnored() =>
        AssertException($"{Header}\n   ", "field_count_mismatch", 2, null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImportFileReadsUtf8WithOptionalBomAndDeletesCleanly(bool emitBom)
    {
        var path = Path.Combine(Path.GetTempPath(), $"moneyway-csv-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, $"{Header}\n{Row()}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitBom));
            Assert.Equal(1, importer.ImportFile(path).Count);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void MissingFilePropagatesFileNotFound()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-moneyway-{Guid.NewGuid():N}.csv");
        Assert.Throws<FileNotFoundException>(() => importer.ImportFile(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" path.csv")]
    [InlineData("path.csv ")]
    public void InvalidFilePathIsRejected(string? path) =>
        Assert.ThrowsAny<ArgumentException>(() => importer.ImportFile(path!));

    [Fact]
    public void NullReaderIsRejected() =>
        Assert.Throws<ArgumentNullException>(() => importer.Import(null!));

    private CandleSeries Import(params string[] rows) =>
        importer.Import(new StringReader(string.Join('\n', new[] { Header }.Concat(rows))));

    private void AssertError(string row, string code, int? line, string? column) =>
        AssertError([row], code, line, column);

    private void AssertError(string[] rows, string code, int? line, string? column) =>
        AssertException(string.Join('\n', new[] { Header }.Concat(rows)), code, line, column);

    private void AssertException(string text, string code, int? line, string? column)
    {
        var exception = Assert.Throws<MarketDataCsvImportException>(() => importer.Import(new StringReader(text)));
        Assert.Equal(code, exception.Code);
        Assert.Equal(line, exception.LineNumber);
        if (column is not null)
        {
            Assert.Equal(column, exception.ColumnName);
        }
    }

    private static string Row(
        string openTime = "2026-01-05T10:00:00Z",
        string closeTime = "2026-01-05T10:05:00Z",
        string provider = "historical-fixture",
        string symbol = "EUR/USD",
        string amount = "5",
        string unit = "Minute",
        string open = "1.1000",
        string high = "1.1010",
        string low = "1.0990",
        string close = "1.1005",
        string volume = "100") =>
        $"{provider},{symbol},{amount},{unit},{openTime},{closeTime},{open},{high},{low},{close},{volume}";
}
