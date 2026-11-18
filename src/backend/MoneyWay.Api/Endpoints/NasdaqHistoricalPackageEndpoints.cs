using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static partial class NasdaqExecutionEvidenceEndpoints
{
    public static void MapNasdaqHistoricalPackageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/nasdaq/historical-packages", (HttpContext c, ReadNasdaqHistoricalEvidencePackages packages) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("Select registered package IDs only.");
            return Task.FromResult<IResult>(Results.Ok(packages.List()));
        }));
        endpoints.MapGet("/api/nasdaq/historical-packages/{id}", (HttpContext c, string id, ReadNasdaqHistoricalEvidencePackages packages) => Execute(c, async () =>
        {
            if (c.Request.Query.Any(p => p.Key is not ("frameStep" or "manifestSha256") || p.Value.Count != 1)) throw new ArgumentException("No caller paths or admission flags supported.");
            long? step = c.Request.Query.TryGetValue("frameStep", out var s) ? long.Parse(s.ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;
            var hash = c.Request.Query.TryGetValue("manifestSha256", out var digest) ? digest.ToString() : null;
            return Results.Ok(await packages.InspectAsync(id, step, c.RequestAborted, hash));
        }));
        endpoints.MapGet("/api/nasdaq/historical-packages/{id}/diagnostic", (HttpContext c, string id, ReadNasdaqHistoricalEvidencePackages packages) => Execute(c, async () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("No admission override supported.");
            var inspection = await packages.InspectAsync(id, cancellationToken: c.RequestAborted);
            return Results.Conflict(new
            {
                code = "HistoricalPackageCanonicalAdmissionUnavailable",
                inspection.State,
                inspection.AdmissionBlockers,
                inspection.InvalidReasons,
                inspection.CanonicalReplayPermitted,
                diagnosticReference = (object?)null
            });
        }));
    }
}
