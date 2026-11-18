using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static partial class NasdaqExecutionEvidenceEndpoints
{
    private sealed record PreparationSaveRequest(Guid SubmissionId, NasdaqProspectivePreparationDraft Evidence, string? ExpectedRevision = null);
    public static void MapNasdaqPreparationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/nasdaq/supervised-sessions/{key}/preparation", (HttpContext c, string key, LocalNasdaqExecutionEvidenceStore store) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("No receipt or admission overrides supported.");
            var records = store.Preparations(key);
            var session = store.Session(key);
            return Task.FromResult<IResult>(Results.Ok(new
            {
                session,
                status = records.Count == 0 ? "NoPreparationRecorded" : "DocumentaryPreparationRecorded",
                records,
                admissionResult = "DataUnavailable",
                selectorResult = "NotEvaluated_AdmissibleSourceAvailabilityAndCanonicalMarketContextMissing",
                admissibleCompletionObservationCount = NasdaqProspectivePreparationProjection.AdmissibleInputs(session.Identity).Observations.Count,
                selectionPolicy = "DocumentsRemainSeparate_NoLatestWinsOrMergedCanonicalAssertion"
            }));
        }));
        endpoints.MapGet("/api/nasdaq/supervised-sessions/{key}/preparation/{id:guid}", (HttpContext c, string key, Guid id, LocalNasdaqExecutionEvidenceStore store) => Execute(c, () =>
        {
            if (c.Request.Query.Any(p => p.Key != "revision" || p.Value.Count != 1)) throw new ArgumentException("Select an immutable preparation revision only.");
            int? revision = c.Request.Query.TryGetValue("revision", out var r) ? int.Parse(r.ToString(), System.Globalization.CultureInfo.InvariantCulture) : null;
            if (revision is <= 0 or > 100) throw new ArgumentException("Invalid preparation revision.");
            return Task.FromResult<IResult>(Results.Ok(store.Preparation(key, id, revision)));
        }));
        endpoints.MapPost("/api/nasdaq/supervised-sessions/{key}/preparation", (HttpContext c, string key, LocalNasdaqExecutionEvidenceStore store) => Execute(c, async () =>
        {
            var request = await Read<PreparationSaveRequest>(c); ArgumentNullException.ThrowIfNull(request.Evidence);
            if (key != request.Evidence.SessionKey || request.ExpectedRevision is not null) throw new ArgumentException("Exact session required; creation cannot replace a record.");
            return Results.Ok(store.SavePreparation(request.SubmissionId, request.Evidence));
        }));
        endpoints.MapPost("/api/nasdaq/supervised-sessions/{key}/preparation/{id:guid}/revisions", (HttpContext c, string key, Guid id, LocalNasdaqExecutionEvidenceStore store) => Execute(c, async () =>
        {
            var request = await Read<PreparationSaveRequest>(c); ArgumentNullException.ThrowIfNull(request.Evidence);
            if (key != request.Evidence.SessionKey) throw new ArgumentException("Exact preparation session required.");
            return Results.Ok(store.SavePreparation(request.SubmissionId, request.Evidence, id, request.ExpectedRevision));
        }));
    }
}
