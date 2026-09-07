namespace MoneyWay.Infrastructure.MarketData.Csv;

/// <summary>
/// Describes a deterministic validation failure in canonical market-data CSV content.
/// </summary>
public sealed class MarketDataCsvImportException : FormatException
{
    public MarketDataCsvImportException(
        string code,
        string message,
        int? lineNumber = null,
        string? columnName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        LineNumber = lineNumber;
        ColumnName = columnName;
    }

    public string Code { get; }

    public int? LineNumber { get; }

    public string? ColumnName { get; }
}
