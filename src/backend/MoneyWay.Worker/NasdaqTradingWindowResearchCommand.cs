using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

public sealed class NasdaqTradingWindowResearchCommand
{
    private const string Usage = "nasdaq-window-research --as-of-utc <UTC Z> [--event-utc <UTC Z>]";
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
        if (args.Count is not (3 or 5) || args[0] != "nasdaq-window-research" ||
            args[1] != "--as-of-utc" || (args.Count == 5 && args[3] != "--event-utc"))
        {
            error.WriteLine(Usage);
            return 2;
        }
        var cutoff = ParseUtc(args[2]);
        var at = args.Count == 5 ? ParseUtc(args[4]) : null;
        if (cutoff is null || (args.Count == 5 && at is null))
        {
            error.WriteLine("InvalidTimestamp: expected explicit ISO 8601 UTC with a Z suffix.");
            return 2;
        }
        output.WriteLine(JsonSerializer.Serialize(NasdaqTradingWindowAlignment.Evaluate(cutoff, at), Json));
        return 0;
    }

    private static DateTimeOffset? ParseUtc(string value) =>
        DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed : null;
}
