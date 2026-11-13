using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// Synthetic snapshots and controlled scheduling. The real reader and store validate every pass.
public sealed class Mt5ReceiptObservationTests
{
    private sealed class Fixture : IDisposable
    {
        internal Mt5ReceiptSyntheticFixture Source { get; } = new();
        internal Mt5ReceiptSyntheticFixture State { get; } = new();
        internal string Checkpoint => Path.Combine(State.DirectoryPath, "receipt.json");
        internal string[] Args(string passes = "3", string interval = "7") =>
            ["mt5-receipt", "observe", "--source", Source.DirectoryPath, "--checkpoint", Checkpoint, "--max-passes", passes, "--interval-ms", interval];
        internal async Task<(int Code, JsonObject[] Rows, TimeSpan[] Waits)> Run(int passes = 3,
            Action<int, CancellationToken>? between = null, CancellationToken token = default,
            IMt5CollectorReceiptReader? reader = null)
        {
            var waits = new List<TimeSpan>();
            var command = new Mt5ReceiptObservationCommand(reader, (delay, cancellation) =>
            {
                waits.Add(delay);
                between?.Invoke(waits.Count, cancellation);
                cancellation.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
            using var output = new StringWriter(); using var error = new StringWriter();
            var code = await command.ExecuteAsync(Args(passes.ToString(CultureInfo.InvariantCulture)), output, error, token);
            Assert.Equal("", error.ToString());
            var rows = Lines(output.ToString()); Assert.Equal(code, rows[^1]["exitCode"]!.GetValue<int>());
            return (code, rows, waits.ToArray());
        }
        internal void CommitCurrent()
        {
            var progress = new LocalMt5CollectorReceiptReader().Read(Source.DirectoryPath).Checkpoint;
            var store = new LocalMt5ReceiptCheckpointStore(Checkpoint, Source.DirectoryPath);
            var state = store.Load();
            Assert.Equal(Mt5CheckpointStatus.Committed, store.Commit(progress, state.Revision).Status);
        }
        public void Dispose() { State.Dispose(); Source.Dispose(); }
    }
    private static JsonObject[] Lines(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
    private static bool Flag(JsonObject row, string name) => row[name]!.GetValue<bool>();
    private static int Count(JsonObject row, string name) => row[name]!.GetValue<int>();
    private static JsonObject Changes(JsonObject row) => row["changes"]!.AsObject();

    [Fact]
    public async Task ExplicitPassLimitAndIntervalProduceCompactDeterministicReadOnlySummaries()
    {
        using var f = new Fixture(); f.Source.Add();
        var hashes = Directory.GetFiles(f.Source.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var a = await f.Run(); var b = await f.Run();
        Assert.Equal(0, a.Code); Assert.Equal(4, a.Rows.Length); Assert.Equal(2, a.Waits.Length);
        Assert.All(a.Waits, delay => Assert.Equal(TimeSpan.FromMilliseconds(7), delay));
        Assert.Equal(a.Rows.Select(r => r.ToJsonString()), b.Rows.Select(r => r.ToJsonString()));
        for (var index = 0; index < 3; index++)
        {
            var row = a.Rows[index]; Assert.Equal(index + 1, Count(row, "pass")); Assert.Equal(3, Count(row, "passLimit"));
            Assert.Equal(1, Count(row, "pendingValidReceipts")); Assert.Equal(0, Count(row, "durablyAcknowledgedRecords"));
            Assert.Null(row["records"]); Assert.Null(row["reviewToken"]); Assert.True(row.ToJsonString().Length < 4000);
            Assert.True(Flag(row, "diagnosticOnly")); Assert.False(Flag(row, "canonicalReady")); Assert.False(Flag(row, "utcConversionAuthenticated"));
            Assert.Equal(index == 0, Flag(Changes(row), "firstObservation"));
            Assert.Equal(index != 0, Flag(Changes(row), "noChange"));
        }
        Assert.Equal("pass_limit", a.Rows[^1]["reason"]!.GetValue<string>());
        Assert.False(File.Exists(f.Checkpoint)); Assert.All(hashes, h => Assert.Equal(h.Value, Mt5ReceiptSyntheticFixture.Hash(h.Key)));
    }
    [Fact]
    public async Task OnePassNeverWaitsAndDoesNotInferWhyPublicationPaused()
    {
        using var f = new Fixture(); f.Source.Add(); var result = await f.Run(1);
        Assert.Empty(result.Waits); Assert.Equal(2, result.Rows.Length); Assert.Equal(1, Count(result.Rows[^1], "completedPasses"));
        Assert.Equal("unresolved", result.Rows[0]["publicationContinuity"]!.GetValue<string>());
    }
    [Fact]
    public async Task MultipleNewReceiptsAndExternalAcknowledgmentAreReportedIndependently()
    {
        using var f = new Fixture(); f.Source.Add(); f.CommitCurrent();
        var original = File.ReadAllBytes(f.Checkpoint);
        var result = await f.Run(3, (pass, _) =>
        {
            if (pass == 1) { f.Source.Add(2, 1); f.Source.Add(3, 2); }
            else f.CommitCurrent(); // Explicit external action, never performed by observation.
        });
        Assert.Equal(0, result.Code); Assert.Equal(2, Count(Changes(result.Rows[1]), "newValidReceipts"));
        Assert.Equal(2, Count(result.Rows[1], "pendingValidReceipts")); Assert.Equal(1, Count(result.Rows[1], "verifiedAcknowledgedRecords"));
        Assert.True(Flag(Changes(result.Rows[2]), "checkpointRevisionChanged")); Assert.Equal(0, Count(Changes(result.Rows[2]), "newValidReceipts"));
        Assert.Equal(3, Count(result.Rows[2], "verifiedAcknowledgedRecords")); Assert.Equal(0, Count(result.Rows[2], "pendingValidReceipts"));
        Assert.NotEqual(original, File.ReadAllBytes(f.Checkpoint));
    }
    [Fact]
    public async Task ExistingCheckpointIsLoadedEveryPassAndNeverModifiedByObservation()
    {
        using var f = new Fixture(); f.Source.Add(); f.CommitCurrent(); var bytes = File.ReadAllBytes(f.Checkpoint);
        var result = await f.Run(); Assert.Equal(0, result.Code);
        Assert.All(result.Rows.Take(3), row => { Assert.Equal("Loaded", row["checkpointStatus"]!.GetValue<string>()); Assert.Equal(1, Count(row, "verifiedAcknowledgedRecords")); });
        Assert.Equal(bytes, File.ReadAllBytes(f.Checkpoint));
    }
    [Fact]
    public async Task ConsecutiveValidDiagnosticEventsRemainUnavailableSamplesAndNewSampleIsDetected()
    {
        using var f = new Fixture(); f.Source.Add();
        var result = await f.Run(4, (pass, _) =>
        {
            if (pass == 1) f.Source.AddUnavailable(2, 1);
            if (pass == 2) f.Source.AddUnavailable(3, 2);
            if (pass == 3)
            {
                f.Source.Add(4, 3);
                f.Source.ChangeRecord(4, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
            }
        });
        Assert.Equal(0, result.Code);
        Assert.Equal(1, Count(Changes(result.Rows[1]), "newReceiptValidDiagnosticEvents"));
        Assert.Equal(1, Count(Changes(result.Rows[2]), "newReceiptValidDiagnosticEvents"));
        Assert.Equal(2, Count(result.Rows[2], "receiptValidDiagnosticEvents")); Assert.Equal(2, Count(result.Rows[2], "incompleteOrUnavailableAcquisitions"));
        Assert.Equal(1, Count(result.Rows[2], "completeNativeSamples")); Assert.Equal(0, Count(result.Rows[2], "blockedReceipts"));
        Assert.Equal("Validated", result.Rows[2]["integrityStatus"]!.GetValue<string>());
        Assert.Equal("Unresolved", result.Rows[2]["acquisitionStatus"]!.GetValue<string>());
        Assert.True(Flag(Changes(result.Rows[3]), "newSampleAfterUnavailableEvidence")); Assert.Equal(2, Count(result.Rows[3], "completeNativeSamples"));
        Assert.Equal(2, Count(result.Rows[3], "incompleteOrUnavailableAcquisitions"));
        Assert.False(File.Exists(f.Checkpoint));
        var diagnostic = new LocalMt5CollectorReceiptReader().Read(f.Source.DirectoryPath).Records[1].Observation!;
        Assert.All(diagnostic.Timeframes, frame => Assert.Empty(frame.Bars)); Assert.Null(diagnostic.Metadata.CapturedM1HeadNative);
    }
    [Fact]
    public async Task HistoricalRecoveredDiagnosticsDoNotCreateRepeatedRecoverySignals()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.AddUnavailable(2, 1); f.Source.Add(3, 2);
        f.Source.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var result = await f.Run(2); Assert.Equal(0, result.Code);
        Assert.All(result.Rows.Take(2), row => Assert.False(Flag(Changes(row), "newSampleAfterUnavailableEvidence")));
        Assert.True(Flag(Changes(result.Rows[1]), "noChange"));
    }
    [Fact]
    public async Task ExplicitShutdownAndLaterRunAreReportedAsReceiptEvidence()
    {
        using var f = new Fixture(); f.Source.Add();
        var result = await f.Run(3, (pass, _) =>
        {
            if (pass == 1)
            {
                f.Source.Add(2, 1, ev: "shutdown");
                f.Source.ChangeRecord(2, r =>
                {
                    r["probe_file"] = null; r["probe_sha256"] = null; r["shutdown_reason"] = 1;
                    r["captured_m1_head_native"] = null; r["trigger_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head);
                    r["m1_usable"] = false; r["sample_complete"] = false; r["source_stable"] = false;
                });
            }
            else f.Source.Add(3, 2, run: 3, poll: 1);
        });
        Assert.Equal(0, result.Code); Assert.Equal(1, Count(result.Rows[1], "shutdownEvents"));
        Assert.Equal(1, Count(result.Rows[1], "receiptValidDiagnosticEvents")); Assert.Equal(1, Count(Changes(result.Rows[1]), "newShutdownEvents"));
        Assert.Equal(1, Count(result.Rows[1], "completeNativeSamples")); Assert.Equal(2, Count(result.Rows[2], "completeNativeSamples"));
        Assert.False(Flag(Changes(result.Rows[2]), "newSampleAfterUnavailableEvidence"));
        Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task MissingProbeAndPartialPublicationCanBeRevalidatedOnFollowingPasses()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1);
        var probe = File.ReadAllBytes(f.Source.ProbePath(2)); File.Delete(f.Source.ProbePath(2));
        var result = await f.Run(3, (pass, _) =>
        {
            if (pass == 1) { File.WriteAllBytes(f.Source.ProbePath(2), probe); f.Source.Add(3, 2); File.WriteAllText(f.Source.RecordPath(3), "{\"schema\":"); }
            if (pass == 2) f.Source.Add(3, 2);
        });
        Assert.Equal(0, result.Code); Assert.Equal(1, Count(result.Rows[0], "unavailableProbeDependencies"));
        Assert.Equal(1, Count(result.Rows[1], "blockedReceipts")); Assert.True(Count(Changes(result.Rows[1]), "newIntegrityBlockers") > 0);
        Assert.Equal(1, Count(Changes(result.Rows[2]), "newValidReceipts")); Assert.Equal(0, Count(result.Rows[2], "blockedReceipts"));
        Assert.Equal(2, Count(result.Rows[^1], "failedPasses"));
    }
    [Theory]
    [InlineData("probe_hash", 4)]
    [InlineData("chain", 4)]
    [InlineData("duplicate", 4)]
    [InlineData("provider", 3)]
    public async Task NewIntegrityOrIdentityViolationsStopWithFinalDiagnostics(string defect, int expected)
    {
        using var f = new Fixture(); f.Source.Add();
        var result = await f.Run(4, (pass, _) =>
        {
            if (pass != 1) return;
            f.Source.Add(2, 1);
            if (defect == "probe_hash") f.Source.ChangeProbe(2, p => p["connected_after"] = false, false);
            if (defect == "chain") f.Source.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64));
            if (defect == "duplicate") f.Source.ChangeRecord(2, r => r["poll_sequence"] = 1);
            if (defect == "provider") f.Source.ChangeRecord(2, r => r["source"]!["company"] = "private-account-marker");
        });
        Assert.Equal(expected, result.Code); Assert.Equal(2, Count(result.Rows[^1], "completedPasses")); Assert.Single(result.Waits);
        Assert.True(Count(Changes(result.Rows[1]), "newIntegrityBlockers") > 0);
        Assert.False(Flag(result.Rows[1], "acknowledgmentEligible")); Assert.False(File.Exists(f.Checkpoint));
        Assert.DoesNotContain("private-account-marker", string.Join("", result.Rows.Select(r => r.ToJsonString())));
    }
    [Fact]
    public async Task ChangedVerifiedManifestIsAConflictEvenWithMissingDurableCheckpoint()
    {
        using var f = new Fixture(); f.Source.Add();
        var result = await f.Run(3, (_, _) => f.Source.ChangeRecord(1, r => r["poll_sequence"] = 9));
        Assert.Equal(4, result.Code); Assert.Equal(1, Count(Changes(result.Rows[1]), "changedReceiptFiles"));
        Assert.Equal("Conflict", result.Rows[1]["integrityStatus"]!.GetValue<string>());
        Assert.Equal(1, Count(result.Rows[1], "blockedReceipts")); Assert.Equal(0, Count(result.Rows[1], "pendingValidReceipts"));
        Assert.Single(Changes(result.Rows[1])["changedReceipts"]!.AsArray()); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task MissingPredecessorIsRetryableAndExplicitPublicationGapRemainsVisible()
    {
        using var f = new Fixture(); f.Source.Add(5, 1);
        var result = await f.Run(2, (_, _) => { f.Source.Add(); f.Source.Add(5, 1); });
        Assert.Equal(0, result.Code); Assert.Equal(1, Count(result.Rows[0], "missingPredecessors"));
        Assert.Equal(1, Count(result.Rows[1], "sequenceGaps")); Assert.True(Flag(result.Rows[1], "acknowledgmentEligible"));
        Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task EmptySourceCanReceiveInitialPublicationDuringTheSession()
    {
        using var f = new Fixture(); var result = await f.Run(2, (_, _) => f.Source.Add());
        Assert.Equal(0, result.Code); Assert.Equal(0, Count(result.Rows[0], "discoveredRecords"));
        Assert.Equal(1, Count(Changes(result.Rows[1]), "newValidReceipts"));
    }
    [Fact]
    public async Task SourceDirectoryLossAndReturnPreserveKnownIdentities()
    {
        using var f = new Fixture(); f.Source.Add(); f.CommitCurrent(); var checkpoint = File.ReadAllBytes(f.Checkpoint);
        var moved = f.Source.DirectoryPath + "-temporarily-unavailable";
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "moneyway-mt5-receipt-"), Path.GetFullPath(moved));
        try
        {
            var result = await f.Run(3, (pass, _) =>
            {
                if (pass == 1) Directory.Move(f.Source.DirectoryPath, moved);
                if (pass == 2) Directory.Move(moved, f.Source.DirectoryPath);
            });
            Assert.Equal(0, result.Code); Assert.True(Flag(Changes(result.Rows[1]), "sourceDirectoryUnavailable"));
            Assert.True(Flag(Changes(result.Rows[2]), "sourceDirectoryReturned"));
            Assert.Equal(0, Count(Changes(result.Rows[2]), "newValidReceipts")); Assert.Equal(1, Count(result.Rows[2], "verifiedAcknowledgedRecords"));
            Assert.Equal(checkpoint, File.ReadAllBytes(f.Checkpoint));
        }
        finally { if (Directory.Exists(moved)) Directory.Move(moved, f.Source.DirectoryPath); }
    }
    [Fact]
    public async Task InitiallyMissingSourceRemainsBoundedAndReportsUnavailable()
    {
        using var f = new Fixture(); var args = f.Args("2"); args[3] = Path.Combine(f.Source.DirectoryPath, "absent");
        using var output = new StringWriter(); using var error = new StringWriter();
        var command = new Mt5ReceiptObservationCommand(wait: (_, _) => Task.CompletedTask);
        Assert.Equal(3, await command.ExecuteAsync(args, output, error));
        var rows = Lines(output.ToString()); Assert.Equal(2, Count(rows[^1], "completedPasses"));
        Assert.True(Flag(Changes(rows[0]), "sourceDirectoryUnavailable")); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task PublicationAfterInventoryReadIsVisibleOnTheNextPass()
    {
        using var f = new Fixture(); f.Source.Add(); var published = false;
        var reader = new CallbackReader(() => { if (!published) { published = true; f.Source.Add(2, 1); } });
        var result = await f.Run(2, reader: reader);
        Assert.Equal(1, Count(result.Rows[0], "discoveredRecords")); Assert.Equal(2, Count(result.Rows[1], "discoveredRecords"));
        Assert.Equal(1, Count(Changes(result.Rows[1]), "newValidReceipts"));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationProducesFinalOutputAndPreventsFurtherReads(bool beforeStart)
    {
        using var f = new Fixture(); f.Source.Add(); using var cancellation = new CancellationTokenSource();
        if (beforeStart) cancellation.Cancel();
        var result = await f.Run(5, (_, _) => cancellation.Cancel(), cancellation.Token);
        Assert.Equal(130, result.Code); Assert.Equal("cancelled", result.Rows[^1]["reason"]!.GetValue<string>());
        Assert.Equal(beforeStart ? 0 : 1, Count(result.Rows[^1], "completedPasses"));
        Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task CancellationDuringSynchronousReadSkipsTheWait()
    {
        using var f = new Fixture(); f.Source.Add(); using var cancellation = new CancellationTokenSource();
        var result = await f.Run(5, token: cancellation.Token, reader: new CallbackReader(cancellation.Cancel));
        Assert.Equal(130, result.Code); Assert.Empty(result.Waits); Assert.Equal(1, Count(result.Rows[^1], "completedPasses"));
    }
    [Theory]
    [InlineData("0", "1")]
    [InlineData("-1", "1")]
    [InlineData("1.5", "1")]
    [InlineData("2147483648", "1")]
    [InlineData("1", "0")]
    [InlineData("1", "-2")]
    [InlineData("1", "NaN")]
    public async Task InvalidSessionLimitsHaveNoFilesystemEffects(string passes, string interval)
    {
        using var f = new Fixture(); using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(2, await new Mt5ReceiptObservationCommand().ExecuteAsync(f.Args(passes, interval), output, error));
        Assert.Equal("", output.ToString()); Assert.Contains("invalid_observation_arguments", error.ToString());
        Assert.Empty(Directory.GetFiles(f.State.DirectoryPath));
    }
    [Fact]
    public async Task MissingLimitUnsafePathAndCommitFlagsAreRejected()
    {
        using var f = new Fixture(); using var output = new StringWriter(); using var error = new StringWriter();
        var command = new Mt5ReceiptObservationCommand();
        Assert.Equal(2, await command.ExecuteAsync(f.Args()[..8], output, error));
        Assert.Equal(2, await command.ExecuteAsync([.. f.Args(), "--acknowledge-receipt"], output, error));
        var args = f.Args(); args[5] = Path.Combine(f.Source.DirectoryPath, "receipt.json");
        Assert.Equal(2, await command.ExecuteAsync(args, output, error)); Assert.False(File.Exists(args[5]));
    }
    [Fact]
    public async Task CorruptCheckpointStopsWithoutReplacement()
    {
        using var f = new Fixture(); f.Source.Add(); File.WriteAllText(f.Checkpoint, "corrupt");
        var result = await f.Run(3); Assert.Equal(4, result.Code); Assert.Empty(result.Waits);
        Assert.Equal(1, Count(result.Rows[^1], "completedPasses")); Assert.Equal("corrupt", File.ReadAllText(f.Checkpoint));
    }
    [Fact]
    public async Task UnexpectedIoFailureHasSanitizedFinalDiagnostics()
    {
        using var f = new Fixture(); var result = await f.Run(reader: new FailingReader());
        Assert.Equal(6, result.Code); Assert.Single(result.Rows); Assert.Equal("io_failure", result.Rows[0]["reason"]!.GetValue<string>());
        Assert.DoesNotContain("sensitive-path", result.Rows[0].ToJsonString()); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public async Task ActualExecutableEmitsOnlyBoundedNdjsonAndExitsBeforeHostStartup()
    {
        using var f = new Fixture(); f.Source.Add();
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("exec"); start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "MoneyWay.IntegrationTests.runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "MoneyWay.IntegrationTests.deps.json"));
        start.ArgumentList.Add(typeof(Mt5ReceiptObservationCommand).Assembly.Location);
        foreach (var arg in f.Args("2", "1")) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.Equal(0, process.ExitCode); Assert.Equal("", await stderr); Assert.Equal(3, Lines(await stdout).Length);
        Assert.False(File.Exists(f.Checkpoint));
    }
    private sealed class CallbackReader(Action afterRead) : IMt5CollectorReceiptReader
    {
        public Mt5ReceiptBatch Read(string sourceDirectory, Mt5ReceiptCheckpoint? checkpoint = null, IReadOnlyDictionary<string, string>? expectedRecordHashes = null)
        {
            var batch = new LocalMt5CollectorReceiptReader().Read(sourceDirectory, checkpoint, expectedRecordHashes);
            afterRead();
            return batch;
        }
    }
    [Fact]
    public async Task BusyCheckpointIsRetriedWithoutInventingAnExternalRevisionChange()
    {
        using var f = new Fixture(); f.Source.Add(); f.CommitCurrent(); FileStream? held = null;
        try
        {
            var result = await f.Run(3, (pass, _) =>
            {
                if (pass == 1) held = new FileStream(f.Checkpoint + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                else { held!.Dispose(); held = null; }
            });
            Assert.Equal(0, result.Code); Assert.Equal("Busy", result.Rows[1]["checkpointStatus"]!.GetValue<string>());
            Assert.False(Flag(Changes(result.Rows[2]), "checkpointRevisionChanged"));
            Assert.Equal(1, Count(result.Rows[2], "verifiedAcknowledgedRecords"));
        }
        finally { held?.Dispose(); }
    }
    [Fact]
    public async Task HelpExitsWithoutCreatingState()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, await new Mt5ReceiptObservationCommand().ExecuteAsync(["mt5-receipt", "observe", "--help"], output, error));
        Assert.Contains("--max-passes", output.ToString()); Assert.Contains("--interval-ms", output.ToString()); Assert.Equal("", error.ToString());
    }
    [Fact]
    public async Task BrokenOutputChannelReturnsIoExitWithoutExposingExceptionDetails()
    {
        using var f = new Fixture(); f.Source.Add(); using var error = new StringWriter(); using var output = new BrokenWriter();
        Assert.Equal(6, await new Mt5ReceiptObservationCommand().ExecuteAsync(f.Args("1"), output, error));
        Assert.Equal("observation_output_unavailable" + Environment.NewLine, error.ToString());
        Assert.False(File.Exists(f.Checkpoint));
    }
    private sealed class BrokenWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("sensitive-output-details");
    }
    private sealed class FailingReader : IMt5CollectorReceiptReader
    {
        public Mt5ReceiptBatch Read(string sourceDirectory, Mt5ReceiptCheckpoint? checkpoint = null, IReadOnlyDictionary<string, string>? expectedRecordHashes = null)
            => throw new IOException("sensitive-path");
    }
}
