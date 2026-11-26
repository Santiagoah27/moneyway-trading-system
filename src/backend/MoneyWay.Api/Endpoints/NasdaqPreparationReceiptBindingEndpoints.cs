using System.Text.Json;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static partial class NasdaqExecutionEvidenceEndpoints
{
    private sealed record PreparationReceiptBindingRequest(string PreparationRevisionSha256, string ReceiptCutId, string ReceiptCutReportSha256);

    public static void MapNasdaqPreparationReceiptBindingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        const string route = "/api/nasdaq/supervised-sessions/{key}/preparation/{id:guid}/revisions/{revision:int}/receipt-cuts";
        endpoints.MapGet(route, (HttpContext c, string key, Guid id, int revision, IConfiguration config,
            LocalNasdaqExecutionEvidenceStore store) => Execute(c, () =>
        {
            ValidateBindingSelection(c, revision);
            var bindings = store.PreparationReceiptBindings(key, id, revision);
            var availableCuts = NasdaqReceiptCutEndpoints.Registry(config).Reports.Where(r => r.StandardSessionKey == key)
                .OrderBy(r => r.Sequence).ThenBy(r => r.Id, StringComparer.Ordinal)
                .Select(r => new { r.Id, r.Sequence, reportSha256 = r.Sha256 }).ToArray();
            return Task.FromResult<IResult>(Results.Ok(new { bindings, availableCuts }));
        }));
        endpoints.MapPost(route, (HttpContext c, string key, Guid id, int revision, IConfiguration config,
            LocalNasdaqExecutionEvidenceStore store, ReadNasdaqReceiptCutReport reader) => Execute(c, async () =>
        {
            ValidateBindingSelection(c, revision);
            var request = await Read<PreparationReceiptBindingRequest>(c);
            if (request.ReceiptCutId is null || !NasdaqReceiptCutEndpoints.ValidId(request.ReceiptCutId)
                || !ReadNasdaqReceiptCutReport.IsDigest(request.PreparationRevisionSha256)
                || !ReadNasdaqReceiptCutReport.IsDigest(request.ReceiptCutReportSha256))
                throw new ArgumentException("Select a registered cut and exact preparation and report SHA-256 pins.");
            var (root, reports) = NasdaqReceiptCutEndpoints.Registry(config);
            var registration = reports.SingleOrDefault(r => r.Id == request.ReceiptCutId) ?? throw new KeyNotFoundException();
            if (registration.StandardSessionKey != key)
                throw new NasdaqEvidenceConflictException("ReceiptCutNotRegisteredForExactStandardSession");
            if (registration.Sha256 != request.ReceiptCutReportSha256.ToLowerInvariant())
                throw new NasdaqEvidenceConflictException("PreparationBindingReceiptCutReportHashMismatch");
            NasdaqReceiptCutReportView report;
            try { report = await reader.ReadAsync(root, registration, c.RequestAborted); }
            catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
            { throw new InvalidDataException("Registered receipt-cut identity or contract is invalid.", e); }
            return Results.Ok(store.BindPreparationReceiptCut(key, id, revision, request.PreparationRevisionSha256.ToLowerInvariant(), report));
        }));
    }

    private static void ValidateBindingSelection(HttpContext context, int revision)
    {
        if (context.Request.Query.Count != 0 || revision is < 1 or > 100)
            throw new ArgumentException("Use the exact preparation revision, with no source paths or query overrides.");
    }
}
