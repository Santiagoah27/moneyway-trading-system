using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

public sealed record Mt5CausalFrameAcquisition(string Timeframe, string Status, int? CopyReturn, int? CopyErrorCode,
    ulong? CopyStartedElapsedUs, ulong? CopyFinishedElapsedUs, bool? HeadConsistent, int ObservedBars, bool AnyInvalidOhlc);
public sealed record Mt5CausalAcquisition(Mt5LifecycleReference Receipt, string EvidenceKind, bool SampleComplete,
    bool SourceStable, bool M1Usable, string? ProbeGate, bool? ConnectedBefore, bool? ConnectedAfter,
    IReadOnlyList<Mt5CausalFrameAcquisition> Frames, IReadOnlyList<string> Diagnostics);
public sealed record Mt5CausalBarObservation(Mt5LifecycleBar Bar, Mt5CausalAcquisition FirstAcquisition,
    Mt5CausalAcquisition LatestAcquisition, Mt5LifecycleValues FirstValues, bool FirstOhlcValid, bool LatestOhlcValid)
{
    public string Availability => "observed_in_validated_receipt_prefix";
    public string LatestPosition => Bar.LastPosition == 0 ? "active_head_potentially_incomplete" : "historical_position_observed";
}

/// <summary>All observations are derived only after prefix validation. Detail limits affect serialization, never facts.</summary>
public sealed class Mt5CausalAvailabilityProjection
{
    internal Mt5CausalAvailabilityProjection(ValidatedMt5ReceiptPrefix prefix, Mt5LifecycleReport lifecycle,
        IReadOnlyList<Mt5CausalAcquisition> acquisitions, IReadOnlyList<Mt5CausalBarObservation> bars)
    {
        Prefix = prefix;
        Lifecycle = lifecycle;
        Acquisitions = acquisitions;
        Bars = bars;
    }

    public ValidatedMt5ReceiptPrefix Prefix { get; }
    public Mt5LifecycleReport Lifecycle { get; }
    public IReadOnlyList<Mt5CausalAcquisition> Acquisitions { get; }
    public IReadOnlyList<Mt5CausalBarObservation> Bars { get; }
    public Mt5CausalBarObservation? Find(string timeframe, long nativeOpen) =>
        Bars.SingleOrDefault(b => b.Bar.Timeframe == timeframe && b.Bar.OpenTimeNative.Raw == nativeOpen);

    public object Report(int detailLimit = 16, string? timeframe = null, long? nativeOpen = null)
    {
        if (detailLimit is < 0 or > 128 || (timeframe is null) != (nativeOpen is null)) throw new ArgumentException("Invalid query or detail limit.");
        var selected = Bars.GroupBy(b => b.Bar.Timeframe, StringComparer.Ordinal).Select(g => g.First())
            .Concat(Bars).DistinctBy(b => (b.Bar.Timeframe, b.Bar.OpenTimeNative.Raw)).Take(detailLimit).ToArray();
        return new
        {
            Schema = "moneyway.mt5-causal-receipt-prefix-availability",
            Version = 1,
            Source = new
            {
                Lifecycle.Company,
                Lifecycle.Server,
                Lifecycle.NativeSymbol,
                Lifecycle.Demo,
                IdentityValidated = Prefix.Records.Count > 0
            },
            Prefix.DirectoryScope,
            Prefix.Boundary,
            Prefix.FinalReceipt,
            Prefix.EvidenceManifestSha256,
            ProjectionIntegrityStatus = Mt5ReceiptStatus.Validated,
            SourceObservationCausality = "context_specific",
            SourceObservationsAvailable = Bars.Count > 0,
            ValidatedRecords = Prefix.Records.Count,
            DistinctObservedCandles = Bars.Count,
            Events = Lifecycle.Events,
            EvidenceKinds = Lifecycle.EvidenceKinds,
            Diagnostics = Lifecycle.Diagnostics,
            RunBoundaries = Lifecycle.RunBoundaries,
            ActiveHeadObservations = Prefix.Records.Sum(r => r.Observation!.Timeframes.Sum(f => f.Bars.Count(b => b.IsPositionZero))),
            HistoricalPositionObservations = Prefix.Records.Sum(r => r.Observation!.Timeframes.Sum(f => f.Bars.Count(b => !b.IsPositionZero))),
            Timeframes = Lifecycle.Timeframes,
            SelectedAcquisitions = Acquisitions.Where(a => a.EvidenceKind != nameof(Mt5ReceiptEvidenceKind.AcquisitionSnapshot)
                    || !a.SampleComplete || !a.SourceStable).Concat(Acquisitions)
                .DistinctBy(a => a.Receipt.RecordFile).Take(detailLimit).ToArray(),
            OmittedAcquisitions = Math.Max(0, Acquisitions.Count - detailLimit),
            SelectedBars = selected,
            OmittedBars = Bars.Count - selected.Length,
            RequestedObservation = timeframe is null ? null : new
            {
                Timeframe = timeframe,
                NativeOpenRaw = nativeOpen,
                AvailableInPrefix = Find(timeframe, nativeOpen!.Value) is not null,
                Observation = Find(timeframe, nativeOpen!.Value)
            },
            HistoricalUtcAvailability = "unresolved",
            ProviderClosure = "unresolved",
            AtomicMultiTimeframeSnapshot = "unresolved",
            VolumeEvidence = "data_unavailable",
            SelectionIntervalPropagation = "unresolved",
            OverallUtcUncertainty = "unresolved",
            WindowsClockAuthenticated = false,
            Mt5NativeUtcAuthenticated = false,
            CanonicalUtcConversionAuthorized = false,
            UtcConversionAuthenticated = false,
            CanonicalReady = false
        };
    }
}

public sealed class Mt5CausalAvailabilityAudit
{
    public Mt5CausalAvailabilityProjection Project(ValidatedMt5ReceiptPrefix prefix, int revisionDetailLimit = 16)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var lifecycle = new Mt5NativeLifecycleAudit().AnalyzePrefix(prefix, revisionDetailLimit);
        var acquisitions = prefix.Records.Select(r =>
        {
            var a = r.Observation!;
            var m = a.Metadata;
            return new Mt5CausalAcquisition(new(a.RecordFile, a.RecordSha256, m.Sequence, m.RunId, m.PollSequence,
                    m.Event, m.ProbeFile, m.ProbeSha256), a.EvidenceKind.ToString(), m.SampleComplete, m.SourceStable,
                m.M1Usable, a.ProbeGate, a.ConnectedBefore, a.ConnectedAfter,
                Array.AsReadOnly(a.Timeframes.Select(f => new Mt5CausalFrameAcquisition(f.Timeframe, f.AcquisitionStatus,
                    f.CopyReturn, f.CopyErrorCode, f.CopyStartedElapsedUs, f.CopyFinishedElapsedUs,
                    f.HeadConsistent, f.Bars.Count, f.Bars.Any(b => !b.OhlcValid))).ToArray()), r.Diagnostics);
        }).ToArray();
        var byRecord = acquisitions.ToDictionary(a => a.Receipt.RecordFile, StringComparer.Ordinal);
        var sourceByRecord = prefix.Records.ToDictionary(r => r.FileName!, r => r.Observation!, StringComparer.Ordinal);
        var bars = lifecycle.SelectedBars.Select(b => new Mt5CausalBarObservation(b,
            byRecord[b.FirstObserved.RecordFile], byRecord[b.LastObserved.RecordFile],
            Values(b.FirstObserved.RecordFile, b.Timeframe, b.OpenTimeNative.Raw),
            Valid(b.FirstObserved.RecordFile, b.Timeframe, b.OpenTimeNative.Raw),
            Valid(b.LastObserved.RecordFile, b.Timeframe, b.OpenTimeNative.Raw))).ToArray();
        return new(prefix, lifecycle, Array.AsReadOnly(acquisitions), Array.AsReadOnly(bars));

        bool Valid(string record, string frame, long raw) => sourceByRecord[record].Timeframes
            .Single(f => f.Timeframe == frame).Bars.Single(b => b.OpenTimeNative.Raw == raw).OhlcValid;
        Mt5LifecycleValues Values(string record, string frame, long raw)
        {
            var b = sourceByRecord[record].Timeframes.Single(f => f.Timeframe == frame).Bars.Single(b => b.OpenTimeNative.Raw == raw);
            return new(b.OpenLiteral, b.HighLiteral, b.LowLiteral, b.CloseLiteral);
        }
    }
}
