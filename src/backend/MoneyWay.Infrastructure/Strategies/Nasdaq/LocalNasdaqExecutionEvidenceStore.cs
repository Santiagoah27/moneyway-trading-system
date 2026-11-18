using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed class NasdaqEvidenceConflictException(string message) : Exception(message);

/// <summary>Bounded local revision files. Uses the established checkpoint publication pattern, never its MT5 files or store.</summary>
public sealed partial class LocalNasdaqExecutionEvidenceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 32 };
    private readonly string root;
    private readonly TimeProvider clock;
    private sealed record Envelope(NasdaqSupervisedExecutionEvidenceRecord Record, string Sha256, int SchemaVersion = 1);
    public LocalNasdaqExecutionEvidenceStore(string directory, TimeProvider? timeProvider = null)
    {
        if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\", StringComparison.Ordinal) || directory.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Evidence storage requires a server-configured absolute local directory.");
        root = Path.GetFullPath(directory); clock = timeProvider ?? TimeProvider.System;
    }
    private static string Hash(NasdaqSupervisedExecutionEvidenceRecord value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private static void Guard(string path)
    {
        for (var p = new DirectoryInfo(path); p is not null; p = p.Parent)
            if (p.Exists && p.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Evidence directory must not contain links.");
    }
    private FileStream Lock()
    {
        Guard(root); Directory.CreateDirectory(root); Guard(root);
        var path = Path.Combine(root, "capture.lock"); Leaf(path);
        try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new NasdaqEvidenceConflictException("EvidenceStoreBusy_RetrySameSubmission"); }
    }
    private static void Leaf(string path)
    {
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked evidence files are not accepted.");
    }
    private List<Envelope> ReadAll()
    {
        var files = Directory.EnumerateFiles(root, "*.json").Take(1001).ToArray();
        if (files.Length > 1000) throw new InvalidDataException("Local capture limit reached; original files preserved.");
        var values = new List<Envelope>();
        foreach (var file in files)
        {
            Leaf(file);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Invalid evidence file size.");
            Envelope envelope;
            string payloadHash;
            try
            {
                using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
                envelope = JsonSerializer.Deserialize<Envelope>(document.RootElement, Json) ?? throw new InvalidDataException("Missing evidence envelope.");
                if (!document.RootElement.TryGetProperty("record", out var payload)) throw new InvalidDataException("Missing persisted payload.");
                payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.GetRawText())));
                if (envelope.Record?.Evidence is null) throw new InvalidDataException("Missing persisted record.");
                envelope.Record.Evidence.Validate();
            }
            catch (Exception e) when (e is JsonException or ArgumentException)
            { throw new InvalidDataException("Invalid stored evidence. Original files preserved.", e); }
            var r = envelope.Record;
            if (envelope.SchemaVersion != 1 || r.RecordId == Guid.Empty || r.SubmissionId == Guid.Empty || r.Revision is < 1 or > 100 || r.RegisteredAtUtc.Offset != TimeSpan.Zero
                || Path.GetFileName(file) != Name(r.RecordId, r.Revision) || payloadHash != envelope.Sha256)
                throw new InvalidDataException("Evidence integrity check failed.");
            values.Add(envelope);
        }
        if (values.Select(v => v.Record.SubmissionId).Distinct().Count() != values.Count) throw new InvalidDataException("Duplicate submission identity.");
        foreach (var group in values.GroupBy(v => v.Record.RecordId))
        {
            Envelope? previous = null; var revision = 0;
            foreach (var item in group.OrderBy(v => v.Record.Revision))
            {
                if (item.Record.Revision != ++revision || item.Record.PreviousRevision != previous?.Sha256
                    || item.Record.RegisteredAtUtc < previous?.Record.RegisteredAtUtc
                    || previous is not null && (item.Record.Evidence.SessionId != previous.Record.Evidence.SessionId || item.Record.Evidence.TradingDay != previous.Record.Evidence.TradingDay))
                    throw new InvalidDataException("Evidence revision chain is incomplete or conflicting.");
                previous = item;
            }
        }
        foreach (var value in values)
            if (value.Record.Evidence.ReportedOutcome is { } outcome && (outcome.ExecutionRecordId != value.Record.RecordId
                || outcome.ExecutionEvidenceRevision >= value.Record.Revision || !values.Any(anchor => anchor.Record.RecordId == outcome.ExecutionRecordId
                    && anchor.Record.Revision == outcome.ExecutionEvidenceRevision && anchor.Sha256 == outcome.ExecutionEvidenceRevisionHash)))
                throw new InvalidDataException("Reported outcome refers to a missing, future or foreign execution-evidence revision.");
        return values;
    }
    private static string Name(Guid id, int revision) => $"{id:N}.{revision:D3}.json";
    private static NasdaqSupervisedExecutionEvidenceView Project(Envelope value, List<Envelope> values) =>
        NasdaqSupervisedExecutionEvidenceProjection.View(value.Record, value.Sha256, value.Record.Evidence.ReportedOutcome is { } o
            ? values.Single(v => v.Record.RecordId == o.ExecutionRecordId && v.Record.Revision == o.ExecutionEvidenceRevision && v.Sha256 == o.ExecutionEvidenceRevisionHash).Record : null);
    public NasdaqSupervisedExecutionEvidenceView Save(Guid submissionId, NasdaqSupervisedExecutionEvidenceDraft draft, Guid? id = null, string? expectedRevision = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.Validate();
        if (draft.ReportedOutcome is { } report) draft = draft with { ReportedOutcome = report with { Costs = report.Costs is null ? null : Array.AsReadOnly(report.Costs.ToArray()) } };
        if (submissionId == Guid.Empty || id == Guid.Empty || id.HasValue != (expectedRevision is not null)) throw new ArgumentException("Explicit submission and expected revision identities are required.");
        using var gate = Lock(); var values = ReadAll();
        if (draft.CanonicalDiagnostic is { } diagnostic)
        {
            var session = SessionState(ReadSessions(values), diagnostic.SessionKey);
            if (session.Path != "Standard" || draft.Scenario != "Standard" || draft.SessionId != session.SessionId
                || draft.TradingDay != session.Identity.TradingDay || draft.OriginalSymbol != session.Identity.Symbol.Value
                || draft.SourceProviderId != session.Identity.ProviderId.Value)
                throw new ArgumentException("A linked diagnostic record cannot move to another session/source/instrument/path.");
        }
        var retry = values.SingleOrDefault(v => v.Record.SubmissionId == submissionId);
        if (retry is not null)
        {
            if ((id is not null && retry.Record.RecordId != id || id is null && retry.Record.Revision != 1)
                || retry.Record.PreviousRevision != expectedRevision || JsonSerializer.Serialize(retry.Record.Evidence, Json) != JsonSerializer.Serialize(draft, Json))
                throw new NasdaqEvidenceConflictException("SubmissionIdentityAlreadyUsedForDifferentEvidence");
            return Project(retry, values);
        }
        var previous = id is null ? null : values.Where(v => v.Record.RecordId == id).MaxBy(v => v.Record.Revision);
        if (id.HasValue && previous is null) throw new KeyNotFoundException();
        if (previous?.Sha256 != expectedRevision) throw new NasdaqEvidenceConflictException("RevisionConflict_ReloadBeforeCorrecting");
        if (previous?.Record.Evidence.CanonicalDiagnostic is { } pinned && pinned != draft.CanonicalDiagnostic)
            throw new NasdaqEvidenceConflictException("CanonicalDiagnosticReferenceImmutable_CreateSeparateRecordForAnotherContext");
        if (draft.ReportedOutcome is { } outcome && (id is null || outcome.ExecutionRecordId != id
            || !values.Any(anchor => anchor.Record.RecordId == id && anchor.Record.Revision == outcome.ExecutionEvidenceRevision
                && anchor.Sha256 == outcome.ExecutionEvidenceRevisionHash)))
            throw new ArgumentException("A reported outcome must reference an existing revision of this exact MoneyWay record.");
        if (previous is not null && (previous.Record.Evidence.SessionId != draft.SessionId || previous.Record.Evidence.TradingDay != draft.TradingDay))
            throw new ArgumentException("A revision cannot move the record to another session; create a separate record.");
        if (values.Count >= 1000 || previous?.Record.Revision >= 100) throw new InvalidDataException("Capture/revision limit reached; no original evidence overwritten.");
        var record = new NasdaqSupervisedExecutionEvidenceRecord(id ?? Guid.NewGuid(), (previous?.Record.Revision ?? 0) + 1,
            submissionId, clock.GetUtcNow(), previous?.Sha256, draft);
        if (record.RegisteredAtUtc < previous?.Record.RegisteredAtUtc) throw new IOException("Registration clock regressed; no revision was published.");
        var envelope = new Envelope(record, Hash(record));
        _ = Project(envelope, values);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Json);
        if (bytes.Length > 65536) throw new ArgumentException("Evidence record is too large.");
        Publish(Name(record.RecordId, record.Revision), bytes);
        var published = ReadAll();
        return Project(published.Single(v => v.Record.SubmissionId == submissionId), published);
    }
    public NasdaqSupervisedExecutionEvidenceView Get(Guid id, int? revision = null)
    {
        using var gate = Lock(); var all = ReadAll(); var versions = all.Where(v => v.Record.RecordId == id && (revision is null || v.Record.Revision == revision));
        var value = versions.MaxBy(v => v.Record.Revision) ?? throw new KeyNotFoundException();
        return Project(value, all);
    }
    private void Publish(string name, byte[] bytes)
    {
        var temporary = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N"));
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        Guard(root); File.Move(temporary, Path.Combine(root, name), overwrite: false);
    }
    public IReadOnlyList<NasdaqSupervisedExecutionEvidenceView> List(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 2048) throw new ArgumentException("Explicit session identity required.");
        using var gate = Lock();
        var all = ReadAll();
        return all.Where(v => v.Record.Evidence.SessionId == sessionId).GroupBy(v => v.Record.RecordId)
            .Select(g => g.MaxBy(v => v.Record.Revision)!).OrderBy(v => v.Record.RegisteredAtUtc).ThenBy(v => v.Record.RecordId)
            .Select(v => Project(v, all)).ToArray();
    }
}
