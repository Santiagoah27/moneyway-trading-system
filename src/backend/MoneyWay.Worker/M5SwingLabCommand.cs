using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Worker;

/// <summary>Bounded, explicit local IO around pure research calculation. Does not start the Worker host.</summary>
public sealed class M5SwingLabCommand
{
    private const string Usage = "m5-swing-lab --input <absolute-json> --output <new-absolute-directory> [--compare-inclusive [--human-evidence <absolute-projection-json>]]";
    private const int MaxBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    private sealed record CandleInput(DateTimeOffset OpenTimeUtc, DateTimeOffset CloseTimeUtc, decimal Open, decimal High, decimal Low, decimal Close, decimal? Volume = null);
    private sealed record InitializationInput(string ReferenceId, DateTimeOffset AnchorOpenTimeUtc, DateTimeOffset AvailableAtUtc, string SourceReference);
    private sealed record Request(string SeriesId, string ProviderId, string Symbol, string Timeframe, M5ImpulseDirection Direction,
        DateTimeOffset CutoffUtc, M5ResearchDataOrigin DataOrigin, string Provenance, InitializationInput? Initialization, CandleInput[] Candles);
    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 2 && args[1] == "--help") { output.WriteLine(Usage); return 0; }
        try
        {
            var paired = args.Count is 6 or 8 && args[5] == "--compare-inclusive";
            var humanEvidence = args.Count == 8 && paired && args[6] == "--human-evidence";
            if ((args.Count != 5 && !paired) || (args.Count == 8 && !humanEvidence) || args[0] != "m5-swing-lab" || args[1] != "--input" || args[3] != "--output" || !Path.IsPathFullyQualified(args[2]) || !Path.IsPathFullyQualified(args[4])) throw new ArgumentException(Usage);
            var inputPath = Path.GetFullPath(args[2]); var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[4]));
            if (inputPath.StartsWith(@"\\", StringComparison.Ordinal) || destination.StartsWith(@"\\", StringComparison.Ordinal) || inputPath.StartsWith("//", StringComparison.Ordinal) || destination.StartsWith("//", StringComparison.Ordinal)) throw new ArgumentException("Local filesystem paths only.");
            if (destination == Path.GetPathRoot(destination) || File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("Output must be a new directory; existing reports are never replaced.");
            Guard(inputPath); Guard(destination);
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length is <= 0 or > MaxBytes) throw new ArgumentException("Input exceeds bounded 8 MiB policy.");
            var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 }); Unique(document.RootElement);
            var request = document.RootElement.Deserialize<Request>(Json) ?? throw new ArgumentException("Missing request.");
            if (request.Timeframe != "5m" || request.Candles is null || request.Candles.Length > 10000 || request.Candles.Any(c => c is null)) throw new ArgumentException("At most 10000 M5 input candles; no timeframe conversion.");
            var provider = new MarketDataProviderId(request.ProviderId); var symbol = new MarketSymbol(request.Symbol); var timeframe = CompareM5SwingHypothesesUseCase.M5;
            var series = new CandleSeries(provider, symbol, timeframe, request.Candles.Select(c => new Candle(provider, symbol, timeframe, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume)));
            var init = request.Initialization is { } i ? new M5ImpulseInitialization(i.ReferenceId, provider, symbol, timeframe, i.AnchorOpenTimeUtc, i.AvailableAtUtc, i.SourceReference) : null;
            var experiment = new M5SwingExperimentInput(request.SeriesId, series, request.Direction, request.CutoffUtc, init, request.DataOrigin, request.Provenance);
            var policyComparison = paired ? new CompareM5SwingPoliciesUseCase().Execute(experiment) : null;
            var comparison = policyComparison?.ExclusiveV1 ?? new CompareM5SwingHypothesesUseCase().Execute(experiment);
            var envelope = new { inputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), inputHashMeaning = "Input byte integrity only; not evidence authentication or temporal admission", comparison };
            var json = JsonSerializer.Serialize(envelope, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n"; var markdown = M5SwingComparisonMarkdown.Render(comparison);
            var filePrefix = "comparison";
            if (paired)
            {
                var policies = policyComparison!;
                M5PolicyHumanEvidence? human = null;
                var legacyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
                if (humanEvidence)
                {
                    if (!Path.IsPathFullyQualified(args[7]) || args[7].StartsWith(@"\\", StringComparison.Ordinal) || args[7].StartsWith("//", StringComparison.Ordinal)) throw new ArgumentException("Explicit local human-evidence path required.");
                    var evidencePath = Path.GetFullPath(args[7]); Guard(evidencePath);
                    using var source = new FileStream(evidencePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (source.Length is <= 0 or > MaxBytes) throw new ArgumentException("Human evidence exceeds bounded input policy.");
                    var evidenceBytes = new byte[(int)source.Length]; source.ReadExactly(evidenceBytes);
                    using var evidenceJson = JsonDocument.Parse(evidenceBytes, new JsonDocumentOptions { MaxDepth = 32 }); Unique(evidenceJson.RootElement);
                    human = M5PolicyComparisonOutput.Bind(evidenceJson.RootElement, Convert.ToHexStringLower(SHA256.HashData(evidenceBytes)), Convert.ToHexStringLower(SHA256.HashData(bytes)), legacyHash, policies);
                }
                json = JsonSerializer.Serialize(new { inputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), inputHashMeaning = "Byte integrity, not authentication or temporal admission", originalExclusiveReportSha256 = legacyHash, pairedComparison = policies, humanReference = human }, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
                markdown = M5PolicyComparisonOutput.Render(policies, human); filePrefix = "policy-comparison";
            }
            // Atomic new-directory publication. Both absolute paths are constrained to the explicitly requested parent.
            var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Output parent required.");
            Directory.CreateDirectory(parent); Guard(parent);
            var pending = Path.Combine(parent, ".m5-lab-pending-" + Guid.NewGuid().ToString("N"));
            if (Path.GetDirectoryName(Path.GetFullPath(pending)) != parent || Path.GetDirectoryName(destination) != parent) throw new IOException("Output publication escaped its parent.");
            Directory.CreateDirectory(pending);
            Write(Path.Combine(pending, filePrefix + ".json"), json); Write(Path.Combine(pending, filePrefix + ".md"), markdown);
            Guard(destination); Directory.Move(pending, destination);
            output.WriteLine(paired
                ? JsonSerializer.Serialize(new { scope = "ExperimentalPairedPolicyResearchOnly", outputDirectory = destination, filePrefix, algorithms = 8, canonicalAdmissionPermitted = false, operationalSignal = false }, Json)
                : JsonSerializer.Serialize(new { scope = "ExperimentalResearchOnly", outputDirectory = destination, comparison.AllAlgorithmsAgree, comparison.AbstainedAlgorithms, canonicalAdmissionPermitted = false, operationalSignal = false }, Json));
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { error.WriteLine("M5 research comparison unavailable: " + e.Message); return 2; }
    }
    private static void Write(string path, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        file.Write(bytes); file.Flush(true);
    }
    private static void Guard(string path)
    {
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked source/output files are not supported.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked directories are not supported.");
        if (Directory.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked output directories are not supported.");
    }
    private static void Unique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var properties = node.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != properties.Length) throw new ArgumentException("Duplicate JSON fields.");
            foreach (var p in properties) Unique(p.Value);
        }
        if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) Unique(item);
    }
}
