using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// All fixtures in this suite are synthetic, read through the production receipt trust boundary.
public sealed class Mt5NativeLifecycleTests
{
    private static Mt5ReceiptBatch Read(Mt5ReceiptSyntheticFixture f) => new LocalMt5CollectorReceiptReader().Read(f.DirectoryPath);
    private static Mt5LifecycleReport Audit(Mt5ReceiptSyntheticFixture f) => new Mt5NativeLifecycleAudit().Analyze(Read(f), detailLimit: 128);
    private static JsonObject Frame(JsonObject p, int i = 0) => p["timeframes"]![i]!.AsObject();
    private static JsonObject Bar(JsonObject p, int j = 0) => Frame(p)["bars"]![j]!.AsObject();
    private static Mt5LifecycleBar M1(Mt5LifecycleReport report, long raw = Mt5ReceiptSyntheticFixture.Head) =>
        report.SelectedBars.Single(b => b.Timeframe == "M1" && b.OpenTimeNative.Raw == raw);
    private static void SameHead(Mt5ReceiptSyntheticFixture f, long seq = 2, long previous = 1)
    {
        f.Add(seq, previous, head: Mt5ReceiptSyntheticFixture.Head, ev: "incomplete_snapshot_retry");
        f.ChangeRecord(seq, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
    }

    [Fact]
    public void InitialSnapshotHasOnlyObservedFactsAndInsufficientStabilityCoverage()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var report = Audit(f); var b = M1(report);
        Assert.Equal(4, report.Timeframes.Count); Assert.Equal(8, report.Timeframes.Sum(t => t.ObservedBars));
        Assert.Equal(1, b.Snapshots); Assert.Equal(0, b.FirstPosition); Assert.True(b.ObservedActiveHead);
        Assert.False(b.ActiveThenHistorical); Assert.Equal(2, report.Timeframes.Single(t => t.Timeframe == "M1").InsufficientCoverage);
        Assert.Null(b.ProviderConfirmedClosed); Assert.Null(b.OpenTimeUtc); Assert.Null(b.HistoricalAvailableAtUtc);
        Assert.False(report.CanonicalReady); Assert.False(report.UtcConversionAuthenticated); Assert.False(report.CanonicalUtcConversionAuthorized);
        Assert.Equal("unresolved", report.ProviderClosure); Assert.Equal("unresolved", report.HistoricalAvailability);
        Assert.Equal("data_unavailable", report.VolumeEvidence);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), b.FirstObserved.RecordSha256);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)), b.FirstObserved.ProbeSha256);
    }

    [Fact]
    public void AdvancementAndHistoricalPositionDoNotAttestClosure()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1); var report = Audit(f); var b = M1(report);
        Assert.True(b.ActiveThenHistorical); Assert.Equal(1, b.LastPosition); Assert.Equal(2, b.Snapshots);
        Assert.Equal(1, report.Timeframes.Single(t => t.Timeframe == "M1").HeadAdvancements);
        Assert.Null(b.ProviderConfirmedClosed); Assert.Null(b.HistoricalAvailableAtUtc);
        Assert.Equal(0, b.RevisionCount); Assert.Equal(1, b.IdenticalComparisons);
    }

    [Fact]
    public void UnchangedActiveHeadIsStableOnlyAcrossAvailableObservations()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f); var b = M1(Audit(f));
        Assert.Equal(2, b.Snapshots); Assert.Equal(1, b.IdenticalComparisons); Assert.Equal(0, b.RevisionCount);
        Assert.False(b.ObservedHistoricalPosition); Assert.Null(b.ProviderConfirmedClosed);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void ActiveAndSupersededRevisionsRetainExactBeforeAfterProvenance(int position, bool advance)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        if (advance) f.Add(2, 1); else SameHead(f);
        f.ChangeProbe(2, p => Bar(p, position)["close"] = JsonNode.Parse("101.25000000000000000000000000000001"));
        var report = Audit(f); var b = M1(report); var revision = Assert.Single(b.Revisions);
        Assert.Equal(1, revision.Before.Sequence); Assert.Equal(2, revision.After.Sequence);
        Assert.Equal("101.25", revision.BeforeValues.Close);
        Assert.Equal("101.25000000000000000000000000000001", revision.AfterValues.Close);
        Assert.Equal(position, revision.AfterPosition); Assert.Equal(1, b.RevisionCount);
        Assert.Equal(advance, b.ActiveThenHistorical);
    }

    [Fact]
    public void RevisionAfterHistoricalPositionIsSeparatelyObservable()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head + 60, ev: "incomplete_snapshot_retry");
        f.ChangeProbe(3, p => Bar(p, 1)["close"] = 101.5m);
        var revision = Assert.Single(M1(Audit(f)).Revisions);
        Assert.Equal(1, revision.BeforePosition); Assert.Equal(1, revision.AfterPosition);
    }

    [Theory]
    [InlineData("101.2500")]
    [InlineData("1.0125e2")]
    public void ExactComparisonIgnoresOnlyEquivalentNumericSpelling(string literal)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f);
        f.ChangeProbe(2, p => Bar(p)["close"] = JsonNode.Parse(literal));
        var b = M1(Audit(f)); Assert.Equal(0, b.RevisionCount); Assert.Equal(1, b.IdenticalComparisons);
        Assert.Equal(literal, b.LastValues.Close);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EmptyAndPartialSamplesRemainIncompleteWithoutInventedBars(int count)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p =>
        {
            var frame = Frame(p); var bars = frame["bars"]!.AsArray(); while (bars.Count > count) bars.RemoveAt(bars.Count - 1);
            if (count > 0) bars[0]!["newer_than_next_seconds"] = null;
            frame["copy_return"] = count; frame["status"] = count == 0 ? "empty" : "partial_sample";
        });
        f.ChangeRecord(1, r => { r["sample_complete"] = false; r["m1_usable"] = count > 0; if (count == 0) r["captured_m1_head_native"] = null; });
        var report = Audit(f); Assert.Equal(1, report.ValidatedRecords); Assert.Equal(0, report.BlockedRecords);
        Assert.Equal(count, report.Timeframes.Single(t => t.Timeframe == "M1").ObservedBars);
        Assert.Contains("sample_incomplete", report.Diagnostics.Keys);
    }

    [Fact]
    public void UnavailableThenRecoveryRetainsInterruptionWithoutZeroPriceOrContinuity()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head, ev: "source_recovery");
        f.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var report = Audit(f); Assert.Equal(3, report.ValidatedRecords); Assert.Equal(1, report.Events["source_unavailable"]);
        Assert.Equal(8, report.Timeframes.Sum(t => t.ObservedBars)); Assert.True(M1(report).SpansInterruption);
        Assert.Equal(0, report.Timeframes.Single(t => t.Timeframe == "M1").HeadAdvancements);
    }

    [Fact]
    public void IncompleteAcquisitionDoesNotJoinNextSampleAsContinuousCoverage()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeRecord(1, r => r["sample_complete"] = false); SameHead(f);
        var b = M1(Audit(f)); Assert.Equal(2, b.Snapshots); Assert.True(b.SpansInterruption);
        Assert.Equal(0, b.RevisionCount);
    }

    [Fact]
    public void ShutdownAndNewRunDoNotImplyContinuousAcquisition()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        f.ChangeRecord(2, r =>
        {
            r["event"] = "shutdown"; r["probe_file"] = null; r["probe_sha256"] = null;
            r["captured_m1_head_native"] = null; r["m1_usable"] = false; r["sample_complete"] = false;
            r["source_stable"] = false; r["shutdown_reason"] = 1;
        });
        f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head, run: 3);
        var report = Audit(f); Assert.Equal(3, report.ValidatedRecords); Assert.Equal(2, report.RunIds.Count);
        Assert.Equal(1, report.Events["shutdown"]); Assert.True(M1(report).SpansInterruption);
    }

    [Fact]
    public void SequenceAndNativeHeadGapsDoNotSynthesizeCandles()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(3, 1, head: Mt5ReceiptSyntheticFixture.Head + 3600);
        var report = Audit(f); Assert.Equal(2, report.ValidatedRecords);
        Assert.Contains("publication_sequence_gap_cause_unresolved", report.Diagnostics.Keys);
        Assert.Equal(4, report.Timeframes.Single(t => t.Timeframe == "M1").ObservedBars);
        Assert.Equal(0, report.Timeframes.Single(t => t.Timeframe == "M1").HeadAdvancements);
    }

    [Fact]
    public void NativeCoordinateDeltasDescribeOnlySampledGaps()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p =>
        {
            Bar(p, 1)["open_time_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head - 3600);
            Bar(p)["newer_than_next_seconds"] = 3600;
        });
        f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head + 3600);
        var m1 = Audit(f).Timeframes.Single(t => t.Timeframe == "M1");
        Assert.Equal(1, m1.NonPeriodAdjacentDeltas); Assert.Equal(1, m1.NonPeriodHeadAdvancements);
        Assert.Equal(4, m1.ObservedBars);
    }

    [Fact]
    public void RegressedHeadIsNotCountedAsAdvancement()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head - 60, ev: "native_head_regressed");
        f.ChangeRecord(2, r => r["condition"] = 3);
        var report = Audit(f); Assert.Equal(2, report.ValidatedRecords);
        var frame = report.Timeframes.Single(t => t.Timeframe == "M1");
        Assert.Equal(1, frame.HeadRegressions); Assert.Equal(0, frame.HeadAdvancements);
        Assert.Equal(3, frame.ObservedBars);
    }

    [Theory]
    [InlineData("unstable")]
    [InlineData("invalid_ohlc")]
    [InlineData("inconsistent_head")]
    public void UnusableFramesRetainDiagnosticsWithoutBarClaims(string condition)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        if (condition == "unstable") f.ChangeRecord(1, r => r["source_stable"] = false);
        else
        {
            f.ChangeProbe(1, p =>
            {
                if (condition == "invalid_ohlc") { Bar(p)["close"] = 200; Bar(p)["ohlc_valid"] = false; }
                else
                {
                    Frame(p)["head_consistent"] = false;
                    foreach (var b in Frame(p)["bars"]!.AsArray()) b!["role"] = "unverified_head";
                }
            });
            f.ChangeRecord(1, r => { r["sample_complete"] = false; r["m1_usable"] = false; });
        }
        var report = Audit(f); Assert.Equal(1, report.ValidatedRecords); Assert.Equal(0, report.BlockedRecords);
        var frame = report.Timeframes.Single(t => t.Timeframe == "M1"); Assert.Equal(0, frame.ObservedBars); Assert.Equal(1, frame.SkippedFrames);
    }

    [Fact]
    public void RevisionDetailsAreBoundedWhileAggregateCountsRemainComplete()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f);
        f.ChangeProbe(2, p => Bar(p)["close"] = 101.5m); SameHead(f, 3, 2);
        var report = new Mt5NativeLifecycleAudit().Analyze(Read(f), detailLimit: 1); var b = Assert.Single(report.SelectedBars);
        Assert.Equal(2, b.RevisionCount); Assert.Single(b.Revisions); Assert.Equal(1, b.OmittedRevisions);
        Assert.Equal(7, report.OmittedBars); Assert.Equal(2, report.OmittedEvents);
    }

    [Fact]
    public void EmptyInventoryReturnsDataUnavailable()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); var report = Audit(f);
        Assert.Equal("data_unavailable", report.Status); Assert.Empty(report.Timeframes); Assert.Empty(report.SelectedBars);
    }

    [Theory]
    [InlineData("missing_probe")]
    [InlineData("corrupt_probe")]
    [InlineData("source")]
    [InlineData("chain")]
    [InlineData("missing_record")]
    [InlineData("duplicate_poll")]
    public void InvalidOrMissingEvidenceCannotSupplyLifecycleFacts(string failure)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        if (failure is "chain" or "missing_record" or "duplicate_poll") f.Add(2, 1);
        switch (failure)
        {
            case "missing_probe": File.Delete(f.ProbePath(1)); break;
            case "corrupt_probe": File.AppendAllText(f.ProbePath(1), "broken"); break;
            case "source": f.ChangeRecord(1, r => r["source"]!["server"] = "untrusted"); break;
            case "chain": f.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64)); break;
            case "missing_record": File.Delete(f.RecordPath(1)); break;
            case "duplicate_poll": f.ChangeRecord(2, r => r["poll_sequence"] = 1); break;
        }
        var report = Audit(f); Assert.True(report.BlockedRecords > 0); Assert.False(report.CanonicalReady);
        Assert.True(report.Timeframes.Sum(t => t.ObservedBars) <= 8);
    }

    [Fact]
    public void DuplicateDeliveryIsExcludedAndNeverDoubleCounted()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var batch = Read(f);
        var duplicate = batch with { Records = batch.Records.Concat(batch.Records).ToArray() };
        var report = new Mt5NativeLifecycleAudit().Analyze(duplicate);
        Assert.Equal(0, report.ValidatedRecords); Assert.Empty(report.SelectedBars);
        Assert.Equal(2, report.Diagnostics["duplicate_receipt_excluded"]);
    }

    [Fact]
    public void LaterSnapshotCannotRewriteEarlierCollectorPrefixOrAlreadyReturnedFacts()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var analyzer = new Mt5NativeLifecycleAudit();
        var before = analyzer.Analyze(Read(f), 1); var frozen = JsonSerializer.Serialize(before);
        SameHead(f); f.ChangeProbe(2, p => Bar(p)["close"] = 101.5m);
        var after = analyzer.Analyze(Read(f), 1);
        Assert.Equal(frozen, JsonSerializer.Serialize(after)); Assert.Equal(frozen, JsonSerializer.Serialize(before));
        Assert.Equal(1, M1(Audit(f)).RevisionCount);
    }

    [Fact]
    public void IdenticalInputsAreDeterministicAndSourceAndCheckpointStayUnchanged()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f); var batch = Read(f);
        var checkpoint = JsonSerializer.Serialize(batch.Checkpoint);
        var hashes = Directory.GetFiles(f.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var audit = new Mt5NativeLifecycleAudit(); var a = JsonSerializer.Serialize(audit.Analyze(batch));
        Assert.Equal(a, JsonSerializer.Serialize(audit.Analyze(batch)));
        Assert.Equal(checkpoint, JsonSerializer.Serialize(batch.Checkpoint));
        Assert.Equal(hashes.Count, Directory.GetFiles(f.DirectoryPath).Length);
        foreach (var item in hashes) Assert.Equal(item.Value, Mt5ReceiptSyntheticFixture.Hash(item.Key));
    }

    [Fact]
    public void CommandProducesBoundedSeparateReportConciseSummaryAndNeverOverwrites()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); using var destination = new Mt5ReceiptSyntheticFixture(); f.Add();
        var path = Path.Combine(destination.DirectoryPath, "audit.json");
        var args = new[] { "mt5-lifecycle", "--source", f.DirectoryPath, "--report", path, "--details", "1" };
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, new Mt5LifecycleCommand().Execute(args, output, error));
        Assert.True(output.ToString().Length < 4000); Assert.Equal("", error.ToString());
        var report = JsonNode.Parse(File.ReadAllText(path))!["audit"]!;
        Assert.Single(report["selected_bars"]!.AsArray()); Assert.Equal(7, report["omitted_bars"]!.GetValue<int>());
        var hash = Mt5ReceiptSyntheticFixture.Hash(path);
        Assert.Equal(4, new Mt5LifecycleCommand().Execute(args, TextWriter.Null, TextWriter.Null));
        Assert.Equal(hash, Mt5ReceiptSyntheticFixture.Hash(path));
        Assert.Equal(2, new Mt5LifecycleCommand().Execute(["mt5-lifecycle", "--source", f.DirectoryPath, "--report", Path.Combine(f.DirectoryPath, "bad.json")], TextWriter.Null, TextWriter.Null));
        Assert.Equal(2, Directory.GetFiles(f.DirectoryPath).Length);
    }
}
