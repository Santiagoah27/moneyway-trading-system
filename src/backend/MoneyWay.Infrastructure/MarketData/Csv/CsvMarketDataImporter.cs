using System.Globalization;
using System.Text;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Infrastructure.MarketData.Csv;

/// <summary>
/// Imports one homogeneous historical <see cref="CandleSeries"/> from canonical MoneyWay CSV.
/// </summary>
public sealed class CsvMarketDataImporter
{
    private static readonly string[] RequiredColumns =
    [
        "provider_id",
        "symbol",
        "timeframe_amount",
        "timeframe_unit",
        "open_time_utc",
        "close_time_utc",
        "open",
        "high",
        "low",
        "close",
        "volume",
    ];

    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public CandleSeries Import(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            throw Error("missing_header", "The CSV header is required.", 1);
        }

        if (headerLine.Length > 0 && headerLine[0] == '\uFEFF')
        {
            headerLine = headerLine[1..];
        }

        var header = ParseCsvLine(headerLine, 1);
        var columns = ValidateHeader(header);
        var candles = new List<Candle>();
        MarketDataProviderId? seriesProvider = null;
        MarketSymbol? seriesSymbol = null;
        Timeframe? seriesTimeframe = null;
        var lineNumber = 1;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }

            var fields = ParseCsvLine(line, lineNumber);
            if (fields.Count != header.Count)
            {
                throw Error(
                    "field_count_mismatch",
                    "The record field count does not match the header.",
                    lineNumber);
            }

            var provider = CreateProvider(Field("provider_id"), lineNumber);
            var symbol = CreateSymbol(Field("symbol"), lineNumber);
            var timeframe = CreateTimeframe(
                Field("timeframe_amount"),
                Field("timeframe_unit"),
                lineNumber);

            if (seriesProvider is null)
            {
                seriesProvider = provider;
                seriesSymbol = symbol;
                seriesTimeframe = timeframe;
            }
            else if (provider != seriesProvider || symbol != seriesSymbol || timeframe != seriesTimeframe)
            {
                throw Error(
                    "mixed_series_metadata",
                    "All records must belong to one homogeneous series.",
                    lineNumber);
            }

            candles.Add(CreateCandle(provider, symbol, timeframe, fields, columns, lineNumber));

            string Field(string columnName) => fields[columns[columnName]];
        }

        if (candles.Count == 0)
        {
            throw Error("missing_data", "At least one data record is required.");
        }

        try
        {
            return new CandleSeries(seriesProvider!, seriesSymbol!, seriesTimeframe!, candles);
        }
        catch (ArgumentException exception)
        {
            throw Error("invalid_series", "The records do not form a valid chronological series.", null, null, exception);
        }
    }

    public CandleSeries ImportFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrWhiteSpace(filePath) || filePath != filePath.Trim())
        {
            throw new ArgumentException(
                "File path must be non-empty and have no surrounding whitespace.",
                nameof(filePath));
        }

        using var reader = new StreamReader(
            filePath,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return Import(reader);
    }

    private static IReadOnlyDictionary<string, int> ValidateHeader(IReadOnlyList<string> header)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < header.Count; index++)
        {
            var name = header[index];
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name != name.Trim())
            {
                throw Error("invalid_header", "Column names must be exact and non-empty.", 1, name);
            }

            if (!RequiredColumns.Contains(name, StringComparer.Ordinal))
            {
                throw Error("unknown_column", "The header contains an unknown column.", 1, name);
            }

            if (!columns.TryAdd(name, index))
            {
                throw Error("duplicate_column", "The header contains a duplicate column.", 1, name);
            }
        }

        foreach (var required in RequiredColumns)
        {
            if (!columns.ContainsKey(required))
            {
                throw Error("missing_column", "The header is missing a required column.", 1, required);
            }
        }

        return columns;
    }

    private static MarketDataProviderId CreateProvider(string value, int lineNumber)
    {
        try
        {
            return new MarketDataProviderId(value);
        }
        catch (ArgumentException exception)
        {
            throw Error("invalid_value", "The provider identifier is invalid.", lineNumber, "provider_id", exception);
        }
    }

    private static MarketSymbol CreateSymbol(string value, int lineNumber)
    {
        try
        {
            return new MarketSymbol(value);
        }
        catch (ArgumentException exception)
        {
            throw Error("invalid_value", "The market symbol is invalid.", lineNumber, "symbol", exception);
        }
    }

    private static Timeframe CreateTimeframe(string amountValue, string unitValue, int lineNumber)
    {
        if (!int.TryParse(amountValue, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            throw Error("invalid_value", "The timeframe amount is invalid.", lineNumber, "timeframe_amount");
        }

        if (!Enum.TryParse<TimeframeUnit>(unitValue, ignoreCase: false, out var unit)
            || !Enum.IsDefined(unit)
            || !string.Equals(unit.ToString(), unitValue, StringComparison.Ordinal))
        {
            throw Error("invalid_value", "The timeframe unit is invalid.", lineNumber, "timeframe_unit");
        }

        try
        {
            return new Timeframe(amount, unit);
        }
        catch (ArgumentException exception)
        {
            throw Error("invalid_value", "The timeframe is invalid.", lineNumber, "timeframe_amount", exception);
        }
    }

    private static Candle CreateCandle(
        MarketDataProviderId provider,
        MarketSymbol symbol,
        Timeframe timeframe,
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> columns,
        int lineNumber)
    {
        var openTime = ParseTimestamp(fields[columns["open_time_utc"]], lineNumber, "open_time_utc");
        var closeTime = ParseTimestamp(fields[columns["close_time_utc"]], lineNumber, "close_time_utc");
        var open = ParseDecimal(fields[columns["open"]], lineNumber, "open");
        var high = ParseDecimal(fields[columns["high"]], lineNumber, "high");
        var low = ParseDecimal(fields[columns["low"]], lineNumber, "low");
        var close = ParseDecimal(fields[columns["close"]], lineNumber, "close");
        var volumeValue = fields[columns["volume"]];
        decimal? volume = volumeValue.Length == 0
            ? null
            : ParseDecimal(volumeValue, lineNumber, "volume");

        try
        {
            return new Candle(provider, symbol, timeframe, openTime, closeTime, open, high, low, close, volume);
        }
        catch (ArgumentException exception)
        {
            throw Error("invalid_candle", "The record does not define a valid candle.", lineNumber, null, exception);
        }
    }

    private static DateTimeOffset ParseTimestamp(string value, int lineNumber, string columnName)
    {
        if (!HasExplicitOffset(value)
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp)
            || timestamp.Offset != TimeSpan.Zero)
        {
            throw Error("invalid_value", "The timestamp must be explicit UTC ISO 8601.", lineNumber, columnName);
        }

        return timestamp;
    }

    private static bool HasExplicitOffset(string value)
    {
        if (value.EndsWith('Z'))
        {
            return true;
        }

        var timeSeparator = value.IndexOf('T');
        if (timeSeparator < 0 || value.Length < 6)
        {
            return false;
        }

        var offsetStart = value.Length - 6;
        return offsetStart > timeSeparator
            && value[offsetStart] is '+' or '-'
            && value[offsetStart + 3] == ':'
            && char.IsAsciiDigit(value[offsetStart + 1])
            && char.IsAsciiDigit(value[offsetStart + 2])
            && char.IsAsciiDigit(value[offsetStart + 4])
            && char.IsAsciiDigit(value[offsetStart + 5]);
    }

    private static decimal ParseDecimal(string value, int lineNumber, string columnName)
    {
        if (!decimal.TryParse(value, DecimalStyles, CultureInfo.InvariantCulture, out var parsed))
        {
            throw Error("invalid_value", "The decimal value is invalid.", lineNumber, columnName);
        }

        return parsed;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line, int lineNumber)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var index = 0;

        while (true)
        {
            field.Clear();
            if (index < line.Length && line[index] == '"')
            {
                index++;
                var closed = false;
                while (index < line.Length)
                {
                    if (line[index] != '"')
                    {
                        field.Append(line[index++]);
                        continue;
                    }

                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        field.Append('"');
                        index += 2;
                        continue;
                    }

                    index++;
                    closed = true;
                    break;
                }

                if (!closed || (index < line.Length && line[index] != ','))
                {
                    throw Error("malformed_csv", "The CSV quoting is malformed.", lineNumber);
                }
            }
            else
            {
                while (index < line.Length && line[index] != ',')
                {
                    if (line[index] == '"')
                    {
                        throw Error("malformed_csv", "Quotes must begin a quoted field.", lineNumber);
                    }

                    field.Append(line[index++]);
                }
            }

            fields.Add(field.ToString());
            if (index >= line.Length)
            {
                return fields;
            }

            index++;
        }
    }

    private static MarketDataCsvImportException Error(
        string code,
        string message,
        int? lineNumber = null,
        string? columnName = null,
        Exception? innerException = null) =>
        new(code, message, lineNumber, columnName, innerException);
}
