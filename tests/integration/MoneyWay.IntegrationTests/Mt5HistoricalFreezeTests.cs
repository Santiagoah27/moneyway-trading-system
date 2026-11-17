using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

// Synthetic source evolution, not evidence of real OANDA historical revisions.
public sealed class Mt5HistoricalFreezeTests
{
    private static Mt5ReceiptPrefixBoundary Boundary(Mt5ReceiptSyntheticFixture f, long n) => new(n, Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(n)));
    private static Mt5CausalAvailabilityProjection Project(Mt5ReceiptSyntheticFixture f, long n, int details = 128) =>
        Project(f, Boundary(f, n), details);
    private static Mt5CausalAvailabilityProjection Project(Mt5ReceiptSyntheticFixture f, Mt5ReceiptPrefixBoundary boundary, int details = 128)
    {
        var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary);
        var selection = ValidatedMt5ReceiptPrefix.Select(batch, boundary);
        Assert.True(selection.Prefix is not null, string.Join(";", selection.Diagnostics.Concat(batch.Records.SelectMany(r => r.Diagnostics))));
        var prefix = selection.Prefix!;
        return new Mt5CausalAvailabilityAudit().Project(prefix, details);
    }
    private static Mt5HistoricalFreeze Freeze(Mt5CausalAvailabilityProjection p, long raw = Mt5ReceiptSyntheticFixture.Head, string frame = "M1") =>
        Assert.IsType<Mt5HistoricalFreeze>(p.FindHistoricalFreeze(frame, raw));
    private static JsonObject Bar(JsonObject p, int position = 1, int frame = 0) => p["timeframes"]![frame]!["bars"]![position]!.AsObject();
    private static string Report(Mt5CausalAvailabilityProjection p) => JsonSerializer.Serialize(p.HistoricalFreezeReport(128));
    private static void SameHead(Mt5ReceiptSyntheticFixture f, long n, long previous, long head)
    {
        f.Add(n, previous, head: head, ev: "incomplete_snapshot_retry");
        f.ChangeRecord(n, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(head));
    }

    [Fact]
    public void DirectHistoricalFirstNeedsNoInventedActiveObservation()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Project(f, 1);
        var b = Freeze(p, Mt5ReceiptSyntheticFixture.Head - 60);
        Assert.Null(b.FirstActive); Assert.Equal("directly_historical_first", b.Classification);
        Assert.Equal(b.FirstHistorical, b.FirstUsableHistorical);
        Assert.Equal(1, b.FirstHistorical!.SourcePosition); Assert.Equal("101.25", b.FirstUsableHistorical!.Values.Close);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.ProbePath(1)), b.FirstHistorical.Receipt.ProbeSha256);
        Assert.Equal(Mt5ReceiptSyntheticFixture.Hash(f.RecordPath(1)), b.FirstHistorical.Receipt.RecordSha256);
        Assert.Equal(1, b.HistoricalObservations);
        var active = Freeze(p); Assert.Equal("active_only", active.Classification);
        Assert.Equal(0, active.FirstActive!.SourcePosition); Assert.Null(active.FirstHistorical); Assert.Null(active.FirstUsableHistorical);
    }

    [Fact]
    public void TransitionFreezesHistoricalValuesRatherThanActiveOrLatestValues()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1);
        f.ChangeProbe(2, p => Bar(p)["close"] = 101.5m);
        SameHead(f, 3, 2, Mt5ReceiptSyntheticFixture.Head + 60);
        f.ChangeProbe(3, p => Bar(p)["close"] = JsonNode.Parse("101.50000000000000000000000000000001"));
        var p = Project(f, 3); var b = Freeze(p);
        Assert.Equal("active_then_historical", b.Classification);
        Assert.Equal("101.25", b.FirstActive!.Values.Close); Assert.Equal("101.5", b.FirstUsableHistorical!.Values.Close);
        Assert.Equal(2, b.FirstUsableHistorical.Receipt.Sequence); Assert.Equal(1, b.LaterHistoricalRevisions);
        Assert.Equal(2, b.HistoricalObservations);
        var life = p.Find("M1", Mt5ReceiptSyntheticFixture.Head)!.Bar;
        Assert.Equal("101.50000000000000000000000000000001", life.LastValues.Close);
        Assert.Equal(1, life.HistoricalToHistoricalRevisions); Assert.Equal(2, life.RevisionCount);
    }

    [Theory]
    [InlineData("101.2500", 0)]
    [InlineData("1.0125e2", 0)]
    [InlineData("101.25000000000000000000000000000001", 1)]
    public void FrozenOriginalLiteralsSurviveExactNumericRevisionComparison(string literal, int revisions)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        f.ChangeProbe(1, p => Bar(p)["close"] = JsonNode.Parse(literal));
        SameHead(f, 2, 1, Mt5ReceiptSyntheticFixture.Head);
        var b = Freeze(Project(f, 2), Mt5ReceiptSyntheticFixture.Head - 60);
        Assert.Equal(literal, b.FirstUsableHistorical!.Values.Close); Assert.Equal(revisions, b.LaterHistoricalRevisions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LowQualityHistoricalFirstRemainsUnusableAfterLaterImprovement(bool invalidOhlc)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        if (invalidOhlc) f.ChangeProbe(1, p => { Bar(p)["high"] = 98m; Bar(p)["ohlc_valid"] = false; });
        f.ChangeRecord(1, r => { r["m1_usable"] = false; r["sample_complete"] = false; });
        var boundary = Boundary(f, 1); var first = Project(f, boundary); var before = Report(first);
        var b = Freeze(first, Mt5ReceiptSyntheticFixture.Head - 60);
        Assert.Equal("no_usable_historical_observation", b.Classification); Assert.Null(b.FirstUsableHistorical);
        Assert.False(b.FirstHistorical!.Usable); Assert.False(b.FirstHistorical.CompleteNativeSampleObserved);
        SameHead(f, 2, 1, Mt5ReceiptSyntheticFixture.Head);
        f.ChangeRecord(2, r => r["previous_committed_m1_head_native"] = null);
        var later = Freeze(Project(f, 2), Mt5ReceiptSyntheticFixture.Head - 60);
        Assert.Equal(b.FirstHistorical, later.FirstHistorical); Assert.Equal(2, later.FirstUsableHistorical!.Receipt.Sequence);
        Assert.Equal(1, later.LimitedQualityObservations); Assert.Equal(before, Report(Project(f, boundary))); Assert.Equal(before, Report(first));
    }

    [Fact]
    public void NewRunRetainsBothProvenancesAndFrozenSourcePositionAcrossGap()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var before = Report(Project(f, 1));
        f.Add(9, 1, head: Mt5ReceiptSyntheticFixture.Head + 60, run: 9, poll: 1);
        var p = Project(f, 9); var b = Freeze(p);
        Assert.Equal("run-000000000001", b.FirstActive!.Receipt.RunId);
        Assert.Equal("run-000000000009", b.FirstUsableHistorical!.Receipt.RunId);
        Assert.True(p.Find("M1", Mt5ReceiptSyntheticFixture.Head)!.Bar.SpansInterruption);
        Assert.Equal(2, p.Lifecycle.RunBoundaries.Count); Assert.Equal(before, Report(Project(f, 1)));
        Assert.Contains("publication_sequence_gap_cause_unresolved", p.Prefix.Records[1].Diagnostics);
    }

    [Fact]
    public void LaterPositionChangesAndIdenticalHistoricalSamplesCannotReplaceFrozenPoint()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        // H4 head advances once; the earlier head is later observed historical at position one.
        f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head + 14400);
        SameHead(f, 3, 2, Mt5ReceiptSyntheticFixture.Head + 14400);
        var b = Freeze(Project(f, 3), Mt5ReceiptSyntheticFixture.Head, "H4");
        Assert.Equal(0, b.FirstActive!.SourcePosition); Assert.Equal(1, b.FirstUsableHistorical!.SourcePosition);
        Assert.Equal(2, b.FirstUsableHistorical.Receipt.Sequence); Assert.Equal(2, b.HistoricalObservations);
        Assert.Equal(0, b.LaterHistoricalRevisions);
    }

    [Fact]
    public void DiagnosticSilenceAndLargeNativeGapDoNotFabricateCandles()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.AddUnavailable(2, 1);
        var diagnostic = Project(f, 2); Assert.Equal(8, diagnostic.Bars.Count);
        Assert.Equal(1, Freeze(diagnostic, Mt5ReceiptSyntheticFixture.Head - 60).HistoricalObservations);
        f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head + 7200, ev: "source_recovery");
        f.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var p = Project(f, 3); Assert.Null(p.FindHistoricalFreeze("M1", Mt5ReceiptSyntheticFixture.Head + 60));
        Assert.Equal(3, p.Acquisitions.Count); Assert.Equal("active_only", Freeze(p).Classification);
    }

    [Theory]
    [InlineData("missing_probe")]
    [InlineData("chain")]
    [InlineData("source")]
    [InlineData("duplicate_poll")]
    [InlineData("ambiguous_coordinate")]
    public void UnverifiableIdentityNeverProducesCandidates(string fault)
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        switch (fault)
        {
            case "missing_probe": File.Delete(f.ProbePath(1)); break;
            case "source": f.ChangeRecord(1, r => r["source"]!["symbol"] = "OTHER"); break;
            case "ambiguous_coordinate": f.ChangeProbe(1, p => Bar(p)["open_time_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head)); break;
        }
        var n = 1L;
        if (fault is "chain" or "duplicate_poll")
        {
            f.Add(2, 1); n = 2;
            f.ChangeRecord(2, r => { if (fault == "chain") r["previous_record_sha256"] = new string('0', 64); else r["poll_sequence"] = 1; });
        }
        var boundary = Boundary(f, n); var batch = new LocalMt5CollectorReceiptReader().ReadPrefix(f.DirectoryPath, boundary);
        Assert.Null(ValidatedMt5ReceiptPrefix.Select(batch, boundary).Prefix);
    }

    [Fact]
    public void FutureCorruptionRunAndQualityChangesCannotEnterEarlierFold()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var one = Boundary(f, 1);
        var beforeOne = Report(Project(f, one)); f.Add(2, 1); var two = Boundary(f, 2);
        var beforeTwo = Report(Project(f, two)); f.Add(3, 2, run: 3, poll: 1);
        f.ChangeProbe(3, p => Bar(p)["close"] = 101.8m); File.WriteAllText(f.RecordPath(4), "corrupt future");
        Assert.Equal(beforeOne, Report(Project(f, one))); Assert.Equal(beforeTwo, Report(Project(f, two)));
        Assert.Equal("101.25", Freeze(Project(f, 3)).FirstUsableHistorical!.Values.Close);
    }

    [Fact]
    public void OutputIsBoundedAndNeverInfersUtcDstFinalizationVolumeOrAtomicity()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); var p = Project(f, 1, 0);
        var json = JsonNode.Parse(JsonSerializer.Serialize(p.HistoricalFreezeReport(0)))!;
        Assert.Empty(json["SelectedBars"]!.AsArray()); Assert.Equal(8, json["OmittedBars"]!.GetValue<int>());
        foreach (var field in new[] { "ProviderFinalization", "HistoricalUtcAvailability", "HistoricalDst", "AtomicMultiTimeframeSnapshot", "OverallUtcUncertainty", "SelectionIntervalPropagation" })
            Assert.Equal("unresolved", json[field]!.GetValue<string>());
        foreach (var field in new[] { "CanonicalReady", "UtcConversionAuthenticated", "CanonicalUtcConversionAuthorized", "WindowsClockAuthenticated", "Mt5NativeUtcAuthenticated" })
            Assert.False(json[field]!.GetValue<bool>());
        Assert.Equal("data_unavailable", json["VolumeEvidence"]!.GetValue<string>());
        Assert.Equal(4, json["Timeframes"]!.AsArray().Count);
        Assert.Null(json["RequestedObservation"]);
        Assert.NotNull(JsonNode.Parse(JsonSerializer.Serialize(p.HistoricalFreezeReport(0, "M1", Mt5ReceiptSyntheticFixture.Head)))!["RequestedObservation"]);
        Assert.Throws<ArgumentException>(() => p.HistoricalFreezeReport(129));
        Assert.Equal(Report(p), Report(Project(f, 1, 0)));
    }

    [Fact]
    public void LaterActiveRevisitIsNotAHistoricalCorrectionAndCannotEraseFrozenHistory()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(head: Mt5ReceiptSyntheticFixture.Head + 60);
        f.Add(2, 1, head: Mt5ReceiptSyntheticFixture.Head, run: 2, poll: 1);
        f.ChangeProbe(2, p => Bar(p, 0)["close"] = 101.8m);
        f.Add(3, 2, head: Mt5ReceiptSyntheticFixture.Head + 60, run: 2, poll: 2);
        f.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(Mt5ReceiptSyntheticFixture.Head));
        var p = Project(f, 3); var b = Freeze(p);
        Assert.Equal("directly_historical_first", b.Classification); Assert.Equal(1, b.FirstUsableHistorical!.Receipt.Sequence);
        Assert.Equal("101.25", b.FirstUsableHistorical.Values.Close); Assert.Equal("101.8", b.FirstActive!.Values.Close);
        Assert.Equal(0, b.LaterHistoricalRevisions); Assert.Equal(0, p.Find("M1", Mt5ReceiptSyntheticFixture.Head)!.Bar.HistoricalToHistoricalRevisions);
    }

    [Fact]
    public void InvalidLaterHistoricalRevisionIsRetainedWithoutDowngradingOrReplacingFrozenPoint()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); SameHead(f, 2, 1, Mt5ReceiptSyntheticFixture.Head);
        f.ChangeProbe(2, p => { Bar(p)["high"] = 98m; Bar(p)["ohlc_valid"] = false; });
        f.ChangeRecord(2, r => { r["sample_complete"] = false; r["m1_usable"] = false; });
        var p = Project(f, 2); var b = Freeze(p, Mt5ReceiptSyntheticFixture.Head - 60);
        Assert.Equal("102.5", b.FirstUsableHistorical!.Values.High); Assert.True(b.FirstUsableHistorical.Usable);
        Assert.Equal(1, b.LaterHistoricalRevisions); Assert.Equal(1, b.LimitedQualityObservations);
        Assert.Equal("98", p.Find("M1", Mt5ReceiptSyntheticFixture.Head - 60)!.Bar.LastValues.High);
    }

    [Fact]
    public void NoUsableActiveObservationIsNeverPromotedByLaterQuality()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.ChangeRecord(1, r => r["sample_complete"] = false);
        var before = Project(f, 1); var raw = Freeze(before);
        Assert.Null(raw.FirstUsableHistorical); Assert.False(raw.FirstActive!.Usable);
        SameHead(f, 2, 1, Mt5ReceiptSyntheticFixture.Head); var later = Freeze(Project(f, 2));
        Assert.Equal(raw.FirstActive, later.FirstActive); Assert.Equal("active_only", later.Classification);
        Assert.Equal(1, later.UsableObservations); Assert.Equal(1, later.LimitedQualityObservations);
        Assert.Equal("no_usable_historical_observation", raw.Classification);
    }

    [Fact]
    public void EmptyPrefixHasNoSourceIdentityOrCandidatesAndDoesNotInspectFutureEvidence()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add();
        var p = Project(f, new Mt5ReceiptPrefixBoundary(0));
        Assert.Empty(p.Bars); Assert.Null(p.FindHistoricalFreeze("M1", Mt5ReceiptSyntheticFixture.Head));
        var report = JsonNode.Parse(Report(p))!;
        Assert.False(report["Source"]!["IdentityValidated"]!.GetValue<bool>()); Assert.Empty(report["Timeframes"]!.AsArray());
    }

    [Fact]
    public void FrozenHistoricalCandidateDoesNotAuthorizeTemporalAdmissionOrRetrospectiveAttestation()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); f.Add(); f.Add(2, 1); var p = Project(f, 2);
        var b = Freeze(p); var point = b.FirstUsableHistorical!;
        var a = new Mt5TemporalEvidenceAdmission().Assess(p.Prefix,
            new(point.Receipt.RecordFile, "M1", Mt5ReceiptSyntheticFixture.Head),
            new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(Mt5ReceiptStatus.DataUnavailable, a.Status); Assert.False(a.TemporalAdmissionSupported);
        Assert.Null(a.ConservativeAvailableByUtc); Assert.False(a.CanonicalReady);
        Assert.Equal(Report(p), Report(Project(f, 2)));
    }

    [Fact]
    public void CliLeavesOriginalEvidenceAndCheckpointUntouchedAndCannotOverwriteReport()
    {
        using var f = new Mt5ReceiptSyntheticFixture(); using var output = new Mt5ReceiptSyntheticFixture(); f.Add();
        var checkpoint = Path.Combine(f.DirectoryPath, "checkpoint.json"); File.WriteAllText(checkpoint, "untouched");
        var pins = Directory.GetFiles(f.DirectoryPath).ToDictionary(p => p, Mt5ReceiptSyntheticFixture.Hash);
        var path = Path.Combine(output.DirectoryPath, "freeze.json"); var stdout = new StringWriter(); var stderr = new StringWriter();
        var args = new[] { "mt5-causal-availability", "--source", f.DirectoryPath, "--through-sequence", "1", "--through-sha256", Boundary(f, 1).RecordSha256!, "--view", "first-historical", "--report", path, "--details", "1" };
        var command = new Mt5CausalAvailabilityCommand(); Assert.Equal(0, command.Execute(args, stdout, stderr));
        var report = JsonNode.Parse(File.ReadAllText(path))!["audit"]!;
        Assert.Equal("moneyway.mt5-first-historical-observation-freeze", report["schema"]!.GetValue<string>());
        Assert.Single(report["selected_bars"]!.AsArray()); Assert.Equal(4, command.Execute(args, stdout, stderr));
        foreach (var (file, hash) in pins) Assert.Equal(hash, Mt5ReceiptSyntheticFixture.Hash(file));
        Assert.Equal(pins.Count, Directory.GetFiles(f.DirectoryPath).Length);
    }
}
