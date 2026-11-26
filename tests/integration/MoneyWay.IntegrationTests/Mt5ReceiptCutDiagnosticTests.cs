using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class Mt5ReceiptCutDiagnosticTests
{
    private static Mt5ReceiptPrefixBoundary Pin(Mt5ReceiptSyntheticFixture fixture, long sequence) =>
        new(sequence, Mt5ReceiptSyntheticFixture.Hash(fixture.RecordPath(sequence)));

    private static Mt5ReceiptCutReport Inspect(Mt5ReceiptSyntheticFixture fixture, Mt5ReceiptPrefixBoundary cut,
        Mt5ReceiptPrefixBoundary? anchor = null, Mt5TemporalSourceIdentity? source = null,
        Mt5LocalReceiptLabel[]? labels = null, Mt5ReceiptCutReviewReference[]? reviews = null)
    {
        var reader = new LocalMt5CollectorReceiptReader();
        var batch = anchor is null ? reader.ReadPrefix(fixture.DirectoryPath, cut) : reader.ReadDiagnosticRange(fixture.DirectoryPath, anchor, cut);
        return new Mt5ReceiptCutDiagnostic().Inspect(batch, source ?? Mt5ReceiptCutDiagnostic.SupportedSource, cut, anchor, labels, reviews);
    }

    [Fact]
    public void PinnedCutExcludesActiveAndFutureSamplesAndNeverRewritesFrozenValues()
    {
        using var fixture = new Mt5ReceiptSyntheticFixture();
        fixture.Add();
        var firstCut = Pin(fixture, 1);
        var native = Mt5ReceiptSyntheticFixture.Head;
        var first = Inspect(fixture, firstCut);
        Assert.Contains(first.Exclusions, e => e.Timeframe == "M1" && e.OpenTimeNative.Raw == native && e.Reason == "position_zero_only");
        Assert.DoesNotContain(first.Samples, e => e.Timeframe == "M1" && e.OpenTimeNative.Raw == native);

        fixture.Add(2, 1);
        var secondCut = Pin(fixture, 2);
        var second = Inspect(fixture, secondCut);
        fixture.Add(3, 2, head: native + 60, ev: "incomplete_snapshot_retry");
        fixture.ChangeRecord(3, r => r["previous_committed_m1_head_native"] = Mt5ReceiptSyntheticFixture.Native(native + 60));
        fixture.ChangeProbe(3, p => p["timeframes"]![0]!["bars"]![1]!["close"] = 101.75m);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(Inspect(fixture, firstCut)));
        Assert.Equal(JsonSerializer.Serialize(second), JsonSerializer.Serialize(Inspect(fixture, secondCut)));
        var frozen = Assert.Single(Inspect(fixture, Pin(fixture, 3)).Samples, e => e.Timeframe == "M1" && e.OpenTimeNative.Raw == native);
        Assert.Equal(2, frozen.FirstUsableHistorical.Receipt.Sequence);
        Assert.Equal("101.25", frozen.FirstUsableHistorical.Values.Close);
        Assert.Equal(1, frozen.LaterHistoricalRevisions);
        Assert.All(second.Samples, e => Assert.True(e.FirstUsableHistorical.SourcePosition > 0));
    }

    [Fact]
    public void AnchoredRangeIsBoundedRecordsDiscontinuitiesAndDoesNotTreatLabelsAsAuthority()
    {
        using var fixture = new Mt5ReceiptSyntheticFixture();
        fixture.Add();
        fixture.Add(3, 1, head: Mt5ReceiptSyntheticFixture.Head + 180);
        fixture.Add(4, 3, head: Mt5ReceiptSyntheticFixture.Head + 360, run: 4, poll: 1);
        var anchor = Pin(fixture, 1);
        var cut = Pin(fixture, 4);
        var label = DateTimeOffset.UnixEpoch.AddHours(4);
        var labels = new[] { new Mt5LocalReceiptLabel(3, Pin(fixture, 3).RecordSha256!, label),
            new Mt5LocalReceiptLabel(4, cut.RecordSha256!, label.AddSeconds(-1)) };
        var report = Inspect(fixture, cut, anchor, labels: labels, reviews: [new(3, "unbound"), new(5, "future")]);
        Assert.True(report.Integrity == Mt5ReceiptStatus.Validated, string.Join(", ", report.Diagnostics));
        Assert.Equal(anchor, report.Anchor);
        Assert.Equal(cut.RecordSha256, report.CutReceipt!.Sha256);
        Assert.All(report.Samples, s => Assert.InRange(s.FirstUsableHistorical.Receipt.Sequence, 3, 4));
        Assert.Contains(report.Notices, n => n.Reason == "publication_sequence_gap_cause_unresolved");
        Assert.Contains(report.Notices, n => n.Reason == "collector_run_changed");
        Assert.Contains(report.Notices, n => n.Reason == "local_receipt_clock_regressed");
        Assert.Contains(report.NativeGaps, gap => gap.Timeframe == "M1" && gap.ObservedDeltaSeconds == 120);
        Assert.Equal("DiscontinuityOrAcquisitionIssueObserved", report.ObservationContinuity);
        Assert.Equal(label.AddSeconds(-1), report.CutLocalReceiptClockLabel);
        Assert.Equal("unbound", Assert.Single(report.ExcludedHumanReviews).Reference);
        Assert.Equal("NoVerifiedReviewBoundToReceiptCut", report.HumanReviewStatus);
        Assert.Equal("Unresolved", report.NativeToUtcMapping);
        Assert.Equal("NotEvaluated", report.StandardEvaluation);
        Assert.False(report.CanonicalReady || report.CanonicalAdmissionPermitted || report.OperationalSignal);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(report));
        Assert.False(json.RootElement.TryGetProperty("StrategyVerdict", out _));
        Assert.False(json.RootElement.TryGetProperty("EntryIntent", out _));

        File.WriteAllText(fixture.RecordPath(5), "invalid future bytes");
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(Inspect(fixture, cut, anchor,
            labels: labels, reviews: [new(3, "unbound"), new(5, "future")])));
    }

    [Fact]
    public void InsufficientQualityAndWrongSourceOrHashFailClosed()
    {
        using var fixture = new Mt5ReceiptSyntheticFixture();
        fixture.Add();
        fixture.ChangeProbe(1, p => p["timeframes"]![1]!["synchronized_after"]!["value"] = 0);
        fixture.ChangeRecord(1, r => r["sample_complete"] = false);
        var pin = Pin(fixture, 1);
        var report = Inspect(fixture, pin);
        Assert.DoesNotContain(report.Samples, b => b.Timeframe == "M5");
        Assert.Contains(report.Exclusions, b => b.Timeframe == "M5" && b.Reason == "no_usable_historical_observation");
        var mismatch = Inspect(fixture, pin, source: Mt5ReceiptCutDiagnostic.SupportedSource with { NativeSymbol = "USTEC" });
        Assert.Equal(Mt5ReceiptStatus.Conflict, mismatch.Integrity);
        Assert.Empty(mismatch.Samples);
        var changed = Inspect(fixture, new(1, new string('0', 64)));
        Assert.Empty(changed.Samples);
        Assert.Contains("record_hash_mismatch", changed.Diagnostics);
    }

    [Fact]
    public void WorkerWritesCreateOnlyReportFromPinnedObserverJournal()
    {
        using var fixture = new Mt5ReceiptSyntheticFixture();
        using var destination = new Mt5ReceiptSyntheticFixture();
        fixture.Add();
        var pin = Pin(fixture, 1);
        var journal = Path.Combine(destination.DirectoryPath, "observer.ndjson");
        Mt5ReceiptSyntheticFixture.Write(journal, new JsonObject { ["sequence"] = 1, ["sha256"] = pin.RecordSha256,
            ["observedAtHostUtcCandidate"] = "2026-11-25T13:00:00Z" });
        var path = Path.Combine(destination.DirectoryPath, "receipt-cut.json");
        string[] args = ["mt5-causal-availability", "--view", "receipt-cut", "--source", fixture.DirectoryPath,
            "--through-sequence", "1", "--through-sha256", pin.RecordSha256!, "--observer-journal", journal,
            "--observer-journal-sha256", Mt5ReceiptSyntheticFixture.Hash(journal), "--report", path];
        var command = new Mt5CausalAvailabilityCommand();
        Assert.Equal(0, command.Execute(args, TextWriter.Null, TextWriter.Null));
        using var json = JsonDocument.Parse(File.ReadAllBytes(path));
        var report = json.RootElement.GetProperty("audit");
        Assert.Equal(DateTimeOffset.Parse("2026-11-25T13:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            report.GetProperty("cut_local_receipt_clock_label").GetDateTimeOffset());
        Assert.Equal(4, command.Execute(args, TextWriter.Null, TextWriter.Null));
        args[^1] = Path.Combine(destination.DirectoryPath, "changed.json");
        File.AppendAllText(journal, "\n");
        Assert.Equal(3, command.Execute(args, TextWriter.Null, TextWriter.Null));
        Assert.False(File.Exists(args[^1]));
        Assert.Equal(pin, Pin(fixture, 1));
    }
}
