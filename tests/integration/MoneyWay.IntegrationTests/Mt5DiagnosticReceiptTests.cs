using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// Synthetic equivalents of the collector's disconnected/no-sample diagnostic; never authentic payload fixtures.
public sealed class Mt5DiagnosticReceiptTests
{
    private readonly LocalMt5CollectorReceiptReader reader = new();
    private static (int Code, JsonObject Report) Command(Mt5ReceiptSyntheticFixture source, string checkpoint,
        string operation = "inspect", JsonObject? reviewed = null)
    {
        var args = new List<string> { "mt5-receipt", operation, "--source", source.DirectoryPath, "--checkpoint", checkpoint };
        if (reviewed is not null) args.AddRange(["--expected-revision", reviewed["expectedRevision"]!.GetValue<string>(),
            "--review-token", reviewed["reviewToken"]!.GetValue<string>(), "--acknowledge-receipt"]);
        using var output = new StringWriter(); using var error = new StringWriter();
        var code = new Mt5ReceiptCommand().Execute(args, output, error); Assert.Equal("", error.ToString());
        return (code, JsonNode.Parse(output.ToString())!.AsObject());
    }
    private static void AssertBlocked(Mt5ReceiptSyntheticFixture source)
    {
        using var state = new Mt5ReceiptSyntheticFixture(); var checkpoint = Path.Combine(state.DirectoryPath, "receipt.json");
        var result = Command(source, checkpoint);
        Assert.NotEqual(0, result.Code); Assert.False(result.Report["safeToAcknowledge"]!.GetValue<bool>());
        Assert.Null(result.Report["reviewToken"]); Assert.False(File.Exists(checkpoint));
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void VerifiedUnavailableReceiptNeverBecomesAUsableMarketSample(int condition, bool connectedAfter)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1, condition: condition, connectedAfter: connectedAfter);
        var batch = reader.Read(f.DirectoryPath); var diagnostic = batch.Records[1];
        Assert.Equal(Mt5ReceiptStatus.Unresolved, diagnostic.Status); Assert.Equal(Mt5ReceiptStatus.Unresolved, batch.Status);
        Assert.True(diagnostic.ReceiptIntegrityValidated); Assert.True(batch.ReceiptIntegrityValidated); Assert.True(batch.SourceIdentityValidated);
        Assert.True(diagnostic.ReceiptAcknowledgmentEligible); Assert.True(batch.ReceiptAcknowledgmentEligible);
        Assert.False(diagnostic.CompleteNativeSampleObserved); Assert.True(batch.Records[0].CompleteNativeSampleObserved);
        var observation = diagnostic.Observation!; Assert.Equal(Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample, observation.EvidenceKind);
        Assert.False(observation.Metadata.M1Usable); Assert.False(observation.Metadata.SampleComplete); Assert.Null(observation.Metadata.CapturedM1HeadNative);
        Assert.All(observation.Timeframes, frame => { Assert.Empty(frame.Bars); Assert.Null(frame.CopyReturn); });
        Assert.Contains("acquisition_unavailable_or_disconnected", diagnostic.Diagnostics);
        Assert.Contains("m1_unusable", diagnostic.Diagnostics); Assert.Contains("sample_incomplete", diagnostic.Diagnostics);
        Assert.False(batch.CanonicalReady); Assert.False(observation.UtcConversionAuthenticated); Assert.Equal(5, batch.TemporalBlockers.Count);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(2)), batch.Checkpoint.Records["record-000000000002.json"].Sha256);
    }
    [Fact]
    public void LaterNewM1RetainsEarlierUnavailableEvidenceAndLastCommittedHead()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1); f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head + 3600);
        f.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var batch = reader.Read(f.DirectoryPath);
        Assert.True(batch.ReceiptAcknowledgmentEligible); Assert.Equal(3, batch.Checkpoint.Records.Count);
        Assert.Equal(new[] { true, false, true }, batch.Records.Select(r => r.CompleteNativeSampleObserved));
        Assert.Equal(new[] { Mt5ReceiptStatus.Validated, Mt5ReceiptStatus.Unresolved, Mt5ReceiptStatus.Validated }, batch.Records.Select(r => r.Status));
        Assert.Empty(batch.Records[1].Observation!.Timeframes[0].Bars);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, batch.Records[2].Observation!.Metadata.PreviousCommittedM1HeadNative!.Raw);
        Assert.All(batch.Records.Where(r => r.CompleteNativeSampleObserved).SelectMany(r => r.Observation!.Timeframes).SelectMany(f => f.Bars),
            b => { Assert.Null(b.OpenTimeUtc); Assert.Null(b.ProviderConfirmedClosed); Assert.Null(b.HistoricalAvailableAtUtc); });
    }
    [Fact]
    public void ConsecutiveDiagnosticsAndSameHeadRecoveryDoNotFabricateInterimBars()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1); f.AddUnavailable(3, 2);
        f.Add(4, 3, head: Mt5ReceiptSyntheticFixture.Head, ev: "source_recovery");
        f.ChangeRecord(4, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var batch = reader.Read(f.DirectoryPath); Assert.True(batch.ReceiptAcknowledgmentEligible);
        Assert.Equal(2, batch.Records.Count(r => r.Observation?.EvidenceKind == Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample));
        Assert.Equal(16, batch.Records.Sum(r => r.Observation!.Timeframes.Sum(f => f.Bars.Count))); // Two real synthetic snapshots only.
        Assert.All(batch.Records.Skip(1).Take(2), r => Assert.False(r.CompleteNativeSampleObserved));
        Assert.Equal(Mt5ReceiptSyntheticFixture.Head, batch.Records[3].Observation!.Metadata.CapturedM1HeadNative!.Raw);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public void InvalidUnavailablePollConditionCannotAuthorizeReceipt(int condition)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1, condition: condition);
        var bad = reader.Read(f.DirectoryPath).Records[1]; Assert.Equal(Mt5ReceiptStatus.Invalid, bad.Status);
        Assert.Contains("invalid_source_unavailable_condition_or_trigger", bad.Diagnostics); Assert.False(bad.ReceiptIntegrityValidated);
        AssertBlocked(f);
    }
    [Fact]
    public void DisconnectedPollCannotClaimAnObservedTriggerHead()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        f.ChangeRecord(2, r => r["trigger_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        Assert.Equal(Mt5ReceiptStatus.Invalid, reader.Read(f.DirectoryPath).Records[1].Status); AssertBlocked(f);
    }
    [Theory]
    [InlineData("probe_hash")]
    [InlineData("predecessor_hash")]
    [InlineData("provider")]
    [InlineData("probe_provider")]
    [InlineData("missing_probe")]
    [InlineData("m1_usable")]
    [InlineData("sample_complete")]
    [InlineData("fabricated_bar")]
    public void InvalidDiagnosticDependenciesAndIdentityStillBlockTheChain(string defect)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        switch (defect)
        {
            case "probe_hash": f.ChangeProbe(2, p => p["connected_after"] = true, false); break;
            case "predecessor_hash": f.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64)); break;
            case "provider": f.ChangeRecord(2, r => r["source"]!["company"] = "wrong"); break;
            case "probe_provider": f.ChangeProbe(2, p => p["source"]!["server"] = "wrong"); break;
            case "missing_probe": File.Delete(f.ProbePath(2)); break;
            case "m1_usable": f.ChangeRecord(2, r => r["m1_usable"] = true); break;
            case "sample_complete": f.ChangeRecord(2, r => r["sample_complete"] = true); break;
            case "fabricated_bar": f.ChangeProbe(2, p => p["timeframes"]![0]!["bars"]!.AsArray().Add(new JsonObject())); break;
        }
        var batch = reader.Read(f.DirectoryPath); Assert.False(batch.ReceiptIntegrityValidated); Assert.False(batch.ReceiptAcknowledgmentEligible);
        Assert.False(batch.SourceIdentityValidated); Assert.False(batch.Records[1].ReceiptAcknowledgmentEligible);
        AssertBlocked(f);
    }
    [Fact]
    public void ExternalRecordHashPinIsNotBypassedForDiagnosticEvents()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        var batch = reader.Read(f.DirectoryPath, expectedRecordHashes: new Dictionary<string, string> { ["record-000000000002.json"] = new('0', 64) });
        Assert.Equal(Mt5ReceiptStatus.Conflict, batch.Records[1].Status); Assert.False(batch.ReceiptAcknowledgmentEligible);
        Assert.Null(batch.Records[1].Observation); Assert.Contains("record_hash_mismatch", batch.Records[1].Diagnostics);
    }
    [Theory]
    [InlineData("unstable")]
    [InlineData("queries_claimed")]
    [InlineData("other_gate")]
    [InlineData("other_event")]
    public void OtherUnresolvedProfilesRemainBlockedRatherThanGeneralized(string variant)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        switch (variant)
        {
            case "unstable": f.ChangeRecord(2, r => r["source_stable"] = false); break;
            case "queries_claimed": f.ChangeProbe(2, p => p["symbol"]!["query_attempted"] = true); break;
            case "other_gate": f.ChangeProbe(2, p => p["gate"] = "tester_rejected"); break;
            case "other_event": f.ChangeRecord(2, r => r["event"] = "initial_snapshot"); break;
        }
        var batch = reader.Read(f.DirectoryPath); Assert.Equal(Mt5ReceiptStatus.Unresolved, batch.Status);
        Assert.True(batch.ReceiptIntegrityValidated); Assert.True(batch.SourceIdentityValidated); Assert.False(batch.ReceiptAcknowledgmentEligible);
        AssertBlocked(f);
    }
    [Fact]
    public void OrdinaryIncompleteSnapshotHasVerifiedIdentityButIsNotEligibleForCliAcknowledgment()
    {
        using var source = new Mt5ReceiptSyntheticFixture(); using var state = new Mt5ReceiptSyntheticFixture(); source.Add();
        source.ChangeRecord(1, r => r["sample_complete"] = false);
        var result = Command(source, Path.Combine(state.DirectoryPath, "receipt.json")); Assert.Equal(4, result.Code);
        Assert.True(result.Report["sourceIdentityValidated"]!.GetValue<bool>()); Assert.True(result.Report["receiptIntegrityValidated"]!.GetValue<bool>());
        Assert.Equal("Validated", result.Report["integrityStatus"]!.GetValue<string>()); Assert.Equal("Unresolved", result.Report["acquisitionStatus"]!.GetValue<string>());
        Assert.Null(result.Report["reviewToken"]);
    }
    [Fact]
    public void MissingPredecessorAndDuplicateDiagnosticPollDoNotBecomeAcknowledgable()
    {
        using var missing = new Mt5ReceiptSyntheticFixture(); missing.AddUnavailable(2, 1);
        Assert.False(reader.Read(missing.DirectoryPath).ReceiptAcknowledgmentEligible); AssertBlocked(missing);
        using var duplicate = new Mt5ReceiptSyntheticFixture(); duplicate.Add(); duplicate.AddUnavailable(2, 1); duplicate.AddUnavailable(3, 2, poll: 2);
        var batch = reader.Read(duplicate.DirectoryPath);
        Assert.Equal(Mt5ReceiptStatus.Conflict, batch.Records[1].Status); Assert.Equal(Mt5ReceiptStatus.Conflict, batch.Records[2].Status);
        AssertBlocked(duplicate);
    }
    [Fact]
    public void RestartAfterDiagnosticKeepsLocalRunAndSequenceIdentity()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1); f.Add(3, 2, run: 3, poll: 1);
        var batch = reader.Read(f.DirectoryPath); Assert.True(batch.ReceiptAcknowledgmentEligible);
        Assert.Equal("run-000000000003", batch.Records[2].Observation!.Metadata.RunId);
        Assert.Null(batch.Records[2].Observation!.Metadata.PreviousCommittedM1HeadNative);
        Assert.False(batch.Records[1].CompleteNativeSampleObserved);
    }
    [Fact]
    public void ExplicitDiagnosticCommitAndRecoveryPreserveAllEvidenceAndNativeUnavailability()
    {
        using var source = new Mt5ReceiptSyntheticFixture(); using var state = new Mt5ReceiptSyntheticFixture();
        source.Add(); source.AddUnavailable(2, 1); source.Add(3, 2);
        source.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var hashes = Directory.GetFiles(source.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var checkpoint = Path.Combine(state.DirectoryPath, "receipt.json");
        var inspect = Command(source, checkpoint); Assert.Equal(0, inspect.Code); Assert.False(File.Exists(checkpoint));
        Assert.True(inspect.Report["sourceIdentityValidated"]!.GetValue<bool>()); Assert.Equal("Validated", inspect.Report["integrityStatus"]!.GetValue<string>());
        Assert.Equal("Unresolved", inspect.Report["acquisitionStatus"]!.GetValue<string>()); Assert.True(inspect.Report["safeToAcknowledge"]!.GetValue<bool>());
        Assert.Equal(3, inspect.Report["newlyValidatedPendingRecords"]!.GetValue<int>()); Assert.Equal(2, inspect.Report["completeNativeSamples"]!.GetValue<int>());
        Assert.Equal(1, inspect.Report["sourceUnavailableDiagnosticEvents"]!.GetValue<int>()); Assert.Equal(0, inspect.Report["blockedRecords"]!.GetValue<int>());
        Assert.Equal(0, Command(source, checkpoint, "commit", inspect.Report).Code);
        var restart = Command(source, checkpoint); Assert.Equal(0, restart.Code);
        Assert.Equal(3, restart.Report["verifiedAcknowledgedRecords"]!.GetValue<int>()); Assert.Equal(0, restart.Report["newlyValidatedPendingRecords"]!.GetValue<int>());
        Assert.Equal("Unresolved", restart.Report["records"]![1]!["status"]!.GetValue<string>());
        Assert.False(restart.Report["records"]![1]!["completeNativeSampleObserved"]!.GetValue<bool>());
        var loaded = new LocalMt5ReceiptCheckpointStore(checkpoint, source.DirectoryPath).Load();
        Assert.True(loaded.Verification!.ReceiptAcknowledgmentEligible); Assert.Equal(hashes[source.RecordPath(2)], loaded.Checkpoint!.Records["record-000000000002.json"].Sha256);
        Assert.Null(loaded.Verification.Records[1].Observation!.Metadata.CapturedM1HeadNative);
        Assert.False(restart.Report["canonicalReady"]!.GetValue<bool>()); Assert.False(restart.Report["utcConversionAuthenticated"]!.GetValue<bool>());
        Assert.All(hashes, h => Assert.Equal(h.Value, Mt5ReceiptSyntheticFixture.Hash(h.Key)));
    }
    [Fact]
    public void DiagnosticHashChangeUnderSameSequenceCannotReplaceCommittedEvidence()
    {
        using var source = new Mt5ReceiptSyntheticFixture(); using var state = new Mt5ReceiptSyntheticFixture(); source.Add(); source.AddUnavailable(2, 1);
        var checkpoint = Path.Combine(state.DirectoryPath, "receipt.json"); var review = Command(source, checkpoint).Report;
        Assert.Equal(0, Command(source, checkpoint, "commit", review).Code); var bytes = File.ReadAllBytes(checkpoint);
        source.ChangeRecord(2, r => r["poll_sequence"] = 8);
        var rejected = Command(source, checkpoint); Assert.NotEqual(0, rejected.Code); Assert.Null(rejected.Report["reviewToken"]);
        Assert.Equal(bytes, File.ReadAllBytes(checkpoint));
    }
    [Fact]
    public void ReviewTokenStillBindsDiagnosticBytesAndPreventsUnreviewedCommit()
    {
        using var source = new Mt5ReceiptSyntheticFixture(); using var state = new Mt5ReceiptSyntheticFixture(); source.Add(); source.AddUnavailable(2, 1);
        var checkpoint = Path.Combine(state.DirectoryPath, "receipt.json"); var review = Command(source, checkpoint).Report;
        source.ChangeRecord(2, r => r["condition"] = 2);
        Assert.Equal(0, Command(source, checkpoint).Code);
        Assert.Equal(5, Command(source, checkpoint, "commit", review).Code); Assert.False(File.Exists(checkpoint));
    }
}
