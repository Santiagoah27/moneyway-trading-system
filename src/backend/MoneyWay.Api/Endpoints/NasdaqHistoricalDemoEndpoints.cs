using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static class NasdaqHistoricalDemoEndpoints
{
    public static IEndpointRouteBuilder MapNasdaqHistoricalDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/nasdaq/historical-demo", Read)
            .WithName("GetNasdaqHistoricalDemo")
            .WithSummary("Read the configured local canonical historical report")
            .Produces<NasdaqHistoricalReportSummary>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(422).ProducesProblem(503);
        return endpoints;
    }

    private static async Task<IResult> Read(HttpContext context, IConfiguration configuration, ReadNasdaqHistoricalReport reader)
    {
        context.Response.Headers.CacheControl = "no-store";
        var query = context.Request.Query;
        if (query.Any(p => p.Key is not ("frameStep" or "reportSha256") || p.Value.Count != 1))
            return Unavailable(400, "InvalidFrameSelection");
        long? frameStep = null;
        if (query.TryGetValue("frameStep", out var step))
        {
            if (!long.TryParse(step[0], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                return Unavailable(400, "InvalidFrameSelection");
            frameStep = parsed;
        }
        var reportHash = query["reportSha256"].FirstOrDefault();
        if (reportHash is not null && (reportHash.Length != 64 || !reportHash.All(char.IsAsciiHexDigit)))
            return Unavailable(400, "InvalidReportIdentity");
        var path = configuration["NasdaqHistoricalDemo:ReportPath"];
        if (string.IsNullOrWhiteSpace(path)) return Unavailable(404, "ReportNotConfigured");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            return Unavailable(503, "ReportConfigurationInvalid");
        try
        {
            var report = await reader.ExecuteAsync(path, configuration["NasdaqHistoricalDemo:ExpectedSha256"], context.RequestAborted, frameStep);
            if (reportHash is not null && !string.Equals(reportHash, report.ReportSha256, StringComparison.OrdinalIgnoreCase))
                return Unavailable(409, "ReportChanged");
            if (frameStep is not null && report.SelectedFrame is null)
                return Unavailable(404, "FrameNotInSelectedSession");
            return Results.Ok(report);
        }
        catch (FileNotFoundException) { return Unavailable(404, "ReportMissing"); }
        catch (DirectoryNotFoundException) { return Unavailable(404, "ReportMissing"); }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or KeyNotFoundException
            or InvalidOperationException or FormatException or OverflowException)
        { return Unavailable(422, "ReportInvalidOrUnsupported"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return Unavailable(503, "ReportInaccessible"); }
    }

    private static IResult Unavailable(int status, string code) => Results.Problem(statusCode: status,
        title: "Historical report unavailable", extensions: new Dictionary<string, object?> { ["code"] = code });
}
