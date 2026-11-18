using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static class NasdaqExecutionEvidenceEndpoints
{
    private sealed record SaveRequest(Guid SubmissionId, NasdaqSupervisedExecutionEvidenceDraft Evidence, string? ExpectedRevision = null);
    private sealed record SessionOpenRequest(Guid SubmissionId, NasdaqSupervisedSessionOpen Session);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 16 };
    public static void MapNasdaqExecutionEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/nasdaq/execution-evidence", (HttpContext c, LocalNasdaqExecutionEvidenceStore s) => Execute(c, async () =>
        {
            var request = await Read<SaveRequest>(c);
            if (request.ExpectedRevision is not null) throw new ArgumentException("Creation cannot replace an existing revision.");
            var saved = s.Save(request.SubmissionId, request.Evidence);
            return Results.Created($"/api/nasdaq/execution-evidence/{saved.Record.RecordId}", saved);
        }));
        endpoints.MapPost("/api/nasdaq/execution-evidence/{id:guid}/revisions", (HttpContext c, Guid id, LocalNasdaqExecutionEvidenceStore s) => Execute(c, async () =>
        {
            var request = await Read<SaveRequest>(c);
            return Results.Ok(s.Save(request.SubmissionId, request.Evidence, id, request.ExpectedRevision));
        }));
        endpoints.MapGet("/api/nasdaq/execution-evidence/{id:guid}", (HttpContext c, Guid id, LocalNasdaqExecutionEvidenceStore s) => Execute(c, () =>
        {
            if (c.Request.Query.Any(p => p.Key != "revision" || p.Value.Count != 1)) throw new ArgumentException("Unsupported record query.");
            int? revision = c.Request.Query.TryGetValue("revision", out var value) ? int.Parse(value.ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;
            if (revision is <= 0 or > 100) throw new ArgumentException("Invalid revision.");
            return Task.FromResult<IResult>(Results.Ok(s.Get(id, revision)));
        }));
        endpoints.MapGet("/api/nasdaq/execution-evidence", (HttpContext c, LocalNasdaqExecutionEvidenceStore s) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 1 || !c.Request.Query.TryGetValue("sessionId", out var session) || session.Count != 1)
                throw new ArgumentException("Select a sessionId.");
            return Task.FromResult<IResult>(Results.Ok(s.List(session.ToString())));
        }));
        endpoints.MapPost("/api/nasdaq/supervised-sessions", (HttpContext c, LocalNasdaqExecutionEvidenceStore s) => Execute(c, async () =>
        {
            var request = await Read<SessionOpenRequest>(c);
            return Results.Ok(s.OpenSession(request.SubmissionId, request.Session));
        }));
        endpoints.MapGet("/api/nasdaq/supervised-sessions", (HttpContext c, LocalNasdaqExecutionEvidenceStore s) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 1 || !c.Request.Query.TryGetValue("tradingDay", out var date) || date.Count != 1)
                throw new ArgumentException("Explicit America/Bogota tradingDay required.");
            return Task.FromResult<IResult>(Results.Ok(s.Sessions(DateOnly.ParseExact(date.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))));
        }));
        endpoints.MapGet("/api/nasdaq/supervised-sessions/{key}", (HttpContext c, string key, LocalNasdaqExecutionEvidenceStore s) => Execute(c, () =>
            Task.FromResult<IResult>(Results.Ok(s.Session(key)))));
        endpoints.MapPost("/api/nasdaq/supervised-sessions/{key}/actions", (HttpContext c, string key, LocalNasdaqExecutionEvidenceStore s) => Execute(c, async () =>
            Results.Ok(s.SessionAction(key, await Read<NasdaqSupervisedSessionCommand>(c)))));
    }
    private static async Task<T> Read<T>(HttpContext c)
    {
        if (!c.Request.HasJsonContentType() || c.Request.Headers["X-MoneyWay-Local-Capture"] != "1")
            throw new ArgumentException("Explicit JSON local-capture request required.");
        var bytes = new byte[65537]; var length = 0;
        while (length < bytes.Length)
        { var count = await c.Request.Body.ReadAsync(bytes.AsMemory(length), c.RequestAborted); if (count == 0) break; length += count; }
        if (length > 65536) throw new ArgumentException("Evidence request exceeds 64 KiB.");
        using var doc = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 16 });
        static void Unique(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                var properties = node.EnumerateObject().ToArray();
                if (properties.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != properties.Length) throw new ArgumentException("Duplicate JSON fields are not allowed.");
                foreach (var property in properties) Unique(property.Value);
            }
            if (node.ValueKind == JsonValueKind.Array) foreach (var value in node.EnumerateArray()) Unique(value);
        }
        Unique(doc.RootElement);
        return JsonSerializer.Deserialize<T>(doc.RootElement, Json) ?? throw new ArgumentException("Missing evidence request.");
    }
    private static async Task<IResult> Execute(HttpContext c, Func<Task<IResult>> operation)
    {
        c.Response.Headers.CacheControl = "no-store";
        var origin = c.Request.Headers.Origin.ToString();
        if (c.Request.Host.Host is not ("localhost" or "127.0.0.1" or "::1" or "[::1]")
            || c.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip)
            || origin.Length > 0 && origin != "http://localhost:5173" && origin != $"{c.Request.Scheme}://{c.Request.Host}")
            return Problem(403, "LocalCaptureOnly", "Use the local supervised application.");
        try { return await operation(); }
        catch (NasdaqEvidenceConflictException e) { return Problem(409, "EvidenceConflict", e.Message); }
        catch (KeyNotFoundException) { return Problem(404, "EvidenceRecordMissing", "Record/revision not found."); }
        catch (ArgumentException e)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["evidence"] = [e.Message] },
            extensions: new Dictionary<string, object?> { ["code"] = "InvalidEvidence" });
        }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException)
        { return Problem(400, "InvalidEvidence", "Invalid evidence fields, exact decimal, source reference or revision. No evidence was replaced."); }
        catch (InvalidDataException) { return Problem(422, "EvidenceIntegrityFailure", "Stored evidence could not be validated. Original files preserved."); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { return Problem(503, "EvidenceStorageUnavailable", "Local persistence unavailable. Retry with the same submission ID; do not assume save succeeded."); }
    }
    private static IResult Problem(int status, string code, string detail) => Results.Problem(statusCode: status,
        title: "Supervised evidence capture", detail: detail, extensions: new Dictionary<string, object?> { ["code"] = code });
}
