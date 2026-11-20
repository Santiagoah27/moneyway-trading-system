using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Worker;

/// <summary>One-shot local IO boundary for isolated H4 research; the calculation is pure Application code.</summary>
public sealed class H4StructureResearchCommand
{
    private const string Usage = "h4-structure-research --input <canonical-h4-csv> --sha256 <source-sha256> --requested-start-utc <UTC> --cutoff-utc <UTC> --provenance <reference> --output <new-directory> --profile <id:left:right> [--profile <id:left:right> ...] [--context-horizon <1|2> ...]";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 2 && args[1] == "--help") { output.WriteLine(Usage); return 0; }
        try
        {
            if (args.Count < 15 || args[0] != "h4-structure-research" || args[1] != "--input" ||
                args[3] != "--sha256" || args[5] != "--requested-start-utc" || args[7] != "--cutoff-utc" ||
                args[9] != "--provenance" || args[11] != "--output" ||
                !Path.IsPathFullyQualified(args[2]) || !Path.IsPathFullyQualified(args[12]) ||
                (args.Count - 13) % 2 != 0)
                throw new ArgumentException(Usage);
            var input = Path.GetFullPath(args[2]);
            var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[12]));
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new ArgumentException("Output must be a new directory.");
            using var source = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (source.Length is 0 or > 8_388_608) throw new ArgumentException("Source exceeds bounded 8 MiB policy.");
            var bytes = new byte[(int)source.Length];
            source.ReadExactly(bytes);
            if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(args[4], StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Bounded source SHA256 mismatch.");
            var start = ParseUtc(args[6]); var cutoff = ParseUtc(args[8]);
            using var reader = new StringReader(new System.Text.UTF8Encoding(false, true).GetString(bytes));
            var series = new CsvMarketDataImporter().Import(reader);
            if (series.Count > 10000) throw new ArgumentException("At most 10000 original H4 candles per report.");
            var calculator = new EvaluateH4MarketStructureCandidateUseCase();
            var profiles = new List<H4PivotProfile>();
            var horizons = new List<int>();
            for (var i = 13; i < args.Count; i += 2)
            {
                if (args[i] == "--context-horizon")
                {
                    if (args[i + 1] is not ("1" or "2"))
                        throw new ArgumentException("Context horizon must be 1 or 2.");
                    horizons.Add(args[i + 1] == "1" ? 1 : 2);
                    continue;
                }
                var pieces = args[i + 1].Split(':');
                if (args[i] != "--profile" || pieces.Length != 3 || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var left) ||
                    !int.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out var right))
                    throw new ArgumentException("Profiles must be id:left:right.");
                profiles.Add(new(pieces[0], left, right));
            }
            if (profiles.Count == 0 || profiles.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != profiles.Count ||
                horizons.Distinct().Count() != horizons.Count)
                throw new ArgumentException("Select at least one unique profile and avoid duplicate horizons.");
            var requests = profiles.Select(profile => new H4MarketStructureRequest(series, start, cutoff, profile, args[10])).ToArray();
            var observations = requests.Select(calculator.Execute).ToArray();
            var report = new
            {
                schemaVersion = 1,
                inputSha256 = args[4].ToLowerInvariant(),
                scope = "IsolatedExperimentalResearch_NotCanonicalStrategyEvaluation",
                canonicalAdmissionPermitted = false,
                operationalSignal = false,
                observations
            };
            var parent = Path.GetDirectoryName(destination) ?? throw new ArgumentException("Output parent required.");
            Directory.CreateDirectory(parent);
            var pending = Path.Combine(parent, ".h4-structure-pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending);
            File.WriteAllText(Path.Combine(pending, "observations.json"), JsonSerializer.Serialize(report, Json) + "\n");
            if (horizons.Count > 0)
            {
                var context = new EvaluateH4ContextCandidateUseCase();
                var contextReport = new
                {
                    report.schemaVersion,
                    report.inputSha256,
                    report.scope,
                    report.canonicalAdmissionPermitted,
                    report.operationalSignal,
                    observations = requests.SelectMany(request => horizons.Select(horizon => context.Execute(request, horizon))).ToArray()
                };
                File.WriteAllText(Path.Combine(pending, "context-observations.json"), JsonSerializer.Serialize(contextReport, Json) + "\n");
            }
            Directory.Move(pending, destination);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                destination,
                profiles = observations.Length,
                canonicalAdmissionPermitted = false,
                operationalSignal = false
            }, Json));
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or
                                          FormatException or OverflowException or MarketDataCsvImportException or JsonException or
                                          System.Text.DecoderFallbackException)
        {
            error.WriteLine("H4 structure research unavailable: " + exception.Message);
            return 2;
        }
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            parsed.Offset != TimeSpan.Zero || !value.EndsWith('Z'))
            throw new ArgumentException("Explicit UTC Z timestamp required.");
        return parsed;
    }
}
