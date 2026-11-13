using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

/// <summary>Bounded, synchronous, read-only directory snapshot. No service registration or hidden receipt state.</summary>
public sealed class LocalMt5CollectorReceiptReader : IMt5CollectorReceiptReader
{
    public const int MaxRecords = 1024;
    public const int MaxDirectoryEntries = 8192;
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxSnapshotBytes = 64 * 1024 * 1024;

    public Mt5ReceiptBatch Read(string sourceDirectory, Mt5ReceiptCheckpoint? checkpoint = null,
        IReadOnlyDictionary<string, string>? expectedRecordHashes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        string root;
        try { root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        { throw new ArgumentException("Invalid source directory configuration.", nameof(sourceDirectory)); }
        var scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root)));
        if (checkpoint is not null && checkpoint.DirectoryScope != scope)
            throw new ArgumentException("Checkpoint belongs to another source-directory scope.", nameof(checkpoint));
        var ledger = checkpoint?.Records.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal) ?? new(StringComparer.Ordinal);
        foreach (var item in ledger.Values)
            if (!Mt5WireContract.RecordName(item.FileName) || !Mt5WireContract.Digest(item.Sha256) || item.FileName != Mt5WireContract.RecordFile(item.Sequence)
                || !System.Text.RegularExpressions.Regex.IsMatch(item.RunId, "^run-[0-9]{12}\\z") || item.PollSequence <= 0)
                throw new ArgumentException("Invalid receipt checkpoint identity.", nameof(checkpoint));
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in expectedRecordHashes ?? new Dictionary<string, string>())
        {
            if (!Mt5WireContract.RecordName(pair.Key) || !Mt5WireContract.Digest(pair.Value))
                throw new ArgumentException("Expected digests require canonical record names and SHA256 values.", nameof(expectedRecordHashes));
            expected.Add(pair.Key, pair.Value.ToLowerInvariant());
            if (expected.Count > MaxRecords) throw new ArgumentException("Expected record map exceeds technical bound.", nameof(expectedRecordHashes));
        }
        var diagnostics = new List<string>();
        var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
        using var files = new SnapshotFiles(root);
        try
        {
            if (!Directory.Exists(root)) return Empty("source_directory_unavailable");
            GuardRoot(root);
            var inventory = Directory.EnumerateFileSystemEntries(root).Take(MaxDirectoryEntries + 1).Select(Path.GetFileName).ToArray();
            if (inventory.Length > MaxDirectoryEntries) return Empty("directory_entry_limit");
            var names = inventory.Where(x => x is not null && x.StartsWith("record-", StringComparison.OrdinalIgnoreCase)).Cast<string>().Order(StringComparer.Ordinal).ToArray();
            if (names.Length > MaxRecords) return Empty("record_count_limit");
            if (names.Any(n => !Mt5WireContract.RecordName(n))) diagnostics.Add("unexpected_record_name_not_echoed");
            foreach (var name in names.Where(Mt5WireContract.RecordName)) entries.Add(name, new(name));
            foreach (var entry in entries.Values)
            {
                try
                {
                    var file = files.Read(entry.Name); entry.Hash = file.Hash;
                    if (expected.TryGetValue(entry.Name, out var pin))
                    {
                        entry.ExpectedMatched = file.Hash == pin;
                        if (!entry.ExpectedMatched.Value) { entry.Fail(Mt5ReceiptStatus.Conflict, "record_hash_mismatch"); continue; }
                    }
                    if (ledger.TryGetValue(entry.Name, out var old) && !string.Equals(old.Sha256, file.Hash, StringComparison.OrdinalIgnoreCase))
                    { entry.Fail(Mt5ReceiptStatus.Conflict, "received_record_content_changed"); continue; }
                    using var json = Mt5WireContract.Document(file.Bytes);
                    entry.Metadata = Mt5WireContract.Metadata(json.RootElement, entry.Name);
                }
                catch (Exception e) when (IsSourceError(e)) { FailSource(entry, e); }
            }
            // A poll can publish one acquisition; shutdown may reuse the final poll counter.
            // Include missing previously received identities to prevent run/poll aliases after removal.
            var identities = entries.Values.Where(x => x.Metadata is not null && x.Status != Mt5ReceiptStatus.Conflict)
                .Select(x => new Mt5ReceivedRecordIdentity(x.Name, x.Hash!, x.Metadata!.Sequence, x.Metadata.RunId, x.Metadata.PollSequence, x.Metadata.Event))
                .Concat(ledger.Values.Where(x => !entries.TryGetValue(x.FileName, out var present)
                    || present.Metadata is null || present.Status == Mt5ReceiptStatus.Conflict)).Where(x => x.Event != "shutdown");
            foreach (var duplicate in identities.GroupBy(x => (x.RunId, x.PollSequence)).Where(x => x.Count() > 1))
                foreach (var item in duplicate)
                    if (entries.TryGetValue(item.FileName, out var entry)) entry.Fail(Mt5ReceiptStatus.Conflict, "duplicate_acquisition_identity");

            foreach (var entry in entries.Values)
            {
                if (entry.Metadata is not { } meta || entry.Status == Mt5ReceiptStatus.Conflict) continue;
                try
                {
                    if (meta.PreviousRecord is { } predecessor)
                    {
                        if (!entries.TryGetValue(predecessor, out var prior)) { entry.Fail(Mt5ReceiptStatus.DataUnavailable, "missing_predecessor_not_deletion"); continue; }
                        if (prior.Hash != meta.PreviousRecordSha256) { entry.Fail(Mt5ReceiptStatus.Conflict, "predecessor_hash_mismatch"); continue; }
                        if (prior.Observation is null) { entry.Fail(Mt5ReceiptStatus.Conflict, "predecessor_not_validated"); continue; }
                        ValidateChain(prior.Metadata!, meta);
                        if (prior.Metadata!.Sequence + 1 != meta.Sequence) entry.Codes.Add("publication_sequence_gap_cause_unresolved");
                    }
                    else if (meta.Sequence != 1) entry.Codes.Add("unanchored_local_sequence_origin");
                    Mt5WireProbe? probe = null;
                    if (meta.ProbeFile is { } probeName)
                    {
                        var source = files.Read(probeName);
                        Mt5WireContract.Need(source.Hash == meta.ProbeSha256, "probe_hash_mismatch");
                        using var json = Mt5WireContract.Document(source.Bytes);
                        probe = Mt5WireContract.Probe(json.RootElement, meta);
                    }
                    entry.Status = Mt5ReceiptStatus.Validated;
                    if (meta.Event != "shutdown")
                    {
                        if (!meta.SourceStable) entry.Codes.Add("source_stability_unresolved");
                        if (!meta.SampleComplete) entry.Codes.Add("sample_incomplete");
                        if (!meta.M1Usable) entry.Codes.Add("m1_unusable");
                        if (probe!.Gate != "passed" || !probe.ConnectedBefore || !probe.ConnectedAfter) entry.Codes.Add("acquisition_unavailable_or_disconnected");
                        if (probe.Frames.Any(f => f.Bars.Any(b => !b.OhlcValid))) entry.Codes.Add("invalid_ohlc_retained_as_diagnostic");
                        if (meta.TriggerM1HeadNative is { } trigger && meta.CapturedM1HeadNative is { } captured && trigger != captured)
                            entry.Codes.Add("trigger_capture_head_disagreement");
                        if (entry.Codes.Any(c => c is not "publication_sequence_gap_cause_unresolved" and not "unanchored_local_sequence_origin"))
                            entry.Status = Mt5ReceiptStatus.Unresolved;
                    }
                    entry.Observation = new(scope, entry.Name, entry.Hash!, meta, probe?.Build, probe?.Gate, probe?.ConnectedBefore,
                        probe?.ConnectedAfter, probe?.Generated, probe?.QuoteBefore, probe?.QuoteAfter,
                        probe?.Frames ?? Array.Empty<Mt5NativeFrame>())
                    {
                        EvidenceKind = meta.Event == "shutdown" ? Mt5ReceiptEvidenceKind.Shutdown
                            : probe?.SourceUnavailableWithoutSample == true ? Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample
                            : Mt5ReceiptEvidenceKind.AcquisitionSnapshot
                    };
                    entry.Disposition = ledger.ContainsKey(entry.Name) ? Mt5ReceiptDisposition.AlreadyReceived : Mt5ReceiptDisposition.New;
                }
                catch (Exception e) when (IsSourceError(e)) { FailSource(entry, e); }
            }
            foreach (var entry in entries.Values.Where(x => x.Observation is not null))
            {
                if (!ledger.ContainsKey(entry.Name) && ledger.Count >= 4096)
                { entry.Fail(Mt5ReceiptStatus.DataUnavailable, "checkpoint_capacity_reached"); continue; }
                var m = entry.Metadata!;
                ledger[entry.Name] = new(entry.Name, entry.Hash!, m.Sequence, m.RunId, m.PollSequence, m.Event);
            }
            foreach (var name in ledger.Keys.Concat(expected.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Where(n => !entries.ContainsKey(n)))
            {
                var missing = new Entry(name); missing.Fail(Mt5ReceiptStatus.DataUnavailable, "expected_or_received_file_unavailable_not_deletion");
                entries.Add(name, missing);
            }
            return new(scope, Array.AsReadOnly(entries.Values.Select(x => x.Result()).ToArray()), Codes(diagnostics), new(scope, ledger.Values));
        }
        catch (Exception e) when (IsSourceError(e)) { return Empty(e is Mt5WireException wire ? wire.Code : "source_directory_unavailable"); }

        Mt5ReceiptBatch Empty(string code) => new(scope, Array.Empty<Mt5ReceiptRecord>(), Codes([code]), new(scope, ledger.Values));
    }

    private static void ValidateChain(Mt5CollectorMetadata prior, Mt5CollectorMetadata current)
    {
        if (prior.RunId == current.RunId)
        {
            Mt5WireContract.Need(prior.Event != "shutdown" && (current.Event == "shutdown" ? current.PollSequence >= prior.PollSequence : current.PollSequence > prior.PollSequence), "run_poll_chain_inconsistent");
            var committed = prior.PreviousCommittedM1HeadNative;
            if (prior.SourceStable && prior.M1Usable && prior.CapturedM1HeadNative is { } captured &&
                (prior.TriggerM1HeadNative is null || prior.TriggerM1HeadNative == captured) && (committed is null || captured.Raw >= committed.Raw)) committed = captured;
            Mt5WireContract.Need(current.PreviousCommittedM1HeadNative == committed, "previous_committed_head_inconsistent");
        }
        else
        {
            var start = long.Parse(current.RunId.AsSpan(4), CultureInfo.InvariantCulture);
            Mt5WireContract.Need(start > prior.Sequence && current.PreviousCommittedM1HeadNative is null, "run_restart_inconsistent");
        }
    }
    private static IReadOnlyList<string> Codes(IEnumerable<string> codes) => Array.AsReadOnly(codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    private static bool IsSourceError(Exception e) => e is IOException or UnauthorizedAccessException or JsonException or Mt5WireException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException;
    private static void FailSource(Entry entry, Exception e)
    {
        if (e is Mt5WireException wire)
        {
            var status = wire.Code is "probe_hash_mismatch" or "run_poll_chain_inconsistent" or "previous_committed_head_inconsistent" or "run_restart_inconsistent"
                ? Mt5ReceiptStatus.Conflict : wire.Code is "unsafe_source_path" or "file_size_bound" or "snapshot_byte_limit" or "file_changed_during_read"
                    ? Mt5ReceiptStatus.DataUnavailable : Mt5ReceiptStatus.Invalid;
            entry.Fail(status, wire.Code);
        }
        else entry.Fail(e is IOException or UnauthorizedAccessException ? Mt5ReceiptStatus.DataUnavailable : Mt5ReceiptStatus.Invalid,
            e is IOException or UnauthorizedAccessException ? "artifact_unavailable_or_unreadable" : "malformed_or_incomplete_json");
    }
    private static void GuardRoot(string root)
    {
        for (DirectoryInfo? node = new(root); node is not null; node = node.Parent)
            Mt5WireContract.Need(!node.Attributes.HasFlag(FileAttributes.ReparsePoint), "unsafe_source_path");
    }
    private sealed class SnapshotFiles(string root) : IDisposable
    {
        private readonly Dictionary<string, SourceFile> cache = new(StringComparer.Ordinal);
        private readonly List<FileStream> handles = [];
        private long total;
        internal SourceFile Read(string name)
        {
            if (cache.TryGetValue(name, out var known)) return known;
            Mt5WireContract.Need(Mt5WireContract.RecordName(name) || System.Text.RegularExpressions.Regex.IsMatch(name, "^probe-[0-9]{12}\\.json\\z"), "unsafe_reference");
            GuardRoot(root);
            var path = Path.Combine(root, name);
            var attributes = File.GetAttributes(path);
            Mt5WireContract.Need(!attributes.HasFlag(FileAttributes.ReparsePoint) && !attributes.HasFlag(FileAttributes.Directory), "unsafe_source_path");
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            handles.Add(stream);
            Mt5WireContract.Need(stream.Length is > 0 and <= MaxFileBytes, "file_size_bound");
            total += stream.Length; Mt5WireContract.Need(total <= MaxSnapshotBytes, "snapshot_byte_limit");
            var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
            Mt5WireContract.Need(stream.Length == bytes.Length, "file_changed_during_read");
            GuardRoot(root);
            Mt5WireContract.Need(!File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint), "unsafe_source_path");
            var file = new SourceFile(bytes, Convert.ToHexStringLower(SHA256.HashData(bytes))); cache.Add(name, file); return file;
        }
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); }
    }
    private sealed record SourceFile(byte[] Bytes, string Hash);
    private sealed class Entry(string name)
    {
        internal string Name { get; } = name;
        internal string? Hash { get; set; }
        internal bool? ExpectedMatched { get; set; }
        internal Mt5CollectorMetadata? Metadata { get; set; }
        internal Mt5ReceiptStatus Status { get; set; } = Mt5ReceiptStatus.DataUnavailable;
        internal Mt5ReceiptDisposition Disposition { get; set; } = Mt5ReceiptDisposition.NotReceived;
        internal Mt5NativeAcquisition? Observation { get; set; }
        internal List<string> Codes { get; } = [];
        internal void Fail(Mt5ReceiptStatus status, string code) { Status = status; Observation = null; Disposition = Mt5ReceiptDisposition.NotReceived; Codes.Add(code); }
        internal Mt5ReceiptRecord Result() => new(Name, Hash, ExpectedMatched, Status, Disposition, Observation, LocalMt5CollectorReceiptReader.Codes(Codes));
    }
}
