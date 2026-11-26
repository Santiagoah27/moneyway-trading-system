using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed record Mt5LocalReceiptLabel(long Sequence, string Sha256, DateTimeOffset ObservedAtHostUtcCandidate);
public sealed record Mt5ReceiptCutSample(string Timeframe, Mt5NativeTimestamp OpenTimeNative,
    Mt5HistoricalObservation FirstUsableHistorical, Mt5NativeTimestamp? CollectorGmtCandidate,
    DateTimeOffset? LocalReceiptClockLabel, bool SpansInterruption, int LaterHistoricalRevisions);
public sealed record Mt5ReceiptCutExclusion(string Timeframe, Mt5NativeTimestamp OpenTimeNative, string Reason);
public sealed record Mt5ReceiptCutNativeGap(string Timeframe, Mt5NativeTimestamp EarlierOpen, Mt5NativeTimestamp LaterOpen,
    int PeriodSeconds, long ObservedDeltaSeconds);
public sealed record Mt5ReceiptCutNotice(long Sequence, string Reason);
public sealed record Mt5ReceiptCutReviewReference(long ClaimedReceiptSequence, string Reference);

public sealed record Mt5ReceiptCutReport(Mt5TemporalSourceIdentity ExpectedSource, string DirectoryScope,
    Mt5ReceiptPrefixBoundary? Anchor, Mt5ReceiptPrefixBoundary Boundary, Mt5ReceivedRecordIdentity? CutReceipt,
    DateTimeOffset? CutLocalReceiptClockLabel, string? PrefixManifestSha256, Mt5ReceiptStatus Integrity, string ObservationContinuity,
    IReadOnlyList<Mt5ReceiptCutSample> Samples, IReadOnlyList<Mt5ReceiptCutExclusion> Exclusions,
    IReadOnlyList<Mt5ReceiptCutNativeGap> NativeGaps, IReadOnlyList<Mt5LifecycleFrame> Timeframes, IReadOnlyList<Mt5ReceiptCutNotice> Notices,
    IReadOnlyList<string> Diagnostics, IReadOnlyList<Mt5ReceiptCutReviewReference> ExcludedHumanReviews)
{
    public string Schema => "moneyway.nasdaq-prospective-receipt-cut-diagnostic";
    public int Version => 1;
    public string ClockBasis => "local_receipt_label_and_collector_gmt_candidate_not_authenticated";
    public string ClockJumpDetection => "regressions_only_forward_steps_unresolved_without_clock_continuity_evidence";
    public string NativeToUtcMapping => "Unresolved";
    public string ProviderFinalization => "Unresolved";
    public string MarketAvailabilityUtc => "Unresolved";
    public string HumanReviewStatus => "NoVerifiedReviewBoundToReceiptCut";
    public string StandardEvaluation => "NotEvaluated";
    public string StandardEvaluationReason => "NoAdmissibleClosedUtcSeriesOrVerifiedTemporalBasisForStandard";
    public bool CanonicalReady => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Projects frozen observation evidence. The report has no strategy or replay-input interface.</summary>
public sealed class Mt5ReceiptCutDiagnostic
{
    public static Mt5TemporalSourceIdentity SupportedSource { get; } =
        new("OANDA Global Markets Limited", "OANDA_Global-Demo-1", "US100", true);

    public Mt5ReceiptCutReport Inspect(Mt5ReceiptBatch batch, Mt5TemporalSourceIdentity expectedSource,
        Mt5ReceiptPrefixBoundary boundary, Mt5ReceiptPrefixBoundary? anchor = null,
        IEnumerable<Mt5LocalReceiptLabel>? labels = null, IEnumerable<Mt5ReceiptCutReviewReference>? reviews = null)
    {
        if (boundary.Sequence == 0) throw new ArgumentException("A diagnostic cut must pin an existing receipt.", nameof(boundary));
        var selected = anchor is null ? ValidatedMt5ReceiptPrefix.Select(batch, boundary)
            : ValidatedMt5ReceiptPrefix.SelectDiagnosticRange(batch, anchor, boundary);
        if (selected.Prefix is not { } prefix)
            return Blocked(selected.Status, selected.Diagnostics.Concat(batch.Records.SelectMany(r => r.Diagnostics)));
        if (prefix.Records.Any(r => Source(r.Observation!) != expectedSource))
            return Blocked(Mt5ReceiptStatus.Conflict, ["expected_source_identity_mismatch"]);

        var projection = new Mt5CausalAvailabilityAudit().Project(prefix, 0);
        var recordLabels = (labels ?? []).Where(l => l.Sequence > (anchor?.Sequence ?? 0) && l.Sequence <= boundary.Sequence)
            .ToLookup(l => l.Sequence);
        var acceptedLabels = new Dictionary<long, DateTimeOffset>();
        var acquisitions = prefix.Records.Select(r => r.Observation!).ToDictionary(a => a.Metadata.Sequence);
        var notices = new List<Mt5ReceiptCutNotice>();
        Mt5NativeAcquisition? previous = null;
        DateTimeOffset? previousLabel = null;
        foreach (var record in prefix.Records)
        {
            var a = record.Observation!;
            var sequence = a.Metadata.Sequence;
            notices.AddRange(record.Diagnostics.Select(reason => new Mt5ReceiptCutNotice(sequence, reason)));
            if (previous is not null)
            {
                if (a.Metadata.RunId != previous.Metadata.RunId) notices.Add(new(sequence, "collector_run_changed"));
                if (a.GeneratedGmtCandidate is { } current && previous.GeneratedGmtCandidate is { } prior && current.Raw < prior.Raw)
                    notices.Add(new(sequence, "collector_clock_candidate_regressed"));
            }
            if (a.EvidenceKind != Mt5ReceiptEvidenceKind.AcquisitionSnapshot || a.ConnectedBefore != true || a.ConnectedAfter != true)
                notices.Add(new(sequence, "acquisition_interrupted_or_connection_unconfirmed"));
            var candidates = recordLabels[sequence].ToArray();
            if (candidates is [var label] && string.Equals(label.Sha256, a.RecordSha256, StringComparison.OrdinalIgnoreCase)
                && label.ObservedAtHostUtcCandidate.Offset == TimeSpan.Zero)
            {
                if (label.ObservedAtHostUtcCandidate < previousLabel) notices.Add(new(sequence, "local_receipt_clock_regressed"));
                previousLabel = label.ObservedAtHostUtcCandidate;
                acceptedLabels.Add(sequence, label.ObservedAtHostUtcCandidate);
            }
            else if (candidates.Length > 0) notices.Add(new(sequence, "local_receipt_label_conflict"));
            previous = a;
        }

        var samples = new List<Mt5ReceiptCutSample>();
        var exclusions = new List<Mt5ReceiptCutExclusion>();
        foreach (var observation in projection.Bars)
        {
            var bar = observation.Bar;
            var freeze = projection.FindHistoricalFreeze(bar.Timeframe, bar.OpenTimeNative.Raw)!;
            if (freeze.FirstUsableHistorical is not { } first)
            {
                exclusions.Add(new(bar.Timeframe, bar.OpenTimeNative, freeze.FirstHistorical is null
                    ? "position_zero_only" : "no_usable_historical_observation"));
                continue;
            }
            var sequence = first.Receipt.Sequence;
            samples.Add(new(bar.Timeframe, bar.OpenTimeNative, first, acquisitions[sequence].GeneratedGmtCandidate,
                acceptedLabels.TryGetValue(sequence, out var time) ? time : null, bar.SpansInterruption, freeze.LaterHistoricalRevisions));
        }

        var periods = acquisitions.Values.SelectMany(a => a.Timeframes).DistinctBy(f => f.Timeframe)
            .ToDictionary(f => f.Timeframe, f => f.PeriodSeconds, StringComparer.Ordinal);
        var gaps = projection.Bars.GroupBy(b => b.Bar.Timeframe, StringComparer.Ordinal).SelectMany(frame =>
        {
            var period = periods[frame.Key];
            var opens = frame.Select(b => b.Bar.OpenTimeNative).OrderBy(o => o.Raw).ToArray();
            return opens.Zip(opens.Skip(1), (earlier, later) => new Mt5ReceiptCutNativeGap(frame.Key, earlier, later, period, later.Raw - earlier.Raw))
                .Where(gap => gap.ObservedDeltaSeconds > period);
        }).ToArray();
        var continuity = notices.Count > 0 || gaps.Length > 0 || projection.Lifecycle.Timeframes.Any(f =>
            f.HeadRegressions > 0 || f.NonPeriodHeadAdvancements > 0 || f.NonPeriodAdjacentDeltas > 0 || f.SkippedFrames > 0)
            ? "DiscontinuityOrAcquisitionIssueObserved" : "ContinuityNotEstablishedByReceiptChain";
        var excludedReviews = (reviews ?? []).Where(r => r.ClaimedReceiptSequence > (anchor?.Sequence ?? 0)
                && r.ClaimedReceiptSequence <= boundary.Sequence)
            .OrderBy(r => r.ClaimedReceiptSequence).ThenBy(r => r.Reference, StringComparer.Ordinal).ToArray();
        return new(expectedSource, prefix.DirectoryScope, anchor, boundary, prefix.FinalReceipt,
            acceptedLabels.TryGetValue(boundary.Sequence, out var cutLabel) ? cutLabel : null, prefix.EvidenceManifestSha256,
            selected.Status, continuity, samples.AsReadOnly(), exclusions.AsReadOnly(), Array.AsReadOnly(gaps), projection.Lifecycle.Timeframes,
            notices.AsReadOnly(), acceptedLabels.Count == prefix.Records.Count ? [] : ["local_receipt_clock_labels_incomplete"],
            Array.AsReadOnly(excludedReviews));

        Mt5ReceiptCutReport Blocked(Mt5ReceiptStatus status, IEnumerable<string> codes) =>
            new(expectedSource, batch.DirectoryScope, anchor, boundary, null, null, null, status, "Unavailable", [], [], [], [], [],
                Array.AsReadOnly(codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()), []);
    }

    private static Mt5TemporalSourceIdentity Source(Mt5NativeAcquisition acquisition) =>
        new(acquisition.Company, acquisition.Server, acquisition.NativeSymbol, acquisition.Demo);
}
