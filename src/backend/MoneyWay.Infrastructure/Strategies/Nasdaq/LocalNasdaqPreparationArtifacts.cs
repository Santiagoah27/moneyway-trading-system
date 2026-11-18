using System.Security.Cryptography;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed partial class LocalNasdaqExecutionEvidenceStore
{
    private static string ArtifactName(Guid id) => $"artifact-{id:N}.png";
    private static string ArtifactHash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void ValidatePreparationArtifacts(List<PreparationEnvelope> records)
    {
        foreach (var envelope in records)
        {
            var r = envelope.Record;
            if (r.Artifacts is null || r.Artifacts.Count > 8 || r.Artifacts.Any(a => a is null)
                || r.Artifacts.Select(a => a.ArtifactId).Distinct().Count() != r.Artifacts.Count) throw new InvalidDataException("Invalid artifact collection.");
            foreach (var a in r.Artifacts)
            {
                if (a.ArtifactId == Guid.Empty || a.PreparationRecordId != r.RecordId || a.SessionKey != r.Evidence.SessionKey
                    || a.AttachedInRevision < 2 || a.AttachedInRevision > r.Revision || a.ByteLength is <= 0 or > NasdaqPreparationPng.MaximumBytes
                    || a.Sha256 is null || a.Sha256.Length != 64 || a.Sha256.Any(c => !char.IsAsciiHexDigitLower(c))
                    || a.ReceivedAtUtc.Offset != TimeSpan.Zero || a.ReceivedAtUtc > r.RegisteredAtUtc || a.Source is null)
                    throw new InvalidDataException("Invalid artifact metadata identity.");
                var owner = records.SingleOrDefault(v => v.Record.RecordId == r.RecordId && v.Record.Revision == a.AttachedInRevision);
                if (owner is null || a.ArtifactId != owner.Record.SubmissionId || owner.Record.ReceivedAtUtc != a.ReceivedAtUtc || owner.Record.ReceiptClockSource != a.ReceiptClockSource
                    || owner.Record.Artifacts.LastOrDefault() != a) throw new InvalidDataException("Broken artifact origin revision.");
                a.Source.Validate(owner.Record);
            }
            if (r.Revision > 1)
            {
                var previous = records.Single(v => v.Record.RecordId == r.RecordId && v.Record.Revision == r.Revision - 1).Record;
                if (r.Artifacts.Count < previous.Artifacts.Count || r.Artifacts.Count > previous.Artifacts.Count + 1
                    || !r.Artifacts.Take(previous.Artifacts.Count).SequenceEqual(previous.Artifacts)) throw new InvalidDataException("Artifact history cannot be replaced or removed.");
            }
        }
    }
    private byte[] ReadArtifactBytes(NasdaqPreparationArtifact artifact)
    {
        var path = Path.Combine(root, ArtifactName(artifact.ArtifactId)); Leaf(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length != artifact.ByteLength || file.Length > NasdaqPreparationPng.MaximumBytes) throw new InvalidDataException("Artifact byte length mismatch.");
        var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
        if (ArtifactHash(bytes) != artifact.Sha256) throw new InvalidDataException("Artifact SHA256 mismatch.");
        return bytes;
    }
    private void PublishArtifact(NasdaqPreparationArtifact artifact, byte[] bytes)
    {
        var path = Path.Combine(root, ArtifactName(artifact.ArtifactId)); Leaf(path);
        if (File.Exists(path)) { _ = ReadArtifactBytes(artifact); return; } // Retry after interruption before the preparation commit.
        if (Directory.EnumerateFiles(root, "artifact-*.png").Take(129).Count() >= 128) throw new InvalidDataException("Bounded local screenshot store is full.");
        Publish(ArtifactName(artifact.ArtifactId), bytes);
    }
    public NasdaqProspectivePreparationView AttachPreparationArtifact(Guid submissionId, string key, Guid recordId, int preparationRevision,
        string expectedRevision, NasdaqPreparationArtifactAnnotation source, byte[] bytes)
    {
        var received = clock.GetUtcNow();
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(bytes);
        bytes = bytes.ToArray(); NasdaqPreparationPng.Validate(bytes);
        var basis = Preparation(key, recordId, preparationRevision);
        if (basis.RevisionHash != expectedRevision) throw new NasdaqEvidenceConflictException("ArtifactBaseRevisionMismatch");
        source.Validate(basis.Record);
        return SavePreparationCore(submissionId, basis.Record.Evidence with { RevisionReason = source.RevisionReason }, recordId, expectedRevision, source, bytes, received);
    }
    public (NasdaqPreparationArtifact Metadata, byte[] Bytes) PreparationArtifact(string key, Guid recordId, int revision, Guid artifactId)
    {
        using var gate = Lock(); var session = PreparationSession(key);
        var value = ReadPreparations().SingleOrDefault(v => v.Record.RecordId == recordId && v.Record.Revision == revision && v.Record.Evidence.SessionKey == key)
            ?? throw new KeyNotFoundException();
        _ = CheckedPreparation(value, session);
        var artifact = value.Record.Artifacts.SingleOrDefault(a => a.ArtifactId == artifactId) ?? throw new KeyNotFoundException();
        return (artifact, ReadArtifactBytes(artifact));
    }
}
