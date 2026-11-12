using System.Diagnostics;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// Synthetic sources only. Command invocations use real reader/store/filesystem behavior.
public sealed class Mt5ReceiptCommandTests
{
    private sealed class Fixture : IDisposable
    {
        internal Mt5ReceiptSyntheticFixture Source { get; } = new();
        internal Mt5ReceiptSyntheticFixture State { get; } = new();
        internal string Checkpoint => Path.Combine(State.DirectoryPath, "receipt.json");
        internal string[] Args(string operation, params string[] more) => ["mt5-receipt", operation, "--source", Source.DirectoryPath, "--checkpoint", Checkpoint, .. more];
        internal (int Code, JsonObject Report) Run(string operation, params string[] more)
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            var code = new Mt5ReceiptCommand().Execute(Args(operation, more), output, error);
            Assert.Equal("", error.ToString());
            var report = JsonNode.Parse(output.ToString())!.AsObject(); Assert.Equal(code, report["exitCode"]!.GetValue<int>());
            return (code, report);
        }
        internal (int Code, JsonObject Report) Inspect() => Run("inspect");
        internal string[] Approval(JsonObject report) => ["--expected-revision", report["expectedRevision"]!.GetValue<string>(), "--review-token", report["reviewToken"]!.GetValue<string>(), "--acknowledge-receipt"];
        internal (int Code, JsonObject Report) Commit(JsonObject report) => Run("commit", Approval(report));
        public void Dispose() { State.Dispose(); Source.Dispose(); }
    }
    private static int Count(JsonObject report, string field) => report[field]!.GetValue<int>();

    [Fact]
    public void InspectWithoutCheckpointReportsPendingNativeEvidenceWithoutPersistingProgress()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1);
        var (code, report) = f.Inspect();
        Assert.Equal(0, code); Assert.Equal("Missing", report["checkpointStatus"]!.GetValue<string>());
        Assert.Equal("missing", report["expectedRevision"]!.GetValue<string>()); Assert.Equal(0, Count(report, "durablyAcknowledgedRecords"));
        Assert.Equal(2, Count(report, "discoveredRecords")); Assert.Equal(2, Count(report, "newlyValidatedPendingRecords"));
        Assert.True(report["safeToAcknowledge"]!.GetValue<bool>()); Assert.Equal(64, report["reviewToken"]!.GetValue<string>().Length);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, report["records"]![0]!["capturedM1HeadNative"]!["raw"]!.GetValue<long>());
        Assert.True(report["diagnosticOnly"]!.GetValue<bool>()); Assert.False(report["canonicalReady"]!.GetValue<bool>());
        Assert.False(report["utcConversionAuthenticated"]!.GetValue<bool>()); Assert.Equal(5, report["temporalBlockers"]!.AsArray().Count);
        Assert.DoesNotContain("openTimeUtc", report.ToJsonString()); Assert.False(File.Exists(f.Checkpoint));
        Assert.True(File.Exists(f.Checkpoint + ".lock")); // Existing store's harmless exclusive-lock initialization only.
    }
    [Fact]
    public void InspectIsDeterministicAndNeverMutatesPublishedCheckpointOrSource()
    {
        using var f = new Fixture(); f.Source.Add();
        var hashes = Directory.GetFiles(f.Source.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var first = f.Inspect(); var repeated = f.Inspect(); Assert.Equal(first.Report.ToJsonString(), repeated.Report.ToJsonString());
        Assert.Equal(0, f.Commit(first.Report).Code); var checkpointBytes = File.ReadAllBytes(f.Checkpoint);
        var a = f.Inspect(); var b = f.Inspect(); Assert.Equal(a.Report.ToJsonString(), b.Report.ToJsonString());
        Assert.Equal(checkpointBytes, File.ReadAllBytes(f.Checkpoint)); Assert.Equal(0, Count(a.Report, "newlyValidatedPendingRecords"));
        Assert.Equal(1, Count(a.Report, "verifiedAcknowledgedRecords"));
        Assert.All(hashes, h => Assert.Equal(h.Value, Mt5ReceiptSyntheticFixture.Hash(h.Key)));
    }
    [Fact]
    public void ExplicitCommitReportsRevisionAndNewRecordsStayPendingAfterRestart()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        var saved = f.Commit(review); Assert.Equal(0, saved.Code); Assert.Equal("Committed", saved.Report["checkpointStatus"]!.GetValue<string>());
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.Checkpoint), saved.Report["checkpointRevision"]!.GetValue<string>());
        Assert.Equal(1, Count(saved.Report, "durablyAcknowledgedRecords")); Assert.Equal(0, Count(saved.Report, "newlyValidatedPendingRecords"));
        f.Source.Add(2, 1); var restart = f.Inspect();
        Assert.Equal(1, Count(restart.Report, "durablyAcknowledgedRecords")); Assert.Equal(1, Count(restart.Report, "newlyValidatedPendingRecords"));
        Assert.Equal(saved.Report["checkpointRevision"]!.GetValue<string>(), Mt5ReceiptSyntheticFixture.Hash(f.Checkpoint));
        Assert.Equal(0, f.Commit(restart.Report).Code); Assert.Equal(2, Count(f.Inspect().Report, "durablyAcknowledgedRecords"));
    }
    [Theory]
    [InlineData("--acknowledge-receipt")]
    [InlineData("--expected-revision")]
    [InlineData("--review-token")]
    public void CommitRequiresExplicitAuthorizationRevisionAndReviewedToken(string missing)
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        var approval = f.Approval(review).ToList(); var at = approval.IndexOf(missing);
        approval.RemoveRange(at, missing == "--acknowledge-receipt" ? 1 : 2);
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(2, new Mt5ReceiptCommand().Execute(f.Args("commit", approval.ToArray()), output, error));
        Assert.Contains("invalid_arguments", error.ToString()); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public void OldReviewCannotResetCheckpointAndNoProgressSaveIsUnchangedOnlyWithFreshReview()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report; Assert.Equal(0, f.Commit(review).Code);
        var bytes = File.ReadAllBytes(f.Checkpoint);
        Assert.Equal(5, f.Commit(review).Code); Assert.Equal(bytes, File.ReadAllBytes(f.Checkpoint));
        var unchanged = f.Commit(f.Inspect().Report); Assert.Equal(0, unchanged.Code);
        Assert.Equal("Unchanged", unchanged.Report["checkpointStatus"]!.GetValue<string>()); Assert.Equal(bytes, File.ReadAllBytes(f.Checkpoint));
    }
    [Fact]
    public void NewPublicationBetweenInspectAndCommitRequiresReviewAgain()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report; f.Source.Add(2, 1);
        var rejected = f.Commit(review); Assert.Equal(5, rejected.Code); Assert.False(File.Exists(f.Checkpoint));
        Assert.Null(rejected.Report["reviewToken"]);
        Assert.Contains("reviewed_progress_changed_inspect_again", rejected.Report["diagnostics"]!.ToJsonString());
    }
    [Fact]
    public void ChangedValidManifestBetweenInspectAndCommitDoesNotGetAcknowledged()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        f.Source.ChangeRecord(1, r => r["poll_sequence"] = 9);
        Assert.Equal(5, f.Commit(review).Code); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public void TokenCannotBeReusedForAnotherCheckpointLocation()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        var args = f.Args("commit", f.Approval(review)); args[5] = Path.Combine(f.State.DirectoryPath, "other.json");
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(5, new Mt5ReceiptCommand().Execute(args, output, error)); Assert.False(File.Exists(args[5]));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrCorruptProbeBlocksCommitAndPreservesPendingProgress(bool corrupt)
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        if (corrupt) File.WriteAllText(f.Source.ProbePath(1), "corrupt"); else File.Delete(f.Source.ProbePath(1));
        var inspect = f.Inspect(); Assert.Equal(4, inspect.Code); Assert.False(inspect.Report["safeToAcknowledge"]!.GetValue<bool>());
        Assert.Null(inspect.Report["candidateCheckpoint"]); Assert.Null(inspect.Report["reviewToken"]);
        if (!corrupt) Assert.Equal(1, Count(inspect.Report, "unavailableProbeDependencies"));
        Assert.Equal(4, f.Commit(review).Code); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public void BrokenChainAndDuplicateIdentityAreRejected()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1);
        f.Source.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64));
        Assert.Equal(4, f.Inspect().Code);
        f.Source.Add(2, 1, poll: 1);
        var duplicate = f.Inspect(); Assert.Equal(4, duplicate.Code); Assert.Equal(2, Count(duplicate.Report, "blockedRecords"));
        Assert.Contains("duplicate_acquisition_identity", duplicate.Report.ToJsonString());
    }
    [Fact]
    public void MissingPredecessorIsReportedSeparatelyFromAllowedPublicationGap()
    {
        using var f = new Fixture(); f.Source.Add(2, 1);
        var missing = f.Inspect(); Assert.Equal(4, missing.Code); Assert.Equal(1, Count(missing.Report, "missingPredecessors"));
        File.Delete(f.Source.RecordPath(2)); f.Source.Add(); f.Source.Add(5, 1);
        var gap = f.Inspect(); Assert.Equal(0, gap.Code); Assert.Equal(1, Count(gap.Report, "sequenceGaps"));
        Assert.Contains("publication_sequence_gap_cause_unresolved", gap.Report.ToJsonString());
        Assert.Equal(0, f.Commit(gap.Report).Code); Assert.Equal(2, Count(f.Inspect().Report, "durablyAcknowledgedRecords"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongProviderOrSymbolIsInvalidSourceWithoutEchoingSourceFreeText(bool probe)
    {
        using var f = new Fixture(); f.Source.Add();
        if (probe) f.Source.ChangeProbe(1, p => p["symbol"]!["native"] = "secret-account-marker");
        else f.Source.ChangeRecord(1, r => r["source"]!["company"] = "secret-account-marker");
        var result = f.Inspect(); Assert.Equal(3, result.Code); Assert.DoesNotContain("secret-account-marker", result.Report.ToJsonString());
        Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public void IncompleteDiagnosticSnapshotCannotBeCommittedByThisCommand()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        f.Source.ChangeRecord(1, r => r["sample_complete"] = false);
        Assert.Equal(4, f.Inspect().Code); Assert.Equal(4, f.Commit(review).Code); Assert.False(File.Exists(f.Checkpoint));
    }
    [Fact]
    public void OrphanTemporaryIsPreservedAndNeverAutoAcknowledged()
    {
        using var f = new Fixture(); f.Source.Add(); var orphan = f.Checkpoint + ".pending-example.tmp"; File.WriteAllText(orphan, "partial");
        var result = f.Inspect(); Assert.Equal(0, result.Code); Assert.False(File.Exists(f.Checkpoint));
        Assert.Contains("uncommitted_temporary_files_preserved_not_acknowledged", result.Report.ToJsonString());
        Assert.Equal(0, f.Commit(result.Report).Code); Assert.Equal("partial", File.ReadAllText(orphan));
    }
    [Fact]
    public void CorruptCheckpointIsNotResetOrReplaced()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        File.WriteAllText(f.Checkpoint, "corrupt checkpoint");
        Assert.Equal(4, f.Inspect().Code); Assert.Equal(4, f.Commit(review).Code); Assert.Equal("corrupt checkpoint", File.ReadAllText(f.Checkpoint));
    }
    [Fact]
    public void RemovedAcknowledgedEvidenceBlocksRegressionAndCountsUnavailableLedgerSeparately()
    {
        using var f = new Fixture(); f.Source.Add(); f.Source.Add(2, 1); f.Commit(f.Inspect().Report);
        var review = f.Inspect().Report; var bytes = File.ReadAllBytes(f.Checkpoint); File.Delete(f.Source.RecordPath(1));
        var broken = f.Inspect(); Assert.Equal(4, broken.Code);
        Assert.Equal(1, Count(broken.Report, "discoveredRecords")); Assert.Equal(2, Count(broken.Report, "recordsReported"));
        Assert.Equal(2, Count(broken.Report, "durablyAcknowledgedRecords")); Assert.Equal(4, f.Commit(review).Code);
        Assert.Equal(bytes, File.ReadAllBytes(f.Checkpoint));
    }
    [Fact]
    public void ConcurrentStoreRevisionChangeOrLockRejectsCommit()
    {
        using var f = new Fixture(); f.Source.Add(); var review = f.Inspect().Report;
        var reader = new LocalMt5CollectorReceiptReader();
        Assert.Equal(Mt5CheckpointStatus.Committed, new LocalMt5ReceiptCheckpointStore(f.Checkpoint, f.Source.DirectoryPath).Commit(reader.Read(f.Source.DirectoryPath).Checkpoint, null).Status);
        Assert.Equal(5, f.Commit(review).Code);
        using var held = new FileStream(f.Checkpoint + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(5, f.Inspect().Code);
    }
    [Fact]
    public void FailedPublicationUsesPersistenceExitAndPreservesOldCheckpoint()
    {
        using var f = new Fixture(); f.Source.Add(); f.Commit(f.Inspect().Report); f.Source.Add(2, 1);
        var review = f.Inspect().Report; var bytes = File.ReadAllBytes(f.Checkpoint);
        using (var held = new FileStream(f.Checkpoint, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = f.Commit(review); Assert.Equal(6, result.Code);
            Assert.Equal("PublicationUncertain", result.Report["checkpointStatus"]!.GetValue<string>());
            Assert.Equal(bytes, File.ReadAllBytes(f.Checkpoint));
        }
        Assert.Equal(1, Count(f.Inspect().Report, "newlyValidatedPendingRecords"));
    }
    [Fact]
    public void MissingSourceIsDistinctFromMissingCheckpointParent()
    {
        using var f = new Fixture();
        using var output = new StringWriter(); using var error = new StringWriter(); var command = new Mt5ReceiptCommand();
        var args = f.Args("inspect"); args[3] = Path.Combine(f.Source.DirectoryPath, "not-present");
        Assert.Equal(3, command.Execute(args, output, error));
        args = f.Args("inspect"); args[5] = Path.Combine(f.State.DirectoryPath, "not-present", "receipt.json");
        Assert.Equal(6, command.Execute(args, output, error));
    }
    [Theory]
    [InlineData("unknown")]
    [InlineData("inspect --force")]
    [InlineData("commit")]
    [InlineData("inspect --source relative")]
    public void InvalidSyntaxDoesNotEchoUnknownValuesOrStartHost(string text)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(2, new Mt5ReceiptCommand().Execute(["mt5-receipt", .. text.Split(' ')], output, error));
        Assert.Equal("", output.ToString()); Assert.Contains("invalid_arguments", error.ToString());
    }
    [Fact]
    public void UnsafePathsAndDuplicateOptionsAreRejectedBeforeReceipt()
    {
        using var f = new Fixture(); using var output = new StringWriter(); using var error = new StringWriter();
        var command = new Mt5ReceiptCommand(); var args = f.Args("inspect"); args[5] = Path.Combine(f.Source.DirectoryPath, "receipt.json");
        Assert.Equal(2, command.Execute(args, output, error)); Assert.Empty(Directory.GetFiles(f.Source.DirectoryPath));
        args = f.Args("inspect", "--source", "sensitive-marker");
        Assert.Equal(2, command.Execute(args, output, error)); Assert.DoesNotContain("sensitive-marker", error.ToString());
        args = f.Args("inspect"); args[3] = "relative"; Assert.Equal(2, command.Execute(args, output, error));
    }
    [Fact]
    public void HelpHasNoFilesystemEffects()
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, new Mt5ReceiptCommand().Execute(["mt5-receipt", "--help"], output, error));
        Assert.Contains("--acknowledge-receipt", output.ToString()); Assert.Equal("", error.ToString());
    }
    [Fact]
    public async Task ActualExecutableExitsAfterInspectCommitAndRestartWithoutStartingHostedWorker()
    {
        using var f = new Fixture(); f.Source.Add();
        async Task<(int Exit, JsonObject Report)> Invoke(string[] args)
        {
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
            start.ArgumentList.Add(typeof(Mt5ReceiptCommand).Assembly.Location);
            foreach (var argument in args) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            Assert.Equal("", await stderr); return (process.ExitCode, JsonNode.Parse(await stdout)!.AsObject());
        }
        var inspect = await Invoke(f.Args("inspect")); Assert.Equal(0, inspect.Exit); Assert.False(File.Exists(f.Checkpoint));
        var commit = await Invoke(f.Args("commit", f.Approval(inspect.Report))); Assert.Equal(0, commit.Exit);
        var restart = await Invoke(f.Args("inspect")); Assert.Equal(0, restart.Exit); Assert.Equal(1, Count(restart.Report, "durablyAcknowledgedRecords"));
        f.Source.Add(2, 1); var later = await Invoke(f.Args("inspect")); Assert.Equal(0, later.Exit);
        Assert.Equal(1, Count(later.Report, "newlyValidatedPendingRecords"));
    }
}
