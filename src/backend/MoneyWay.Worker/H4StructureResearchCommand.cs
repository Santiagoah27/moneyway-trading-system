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
    private const string Usage = "h4-structure-research --input <canonical-h4-csv> --sha256 <source-sha256> --requested-start-utc <UTC> --cutoff-utc <UTC> --provenance <reference> --output <new-directory> --profile <id:left:right> [--profile <id:left:right> ...]";
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
                (args.Count - 13) % 2 != 0 || args.Skip(13).Where((_, index) => index % 2 == 0).Any(value => value != "--profile"))
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
            var observations = new List<H4MarketStructureObservation>();
            for (var i = 13; i < args.Count; i += 2)
            {
                var pieces = args[i + 1].Split(':');
                if (pieces.Length != 3 || !int.TryParse(pieces[1], NumberStyles.None, CultureInfo.InvariantCulture, out var left) ||
                    !int.TryParse(pieces[2], NumberStyles.None, CultureInfo.InvariantCulture, out var right))
                    throw new ArgumentException("Profiles must be id:left:right.");
                observations.Add(calculator.Execute(new(series, start, cutoff,
                    new(pieces[0], left, right), args[10])));
            }
            if (observations.Select(o => o.Profile.Id).Distinct(StringComparer.Ordinal).Count() != observations.Count)
                throw new ArgumentException("Profile identifiers must be unique.");
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
            Directory.Move(pending, destination);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                destination,
                profiles = observations.Count,
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
