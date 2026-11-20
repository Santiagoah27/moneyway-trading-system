using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Worker;

public sealed class M5FvgResearchCommand
{
    private const string Usage = "m5-fvg-research --input <absolute-csv> --sha256 <sha256> --start-utc <UTC> --cutoff-utc <UTC> --provenance <source> --profile <GeometryOnly|MiddleCloseConfirmation> [--profile <other-profile>]";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 2 && args[1] == "--help")
        {
            output.WriteLine(Usage);
            return 0;
        }

        try
        {
            if (args.Count is not (13 or 15) || args[0] != "m5-fvg-research" || args[1] != "--input" ||
                args[3] != "--sha256" || args[5] != "--start-utc" || args[7] != "--cutoff-utc" ||
                args[9] != "--provenance" || !Path.IsPathFullyQualified(args[2]))
                throw new ArgumentException(Usage);
            var profiles = new List<M5FvgProfile>();
            for (var i = 11; i < args.Count; i += 2)
            {
                if (args[i] != "--profile" || args[i + 1] is not ("GeometryOnly" or "MiddleCloseConfirmation"))
                    throw new ArgumentException(Usage);
                profiles.Add(Enum.Parse<M5FvgProfile>(args[i + 1]));
            }
            if (profiles.Distinct().Count() != profiles.Count)
                throw new ArgumentException("Profile selections must be distinct.");

            var start = ParseUtc(args[6]);
            var cutoff = ParseUtc(args[8]);
            // The report contains an observation per active zone per candle.
            if (cutoff - start > TimeSpan.FromMinutes(5 * 1000))
                throw new ArgumentException("A CLI report covers at most 1000 nominal M5 intervals.");
            using var source = new FileStream(args[2], FileMode.Open, FileAccess.Read, FileShare.Read);
            if (source.Length is 0 or > 8_388_608) throw new ArgumentException("Source exceeds the bounded 8 MiB policy.");
            var bytes = new byte[(int)source.Length];
            source.ReadExactly(bytes);
            var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!digest.Equals(args[4], StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Source SHA256 mismatch.");

            using var reader = new StringReader(new UTF8Encoding(false, true).GetString(bytes));
            var series = new CsvMarketDataImporter().Import(reader);
            var evaluator = new EvaluateM5FvgCandidateUseCase();
            var reports = profiles.Select(profile => evaluator.Execute(new(series, start, cutoff, profile, args[10]))).ToArray();
            output.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, inputSha256 = digest, reports }, Json));
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or
                                          FormatException or OverflowException or MarketDataCsvImportException)
        {
            error.WriteLine("M5 FVG research unavailable: " + exception.Message);
            return 2;
        }
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            throw new ArgumentException("An explicit ISO 8601 UTC Z timestamp is required.");
        return parsed;
    }
}
