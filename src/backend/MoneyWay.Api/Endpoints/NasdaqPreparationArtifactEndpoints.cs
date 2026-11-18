using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.Api.Endpoints;

public static partial class NasdaqExecutionEvidenceEndpoints
{
    private sealed record ArtifactUploadRequest(Guid SubmissionId, int PreparationRevision, string ExpectedRevision,
        NasdaqPreparationArtifactAnnotation Source, byte[] Bytes);
    public static void MapNasdaqPreparationArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/nasdaq/supervised-sessions/{key}/preparation/{id:guid}/artifacts", (HttpContext c, string key, Guid id, LocalNasdaqExecutionEvidenceStore store) => Execute(c, async () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("No upload overrides supported.");
            var request = await Read<ArtifactUploadRequest>(c, 6 * 1024 * 1024);
            return Results.Ok(store.AttachPreparationArtifact(request.SubmissionId, key, id, request.PreparationRevision, request.ExpectedRevision, request.Source, request.Bytes));
        }));
        const string route = "/api/nasdaq/supervised-sessions/{key}/preparation/{id:guid}/revisions/{revision:int}/artifacts/{artifactId:guid}";
        endpoints.MapGet(route, (HttpContext c, string key, Guid id, int revision, Guid artifactId, LocalNasdaqExecutionEvidenceStore store) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("Select an exact immutable artifact only.");
            return Task.FromResult<IResult>(Results.Ok(store.PreparationArtifact(key, id, revision, artifactId).Metadata));
        }));
        endpoints.MapGet(route + "/content", (HttpContext c, string key, Guid id, int revision, Guid artifactId, LocalNasdaqExecutionEvidenceStore store) => Execute(c, () =>
        {
            if (c.Request.Query.Count != 0) throw new ArgumentException("No filesystem or presentation overrides supported.");
            var artifact = store.PreparationArtifact(key, id, revision, artifactId);
            c.Response.Headers.XContentTypeOptions = "nosniff";
            c.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
            return Task.FromResult<IResult>(Results.File(artifact.Bytes, "image/png", artifact.Metadata.Source.OriginalFilename, enableRangeProcessing: false));
        }));
    }
}
