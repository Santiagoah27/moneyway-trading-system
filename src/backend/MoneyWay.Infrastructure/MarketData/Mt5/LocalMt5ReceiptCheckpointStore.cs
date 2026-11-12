using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

/// <summary>Explicit receipt commits on a local fixed NTFS volume. No host registration or source writes.</summary>
public sealed class LocalMt5ReceiptCheckpointStore : IMt5ReceiptCheckpointStore
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const string Schema = "moneyway.mt5-receipt-checkpoint";
    private static readonly byte[] LockMarker = Encoding.UTF8.GetBytes("moneyway.mt5-receipt-checkpoint-lock.v1\n");
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 32
    };
    private static readonly string[] Events = ["initial_snapshot", "new_native_m1", "source_recovery", "source_unavailable", "native_head_regressed", "incomplete_snapshot_retry", "shutdown"];
    private readonly string path;
    private readonly string source;
    private readonly string parent;
    private readonly string scope;
    private readonly LocalMt5CollectorReceiptReader reader = new();

    public LocalMt5ReceiptCheckpointStore(string checkpointPath, string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        if (!Path.IsPathFullyQualified(checkpointPath) || !Path.IsPathFullyQualified(sourceDirectory))
            throw new ArgumentException("Explicit absolute checkpoint and source paths are required.");
        if (OperatingSystem.IsWindows() && (!SafeWindowsPath(checkpointPath.Replace('/', '\\')) || !SafeWindowsPath(sourceDirectory.Replace('/', '\\'))))
            throw new ArgumentException("Use explicit local drive paths without device, short-name or trailing-dot aliases.");
        path = Path.GetFullPath(checkpointPath);
        source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        if (OperatingSystem.IsWindows() && (!SafeWindowsPath(path) || !SafeWindowsPath(source)))
            throw new ArgumentException("Use explicit local drive paths without device, short-name or trailing-dot aliases.");
        parent = Path.GetDirectoryName(path)!;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (path.Equals(source, comparison) || path.StartsWith(Path.EndsInDirectorySeparator(source) ? source : source + Path.DirectorySeparatorChar, comparison)
            || Path.GetFileName(path).IndexOfAny([':', '*', '?']) >= 0 || Path.GetFileName(path).Length > 120)
            throw new ArgumentException("Checkpoint must be a safe file outside the source evidence directory.");
        scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? source.ToUpperInvariant() : source)));
    }

    private static bool SafeWindowsPath(string value) => Regex.IsMatch(value, "^[A-Za-z]:\\\\")
        && value[3..].Split('\\').All(part => !part.Contains(':') && !part.Contains('~')
            && !part.EndsWith('.') && !part.EndsWith(' '));

    public Mt5CheckpointResult Load() => Execute(() =>
    {
        var stored = ReadStored();
        var codes = PendingCodes();
        if (stored is null) return Result(Mt5CheckpointStatus.Missing, codes: codes);
        var verification = reader.Read(source, stored.Checkpoint);
        foreach (var identity in stored.Checkpoint.Records.Values)
        {
            var observation = verification.Records.FirstOrDefault(r => r.FileName == identity.FileName)?.Observation;
            if (observation is not null && identity != ActualIdentity(observation))
                return Result(Mt5CheckpointStatus.Conflict, revision: stored.Revision, verification: verification,
                    codes: ["checkpoint_identity_disagrees_with_evidence"]);
        }
        return Result(Mt5CheckpointStatus.Loaded, stored.Checkpoint, stored.Revision, verification, codes);
    });

    public Mt5CheckpointResult Commit(Mt5ReceiptCheckpoint progress, string? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return Execute(() =>
        {
            ValidateProgress(progress);
            var stored = ReadStored();
            Require(stored?.Revision == expectedRevision, Mt5CheckpointStatus.Conflict, "checkpoint_revision_conflict_reload_required");
            if (stored is not null)
                foreach (var previous in stored.Checkpoint.Records.Values)
                    Require(progress.Records.TryGetValue(previous.FileName, out var candidate) && candidate == previous,
                        Mt5CheckpointStatus.Conflict, "checkpoint_regression_or_anchor_change");

            // A caller-created checkpoint is not proof: revalidate every acknowledged identity from source bytes.
            var verification = reader.Read(source, progress);
            if (verification.Diagnostics.Count > 0)
                return Result(Mt5CheckpointStatus.DataUnavailable, stored?.Checkpoint, stored?.Revision, verification,
                    ["source_inventory_unverified_inspect_receipt_diagnostics"]);
            foreach (var identity in progress.Records.Values)
            {
                var record = verification.Records.SingleOrDefault(r => r.FileName == identity.FileName);
                if (record?.Observation is null || record.Status is not (Mt5ReceiptStatus.Validated or Mt5ReceiptStatus.Unresolved))
                    return Result(Mt5CheckpointStatus.DataUnavailable, stored?.Checkpoint, stored?.Revision, verification,
                        ["acknowledged_evidence_unverified_inspect_receipt_diagnostics"]);
                Require(identity == ActualIdentity(record.Observation), Mt5CheckpointStatus.Conflict, "checkpoint_identity_disagrees_with_evidence");
            }
            var payload = ToPayload(progress);
            var bytes = Serialize(payload);
            if (stored is not null && bytes.AsSpan().SequenceEqual(stored.Bytes))
                return Result(Mt5CheckpointStatus.Unchanged, stored.Checkpoint, stored.Revision, verification, PendingCodes());

            var temporary = path + ".pending-" + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            // Same-directory publication; never fall back to truncate/copy/delete of the committed file.
            try
            {
                GuardPaths();
                var latest = ReadStored();
                Require(latest?.Revision == expectedRevision, Mt5CheckpointStatus.Conflict, "checkpoint_changed_before_publication");
                if (stored is null) File.Move(temporary, path, overwrite: false);
                else File.Replace(temporary, path, destinationBackupFileName: null);
                var published = ReadStored();
                Require(published is not null && published.Bytes.AsSpan().SequenceEqual(bytes),
                    Mt5CheckpointStatus.PublicationUncertain, "publication_uncertain_reload_and_verify");
                return Result(Mt5CheckpointStatus.Committed, published!.Checkpoint, published.Revision, verification, PendingCodes());
            }
            catch (IOException) { throw new StorageException(Mt5CheckpointStatus.PublicationUncertain, "publication_uncertain_reload_and_verify"); }
        });
    }

    private Mt5CheckpointResult Execute(Func<Mt5CheckpointResult> operation)
    {
        try
        {
            GuardPaths();
            using var gate = Lock();
            if (gate is null) return Result(Mt5CheckpointStatus.Busy, codes: ["checkpoint_busy_retry_or_reload"]);
            GuardPaths();
            return operation();
        }
        catch (StorageException e) { return Result(e.Status, codes: [e.Code]); }
        catch (Mt5WireException) { return Result(Mt5CheckpointStatus.Invalid, codes: ["invalid_checkpoint_structure"]); }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or ArgumentException or InvalidOperationException)
        { return Result(Mt5CheckpointStatus.Invalid, codes: ["invalid_checkpoint_structure"]); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { return Result(Mt5CheckpointStatus.DataUnavailable, codes: ["checkpoint_storage_unavailable_inspect_path_and_permissions"]); }
    }

    private void GuardPaths()
    {
        Require(OperatingSystem.IsWindows(), Mt5CheckpointStatus.DataUnavailable, "local_fixed_ntfs_required");
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        Require(drive.DriveType == DriveType.Fixed && drive.DriveFormat == "NTFS", Mt5CheckpointStatus.DataUnavailable, "local_fixed_ntfs_required");
        Require(Directory.Exists(parent), Mt5CheckpointStatus.DataUnavailable, "checkpoint_parent_missing_create_explicitly");
        GuardAncestors(parent);
        // Existing source ancestors are checked even when the source directory has gone away.
        for (DirectoryInfo? node = new(source); node is not null; node = node.Parent)
            if (node.Exists) Require(!node.Attributes.HasFlag(FileAttributes.ReparsePoint), Mt5CheckpointStatus.DataUnavailable, "unsafe_source_path");
        GuardLeaf(path); GuardLeaf(path + ".lock");
    }
    private static void GuardAncestors(string directory)
    {
        for (DirectoryInfo? node = new(directory); node is not null; node = node.Parent)
            Require(!node.Attributes.HasFlag(FileAttributes.ReparsePoint), Mt5CheckpointStatus.DataUnavailable, "unsafe_checkpoint_path");
    }
    private static void GuardLeaf(string file)
    {
        try
        {
            var attributes = File.GetAttributes(file);
            Require(!attributes.HasFlag(FileAttributes.ReparsePoint) && !attributes.HasFlag(FileAttributes.Directory),
                Mt5CheckpointStatus.DataUnavailable, "unsafe_checkpoint_path");
        }
        catch (FileNotFoundException) { }
    }
    private FileStream? Lock()
    {
        FileStream stream;
        bool created;
        try { stream = new(path + ".lock", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None); created = true; }
        catch (IOException)
        {
            try { stream = new(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None); created = false; }
            catch (IOException) { return null; }
        }
        try
        {
            if (created) { stream.Write(LockMarker); stream.Flush(flushToDisk: true); }
            else
            {
                Require(stream.Length == LockMarker.Length, Mt5CheckpointStatus.Invalid, "unrecognized_lock_file_preserved");
                var bytes = new byte[LockMarker.Length]; stream.ReadExactly(bytes);
                Require(bytes.AsSpan().SequenceEqual(LockMarker), Mt5CheckpointStatus.Invalid, "unrecognized_lock_file_preserved");
            }
            GuardPaths();
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private Stored? ReadStored()
    {
        GuardLeaf(path);
        FileStream stream;
        try { stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (FileNotFoundException) { return null; }
        using (stream)
        {
            Require(stream.Length is > 0 and <= MaxBytes, Mt5CheckpointStatus.Invalid, "invalid_checkpoint_size");
            var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
            using var document = Mt5WireContract.Document(bytes);
            var envelope = document.RootElement.Deserialize<Envelope>(Json)!;
            Require(envelope is not null && envelope.Schema == Schema && envelope.Version == 1,
                Mt5CheckpointStatus.Invalid, "unsupported_checkpoint_schema_or_version");
            var payload = envelope!.Payload;
            Require(payload is not null && envelope.PayloadSha256 == Hash(JsonSerializer.SerializeToUtf8Bytes(payload, Json)),
                Mt5CheckpointStatus.Invalid, "checkpoint_payload_digest_mismatch");
            Require(payload!.Company == Mt5WireContract.Company && payload.Server == Mt5WireContract.Server
                && payload.NativeSymbol == "US100" && payload.Demo && payload.DiagnosticOnly && !payload.CanonicalReady,
                Mt5CheckpointStatus.Invalid, "checkpoint_source_identity_or_readiness_mismatch");
            Require(payload.Records is not null && payload.Records.Length <= 4096,
                Mt5CheckpointStatus.Invalid, "invalid_checkpoint_records");
            var checkpoint = new Mt5ReceiptCheckpoint(payload.DirectoryScope, payload.Records!);
            ValidateProgress(checkpoint);
            var anchor = checkpoint.Records.Values.LastOrDefault();
            Require(payload.LastAcknowledgedSequence == (anchor?.Sequence ?? 0) && payload.LastAcknowledgedRecordSha256 == anchor?.Sha256,
                Mt5CheckpointStatus.Invalid, "checkpoint_anchor_mismatch");
            return new(checkpoint, Hash(bytes), bytes);
        }
    }
    private void ValidateProgress(Mt5ReceiptCheckpoint checkpoint)
    {
        Require(checkpoint.DirectoryScope == scope, Mt5CheckpointStatus.Invalid, "checkpoint_directory_scope_mismatch");
        long last = 0;
        var polls = new HashSet<(string, long)>();
        foreach (var item in checkpoint.Records.Values)
        {
            Require(item.Sequence is > 0 and <= 999999999999 && item.Sequence > last && Mt5WireContract.RecordName(item.FileName)
                && item.FileName == Mt5WireContract.RecordFile(item.Sequence) && Mt5WireContract.Digest(item.Sha256)
                && item.Sha256 == item.Sha256.ToLowerInvariant() && item.RunId is not null
                && Regex.IsMatch(item.RunId, "^run-[0-9]{12}\\z") && item.PollSequence > 0 && Events.Contains(item.Event),
                Mt5CheckpointStatus.Invalid, "invalid_checkpoint_record_identity");
            var start = long.Parse(item.RunId.AsSpan(4), CultureInfo.InvariantCulture);
            Require(start > 0 && start <= item.Sequence && (item.Event == "shutdown" || polls.Add((item.RunId!, item.PollSequence))),
                Mt5CheckpointStatus.Invalid, "invalid_or_duplicate_checkpoint_run_poll");
            last = item.Sequence;
        }
    }
    private static Payload ToPayload(Mt5ReceiptCheckpoint checkpoint)
    {
        var anchor = checkpoint.Records.Values.LastOrDefault();
        return new(Mt5WireContract.Company, Mt5WireContract.Server, "US100", true, true, false, checkpoint.DirectoryScope,
            checkpoint.Records.Values.ToArray(), anchor?.Sequence ?? 0, anchor?.Sha256);
    }
    private static byte[] Serialize(Payload payload) => JsonSerializer.SerializeToUtf8Bytes(new Envelope(Schema, 1, payload,
        Hash(JsonSerializer.SerializeToUtf8Bytes(payload, Json))), Json);
    private IReadOnlyList<string> PendingCodes()
    {
        // Orphans are never trusted, promoted, overwritten or deleted automatically.
        return Directory.EnumerateFileSystemEntries(parent, Path.GetFileName(path) + ".pending-*.tmp").Any()
            ? Array.AsReadOnly(new[] { "uncommitted_temporary_files_preserved_not_acknowledged" }) : Array.Empty<string>();
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static Mt5ReceivedRecordIdentity ActualIdentity(Mt5NativeAcquisition observation) => new(observation.RecordFile,
        observation.RecordSha256, observation.Metadata.Sequence, observation.Metadata.RunId, observation.Metadata.PollSequence, observation.Metadata.Event);
    private static void Require(bool condition, Mt5CheckpointStatus status, string code)
    { if (!condition) throw new StorageException(status, code); }
    private static Mt5CheckpointResult Result(Mt5CheckpointStatus status, Mt5ReceiptCheckpoint? checkpoint = null,
        string? revision = null, Mt5ReceiptBatch? verification = null, IReadOnlyList<string>? codes = null)
        => new(status, checkpoint, revision, verification, codes ?? Array.Empty<string>());
    private sealed class StorageException(Mt5CheckpointStatus status, string code) : Exception(code)
    { internal Mt5CheckpointStatus Status { get; } = status; internal string Code { get; } = code; }
    private sealed record Stored(Mt5ReceiptCheckpoint Checkpoint, string Revision, byte[] Bytes);
    private sealed record Envelope(string Schema, int Version, Payload Payload, string PayloadSha256);
    private sealed record Payload(string Company, string Server, string NativeSymbol, bool Demo, bool DiagnosticOnly,
        bool CanonicalReady, string DirectoryScope, Mt5ReceivedRecordIdentity[] Records,
        long LastAcknowledgedSequence, string? LastAcknowledgedRecordSha256);
}
