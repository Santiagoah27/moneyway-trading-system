using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed partial class LocalNasdaqExecutionEvidenceStore
{
    private sealed record PreparationEnvelope(NasdaqProspectivePreparationRecord Record, string Sha256, int SchemaVersion = 1);
    private static string PreparationName(Guid id, int revision) => $"preparation-{id:N}.{revision:D3}.prep";
    private List<PreparationEnvelope> ReadPreparations()
    {
        var files = Directory.EnumerateFiles(root, "preparation-*.prep").Take(1001).ToArray();
        if (files.Length > 1000) throw new InvalidDataException("Bounded preparation store limit exceeded.");
        var all = new List<PreparationEnvelope>();
        foreach (var file in files)
        {
            Leaf(file); using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Invalid preparation size.");
            try
            {
                using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
                var envelope = JsonSerializer.Deserialize<PreparationEnvelope>(doc.RootElement, Json) ?? throw new InvalidDataException();
                var r = envelope.Record; var actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(doc.RootElement.GetProperty("record").GetRawText())));
                if (r is null || r.Evidence is null || r.Identity is null || envelope.SchemaVersion != 1 || envelope.Sha256 != actual || r.RecordId == Guid.Empty
                    || r.SubmissionId == Guid.Empty || r.Revision is < 1 or > 100 || r.SubmissionSequence < 1 || r.ReceivedAtUtc.Offset != TimeSpan.Zero
                    || r.RegisteredAtUtc.Offset != TimeSpan.Zero || r.RegisteredAtUtc < r.ReceivedAtUtc || Path.GetFileName(file) != PreparationName(r.RecordId, r.Revision))
                    throw new InvalidDataException("Invalid preparation identity/hash/timestamps.");
                r.Evidence.Validate(r.Identity); all.Add(envelope);
            }
            catch (Exception error) when (error is JsonException or ArgumentException or KeyNotFoundException or NullReferenceException)
            { throw new InvalidDataException("Stored preparation invalid; original files preserved.", error); }
        }
        if (all.Select(v => v.Record.SubmissionId).Distinct().Count() != all.Count) throw new InvalidDataException("Duplicate preparation submission.");
        var ordered = all.OrderBy(v => v.Record.SubmissionSequence).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            if (ordered[i].Record.SubmissionSequence != i + 1 || i > 0 && ordered[i].Record.RegisteredAtUtc < ordered[i - 1].Record.RegisteredAtUtc)
                throw new InvalidDataException("Invalid preparation publication sequence.");
        foreach (var group in all.GroupBy(v => v.Record.RecordId))
        {
            PreparationEnvelope? previous = null;
            foreach (var value in group.OrderBy(v => v.Record.Revision))
            {
                if (value.Record.Revision != (previous?.Record.Revision ?? 0) + 1 || value.Record.PreviousRevisionHash != previous?.Sha256
                    || previous is not null && (value.Record.Identity != previous.Record.Identity || value.Record.Evidence.SessionKey != previous.Record.Evidence.SessionKey
                        || value.Record.SubmissionSequence <= previous.Record.SubmissionSequence || value.Record.ReceivedAtUtc < previous.Record.RegisteredAtUtc))
                    throw new InvalidDataException("Broken or moved preparation revision chain.");
                previous = value;
            }
        }
        return all;
    }
    private static NasdaqProspectivePreparationView PreparationView(PreparationEnvelope value) => NasdaqProspectivePreparationProjection.View(value.Record, value.Sha256);
    private NasdaqSupervisedSessionState PreparationSession(string key)
    {
        var session = SessionState(ReadSessions(ReadAll()), key);
        if (session.Path != "Standard") throw new ArgumentException("Prospective preparation capture is Standard-only; WF2 remains independent.");
        return session;
    }
    public IReadOnlyList<NasdaqProspectivePreparationView> Preparations(string sessionKey)
    {
        using var gate = Lock(); var session = PreparationSession(sessionKey); var values = ReadPreparations();
        return values.Where(v => v.Record.Evidence.SessionKey == sessionKey).GroupBy(v => v.Record.RecordId).Select(g => g.MaxBy(v => v.Record.Revision)!)
            .OrderBy(v => v.Record.SubmissionSequence).Select(v => CheckedPreparation(v, session)).ToArray();
    }
    private static NasdaqProspectivePreparationView CheckedPreparation(PreparationEnvelope value, NasdaqSupervisedSessionState session)
    {
        if (value.Record.Identity != session.Identity || value.Record.Evidence.SessionKey != session.SessionKey) throw new InvalidDataException("Preparation session identity mismatch.");
        return PreparationView(value);
    }
    public NasdaqProspectivePreparationView Preparation(string key, Guid recordId, int? revision = null)
    {
        using var gate = Lock(); var session = PreparationSession(key);
        var records = ReadPreparations().Where(v => v.Record.RecordId == recordId && v.Record.Evidence.SessionKey == key);
        var value = revision is null ? records.MaxBy(v => v.Record.Revision) : records.SingleOrDefault(v => v.Record.Revision == revision);
        return CheckedPreparation(value ?? throw new KeyNotFoundException(), session);
    }
    public NasdaqProspectivePreparationView SavePreparation(Guid submissionId, NasdaqProspectivePreparationDraft draft, Guid? recordId = null, string? expectedRevision = null)
    {
        // Actual receipt of the fully parsed service request, before validation/locking/publication. Not source availability.
        var received = clock.GetUtcNow(); ArgumentNullException.ThrowIfNull(draft);
        if (received.Offset != TimeSpan.Zero || submissionId == Guid.Empty || recordId == Guid.Empty || recordId.HasValue != (expectedRevision is not null)) throw new ArgumentException("Explicit submission/revision identity and UTC receipt clock required.");
        using var gate = Lock(); var session = PreparationSession(draft.SessionKey); draft.Validate(session.Identity);
        draft = draft with { References = Array.AsReadOnly(draft.References.ToArray()) };
        var values = ReadPreparations(); var retry = values.SingleOrDefault(v => v.Record.SubmissionId == submissionId);
        if (retry is not null)
        {
            if (retry.Record.Evidence.SessionKey != session.SessionKey || (recordId is null ? retry.Record.Revision != 1 : retry.Record.RecordId != recordId)
                || retry.Record.PreviousRevisionHash != expectedRevision || JsonSerializer.Serialize(retry.Record.Evidence, Json) != JsonSerializer.Serialize(draft, Json))
                throw new NasdaqEvidenceConflictException("PreparationSubmissionIdentityConflict");
            return CheckedPreparation(retry, session);
        }
        var previous = recordId is null ? null : values.Where(v => v.Record.RecordId == recordId).MaxBy(v => v.Record.Revision);
        if (recordId.HasValue && previous is null) throw new KeyNotFoundException();
        if (previous?.Sha256 != expectedRevision) throw new NasdaqEvidenceConflictException("PreparationRevisionConflict_ReloadOriginal");
        if (previous is not null && (previous.Record.Identity != session.Identity || previous.Record.Evidence.SessionKey != session.SessionKey)) throw new ArgumentException("Preparation revisions cannot move session or strategy identity.");
        var registered = clock.GetUtcNow();
        if (registered.Offset != TimeSpan.Zero || registered < received || values.Any(v => v.Record.RegisteredAtUtc > received)) throw new IOException("Preparation receipt/publication clock order regressed; no publication.");
        if (values.Count >= 1000 || previous?.Record.Revision >= 100) throw new InvalidDataException("Bounded preparation revision limit reached.");
        var mode = session.Terminal ? "TerminalSession_DocumentaryOnly" : NasdaqSupervisedSessionPolicy.TradingDay(received) == session.Identity.TradingDay
            ? "CurrentBogotaDate_ReportOnly_NoCalendarOrAdmissionClaim" : "DifferentBogotaDate_RetrospectiveOrOtherSessionReport";
        var clockSource = ReferenceEquals(clock, TimeProvider.System) ? "TimeProvider.System.GetUtcNow_OrdinaryUnauthenticatedWallClock" : "InjectedTimeProvider_NotAuthenticated";
        var record = new NasdaqProspectivePreparationRecord(recordId ?? Guid.NewGuid(), (previous?.Record.Revision ?? 0) + 1, values.Count + 1,
            submissionId, session.Identity, draft, received, registered, clockSource, mode, previous?.Sha256);
        var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(record, Json)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PreparationEnvelope(record, hash), Json);
        if (bytes.Length > 65536) throw new ArgumentException("Preparation payload too large.");
        Publish(PreparationName(record.RecordId, record.Revision), bytes);
        return CheckedPreparation(ReadPreparations().Single(v => v.Record.SubmissionId == submissionId), session);
    }
}
