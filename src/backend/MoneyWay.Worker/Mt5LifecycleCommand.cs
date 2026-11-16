using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Worker;

/// <summary>One bounded inspection, no host, checkpoint store, receipt acknowledgment or terminal call.</summary>
public sealed class Mt5LifecycleCommand
{
    private const string Usage = "mt5-lifecycle --source <absolute-directory> [--report <new-absolute-file-outside-git-and-source>] [--through-sequence <positive-integer>] [--details <0..128>]";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 2 && args[1] == "--help") { output.WriteLine(Usage); return 0; }
        try
        {
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 1; i < args.Count; i += 2)
            {
                if (i + 1 == args.Count || args[i] is not ("--source" or "--report" or "--through-sequence" or "--details")
                    || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException();
            }
            if (!options.TryGetValue("--source", out var source) || !Path.IsPathFullyQualified(source)) throw new ArgumentException();
            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            var details = options.TryGetValue("--details", out var d) ? int.Parse(d, CultureInfo.InvariantCulture) : 16;
            long? through = options.TryGetValue("--through-sequence", out var t) ? long.Parse(t, CultureInfo.InvariantCulture) : null;
            var path = options.GetValueOrDefault("--report") ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoneyWay", "audits", $"mt5-lifecycle-{Guid.NewGuid():N}.json");
            if (!Path.IsPathFullyQualified(path) || details is < 0 or > 128 || through is <= 0) throw new ArgumentException();
            path = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(source, path);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException();
            // Reports must not alter an evidence directory, repository or aliased ancestor.
            for (DirectoryInfo? parent = new(Path.GetDirectoryName(path)!); parent is not null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git"))))
                    throw new ArgumentException();
            var batch = new LocalMt5CollectorReceiptReader().Read(source);
            var report = new Mt5NativeLifecycleAudit().Analyze(batch, through, details);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(file, new { SourceDirectory = source, Audit = report }, Json);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Report = path,
                report.Status,
                report.InspectedRecords,
                report.ValidatedRecords,
                report.BlockedRecords,
                Timeframes = report.Timeframes.Select(t => new
                {
                    t.Timeframe,
                    t.ObservedBars,
                    t.ActiveThenHistorical,
                    t.RevisedBars,
                    t.HistoricalToHistoricalRevisions
                }),
                report.CanonicalReady,
                report.UtcConversionAuthenticated,
                report.CanonicalUtcConversionAuthorized
            }, Json));
            return report.BlockedRecords > 0 || report.ValidatedRecords == 0 || batch.Diagnostics.Count > 0 ? 3 : 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or NotSupportedException)
        { error.WriteLine("invalid_arguments_or_unsafe_report_path\n" + Usage); return 2; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { error.WriteLine("report_io_unavailable_use_a_new_separate_local_path"); return 4; }
    }
}
