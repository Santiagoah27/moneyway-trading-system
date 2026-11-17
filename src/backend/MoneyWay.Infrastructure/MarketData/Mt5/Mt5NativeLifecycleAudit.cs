using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed record Mt5LifecycleReference(string RecordFile, string RecordSha256, long Sequence,
    string RunId, long PollSequence, string Event, string? ProbeFile, string? ProbeSha256);
public sealed record Mt5LifecycleValues(string Open, string High, string Low, string Close);
public sealed record Mt5HistoricalObservation(Mt5LifecycleReference Receipt, int SourcePosition,
    Mt5LifecycleValues Values, Mt5ReceiptStatus ReceiptStatus, bool CompleteNativeSampleObserved,
    bool UsableFrame, bool OhlcValid)
{
    public bool Usable => CompleteNativeSampleObserved && UsableFrame && OhlcValid;
}
public sealed record Mt5HistoricalFreeze(Mt5HistoricalObservation? FirstActive,
    Mt5HistoricalObservation? FirstHistorical, Mt5HistoricalObservation? FirstUsableHistorical,
    int HistoricalObservations, int UsableObservations, int LimitedQualityObservations, int LaterHistoricalRevisions)
{
    public string Classification => FirstUsableHistorical is not null
        ? FirstHistorical!.Receipt.Sequence < (FirstActive?.Receipt.Sequence ?? long.MaxValue)
            ? "directly_historical_first" : "active_then_historical"
        : FirstHistorical is null && UsableObservations > 0 ? "active_only" : "no_usable_historical_observation";
    public string Status => FirstUsableHistorical is null ? "data_unavailable" : "context_specific";
}
public sealed record Mt5LifecycleRevision(Mt5LifecycleReference Before, Mt5LifecycleReference After,
    int BeforePosition, int AfterPosition, Mt5LifecycleValues BeforeValues, Mt5LifecycleValues AfterValues,
    bool AcrossInterruption);
public sealed record Mt5LifecycleBar(string Timeframe, Mt5NativeTimestamp OpenTimeNative,
    Mt5LifecycleReference FirstObserved, int FirstPosition, Mt5LifecycleReference LastObserved, int LastPosition,
    Mt5LifecycleValues LastValues, int Snapshots, bool ObservedActiveHead, bool ObservedHistoricalPosition,
    bool ActiveThenHistorical, bool SpansInterruption, int IdenticalComparisons, int RevisionCount,
    int ActiveToHistoricalRevisions, int HistoricalToHistoricalRevisions,
    IReadOnlyList<Mt5LifecycleRevision> Revisions, int OmittedRevisions)
{
    public bool? ProviderConfirmedClosed => null;
    public DateTimeOffset? OpenTimeUtc => null;
    public DateTimeOffset? HistoricalAvailableAtUtc => null;
}
public sealed record Mt5LifecycleFrame(string Timeframe, Mt5NativeTimestamp? EarliestNativeOpen,
    Mt5NativeTimestamp? LatestNativeOpen, int ObservedBars, int OnlyActiveHead,
    int ActiveThenHistorical, int RevisedBars, int IdenticalAcrossAllSnapshots, int InsufficientCoverage,
    int ActiveToHistoricalRevisions, int HistoricalToHistoricalRevisions,
    int HeadAdvancements, int HeadRegressions, int NonPeriodHeadAdvancements, int NonPeriodAdjacentDeltas, int SkippedFrames);
public sealed record Mt5LifecycleEvent(string? FileName, string? Sha256, string Status,
    Mt5LifecycleReference? Reference, string? EvidenceKind, bool? SampleComplete, bool? SourceStable,
    IReadOnlyList<string> Diagnostics);
public sealed record Mt5LifecycleRun(string RunId, Mt5LifecycleReference FirstObserved,
    Mt5LifecycleReference LastObserved, int Records);
public sealed record Mt5LifecycleReport(string DirectoryScope, long? ThroughSequence, int InspectedRecords,
    int ValidatedRecords, int BlockedRecords, string InputManifestSha256,
    IReadOnlyDictionary<string, int> Events, IReadOnlyDictionary<string, int> EvidenceKinds, IReadOnlyDictionary<string, int> Diagnostics,
    IReadOnlyList<Mt5LifecycleRun> RunBoundaries, IReadOnlyList<Mt5LifecycleFrame> Timeframes,
    IReadOnlyList<Mt5LifecycleEvent> SelectedEvents, int OmittedEvents,
    IReadOnlyList<Mt5LifecycleBar> SelectedBars, int OmittedBars)
{
    public string Schema => "moneyway.mt5-native-candle-lifecycle";
    public int Version => 1;
    public string Company => "OANDA Global Markets Limited";
    public string Server => "OANDA_Global-Demo-1";
    public string NativeSymbol => "US100";
    public bool Demo => true;
    public string Status => InspectedRecords == 0 || ValidatedRecords == 0 ? "data_unavailable" : "unresolved";
    public string VolumeEvidence => "data_unavailable";
    public string ProviderClosure => "unresolved";
    public string HistoricalAvailability => "unresolved";
    public bool DiagnosticOnly => true;
    public bool CanonicalUtcConversionAuthorized => false;
    public bool UtcConversionAuthenticated => false;
    public bool CanonicalReady => false;
    public IReadOnlyList<string> RunIds => Array.AsReadOnly(RunBoundaries.Select(r => r.RunId).ToArray());
}

/// <summary>Offline facts over a receipt-validated inventory, never canonical candles or UTC availability.</summary>
public sealed class Mt5NativeLifecycleAudit
{
    public Mt5LifecycleReport Analyze(Mt5ReceiptBatch batch, long? throughSequence = null, int detailLimit = 16)
        => AnalyzeCore(batch, throughSequence, detailLimit, false);

    internal Mt5LifecycleReport AnalyzePrefix(ValidatedMt5ReceiptPrefix prefix, int detailLimit,
        IDictionary<(string Frame, long Raw), Mt5HistoricalFreeze> freezes)
        => AnalyzeCore(prefix.ToBatch(), null, detailLimit, true, freezes);

    private static Mt5LifecycleReport AnalyzeCore(Mt5ReceiptBatch batch, long? throughSequence, int detailLimit, bool causalProjection,
        IDictionary<(string Frame, long Raw), Mt5HistoricalFreeze>? freezes = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (throughSequence is <= 0 || detailLimit is < 0 or > 128) throw new ArgumentOutOfRangeException(nameof(detailLimit));
        // Filename sequence also bounds rejected records, whose parsed observation is deliberately absent.
        var records = batch.Records.Where(r => throughSequence is null || Sequence(r) <= throughSequence)
            .OrderBy(Sequence).ThenBy(r => r.FileName, StringComparer.Ordinal).ToArray();
        var duplicates = records.GroupBy(r => r.FileName).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var diagnostics = new List<string>(batch.Diagnostics);
        var events = new List<Mt5LifecycleEvent>();
        var bars = new Dictionary<(string Frame, long Raw), State>();
        var frames = new SortedDictionary<string, FrameState>(StringComparer.Ordinal);
        var heads = new Dictionary<string, long>(StringComparer.Ordinal);
        string? run = null; long? priorSequence = null; var segment = 0; var validated = 0;
        foreach (var receipt in records)
        {
            var codes = receipt.Diagnostics.ToList();
            var valid = receipt.ReceiptIntegrityValidated && receipt.Observation?.DirectoryScope == batch.DirectoryScope
                && !duplicates.Contains(receipt.FileName);
            if (duplicates.Contains(receipt.FileName)) codes.Add("duplicate_receipt_excluded");
            var acquisition = valid ? receipt.Observation : null;
            var reference = acquisition is null ? null : Reference(acquisition);
            events.Add(new(receipt.FileName, receipt.ActualSha256, valid ? receipt.Status.ToString() : "data_unavailable",
                reference, acquisition?.EvidenceKind.ToString(), acquisition?.Metadata.SampleComplete, acquisition?.Metadata.SourceStable, codes.AsReadOnly()));
            diagnostics.AddRange(codes);
            if (acquisition is null) { segment++; heads.Clear(); continue; }
            validated++;
            var meta = acquisition.Metadata;
            if (run != meta.RunId || priorSequence + 1 != meta.Sequence)
            {
                segment++; heads.Clear();
                run = meta.RunId;
            }
            priorSequence = meta.Sequence;
            if (acquisition.EvidenceKind != Mt5ReceiptEvidenceKind.AcquisitionSnapshot)
            { segment++; heads.Clear(); continue; }
            // Partial/unusable acquisitions are retained above; no quality or continuity is silently upgraded.
            if (!receipt.CompleteNativeSampleObserved) { segment++; heads.Clear(); }
            foreach (var frame in acquisition.Timeframes)
            {
                if (!frames.TryGetValue(frame.Timeframe, out var stats)) frames.Add(frame.Timeframe, stats = new());
                var usable = meta.SourceStable && acquisition.ProbeGate == "passed" && acquisition.ConnectedBefore == true
                    && acquisition.ConnectedAfter == true && frame.HeadConsistent == true && frame.CopyErrorCode == 0
                    && frame.SynchronizedAfter?.Value == 1 && frame.Bars.Count > 0 && frame.Bars.All(b => b.OhlcValid);
                if (!usable)
                {
                    stats.Skipped++;
                    heads.Remove(frame.Timeframe);
                    if (!causalProjection) continue;
                    segment++;
                }
                if (frame.Bars.Count == 0) continue;
                /* The causal projection retains explicitly diagnostic raw observations and their quality flags.
                   The existing retrospective audit keeps its original usable-frame filtering. */
                if (usable && heads.TryGetValue(frame.Timeframe, out var previous))
                {
                    var current = frame.Bars[0].OpenTimeNative.Raw;
                    if (current > previous)
                    {
                        stats.Advancements++;
                        if (current - previous != frame.PeriodSeconds) stats.NonPeriodAdvancements++;
                    }
                    if (current < previous) { stats.Regressions++; segment++; }
                }
                if (usable) heads[frame.Timeframe] = frame.Bars[0].OpenTimeNative.Raw;
                foreach (var bar in frame.Bars)
                {
                    if (bar.NewerThanNextSeconds is { } delta && delta != frame.PeriodSeconds) stats.NonPeriodDeltas++;
                    var key = (frame.Timeframe, bar.OpenTimeNative.Raw);
                    var observation = new Mt5HistoricalObservation(reference!, bar.SeriesPosition, Values(bar), receipt.Status,
                        receipt.CompleteNativeSampleObserved, usable, bar.OhlcValid);
                    if (!bars.TryGetValue(key, out var state)) bars.Add(key, new(bar, reference!, segment, observation));
                    else state.Add(bar, reference!, segment, observation);
                }
                // A partial sample can describe bars, but cannot join the next sample as continuous coverage.
                if (!receipt.CompleteNativeSampleObserved || !usable) heads.Remove(frame.Timeframe);
            }
            if (!receipt.CompleteNativeSampleObserved) segment++;
        }
        if (freezes is not null)
            foreach (var pair in bars) freezes.Add(pair.Key, pair.Value.Freeze());
        var all = bars.OrderBy(p => p.Key.Frame, StringComparer.Ordinal).ThenBy(p => p.Key.Raw)
            .Select(p => p.Value.Result(p.Key.Frame, detailLimit)).ToArray();
        var summaries = frames.Select(pair =>
        {
            var selected = all.Where(b => b.Timeframe == pair.Key).ToArray(); var s = pair.Value;
            return new Mt5LifecycleFrame(pair.Key, selected.FirstOrDefault()?.OpenTimeNative, selected.LastOrDefault()?.OpenTimeNative,
                selected.Length, selected.Count(b => b.ObservedActiveHead && !b.ObservedHistoricalPosition),
                selected.Count(b => b.ActiveThenHistorical), selected.Count(b => b.RevisionCount > 0),
                selected.Count(b => b.Snapshots > 1 && b.RevisionCount == 0), selected.Count(b => b.Snapshots == 1),
                selected.Sum(b => b.ActiveToHistoricalRevisions), selected.Sum(b => b.HistoricalToHistoricalRevisions),
                s.Advancements, s.Regressions, s.NonPeriodAdvancements, s.NonPeriodDeltas, s.Skipped);
        }).ToArray();
        var manifest = string.Join("\n", records.Select(r => $"{r.FileName}:{r.ActualSha256}:{r.Status}"));
        var selectedEvents = events.OrderByDescending(e => e.Diagnostics.Count > 0 || e.Reference?.Event is "shutdown" or "source_unavailable")
            .ThenBy(e => e.FileName, StringComparer.Ordinal).Take(detailLimit).ToArray();
        var rankedBars = all.OrderByDescending(b => b.RevisionCount).ThenBy(b => b.Timeframe, StringComparer.Ordinal)
            .ThenBy(b => b.OpenTimeNative.Raw).ToArray();
        // Prefer a representative of each supported frame before filling remaining detail slots.
        var selectedBars = causalProjection ? all : rankedBars.DistinctBy(b => b.Timeframe).Concat(rankedBars)
            .DistinctBy(b => (b.Timeframe, b.OpenTimeNative.Raw)).Take(detailLimit).ToArray();
        var runs = events.Where(e => e.Reference is not null).Select(e => e.Reference!).GroupBy(r => r.RunId)
            .Select(g => new Mt5LifecycleRun(g.Key, g.First(), g.Last(), g.Count())).ToArray();
        return new(batch.DirectoryScope, throughSequence, records.Length, validated, records.Length - validated,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))),
            Counts(events.Select(e => e.Reference?.Event ?? "blocked")), Counts(events.Select(e => e.EvidenceKind ?? "blocked")),
            Counts(diagnostics), Array.AsReadOnly(runs),
            Array.AsReadOnly(summaries), Array.AsReadOnly(selectedEvents), events.Count - selectedEvents.Length,
            Array.AsReadOnly(selectedBars), all.Length - selectedBars.Length);
    }

    private static long Sequence(Mt5ReceiptRecord r) => r.Observation?.Metadata.Sequence
        ?? (r.FileName is { Length: 24 } name && long.TryParse(name.AsSpan(7, 12), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : long.MaxValue);
    private static IReadOnlyDictionary<string, int> Counts(IEnumerable<string> codes) =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(codes.GroupBy(c => c, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal));
    private static Mt5LifecycleReference Reference(Mt5NativeAcquisition a) => new(a.RecordFile, a.RecordSha256,
        a.Metadata.Sequence, a.Metadata.RunId, a.Metadata.PollSequence, a.Metadata.Event, a.Metadata.ProbeFile, a.Metadata.ProbeSha256);
    private static Mt5LifecycleValues Values(Mt5NativeBar b) => new(b.OpenLiteral, b.HighLiteral, b.LowLiteral, b.CloseLiteral);
    private static bool Same(Mt5LifecycleValues a, Mt5LifecycleValues b) => Mt5WireContract.SamePrice(a.Open, b.Open)
        && Mt5WireContract.SamePrice(a.High, b.High) && Mt5WireContract.SamePrice(a.Low, b.Low) && Mt5WireContract.SamePrice(a.Close, b.Close);
    private sealed class FrameState { internal int Advancements, Regressions, NonPeriodAdvancements, NonPeriodDeltas, Skipped; }
    private sealed class State(Mt5NativeBar first, Mt5LifecycleReference reference, int firstSegment, Mt5HistoricalObservation observation)
    {
        private readonly List<Mt5LifecycleRevision> revisions = [];
        private readonly Mt5NativeBar initial = first;
        private readonly Mt5LifecycleReference initialReference = reference;
        private Mt5NativeBar last = first;
        private Mt5LifecycleReference lastReference = reference;
        private int segment = firstSegment, snapshots = 1, identical;
        private bool active = first.IsPositionZero, historical = !first.IsPositionZero, activeThenHistorical, interrupted;
        private Mt5HistoricalObservation? firstActive = first.IsPositionZero ? observation : null;
        private Mt5HistoricalObservation? firstHistorical = first.IsPositionZero ? null : observation;
        private Mt5HistoricalObservation? firstUsableHistorical = !first.IsPositionZero && observation.Usable ? observation : null;
        private Mt5LifecycleValues? lastHistoricalValues = first.IsPositionZero ? null : observation.Values;
        private int historicalObservations = first.IsPositionZero ? 0 : 1;
        private int usableObservations = observation.Usable ? 1 : 0;
        private int limitedQualityObservations = observation.Usable ? 0 : 1;
        private int laterHistoricalRevisions;
        internal Mt5HistoricalFreeze Freeze() => new(firstActive, firstHistorical, firstUsableHistorical,
            historicalObservations, usableObservations, limitedQualityObservations, laterHistoricalRevisions);
        internal void Add(Mt5NativeBar bar, Mt5LifecycleReference next, int nextSegment, Mt5HistoricalObservation point)
        {
            var before = Values(last); var after = Values(bar);
            if (!bar.IsPositionZero)
            {
                historicalObservations++;
                // A run can revisit position zero. Compare historical versions to historical versions,
                // rather than misclassifying active-head evolution after a frozen point as history revision.
                if (firstUsableHistorical is not null && lastHistoricalValues is not null && !Same(lastHistoricalValues, after)) laterHistoricalRevisions++;
                lastHistoricalValues = after;
                firstHistorical ??= point;
                if (point.Usable) firstUsableHistorical ??= point;
            }
            else firstActive ??= point;
            if (point.Usable) usableObservations++;
            if (!point.Usable) limitedQualityObservations++;
            if (Same(before, after)) identical++;
            else revisions.Add(new(lastReference, next, last.SeriesPosition, bar.SeriesPosition, before, after, segment != nextSegment));
            activeThenHistorical |= active && !bar.IsPositionZero;
            active |= bar.IsPositionZero; historical |= !bar.IsPositionZero; interrupted |= segment != nextSegment;
            snapshots++; last = bar; lastReference = next; segment = nextSegment;
        }
        internal Mt5LifecycleBar Result(string frame, int limit) => new(frame, initial.OpenTimeNative, initialReference, initial.SeriesPosition,
            lastReference, last.SeriesPosition, Values(last), snapshots, active, historical, activeThenHistorical, interrupted,
            identical, revisions.Count, revisions.Count(r => r.BeforePosition == 0 && r.AfterPosition > 0),
            revisions.Count(r => r.BeforePosition > 0 && r.AfterPosition > 0),
            Array.AsReadOnly(revisions.Take(limit).ToArray()), Math.Max(0, revisions.Count - limit));
    }
}
