using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Worker;

/// <summary>One read-only prefix projection. Execution returns before creating a Worker host.</summary>
public sealed class Mt5CausalAvailabilityCommand
{
    private const string Usage = "mt5-causal-availability --source <absolute-directory> --through-sequence <0..999999999999> "
        + "[--through-sha256 <required-for-nonempty-prefix>] [--report <new-absolute-file-outside-git-and-source>] "
        + "[--view <observations|first-historical|receipt-cut>] [--details <0..128>] [--timeframe <M1|M5|H1|H4> --native-open <positive-native-raw>] "
        + "[--anchor-sequence <N> --anchor-sha256 <hash>] [--observer-journal <absolute-NDJSON-file> --observer-journal-sha256 <hash>]";
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
                if (i + 1 == args.Count || args[i] is not ("--source" or "--through-sequence" or "--through-sha256"
                    or "--report" or "--details" or "--timeframe" or "--native-open" or "--view"
                    or "--anchor-sequence" or "--anchor-sha256" or "--observer-journal" or "--observer-journal-sha256") || !options.TryAdd(args[i], args[i + 1]))
                    throw new ArgumentException();
            }
            if (!options.TryGetValue("--source", out var source) || !Path.IsPathFullyQualified(source)
                || !options.TryGetValue("--through-sequence", out var sequence)) throw new ArgumentException();
            source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
            var boundary = new Mt5ReceiptPrefixBoundary(long.Parse(sequence, CultureInfo.InvariantCulture), options.GetValueOrDefault("--through-sha256"));
            var details = options.TryGetValue("--details", out var d) ? int.Parse(d, CultureInfo.InvariantCulture) : 16;
            var timeframe = options.GetValueOrDefault("--timeframe");
            var view = options.GetValueOrDefault("--view") ?? "observations";
            if (view is not ("observations" or "first-historical" or "receipt-cut")) throw new ArgumentException();
            var isCut = view == "receipt-cut";
            var anchorSequence = options.GetValueOrDefault("--anchor-sequence");
            var anchorHash = options.GetValueOrDefault("--anchor-sha256");
            var journalPath = options.GetValueOrDefault("--observer-journal");
            var journalHash = options.GetValueOrDefault("--observer-journal-sha256");
            if ((anchorSequence is null) != (anchorHash is null) || (journalPath is null) != (journalHash is null)
                || (!isCut && (anchorSequence is not null || journalPath is not null))
                || (isCut && (boundary.Sequence == 0 || timeframe is not null || options.ContainsKey("--details"))))
                throw new ArgumentException();
            var anchor = anchorSequence is null ? null : new Mt5ReceiptPrefixBoundary(long.Parse(anchorSequence, CultureInfo.InvariantCulture), anchorHash);
            long? native = options.TryGetValue("--native-open", out var n) ? long.Parse(n, CultureInfo.InvariantCulture) : null;
            if (details is < 0 or > 128 || (timeframe is null) != (native is null) || native is <= 0
                || timeframe is not null && timeframe is not ("M1" or "M5" or "H1" or "H4")) throw new ArgumentException();
            var path = options.GetValueOrDefault("--report") ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoneyWay", "audits", $"mt5-causal-availability-{Guid.NewGuid():N}.json");
            if (!Path.IsPathFullyQualified(path)) throw new ArgumentException();
            path = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(source, path);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new ArgumentException();
            for (DirectoryInfo? parent = new(Path.GetDirectoryName(path)!); parent is not null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes.HasFlag(FileAttributes.ReparsePoint)
                    || Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git"))))
                    throw new ArgumentException();
            if (File.Exists(path) || File.Exists(path + ".pending")) throw new IOException("Report already exists.");

            var reader = new LocalMt5CollectorReceiptReader();
            var batch = anchor is null ? reader.ReadPrefix(source, boundary) : reader.ReadDiagnosticRange(source, anchor, boundary);
            if (isCut)
            {
                var labels = journalPath is null ? [] : ReadLabels(journalPath, journalHash!);
                var diagnostic = new Mt5ReceiptCutDiagnostic().Inspect(batch, Mt5ReceiptCutDiagnostic.SupportedSource, boundary, anchor, labels);
                var hash = WriteReport(path, new { SourceDirectory = source, ObserverJournalSha256 = journalHash?.ToLowerInvariant(), Audit = diagnostic });
                output.WriteLine(JsonSerializer.Serialize(new { Report = path, ReportSha256 = hash,
                    diagnostic.Integrity, IncludedSamples = diagnostic.Samples.Count, diagnostic.CanonicalReady }, Json));
                return diagnostic.Integrity == Mt5ReceiptStatus.Validated ? 0 : 3;
            }
            var selection = ValidatedMt5ReceiptPrefix.Select(batch, boundary);
            var projection = selection.Prefix is { } prefix ? new Mt5CausalAvailabilityAudit().Project(prefix, details) : null;
            object report = (view == "first-historical" ? projection?.HistoricalFreezeReport(details, timeframe, native)
                : projection?.Report(details, timeframe, native)) ?? new
                {
                    Schema = view == "first-historical" ? "moneyway.mt5-first-historical-observation-freeze" : "moneyway.mt5-causal-receipt-prefix-availability",
                    Version = 1,
                    batch.DirectoryScope,
                    Boundary = boundary,
                    ProjectionIntegrityStatus = selection.Status,
                    selection.InspectedRecords,
                    selection.Diagnostics,
                    ReceiptDiagnostics = batch.Records.SelectMany(r => r.Diagnostics).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    SourceObservationCausality = "unresolved",
                    SourceObservationsAvailable = false,
                    HistoricalUtcAvailability = "unresolved",
                    ProviderClosure = "unresolved",
                    AtomicMultiTimeframeSnapshot = "unresolved",
                    SelectionIntervalPropagation = "unresolved",
                    OverallUtcUncertainty = "unresolved",
                    WindowsClockAuthenticated = false,
                    Mt5NativeUtcAuthenticated = false,
                    CanonicalUtcConversionAuthorized = false,
                    UtcConversionAuthenticated = false,
                    CanonicalReady = false
                };
            var reportHash = WriteReport(path, new { SourceDirectory = source, Audit = report });
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Report = path,
                ReportSha256 = reportHash,
                ProjectionIntegrityStatus = selection.Status,
                boundary.Sequence,
                selection.InspectedRecords,
                ValidatedRecords = selection.Prefix?.Records.Count ?? 0,
                ObservedCandles = projection?.Bars.Count ?? 0,
                PrefixManifestSha256 = selection.Prefix?.EvidenceManifestSha256,
                RequestedObservationAvailable = timeframe is null || projection is null ? (bool?)null
                    : projection.Find(timeframe, native!.Value) is not null,
                HistoricalUtcAvailability = "unresolved",
                ProviderClosure = "unresolved",
                CanonicalReady = false
            }, Json));
            return selection.Prefix is null ? 3 : 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or NotSupportedException)
        { error.WriteLine("invalid_arguments_or_unsafe_report_path\n" + Usage); return 2; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { error.WriteLine("report_io_unavailable_use_a_new_separate_local_path"); return 4; }
        catch (JsonException)
        { error.WriteLine("observer_journal_invalid"); return 3; }
    }

    private static Mt5LocalReceiptLabel[] ReadLabels(string path, string expectedHash)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Use an absolute observer journal path.");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > LocalMt5CollectorReceiptReader.MaxFileBytes) throw new JsonException("Observer journal exceeds its bound.");
        var bytes = new byte[file.Length];
        file.ReadExactly(bytes);
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new JsonException("Observer journal hash mismatch.");
        using var lines = new StreamReader(new MemoryStream(bytes));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var labels = new List<Mt5LocalReceiptLabel>();
        while (lines.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var label = JsonSerializer.Deserialize<Mt5LocalReceiptLabel>(line, options);
            if (label is null || label.Sequence <= 0 || label.Sha256 is null || label.ObservedAtHostUtcCandidate == default)
                throw new JsonException("Observer journal row is incomplete.");
            labels.Add(label);
        }
        return labels.ToArray();
    }

    private static string WriteReport(string path, object report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, report, Json);
            file.Flush(true);
        }
        File.Move(path + ".pending", path);
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }
}
