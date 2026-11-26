using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static class NasdaqReceiptCutEndpoints
{
    private const string Route = "/api/nasdaq/historical-demo/receipt-cuts";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static void MapNasdaqReceiptCutEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, (HttpContext context, IConfiguration configuration, ReadNasdaqReceiptCutReport reader) =>
            Read(context, configuration, reader, null));
        endpoints.MapGet(Route + "/{id}", (HttpContext context, IConfiguration configuration, ReadNasdaqReceiptCutReport reader, string id) =>
            Read(context, configuration, reader, id));
    }

    private static async Task<IResult> Read(HttpContext context, IConfiguration configuration, ReadNasdaqReceiptCutReport reader, string? id)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!NasdaqExecutionEvidenceEndpoints.IsLocalEvidenceRequest(context))
            return Problem(403, "LocalDevelopmentOnly");
        if (context.Request.Query.Count != 0 || id is not null && !ValidId(id))
            return Problem(400, "InvalidReceiptCutSelection");

        string root; IReadOnlyList<NasdaqReceiptCutRegistration> registrations;
        try { (root, registrations) = Registry(configuration); }
        catch (IOException) { return Problem(503, "ReceiptCutConfigurationInvalid"); }
        if (id is null)
            return Results.Ok(registrations.OrderBy(r => r.Sequence).ThenBy(r => r.Id, StringComparer.Ordinal)
                .Select(r => new { r.Id, r.Sequence, reportSha256 = r.Sha256 }).ToArray());
        var match = registrations.SingleOrDefault(r => r.Id == id);
        if (match is null) return Problem(404, "ReceiptCutNotConfigured");
        try
        {
            return Results.Json(await reader.ReadAsync(root, match, context.RequestAborted), Json);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return Problem(404, "ReceiptCutMissing");
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or KeyNotFoundException
            or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return Problem(422, "ReceiptCutIntegrityFailure");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Problem(503, "ReceiptCutUnavailable");
        }
    }

    internal static (string Root, IReadOnlyList<NasdaqReceiptCutRegistration> Reports) Registry(IConfiguration configuration)
    {
        var registry = configuration.GetSection("NasdaqReceiptCuts");
        var entries = registry.GetSection("Reports").GetChildren().Take(33).ToArray();
        var root = registry["DevelopmentDirectory"];
        if (entries.Length == 0 && string.IsNullOrWhiteSpace(root)) return (string.Empty, []);
        if (string.IsNullOrWhiteSpace(root) || entries.Length > 32) throw new IOException("Invalid receipt-cut registry.");
        var registrations = new List<NasdaqReceiptCutRegistration>();
        foreach (var entry in entries)
        {
            var file = entry["File"]; var hash = entry["Sha256"]; var sessionKey = entry["StandardSessionKey"];
            if (!ValidId(entry.Key) || !ReadNasdaqReceiptCutReport.IsDigest(hash)
                || sessionKey is not null && (sessionKey.Length != 64 || !sessionKey.All(char.IsAsciiHexDigitLower))
                || file is not { Length: > 5 and <= 128 } || !file.EndsWith(".json", StringComparison.Ordinal)
                || !file.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
                || !long.TryParse(entry["Sequence"], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                || sequence is <= 0 or > 999999999999)
                throw new IOException("Invalid receipt-cut registry entry.");
            registrations.Add(new(entry.Key, file, sequence, hash!.ToLowerInvariant(), sessionKey));
        }
        return (root, registrations);
    }

    internal static bool ValidId(string id) => id.Length is > 0 and <= 48
        && id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '-' or '_');

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "Receipt-cut diagnostic unavailable", extensions: new Dictionary<string, object?> { ["code"] = code });
}
