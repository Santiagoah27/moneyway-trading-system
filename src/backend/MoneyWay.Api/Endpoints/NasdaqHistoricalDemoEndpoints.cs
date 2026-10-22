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
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(422).ProducesProblem(503);
        return endpoints;
    }

    private static async Task<IResult> Read(HttpContext context, IConfiguration configuration, ReadNasdaqHistoricalReport reader)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.Query.Count != 0)
            return Results.Problem(statusCode: 400, title: "This endpoint accepts no query parameters.");
        var path = configuration["NasdaqHistoricalDemo:ReportPath"];
        if (string.IsNullOrWhiteSpace(path)) return Unavailable(404, "ReportNotConfigured");
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            return Unavailable(503, "ReportConfigurationInvalid");
        try
        {
            return Results.Ok(await reader.ExecuteAsync(path, configuration["NasdaqHistoricalDemo:ExpectedSha256"], context.RequestAborted));
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
