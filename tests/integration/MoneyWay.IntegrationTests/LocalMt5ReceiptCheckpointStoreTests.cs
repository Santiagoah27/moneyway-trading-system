using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.IntegrationTests;

// All artifacts are synthetic; interrupted states are constructed on the real local filesystem.
public sealed class LocalMt5ReceiptCheckpointStoreTests
{
    private sealed class Fixture : IDisposable
    {
        internal Mt5ReceiptSyntheticFixture Source { get; } = new();
        internal Mt5ReceiptSyntheticFixture Storage { get; } = new();
        internal string Path => System.IO.Path.Combine(Storage.DirectoryPath, "receipt.json");
        internal LocalMt5CollectorReceiptReader Reader { get; } = new();
        internal LocalMt5ReceiptCheckpointStore Store() => new(Path, Source.DirectoryPath);
        internal Mt5ReceiptCheckpoint Progress() => Reader.Read(Source.DirectoryPath).Checkpoint;
        internal Mt5CheckpointResult Save() => Store().Commit(Progress(), null);
        internal string Pending => Path + ".pending-00000000000000000000000000000001.tmp";
        internal void Change(Action<JsonObject> action, bool checksum = false)
        {
            var node = JsonNode.Parse(File.ReadAllText(Path))!.AsObject(); action(node);
            if (checksum) node["payload_sha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(node["payload"]!.ToJsonString())));
            File.WriteAllText(Path, node.ToJsonString());
        }
        public void Dispose() { Storage.Dispose(); Source.Dispose(); }
    }
    private static JsonObject Payload(JsonObject node) => node["payload"]!.AsObject();
    private static JsonObject Identity(JsonObject node) => Payload(node)["records"]![0]!.AsObject();

    [Fact]
    public void FirstStartHasNoAcknowledgmentsAndDoesNotCreateCheckpoint()
    {
        using var f = new Fixture(); f.Source.Add();
        var loaded = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Missing, loaded.Status); Assert.Null(loaded.Checkpoint);
        Assert.False(File.Exists(f.Path)); Assert.Single(f.Reader.Read(f.Source.DirectoryPath).NewObservations);
    }
    [Fact]
    public void CommitIsExplicitAndANewStoreLoadsAcknowledgedProgress()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1);
        var saved = f.Save();
        Assert.Equal(Mt5CheckpointStatus.Committed, saved.Status);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.Path), saved.Revision);
        var loaded = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Loaded, loaded.Status); Assert.Equal(saved.Revision, loaded.Revision);
        Assert.Equal(2, loaded.Checkpoint!.Records.Count); Assert.Empty(loaded.Verification!.NewObservations);
        Assert.All(loaded.Verification.Records, r => Assert.Equal(Mt5ReceiptDisposition.AlreadyReceived, r.Disposition));
    }
    [Fact]
    public void IdenticalSaveWithCurrentRevisionDoesNotRewriteCheckpoint()
    {
        using var f = new Fixture(); f.Source.Add(); var saved = f.Save();
        var bytes = File.ReadAllBytes(f.Path); var time = File.GetLastWriteTimeUtc(f.Path);
        var again = f.Store().Commit(saved.Checkpoint!, saved.Revision);
        Assert.Equal(Mt5CheckpointStatus.Unchanged, again.Status); Assert.Equal(bytes, File.ReadAllBytes(f.Path));
        Assert.Equal(time, File.GetLastWriteTimeUtc(f.Path)); Assert.Equal(saved.Revision, again.Revision);
        Assert.False(File.Exists(f.Pending));
    }
    [Fact]
    public void ReadAfterRestartDoesNotCommitNewRecordsUntilExplicitSave()
    {
        using var f = new Fixture(); f.Source.Add(); var saved = f.Save(); f.Source.Add(2, 1);
        var loaded = f.Store().Load();
        Assert.Single(loaded.Checkpoint!.Records); Assert.Single(loaded.Verification!.NewObservations);
        Assert.Equal(saved.Revision, f.Store().Load().Revision);
        Assert.Single(f.Store().Load().Verification!.NewObservations);
        var committed = f.Store().Commit(loaded.Verification.Checkpoint, loaded.Revision);
        Assert.Equal(Mt5CheckpointStatus.Committed, committed.Status);
        Assert.Equal(2, f.Store().Load().Checkpoint!.Records.Count); Assert.Empty(f.Store().Load().Verification!.NewObservations);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnpublishedInitialCheckpointNeverAcknowledgesRecords(bool partial)
    {
        using var f = new Fixture(); f.Source.Add(); Assert.Equal(Mt5CheckpointStatus.Committed, f.Save().Status);
        File.Move(f.Path, f.Pending);
        if (partial) File.WriteAllText(f.Pending, "{\"schema\":");
        var pendingBytes = File.ReadAllBytes(f.Pending);
        var loaded = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Missing, loaded.Status);
        Assert.Contains("uncommitted_temporary_files_preserved_not_acknowledged", loaded.Diagnostics);
        Assert.Single(f.Reader.Read(f.Source.DirectoryPath).NewObservations);
        Assert.Equal(Mt5CheckpointStatus.Committed, f.Save().Status);
        Assert.Equal(pendingBytes, File.ReadAllBytes(f.Pending));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedReplacementKeepsPreviouslyPublishedCheckpoint(bool partial)
    {
        using var f = new Fixture(); f.Source.Add(); var previous = f.Save(); var previousBytes = File.ReadAllBytes(f.Path);
        f.Source.Add(2, 1);
        var replacement = f.Store().Commit(f.Progress(), previous.Revision); Assert.Equal(Mt5CheckpointStatus.Committed, replacement.Status);
        File.Move(f.Path, f.Pending); File.WriteAllBytes(f.Path, previousBytes);
        if (partial) File.WriteAllText(f.Pending, "partial temporary bytes");
        var loaded = f.Store().Load();
        Assert.Equal(previous.Revision, loaded.Revision); Assert.Single(loaded.Checkpoint!.Records);
        Assert.Single(loaded.Verification!.NewObservations);
        Assert.Equal(Mt5CheckpointStatus.Committed, f.Store().Commit(loaded.Verification.Checkpoint, loaded.Revision).Status);
        Assert.Empty(f.Store().Load().Verification!.NewObservations); Assert.True(File.Exists(f.Pending));
    }
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"schema\":\"a\",\"schema\":\"b\"}")]
    public void MalformedOrDuplicateCheckpointNeverRestartsSilently(string content)
    {
        using var f = new Fixture(); f.Source.Add(); File.WriteAllText(f.Path, content);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Store().Load().Status);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Save().Status);
        Assert.Equal(content, File.ReadAllText(f.Path));
    }
    [Fact]
    public void UnsupportedSchemaVersionIsPreserved()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save(); f.Change(n => n["version"] = 2);
        var bytes = File.ReadAllBytes(f.Path);
        Assert.Contains("unsupported_checkpoint_schema_or_version", f.Store().Load().Diagnostics);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Save().Status); Assert.Equal(bytes, File.ReadAllBytes(f.Path));
    }
    [Fact]
    public void PayloadCorruptionIsDetectedWithoutDiscardingCheckpoint()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save(); f.Change(n => Identity(n)["poll_sequence"] = 9);
        Assert.Contains("checkpoint_payload_digest_mismatch", f.Store().Load().Diagnostics);
        Assert.Null(f.Store().Load().Checkpoint); Assert.True(File.Exists(f.Path));
    }
    [Theory]
    [InlineData("company")]
    [InlineData("server")]
    [InlineData("native_symbol")]
    [InlineData("demo")]
    [InlineData("canonical_ready")]
    public void UnapprovedSourceOrReadinessCannotBeLoadedEvenWithRecomputedChecksum(string field)
    {
        using var f = new Fixture(); f.Source.Add(); f.Save();
        f.Change(n => Payload(n)[field] = field == "demo" ? JsonValue.Create(false)
            : field == "canonical_ready" ? JsonValue.Create(true) : JsonValue.Create("unapproved-sensitive-marker"), true);
        var loaded = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Invalid, loaded.Status); Assert.Null(loaded.Checkpoint);
        Assert.DoesNotContain("unapproved-sensitive-marker", JsonSerializer.Serialize(loaded));
    }
    [Theory]
    [InlineData("run_id", "run-000000000000")]
    [InlineData("run_id", "not-a-run")]
    [InlineData("event", "invented")]
    [InlineData("sha256", "not-a-sha")]
    [InlineData("file_name", "../outside.json")]
    public void InvalidCheckpointRecordIdentityIsRejected(string field, string value)
    {
        using var f = new Fixture(); f.Source.Add(); f.Save(); f.Change(n => Identity(n)[field] = value, true);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Store().Load().Status);
    }
    [Fact]
    public void InvalidSequenceAndConflictingDuplicateAreRejected()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save();
        f.Change(n => Identity(n)["sequence"] = 2, true);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Store().Load().Status);
        File.Delete(f.Path); f.Save();
        f.Change(n => Payload(n)["records"]!.AsArray().Add(Identity(n).DeepClone()), true);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Store().Load().Status);
    }
    [Fact]
    public void ChangedAnchorDigestIsRejectedAndRehashedForgeryStillHasNoVerifiedObservation()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save();
        f.Change(n => Payload(n)["last_acknowledged_record_sha256"] = new string('0', 64), true);
        Assert.Contains("checkpoint_anchor_mismatch", f.Store().Load().Diagnostics);
        File.Delete(f.Path); f.Save();
        f.Change(n => { Identity(n)["sha256"] = new string('0', 64); Payload(n)["last_acknowledged_record_sha256"] = new string('0', 64); }, true);
        var forged = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Loaded, forged.Status);
        Assert.Equal(Mt5ReceiptStatus.Conflict, forged.Verification!.Status);
        Assert.Empty(forged.Verification.NewObservations); Assert.Null(Assert.Single(forged.Verification.Records).Observation);
    }
    [Fact]
    public void RegressionAndOldRevisionNeverReplaceCommittedProgress()
    {
        using var f = new Fixture(); f.Source.Add(); var first = f.Save(); f.Source.Add(2, 1);
        var second = f.Store().Commit(f.Progress(), first.Revision);
        var bytes = File.ReadAllBytes(f.Path);
        Assert.Equal(Mt5CheckpointStatus.Conflict, f.Store().Commit(first.Checkpoint!, second.Revision).Status);
        Assert.Equal(Mt5CheckpointStatus.Conflict, f.Store().Commit(second.Checkpoint!, first.Revision).Status);
        Assert.Equal(Mt5CheckpointStatus.Conflict, f.Save().Status);
        Assert.Equal(bytes, File.ReadAllBytes(f.Path));
    }
    [Fact]
    public void CommitRevalidatesHashesRatherThanTrustingCallerConstructedLedger()
    {
        using var f = new Fixture(); f.Source.Add(); var progress = f.Progress();
        f.Source.ChangeProbe(1, n => n["connected_after"] = false, rebind: false);
        var result = f.Store().Commit(progress, null);
        Assert.Equal(Mt5CheckpointStatus.DataUnavailable, result.Status); Assert.False(File.Exists(f.Path));
        Assert.Contains("probe_hash_mismatch", Assert.Single(result.Verification!.Records).Diagnostics);
    }
    [Fact]
    public void ClaimedIdentityMustMatchActualValidatedSourceMetadata()
    {
        using var f = new Fixture(); f.Source.Add(); var progress = f.Progress();
        var wrong = new Mt5ReceiptCheckpoint(progress.DirectoryScope, progress.Records.Values.Select(i => i with { PollSequence = 8 }));
        Assert.Equal(Mt5CheckpointStatus.Conflict, f.Store().Commit(wrong, null).Status); Assert.False(File.Exists(f.Path));
    }
    [Fact]
    public void LoadedIdentityMetadataMustAgreeWithSourceEvenWhenChecksumWasRecomputed()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save();
        f.Change(n => Identity(n)["poll_sequence"] = 8, true);
        var loaded = f.Store().Load();
        Assert.Equal(Mt5CheckpointStatus.Conflict, loaded.Status); Assert.Null(loaded.Checkpoint);
        Assert.Contains("checkpoint_identity_disagrees_with_evidence", loaded.Diagnostics);
        Assert.True(File.Exists(f.Path));
    }
    [Fact]
    public void MissingHistoricalPredecessorDoesNotBecomeVerifiedFromPersistedHash()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1); var saved = f.Save(); File.Delete(f.Source.RecordPath(1));
        var loaded = f.Store().Load(); Assert.Equal(saved.Revision, loaded.Revision);
        Assert.Equal(2, loaded.Checkpoint!.Records.Count); Assert.Empty(loaded.Verification!.NewObservations);
        Assert.Contains("missing_predecessor_not_deletion", loaded.Verification.Records[1].Diagnostics);
        Assert.Equal(Mt5CheckpointStatus.DataUnavailable, f.Store().Commit(loaded.Checkpoint, loaded.Revision).Status);
    }
    [Fact]
    public void MissingSourcePreservesCommittedProgressAndReturnsUnavailableVerification()
    {
        using var f = new Fixture(); f.Source.Add(); var saved = f.Save();
        // Move only the explicitly owned synthetic fixture directory to a verified sibling path.
        var moved = f.Source.DirectoryPath + "-unavailable";
        Assert.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), System.IO.Path.GetFullPath(moved));
        Directory.Move(f.Source.DirectoryPath, moved);
        try
        {
            var loaded = f.Store().Load(); Assert.Equal(saved.Revision, loaded.Revision);
            Assert.Equal(Mt5ReceiptStatus.DataUnavailable, loaded.Verification!.Status);
            Assert.Equal(Mt5CheckpointStatus.DataUnavailable, f.Store().Commit(loaded.Checkpoint!, loaded.Revision).Status);
        }
        finally { Directory.Move(moved, f.Source.DirectoryPath); }
    }
    [Fact]
    public async Task ExclusiveLockAndRevisionFenceConcurrentSavers()
    {
        using var f = new Fixture(); f.Source.Add(); var saved = f.Save(); f.Source.Add(2, 1); var progress = f.Progress();
        using (var held = new FileStream(f.Path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(Mt5CheckpointStatus.Busy, f.Store().Load().Status);
            Assert.Equal(Mt5CheckpointStatus.Busy, f.Store().Commit(progress, saved.Revision).Status);
        }
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(() => { start.Wait(); return f.Store().Commit(progress, saved.Revision); })).ToArray();
        start.Set(); var results = await Task.WhenAll(tasks);
        Assert.Single(results, r => r.Status == Mt5CheckpointStatus.Committed);
        Assert.Single(results, r => r.Status is Mt5CheckpointStatus.Busy or Mt5CheckpointStatus.Conflict);
        Assert.Equal(2, f.Store().Load().Checkpoint!.Records.Count);
    }
    [Fact]
    public void UnsafePathsAndDifferentSourceScopeAreRejected()
    {
        using var f = new Fixture(); f.Source.Add();
        Assert.Throws<ArgumentException>(() => new LocalMt5ReceiptCheckpointStore("relative.json", f.Source.DirectoryPath));
        Assert.Throws<ArgumentException>(() => new LocalMt5ReceiptCheckpointStore(System.IO.Path.Combine(f.Source.DirectoryPath, "receipt.json"), f.Source.DirectoryPath));
        Assert.Throws<ArgumentException>(() => new LocalMt5ReceiptCheckpointStore(f.Path + ":stream", f.Source.DirectoryPath));
        Assert.Throws<ArgumentException>(() => new LocalMt5ReceiptCheckpointStore(f.Path, "\\\\?\\" + f.Source.DirectoryPath));
        Assert.Throws<ArgumentException>(() => new LocalMt5ReceiptCheckpointStore(f.Path, f.Source.DirectoryPath + "."));
        f.Save(); using var other = new Mt5ReceiptSyntheticFixture();
        Assert.Equal(Mt5CheckpointStatus.Invalid, new LocalMt5ReceiptCheckpointStore(f.Path, other.DirectoryPath).Load().Status);
    }
    [Fact]
    public void UnrelatedCheckpointAndLockFilesAreNeverOverwritten()
    {
        using var f = new Fixture(); f.Source.Add(); File.WriteAllText(f.Path, "unrelated file");
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Save().Status); Assert.Equal("unrelated file", File.ReadAllText(f.Path));
        File.Delete(f.Path + ".lock"); File.WriteAllText(f.Path + ".lock", "unrelated lock");
        Assert.Contains("unrecognized_lock_file_preserved", f.Store().Load().Diagnostics);
        Assert.Equal("unrelated lock", File.ReadAllText(f.Path + ".lock"));
    }
    [Fact]
    public void MissingCheckpointParentIsNotCreatedImplicitly()
    {
        using var f = new Fixture(); var missing = System.IO.Path.Combine(f.Storage.DirectoryPath, "not-created", "receipt.json");
        var result = new LocalMt5ReceiptCheckpointStore(missing, f.Source.DirectoryPath).Load();
        Assert.Equal(Mt5CheckpointStatus.DataUnavailable, result.Status); Assert.False(Directory.Exists(System.IO.Path.GetDirectoryName(missing)));
    }
    [Fact]
    public void FailedReplacementReturnsUncertaintyPreservesOldCommitAndCanRecover()
    {
        using var f = new Fixture(); f.Source.Add(); var first = f.Save(); var original = File.ReadAllBytes(f.Path);
        f.Source.Add(2, 1);
        using (var held = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = f.Store().Commit(f.Progress(), first.Revision);
            Assert.Equal(Mt5CheckpointStatus.PublicationUncertain, failed.Status);
            Assert.Equal(original, File.ReadAllBytes(f.Path));
        }
        var restart = f.Store().Load(); Assert.Equal(first.Revision, restart.Revision);
        Assert.Contains("uncommitted_temporary_files_preserved_not_acknowledged", restart.Diagnostics);
        Assert.Single(restart.Verification!.NewObservations);
        Assert.Equal(Mt5CheckpointStatus.Committed, f.Store().Commit(restart.Verification.Checkpoint, restart.Revision).Status);
        Assert.Empty(f.Store().Load().Verification!.NewObservations);
    }
    [Fact]
    public void JunctionCheckpointParentCannotRedirectWritesIntoSourceEvidence()
    {
        using var f = new Fixture(); f.Source.Add();
        var link = System.IO.Path.Combine(f.Storage.DirectoryPath, "linked");
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
        start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{link.Replace("'", "''")}' -Target '{f.Source.DirectoryPath.Replace("'", "''")}' -ErrorAction Stop | Out-Null");
        using var process = Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        var result = new LocalMt5ReceiptCheckpointStore(System.IO.Path.Combine(link, "receipt.json"), f.Source.DirectoryPath).Load();
        Assert.Equal(Mt5CheckpointStatus.DataUnavailable, result.Status);
        Assert.Contains("unsafe_checkpoint_path", result.Diagnostics);
        Assert.False(File.Exists(System.IO.Path.Combine(f.Source.DirectoryPath, "receipt.json.lock")));
    }
    [Fact]
    public void MissingRequiredFieldAndUnexpectedMetadataAreRejected()
    {
        using var f = new Fixture(); f.Source.Add(); f.Save();
        f.Change(n => Payload(n).Remove("canonical_ready"), true);
        Assert.Equal(Mt5CheckpointStatus.Invalid, f.Store().Load().Status);
        File.Delete(f.Path); f.Save(); f.Change(n => n["account_id"] = "sensitive-marker");
        var result = f.Store().Load(); Assert.Equal(Mt5CheckpointStatus.Invalid, result.Status);
        Assert.DoesNotContain("sensitive-marker", JsonSerializer.Serialize(result));
    }
    [Fact]
    public void IncompleteDiagnosticReceiptCanBeExplicitlyCommittedWithoutCanonicalReadiness()
    {
        using var f = new Fixture(); f.Source.Add();
        f.Source.ChangeRecord(1, n => n["sample_complete"] = false);
        var saved = f.Save(); Assert.Equal(Mt5CheckpointStatus.Committed, saved.Status);
        Assert.Equal(Mt5ReceiptStatus.Unresolved, f.Store().Load().Verification!.Status);
        Assert.False(saved.CanonicalReady);
    }
    [Fact]
    public void RecoveryIsDeterministicPreservesNativeTimeAndNeverMutatesSource()
    {
        using var f = new Fixture(); f.Source.Add();
        var hashes = Directory.GetFiles(f.Source.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var pending = f.Reader.Read(f.Source.DirectoryPath); Assert.False(File.Exists(f.Path));
        var saved = f.Store().Commit(pending.Checkpoint, null);
        var a = f.Store().Load(); var b = f.Store().Load();
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b)); Assert.False(a.CanonicalReady);
        var observation = Assert.Single(a.Verification!.Records).Observation!;
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, observation.Timeframes[0].Bars[0].OpenTimeNative.Raw);
        Assert.Null(observation.Timeframes[0].Bars[0].OpenTimeUtc); Assert.Null(observation.Timeframes[0].Bars[0].ProviderConfirmedClosed);
        Assert.Null(observation.Timeframes[0].Bars[0].HistoricalAvailableAtUtc);
        Assert.All(hashes, h => Assert.Equal(h.Value, Mt5ReceiptSyntheticFixture.Hash(h.Key)));
        var persisted = File.ReadAllText(f.Path);
        foreach (var forbidden in new[] { "account_id", "password", "credential", "order", "timeframes", "open_time_utc", "generated_at" }) Assert.DoesNotContain(forbidden, persisted);
        Assert.Equal(saved.Revision, b.Revision);
    }
    [Fact]
    public void DurableReceiptDoesNotMeanDownstreamEffectsExecutedExactlyOnce()
    {
        using var f = new Fixture(); f.Source.Add(); var observed = f.Reader.Read(f.Source.DirectoryPath);
        // An external effect before commit may be repeated after a crash: receipt owns no consumer transaction.
        Assert.Single(f.Reader.Read(f.Source.DirectoryPath).NewObservations);
        f.Store().Commit(observed.Checkpoint, null);
        // Conversely a commit before an external effect suppresses redelivery without proving that effect happened.
        Assert.Empty(f.Store().Load().Verification!.NewObservations);
    }
}
