using System.Text.Json;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Worker;

/// <summary>
/// Runs the local one-shot historical replay command and writes its neutral report as JSON.
/// </summary>
public sealed class HistoricalReplayCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (args.Count != 2
            || !string.Equals(args[0], "replay-csv", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(args[1])
            || args[1] != args[1].Trim())
        {
            error.WriteLine("Invalid command arguments.");
            return 2;
        }

        try
        {
            var series = new CsvMarketDataImporter().ImportFile(ResolveLocalPath(args[1]));
            var report = new GenerateHistoricalReplayReportUseCase().Execute(series);
            var response = new HistoricalReplayOutput(
                report.ProviderId.ToString(),
                report.Symbol.ToString(),
                report.Timeframe.ToString(),
                report.CandleCount,
                report.DatasetStartTimeUtc,
                report.DatasetEndTimeUtc,
                report.FramesProcessed,
                report.FirstFrameAsOfUtc,
                report.LastFrameAsOfUtc);

            output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            return 0;
        }
        catch (MarketDataCsvImportException exception)
        {
            error.WriteLine(FormatCsvError(exception));
            return 4;
        }
        catch (UnauthorizedAccessException)
        {
            error.WriteLine("Input file could not be read.");
            return 3;
        }
        catch (IOException)
        {
            error.WriteLine("Input file could not be read.");
            return 3;
        }
    }

    private static string ResolveLocalPath(string filePath)
    {
        if (Path.IsPathRooted(filePath) || File.Exists(filePath))
        {
            return filePath;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln")))
            {
                return Path.Combine(directory.FullName, filePath);
            }
        }

        return filePath;
    }

    private static string FormatCsvError(MarketDataCsvImportException exception)
    {
        var location = exception.LineNumber is null
            ? string.Empty
            : $" line={exception.LineNumber}";
        var column = exception.ColumnName is null
            ? string.Empty
            : $" column={exception.ColumnName}";

        return $"Invalid CSV content: code={exception.Code}{location}{column}. {exception.Message}";
    }

    private sealed record HistoricalReplayOutput(
        string ProviderId,
        string Symbol,
        string Timeframe,
        int CandleCount,
        DateTimeOffset? DatasetStartTimeUtc,
        DateTimeOffset? DatasetEndTimeUtc,
        int FramesProcessed,
        DateTimeOffset? FirstFrameAsOfUtc,
        DateTimeOffset? LastFrameAsOfUtc);
}
