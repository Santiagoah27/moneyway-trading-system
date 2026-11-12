using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.IntegrationTests;

// Synthetic file-backed contract tests, never live-terminal or authentic-source substitutes.
public sealed class LocalMt5CollectorReceiptReaderTests
{
    private readonly LocalMt5CollectorReceiptReader reader = new();
    private static JsonObject Frame(JsonObject p, int i = 0) => p["timeframes"]![i]!.AsObject();
    private static JsonObject Bar(JsonObject p, int i = 0, int j = 0) => Frame(p, i)["bars"]![j]!.AsObject();
    private static Mt5ReceiptRecord One(Mt5ReceiptBatch b) => Assert.Single(b.Records);
    private static Mt5NativeAcquisition Observation(Mt5ReceiptBatch b) => Assert.IsType<Mt5NativeAcquisition>(One(b).Observation);

    [Fact]
    public void ValidPairPreservesExactNativeValuesAndProvisionalPositionZero()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var batch = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Validated, batch.Status);
        var observation = Observation(batch);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), observation.RecordSha256);
        Assert.Equal(4, observation.Timeframes.Count);
        Assert.Equal(8, observation.Timeframes.Sum(t => t.Bars.Count));
        foreach (var frame in observation.Timeframes)
        {
            Assert.True(frame.Bars[0].IsPositionZero);
            Assert.Equal("current_bar_completion_unverified", frame.Bars[0].Role);
            Assert.Null(frame.Bars[0].OpenTimeUtc);
            Assert.All(frame.Bars, b => { Assert.Null(b.ProviderConfirmedClosed); Assert.Null(b.HistoricalAvailableAtUtc); });
        }
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, observation.Timeframes[0].Bars[0].OpenTimeNative.Raw);
        Assert.False(batch.CanonicalReady);
        Assert.False(observation.UtcConversionAuthenticated);
        Assert.Equal(5, batch.TemporalBlockers.Count);
        Assert.Single(batch.NewObservations);
    }
    [Fact]
    public void LinkedRecordsAreSortedDespiteOppositeDiscoveryCreationOrder()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(2, 1); f.Add();
        f.ChangeRecord(2, r => r["previous_record_sha256"] = Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)));
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(new long[] { 1, 2 }, result.Records.Select(r => r.Observation!.Metadata.Sequence));
        Assert.All(result.Records, r => Assert.Equal(Mt5ReceiptStatus.Validated, r.Status));
    }
    [Fact]
    public void ExplicitCheckpointSuppressesRepeatDeliveryWithoutHiddenReaderState()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var first = reader.Read(f.DirectoryPath);
        var repeated = new LocalMt5CollectorReceiptReader().Read(f.DirectoryPath, first.Checkpoint);
        Assert.Empty(repeated.NewObservations);
        Assert.Equal(Mt5ReceiptDisposition.AlreadyReceived, One(repeated).Disposition);
        f.Add(2, 1);
        var appended = reader.Read(f.DirectoryPath, repeated.Checkpoint);
        Assert.Equal(2, appended.Checkpoint.Records.Count);
        Assert.Single(first.Checkpoint.Records);
        Assert.Equal(2, Assert.Single(appended.NewObservations).Metadata.Sequence);
    }
    [Fact]
    public void ChangedSameFilenameIsAConflictAndDoesNotReplaceCheckpointEvidence()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var first = reader.Read(f.DirectoryPath); var original = first.Checkpoint.Records.Values.Single().Sha256;
        f.ChangeRecord(1, r => r["poll_sequence"] = 99);
        var next = reader.Read(f.DirectoryPath, first.Checkpoint);
        Assert.Equal(Mt5ReceiptStatus.Conflict, One(next).Status);
        Assert.Contains("received_record_content_changed", One(next).Diagnostics);
        Assert.Equal(original, next.Checkpoint.Records.Values.Single().Sha256);
        Assert.Empty(next.NewObservations);
    }
    [Fact]
    public void OptionalExternalRecordDigestIsVerifiedFromActualBytes()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var name = Path.GetFileName(f.RecordPath(1));
        var good = reader.Read(f.DirectoryPath, expectedRecordHashes: new Dictionary<string, string> { [name] = Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)) });
        Assert.True(One(good).ExpectedHashMatched);
        var bad = reader.Read(f.DirectoryPath, expectedRecordHashes: new Dictionary<string, string> { [name] = new('0', 64) });
        Assert.False(One(bad).ExpectedHashMatched);
        Assert.Equal(Mt5ReceiptStatus.Conflict, One(bad).Status);
        Assert.Contains("record_hash_mismatch", One(bad).Diagnostics);
    }
    [Fact]
    public void ReferencedProbeDigestMismatchBlocksReceipt()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => Bar(p)["close"] = 101.5m, false);
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Conflict, One(result).Status);
        Assert.Contains("probe_hash_mismatch", One(result).Diagnostics);
        Assert.Empty(result.NewObservations);
    }
    [Fact]
    public void BrokenPredecessorDigestBlocksChildButPreservesValidParent()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        f.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64));
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Validated, result.Records[0].Status);
        Assert.Equal(Mt5ReceiptStatus.Conflict, result.Records[1].Status);
        Assert.Contains("predecessor_hash_mismatch", result.Records[1].Diagnostics);
        Assert.Single(result.NewObservations);
    }
    [Fact]
    public void MissingPredecessorIsRetryableAndNeverAcknowledgedAsComplete()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(2, 1);
        var missing = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, One(missing).Status);
        Assert.Contains("missing_predecessor_not_deletion", One(missing).Diagnostics);
        Assert.Empty(missing.Checkpoint.Records);
        f.Add(); f.ChangeRecord(2, r => r["previous_record_sha256"] = Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)));
        Assert.Equal(2, reader.Read(f.DirectoryPath, missing.Checkpoint).NewObservations.Count());
    }
    [Fact]
    public void MissingProbeCanArriveLaterWithoutPermanentDeletionInference()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var bytes = File.ReadAllBytes(f.ProbePath(1)); File.Delete(f.ProbePath(1));
        var missing = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, One(missing).Status);
        Assert.Empty(missing.NewObservations);
        File.WriteAllBytes(f.ProbePath(1), bytes);
        Assert.Single(reader.Read(f.DirectoryPath, missing.Checkpoint).NewObservations);
    }
    [Fact]
    public void UnavailablePreviouslyReceivedFileRemainsInCheckpointAndIsNotDeleted()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var first = reader.Read(f.DirectoryPath); var bytes = File.ReadAllBytes(f.RecordPath(1)); File.Delete(f.RecordPath(1));
        var missing = reader.Read(f.DirectoryPath, first.Checkpoint);
        Assert.Single(missing.Checkpoint.Records);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, One(missing).Status);
        File.WriteAllBytes(f.RecordPath(1), bytes);
        Assert.Empty(reader.Read(f.DirectoryPath, missing.Checkpoint).NewObservations);
    }
    [Fact]
    public void ExplicitLinkedSequenceGapDoesNotFabricateMissingRecordsOrProviderLoss()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(5, 1);
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(Mt5ReceiptStatus.Validated, result.Records[1].Status);
        Assert.Contains("publication_sequence_gap_cause_unresolved", result.Records[1].Diagnostics);
    }
    [Fact]
    public void NewRunResetsPollCounterWithinTheSameDirectorySequence()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1); f.Add(3, 2, run: 3, poll: 1);
        var result = reader.Read(f.DirectoryPath);
        Assert.All(result.Records, r => Assert.Equal(Mt5ReceiptStatus.Validated, r.Status));
        Assert.Equal("run-000000000003", result.Records[2].Observation!.Metadata.RunId);
    }
    [Fact]
    public void DuplicateAcquisitionIdentityBlocksEveryAmbiguousAlternative()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1, poll: 1);
        var result = reader.Read(f.DirectoryPath);
        Assert.All(result.Records, r => { Assert.Equal(Mt5ReceiptStatus.Conflict, r.Status); Assert.Contains("duplicate_acquisition_identity", r.Diagnostics); });
        Assert.Empty(result.NewObservations);
    }
    [Fact]
    public void CorruptPreviouslyReceivedFileCannotHideItsIdentityFromAnAlias()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var first = reader.Read(f.DirectoryPath);
        File.WriteAllText(f.RecordPath(1), "corrupt");
        f.Add(2, poll: 1, ev: "initial_snapshot");
        var result = reader.Read(f.DirectoryPath, first.Checkpoint);
        Assert.Empty(result.NewObservations);
        Assert.Contains("duplicate_acquisition_identity", result.Records[1].Diagnostics);
        Assert.Equal(first.Checkpoint.Records["record-000000000001.json"], result.Checkpoint.Records["record-000000000001.json"]);
    }
    [Fact]
    public void ShutdownMayReuseTheLastPollWithoutFabricatingCandles()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1, poll: 1);
        f.ChangeRecord(2, r =>
        {
            r["event"] = "shutdown"; r["probe_file"] = null; r["probe_sha256"] = null;
            r["captured_m1_head_native"] = null; r["m1_usable"] = false; r["sample_complete"] = false; r["source_stable"] = false; r["shutdown_reason"] = 1;
        });
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Validated, result.Records[1].Status);
        Assert.Empty(result.Records[1].Observation!.Timeframes);
    }
    [Theory]
    [InlineData("company")]
    [InlineData("server")]
    [InlineData("symbol")]
    [InlineData("demo")]
    public void WrongCollectorSourceIsRejectedWithoutEchoingItsValues(string key)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeRecord(1, r => r["source"]![key] = key == "demo" ? JsonValue.Create(false) : JsonValue.Create("sensitive-account-marker"));
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(result).Status);
        Assert.DoesNotContain("sensitive-account-marker", JsonSerializer.Serialize(result));
    }
    [Theory]
    [InlineData("schema", "wrong")]
    [InlineData("event", "unknown")]
    [InlineData("run_id", "run-not-a-local-sequence")]
    [InlineData("sequence_basis", "utc")]
    public void UnsupportedRecordClaimsAreRejected(string key, string value)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeRecord(1, r => r[key] = value);
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(reader.Read(f.DirectoryPath)).Status);
    }
    [Fact]
    public void UnsupportedProbeSchemaAndWrongSymbolAreRejected()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeProbe(1, p => p["schema"] = "wrong");
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(reader.Read(f.DirectoryPath)).Status);
        f.Add(); f.ChangeProbe(1, p => p["symbol"]!["native"] = "USTEC");
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(reader.Read(f.DirectoryPath)).Status);
    }
    [Theory]
    [InlineData("canonical_ready")]
    [InlineData("utc_conversion_authenticated")]
    public void CollectorCannotPromoteReadinessOrAuthenticateUtc(string key)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeRecord(1, r => r[key] = true);
        Assert.Contains("unapproved_readiness_or_utc_assertion", One(reader.Read(f.DirectoryPath)).Diagnostics);
    }
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("not-json")]
    [InlineData("{\"schema\":\"partial\"")]
    public void EmptyMalformedAndPartialPublicationsNeverBecomeReceipts(string partial)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var complete = File.ReadAllBytes(f.RecordPath(1));
        File.WriteAllText(f.RecordPath(1), partial);
        var result = reader.Read(f.DirectoryPath);
        Assert.Null(One(result).Observation); Assert.Empty(result.NewObservations); Assert.Empty(result.Checkpoint.Records);
        File.WriteAllBytes(f.RecordPath(1), complete);
        Assert.Single(reader.Read(f.DirectoryPath, result.Checkpoint).NewObservations);
    }
    [Theory]
    [InlineData("../outside.json")]
    [InlineData("C:\\private\\account.json")]
    [InlineData("probe-000000000001.json:stream")]
    public void UnsafeProbeReferencesCannotEscapeTheDirectory(string reference)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeRecord(1, r => r["probe_file"] = reference);
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(result).Status);
        Assert.Contains("unsafe_probe_reference", One(result).Diagnostics);
    }
    [Fact]
    public void UnsafePredecessorAndDuplicateJsonKeysAreRejected()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeRecord(1, r => { r["previous_record"] = "../outside.json"; r["previous_record_sha256"] = new string('0', 64); });
        Assert.Contains("unsafe_predecessor_reference", One(reader.Read(f.DirectoryPath)).Diagnostics);
        f.Add(); var json = File.ReadAllText(f.RecordPath(1)); File.WriteAllText(f.RecordPath(1), json.Replace("\"version\":1", "\"version\":1,\"version\":1"));
        Assert.Contains("duplicate_json_property", One(reader.Read(f.DirectoryPath)).Diagnostics);
    }
    [Fact]
    public void UnreadableLockedFileIsUnavailableAndRetryable()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        using (var locked = new FileStream(f.RecordPath(1), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(Mt5ReceiptStatus.DataUnavailable, One(reader.Read(f.DirectoryPath)).Status);
        Assert.Single(reader.Read(f.DirectoryPath).NewObservations);
    }
    [Fact]
    public void IncompleteAcquisitionRemainsAnUnresolvedDiagnosticNotATradingVerdict()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => { var frame = Frame(p, 2); frame["status"] = "copy_failed"; frame["copy_return"] = -1; frame["copy_error_code"] = 4401; frame["bars"] = new JsonArray(); frame["head_consistent"] = false; });
        f.ChangeRecord(1, r => r["sample_complete"] = false);
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Unresolved, One(result).Status);
        Assert.Equal(4401, Observation(result).Timeframes[2].CopyErrorCode);
        Assert.False(result.CanonicalReady);
    }
    [Fact]
    public void DisconnectedUnattemptedSnapshotIsRetainedWithoutFabricatedBars()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p =>
        {
            p["gate"] = "terminal_disconnected"; p["source"]!["connected_before"] = false; p["connected_after"] = false;
            foreach (var frame in p["timeframes"]!.AsArray()) { frame!["status"] = "not_attempted"; frame["copy_return"] = null; frame["copy_error_code"] = null; frame["bars"] = new JsonArray(); }
        });
        f.ChangeRecord(1, r => { r["condition"] = 1; r["sample_complete"] = false; r["m1_usable"] = false; r["trigger_m1_head_native"] = null; r["captured_m1_head_native"] = null; });
        var result = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Unresolved, result.Status);
        Assert.All(Observation(result).Timeframes, frame => Assert.Empty(frame.Bars));
    }
    [Fact]
    public void TruthfullyFlaggedInvalidOhlcIsUnresolvedWhileAFalseValidityClaimIsInvalid()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => { Bar(p, 1)["high"] = 1; Bar(p, 1)["ohlc_valid"] = false; });
        f.ChangeRecord(1, r => r["sample_complete"] = false);
        var unresolved = reader.Read(f.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Unresolved, unresolved.Status); Assert.False(Observation(unresolved).Timeframes[1].Bars[0].OhlcValid);
        f.ChangeProbe(1, p => Bar(p, 1)["ohlc_valid"] = true);
        Assert.Equal(Mt5ReceiptStatus.Invalid, One(reader.Read(f.DirectoryPath)).Status);
    }
    [Fact]
    public void DuplicateNativeBarIdentityAndForgedPositionZeroRoleAreRejected()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => Bar(p, 0, 1)["open_time_native"] = Bar(p)["open_time_native"]!.DeepClone());
        Assert.Contains("duplicate_or_unordered_native_identity", One(reader.Read(f.DirectoryPath)).Diagnostics);
        f.Add(); f.ChangeProbe(1, p => Bar(p)["role"] = "historical_preceding_bar");
        Assert.Contains("bar_role_mismatch", One(reader.Read(f.DirectoryPath)).Diagnostics);
    }
    [Fact]
    public void RawNumericLiteralsRemainExactBeyondDoublePrecisionAndDoNotBecomeUtc()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => { Bar(p)["open"] = JsonNode.Parse("100.25000000000000001"); p["generated_gmt_candidate"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head - 7200); });
        var result = reader.Read(f.DirectoryPath); var observation = Observation(result);
        Assert.Equal("100.25000000000000001", observation.Timeframes[0].Bars[0].OpenLiteral);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, observation.Metadata.CapturedM1HeadNative!.Raw);
        Assert.Null(observation.Timeframes[0].Bars[0].OpenTimeUtc);
        Assert.False(observation.UtcConversionAuthenticated);
    }
    [Theory]
    [InlineData("open_time_utc")]
    [InlineData("provider_confirmed_closed")]
    [InlineData("historical_available_at_utc")]
    public void UnsupportedUtcClosureOrAvailabilityClaimsAreNotAccepted(string key)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeProbe(1, p => Bar(p)[key] = "unapproved");
        Assert.Contains("unapproved_bar_time_assertion", One(reader.Read(f.DirectoryPath)).Diagnostics);
    }
    [Fact]
    public void FixedSnapshotAndExplicitCheckpointGiveDeterministicOutputsWithoutInputMutation()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        var before = Directory.GetFiles(f.DirectoryPath).ToDictionary(path => Path.GetFileName(path)!, Mt5ReceiptSyntheticFixture.Hash);
        var a = reader.Read(f.DirectoryPath); var b = reader.Read(f.DirectoryPath);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        var c = reader.Read(f.DirectoryPath, a.Checkpoint); var d = reader.Read(f.DirectoryPath, a.Checkpoint);
        Assert.Equal(JsonSerializer.Serialize(c), JsonSerializer.Serialize(d)); Assert.Empty(c.NewObservations);
        Assert.All(Directory.GetFiles(f.DirectoryPath), path => Assert.Equal(before[Path.GetFileName(path)], Mt5ReceiptSyntheticFixture.Hash(path)));
    }
    [Fact]
    public void RunIdsAreLocalToTheDirectoryScopeNotGlobalInstallationIds()
    {
        using var a = new Mt5ReceiptSyntheticFixture(); using var b = new Mt5ReceiptSyntheticFixture(); a.Add(); b.Add();
        var left = reader.Read(a.DirectoryPath); var right = reader.Read(b.DirectoryPath);
        Assert.NotEqual(left.DirectoryScope, right.DirectoryScope);
        Assert.Equal(Observation(left).Metadata.RunId, Observation(right).Metadata.RunId);
        Assert.Single(right.NewObservations);
        Assert.Throws<ArgumentException>(() => reader.Read(b.DirectoryPath, left.Checkpoint));
    }
    [Fact]
    public void InconsistentCommittedHeadOrPollChainBlocksTheChild()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        f.ChangeRecord(2, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head - 60));
        Assert.Contains("previous_committed_head_inconsistent", reader.Read(f.DirectoryPath).Records[1].Diagnostics);
    }
    [Fact]
    public void MissingDirectoryAndUnpublishedTemporaryFilesAreNotReceipts()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); File.WriteAllText(Path.Combine(f.DirectoryPath, "pending-example.tmp"), "partial");
        Assert.Empty(reader.Read(f.DirectoryPath).NewObservations);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, reader.Read(f.DirectoryPath).Status);
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, reader.Read(Path.Combine(f.DirectoryPath, "not-present")).Status);
    }
    [Fact]
    public void ReparseDirectoryCannotExposeOutsideProbeData()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); using var outside = new Mt5ReceiptSyntheticFixture(); outside.Add();
        var link = Path.Combine(f.DirectoryPath, "linked");
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
            start.ArgumentList.Add($"New-Item -ItemType Junction -Path '{link.Replace("'", "''")}' -Target '{outside.DirectoryPath.Replace("'", "''")}' -ErrorAction Stop | Out-Null");
            using var process = Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, outside.DirectoryPath);
        var result = reader.Read(link);
        Assert.Contains("unsafe_source_path", result.Diagnostics); Assert.Empty(result.NewObservations);
    }
}
