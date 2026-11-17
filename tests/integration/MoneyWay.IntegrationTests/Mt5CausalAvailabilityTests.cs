using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// Synthetic file-backed prefixes. Authentic MT5 evidence is exercised separately through the Worker CLI.
public sealed class Mt5CausalAvailabilityTests
{
    private static Mt5ReceiptPrefixBoundary Boundary(Mt5ReceiptSyntheticFixture f, long sequence) =>
        new(sequence, Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(sequence)));
    private static Mt5ReceiptPrefixSelection Select(Mt5ReceiptSyntheticFixture f, Mt5ReceiptPrefixBoundary boundary) =>
        ValidatedMt5ReceiptPrefix.Select(new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary), boundary);
    private static Mt5CausalAvailabilityProjection Project(Mt5ReceiptSyntheticFixture f, Mt5ReceiptPrefixBoundary boundary) =>
        new Mt5CausalAvailabilityAudit().Project(Assert.IsType<ValidatedMt5ReceiptPrefix>(Select(f, boundary).Prefix), 128);
    private static string Json(Mt5CausalAvailabilityProjection p) => JsonSerializer.Serialize(p.Report(128));
    private static JsonObject Frame(JsonObject p, int frame = 0) => p["timeframes"]![frame]!.AsObject();
    private static JsonObject Bar(JsonObject p, int position = 0) => Frame(p)["bars"]![position]!.AsObject();
    private static Mt5CausalBarObservation M1(Mt5CausalAvailabilityProjection p) => p.Find("M1", Mt5ReceiptSyntheticFixture.Head)!;
    private static void SameHead(Mt5ReceiptSyntheticFixture f, long n = 2, long previous = 1)
    {
        f.Add(n, previous, head: Mt5ReceiptSyntheticFixture.Head, ev: "incomplete_snapshot_retry");
        f.ChangeRecord(n, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
    }
    private static void Shutdown(Mt5ReceiptSyntheticFixture f, long n, long previous)
    {
        f.Add(n, previous, poll: previous);
        f.ChangeRecord(n, r =>
        {
            r["event"] = "shutdown"; r["probe_file"] = null; r["probe_sha256"] = null;
            r["shutdown_reason"] = 0; r["captured_m1_head_native"] = null;
            r["m1_usable"] = false; r["sample_complete"] = false; r["source_stable"] = false;
        });
    }

    [Fact]
    public void MinimalPrefixExposesExactProvenanceAndIndependentFrames()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Project(f, Boundary(f, 1));
        Assert.Single(p.Prefix.Records); Assert.Equal(8, p.Bars.Count);
        Assert.Equal(new[] { "H1", "H4", "M1", "M5" }, p.Lifecycle.Timeframes.Select(x => x.Timeframe));
        var b = M1(p); Assert.Equal("100.25", b.FirstValues.Open); Assert.Equal("101.25", b.Bar.LastValues.Close);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), b.Bar.FirstObserved.RecordSha256);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)), b.Bar.LastObserved.ProbeSha256);
        Assert.Equal("run-000000000001", b.Bar.FirstObserved.RunId);
        Assert.Equal("active_head_potentially_incomplete", b.LatestPosition);
        Assert.Equal((ulong)10, b.LatestAcquisition.Frames.Single(x => x.Timeframe == "M1").CopyStartedElapsedUs);
    }

    [Fact]
    public void ExplicitEmptyPrefixIsValidWithoutAssertingSourceIdentity()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var selection = Select(f, new(0)); Assert.Equal(Mt5ReceiptStatus.Validated, selection.Status);
        var p = new Mt5CausalAvailabilityAudit().Project(selection.Prefix!);
        Assert.Empty(p.Bars); Assert.Null(p.Prefix.FinalReceipt);
        var report = JsonNode.Parse(Json(p))!;
        Assert.False(report["Source"]!["IdentityValidated"]!.GetValue<bool>());
        Assert.False(report["SourceObservationsAvailable"]!.GetValue<bool>());
    }

    [Fact]
    public void UnavailableDirectoryIsDifferentFromEmptyValidPrefix()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); var boundary = new Mt5ReceiptPrefixBoundary(0);
        var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(Path.Combine(f.DirectoryPath, "absent"), boundary);
        Assert.Null(ValidatedMt5ReceiptPrefix.Select(batch, boundary).Prefix);
        Assert.Contains("source_directory_unavailable", batch.Diagnostics);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BoundariesBeforeAndAfterAdvancementSeeOnlyTheirOwnPositions(long n)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        var p = Project(f, Boundary(f, n)); var bar = M1(p).Bar;
        Assert.Equal(n == 2, bar.ActiveThenHistorical); Assert.Equal(n - 1, bar.LastPosition);
        Assert.Equal(n, p.Prefix.FinalReceipt!.Sequence);
    }

    [Fact]
    public void FutureRevisionCannotAlterEarlierPrefixAndLaterPrefixRetainsBothValues()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var first = Project(f, boundary); var original = Json(first);
        f.Add(2, 1); f.ChangeProbe(2, p => Bar(p, 1)["close"] = JsonNode.Parse("101.25000000000000000000000000000001"));
        Assert.Equal(original, Json(Project(f, boundary))); Assert.Equal(original, Json(first));
        var later = M1(Project(f, Boundary(f, 2))); var revision = Assert.Single(later.Bar.Revisions);
        Assert.Equal("101.25", later.FirstValues.Close);
        Assert.Equal("101.25000000000000000000000000000001", later.Bar.LastValues.Close);
        Assert.Equal(1, revision.Before.Sequence); Assert.Equal(2, revision.After.Sequence);
    }

    [Fact]
    public void FutureHistoricalObservationDoesNotSupersedeEarlierActiveObservation()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var snapshot = Json(Project(f, boundary)); f.Add(2, 1);
        Assert.Equal(snapshot, Json(Project(f, boundary)));
        Assert.False(M1(Project(f, boundary)).Bar.ObservedHistoricalPosition);
    }

    [Fact]
    public void FutureDiagnosticDoesNotChangeEarlierAcquisitionStatus()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var snapshot = Json(Project(f, boundary)); f.AddUnavailable(2, 1);
        Assert.Equal(snapshot, Json(Project(f, boundary)));
        var p = Project(f, Boundary(f, 2)); Assert.Equal(8, p.Bars.Count);
        Assert.Equal(nameof(Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample), p.Acquisitions[1].EvidenceKind);
        Assert.True(p.Acquisitions[1].Frames.All(x => x.ObservedBars == 0));
    }

    [Fact]
    public void FutureRecoveryCannotBackfillUnavailablePrefix()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1); var boundary = Boundary(f, 2);
        var snapshot = Json(Project(f, boundary)); f.Add(3, 2, ev: "source_recovery");
        f.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        Assert.Equal(snapshot, Json(Project(f, boundary)));
        var p = Project(f, Boundary(f, 3)); Assert.Equal(3, p.Acquisitions.Count);
        Assert.False(p.Acquisitions[1].SampleComplete); Assert.True(p.Acquisitions[2].SampleComplete);
    }

    [Fact]
    public void FutureRunCannotAffectEarlierProjectionAndHashChainCrossesRuns()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var snapshot = Json(Project(f, boundary)); f.Add(2, 1, run: 2, poll: 1);
        Assert.Equal(snapshot, Json(Project(f, boundary)));
        var p = Project(f, Boundary(f, 2)); Assert.Equal(2, p.Lifecycle.RunBoundaries.Count);
        Assert.Equal(2, p.Prefix.FinalReceipt!.Sequence); Assert.Equal("run-000000000002", p.Prefix.FinalReceipt.RunId);
    }

    [Fact]
    public void FutureDuplicatePollOrCorruptionIsExcludedBeforeValidation()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var snapshot = Json(Project(f, boundary)); f.Add(2, 1, poll: 1);
        Assert.False(new LocalMt5CollectorReceiptReader().Read(f.DirectoryPath).ReceiptIntegrityValidated);
        Assert.Equal(snapshot, Json(Project(f, boundary)));
        File.WriteAllText(f.RecordPath(2), "corrupt future record");
        using var locked = new FileStream(f.RecordPath(2), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(snapshot, Json(Project(f, boundary)));
    }

    [Fact]
    public void MoreFutureRecordsThanWholeDirectoryLimitCannotInvalidatePinnedPrefix()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var snapshot = Json(Project(f, boundary));
        for (var n = 2; n < LocalMt5CollectorReceiptReader.MaxDirectoryEntries + 4; n++) File.WriteAllText(f.RecordPath(n), "future");
        Assert.Equal(snapshot, Json(Project(f, boundary)));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(19)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(999999999999)]
    public void DecimalFilenameRangesHandleCarriesAndSparsePublicationGaps(long n)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(n, 1, head: Mt5ReceiptSyntheticFixture.Head + 60);
        if (n < 999999999999) File.WriteAllText(f.RecordPath(n + 1), "unrelated");
        var p = Project(f, Boundary(f, n)); Assert.Equal(2, p.Prefix.Records.Count);
        Assert.Contains("publication_sequence_gap_cause_unresolved", p.Prefix.Records[1].Diagnostics);
    }

    [Theory]
    [InlineData("missing_predecessor")]
    [InlineData("missing_probe")]
    [InlineData("corrupt_chain")]
    [InlineData("source")]
    [InlineData("duplicate_poll")]
    [InlineData("unanchored_origin")]
    [InlineData("fork")]
    public void InvalidOrAmbiguousSelectedLineageProducesNoProjection(string fault)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        long last = 2;
        switch (fault)
        {
            case "missing_predecessor": File.Delete(f.RecordPath(1)); break;
            case "missing_probe": File.Delete(f.ProbePath(1)); break;
            case "corrupt_chain": f.ChangeRecord(2, r => r["previous_record_sha256"] = new string('0', 64)); break;
            case "source": f.ChangeRecord(1, r => r["source"]!["server"] = "wrong"); break;
            case "duplicate_poll": f.ChangeRecord(2, r => r["poll_sequence"] = 1); break;
            case "unanchored_origin": f.ChangeRecord(2, r => { r["previous_record"] = null; r["previous_record_sha256"] = null; }); File.Delete(f.RecordPath(1)); break;
            case "fork": f.Add(3, 1); last = 3; break;
        }
        Assert.Null(Select(f, Boundary(f, last)).Prefix);
    }

    [Fact]
    public void BoundaryHashDetectsChangedEvidenceAndOldInMemoryProjectionRemainsImmutable()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var p = Project(f, boundary); var snapshot = Json(p);
        f.ChangeProbe(1, probe => Bar(probe)["close"] = 101.5m);
        Assert.Equal(snapshot, Json(p)); Assert.Null(Select(f, boundary).Prefix);
    }

    [Fact]
    public void ProbeTamperingWithoutRecordRebindingIsDetected()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        f.ChangeProbe(1, p => Bar(p)["close"] = 101.5m, rebind: false);
        Assert.Null(Select(f, boundary).Prefix);
    }

    [Fact]
    public void DuplicateDeliveriesAndFutureBatchInputsAreRejectedByPrefixPrimitive()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary);
        Assert.Null(ValidatedMt5ReceiptPrefix.Select(batch with { Records = batch.Records.Concat(batch.Records).ToArray() }, boundary).Prefix);
        f.Add(2, 1);
        Assert.Null(ValidatedMt5ReceiptPrefix.Select(new LocalMt5CollectorReceiptReader().Read(f.DirectoryPath), boundary).Prefix);
    }

    [Fact]
    public void PrefixCopiesCallerOwnedCollections()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary);
        var bars = batch.Records[0].Observation!.Timeframes[0].Bars.ToArray();
        var frames = batch.Records[0].Observation!.Timeframes.ToArray(); frames[0] = frames[0] with { Bars = bars };
        var records = batch.Records.ToArray(); records[0] = records[0] with { Observation = records[0].Observation! with { Timeframes = frames } };
        var prefix = ValidatedMt5ReceiptPrefix.Select(batch with { Records = records }, boundary).Prefix!;
        bars[0] = bars[0] with { CloseLiteral = "999" }; frames[0] = frames[0] with { Bars = Array.Empty<Mt5NativeBar>() };
        Assert.Equal(8, new Mt5CausalAvailabilityAudit().Project(prefix).Bars.Count);
        Assert.Equal("101.25", new Mt5CausalAvailabilityAudit().Project(prefix).Find("M1", Mt5ReceiptSyntheticFixture.Head)!.Bar.LastValues.Close);
    }

    [Fact]
    public void PartialAndUnstableSamplesPreserveObservedValuesAndQuality()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => { Frame(p)["bars"]!.AsArray().RemoveAt(1); Bar(p)["newer_than_next_seconds"] = null; Frame(p)["copy_return"] = 1; Frame(p)["status"] = "partial_sample"; });
        f.ChangeRecord(1, r => { r["sample_complete"] = false; r["source_stable"] = false; });
        var p = Project(f, Boundary(f, 1)); Assert.Equal(7, p.Bars.Count);
        Assert.False(M1(p).LatestAcquisition.SampleComplete); Assert.False(M1(p).LatestAcquisition.SourceStable);
        Assert.Equal("101.25", M1(p).Bar.LastValues.Close);
    }

    [Fact]
    public void InvalidOhlcIsRetainedAsDiagnosticRawEvidenceWithItsFlag()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => { Bar(p)["high"] = 98m; Bar(p)["ohlc_valid"] = false; });
        f.ChangeRecord(1, r => { r["m1_usable"] = false; r["sample_complete"] = false; });
        var p = Project(f, Boundary(f, 1)); Assert.False(M1(p).LatestOhlcValid);
        Assert.Equal("98", M1(p).Bar.LastValues.High); Assert.Contains("invalid_ohlc_retained_as_diagnostic", p.Lifecycle.Diagnostics.Keys);
    }

    [Fact]
    public void ShutdownIsAReceiptWithoutASyntheticMarketSample()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); Shutdown(f, 2, 1);
        var p = Project(f, Boundary(f, 2)); Assert.Equal(8, p.Bars.Count);
        Assert.Equal(nameof(Mt5ReceiptEvidenceKind.Shutdown), p.Acquisitions[1].EvidenceKind);
        Assert.Empty(p.Acquisitions[1].Frames);
    }

    [Fact]
    public void NativeGapsAndDistinctTimeframeCoordinatesRemainNative()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head + 3600);
        var p = Project(f, Boundary(f, 2)); Assert.Equal(1, p.Lifecycle.Timeframes.Single(x => x.Timeframe == "M1").NonPeriodHeadAdvancements);
        Assert.Null(p.Find("M1", Mt5ReceiptSyntheticFixture.Head + 60));
        Assert.All(p.Bars, b => { Assert.Null(b.Bar.OpenTimeUtc); Assert.Null(b.Bar.ProviderConfirmedClosed); Assert.Null(b.Bar.HistoricalAvailableAtUtc); });
    }

    [Theory]
    [InlineData("101.2500", 0)]
    [InlineData("1.0125e2", 0)]
    [InlineData("101.25000000000000000000000000000001", 1)]
    public void ExistingExactOhlcComparisonRetainsLiteralPrecision(string literal, int revisions)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f);
        f.ChangeProbe(2, p => Bar(p)["close"] = JsonNode.Parse(literal));
        var b = M1(Project(f, Boundary(f, 2))); Assert.Equal(revisions, b.Bar.RevisionCount); Assert.Equal(literal, b.Bar.LastValues.Close);
    }

    [Fact]
    public void DeterministicProjectionAndQueryDoNotInferVolumeUtcOrAtomicity()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        Assert.Equal(Json(Project(f, boundary)), Json(Project(f, boundary)));
        var p = Project(f, boundary); var json = JsonNode.Parse(Json(p))!;
        Assert.Equal("unresolved", json["HistoricalUtcAvailability"]!.GetValue<string>());
        Assert.Equal("unresolved", json["ProviderClosure"]!.GetValue<string>());
        Assert.Equal("unresolved", json["AtomicMultiTimeframeSnapshot"]!.GetValue<string>());
        Assert.Equal("data_unavailable", json["VolumeEvidence"]!.GetValue<string>());
        Assert.False(json["CanonicalReady"]!.GetValue<bool>()); Assert.False(json["UtcConversionAuthenticated"]!.GetValue<bool>());
        Assert.Null(p.Find("M1", 1)); Assert.NotNull(p.Find("M1", Mt5ReceiptSyntheticFixture.Head));
    }

    [Fact]
    public void MissingPinnedBoundaryAndWrongHashAreUnavailableOrConflicting()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, Select(f, new(2, new string('0', 64))).Status);
        Assert.Equal(Mt5ReceiptStatus.Conflict, Select(f, new(1, new string('0', 64))).Status);
        Assert.Throws<ArgumentException>(() => new Mt5ReceiptPrefixBoundary(1));
        Assert.Throws<ArgumentException>(() => new Mt5ReceiptPrefixBoundary(0, new string('0', 64)));
        Assert.Throws<ArgumentException>(() => new Mt5ReceiptPrefixBoundary(-1));
    }

    [Fact]
    public void BoundaryPinAndAdditionalPinsMustHaveOneScope()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var boundary = Boundary(f, 1);
        Assert.Throws<ArgumentException>(() => new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary,
            new Dictionary<string, string> { ["record-000000000002.json"] = new string('0', 64) }));
    }

    [Fact]
    public void CliIsBoundedCreateOnlyAndPreservesSourceAndCheckpoint()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); using var dest = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        var boundary = Boundary(f, 2); var checkpoint = Path.Combine(dest.DirectoryPath, "checkpoint.json");
        File.WriteAllText(checkpoint, "checkpoint-sentinel");
        var paths = Directory.GetFiles(f.DirectoryPath).Append(checkpoint).ToArray();
        var pins = paths.Select(Mt5ReceiptSyntheticFixture.Hash).ToArray();
        var reportPath = Path.Combine(dest.DirectoryPath, "report.json");
        string[] args = ["mt5-causal-availability", "--source", f.DirectoryPath, "--through-sequence", "2", "--through-sha256", boundary.RecordSha256!,
            "--report", reportPath, "--details", "1", "--timeframe", "M1", "--native-open", Mt5ReceiptSyntheticFixture.Head.ToString()];
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, new Mt5CausalAvailabilityCommand().Execute(args, output, error)); Assert.Equal("", error.ToString());
        Assert.True(output.ToString().Length < 3000); Assert.Equal(pins, paths.Select(Mt5ReceiptSyntheticFixture.Hash));
        var report = JsonNode.Parse(File.ReadAllText(reportPath))!["audit"]!;
        Assert.Single(report["selected_bars"]!.AsArray()); Assert.Single(report["selected_acquisitions"]!.AsArray());
        Assert.True(report["requested_observation"]!["available_in_prefix"]!.GetValue<bool>());
        Assert.Equal(4, new Mt5CausalAvailabilityCommand().Execute(args, TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(reportPath + ".pending"));
    }

    [Fact]
    public void CliReportsInvalidPrefixAndRejectsUnsafeOrIncompleteArguments()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); using var dest = new Mt5ReceiptSyntheticFixture(); f.Add();
        var path = Path.Combine(dest.DirectoryPath, "bad.json");
        Assert.Equal(3, new Mt5CausalAvailabilityCommand().Execute(["mt5-causal-availability", "--source", f.DirectoryPath,
            "--through-sequence", "1", "--through-sha256", new string('0', 64), "--report", path], TextWriter.Null, TextWriter.Null));
        var report = JsonNode.Parse(File.ReadAllText(path))!["audit"]!;
        Assert.False(report["source_observations_available"]!.GetValue<bool>());
        Assert.Equal(2, new Mt5CausalAvailabilityCommand().Execute(["mt5-causal-availability", "--source", f.DirectoryPath,
            "--through-sequence", "1"], TextWriter.Null, TextWriter.Null));
        Assert.Equal(2, new Mt5CausalAvailabilityCommand().Execute(["mt5-causal-availability", "--source", f.DirectoryPath,
            "--through-sequence", "0", "--report", Path.Combine(f.DirectoryPath, "unsafe.json")], TextWriter.Null, TextWriter.Null));
    }
}
