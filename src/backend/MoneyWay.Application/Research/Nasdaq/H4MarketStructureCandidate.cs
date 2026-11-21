using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum H4PivotKind { High, Low }
public enum H4HighRelation { HigherHigh, LowerHigh, EqualHigh, InsufficientEvidence }
public enum H4LowRelation { HigherLow, LowerLow, EqualLow, InsufficientEvidence }
public enum H4StructureClassification { Bullish, Bearish, Mixed, Indeterminate, InsufficientEvidence }
public enum H4ResearchStatus { Classified, AmbiguousStructure, InsufficientHistory, MissingSourceIntervals, InsufficientConfirmedPivots }

public sealed record H4PivotProfile(string Id, int LeftBars, int RightBars)
{
    public static IReadOnlyList<H4PivotProfile> InitialResearchProfiles { get; } =
    [new("EXP-NQ-H4-STRICT-1-1-V1", 1, 1), new("EXP-NQ-H4-STRICT-2-2-V1", 2, 2),
     new("EXP-NQ-H4-STRICT-3-3-V1", 3, 3)];
}

public sealed record H4DocumentedClosure(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string EvidenceReference);
public sealed record H4SourceGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc, bool DocumentedClosure, string? EvidenceReference);

public sealed record H4ConfirmedPivot(string CandidateId, H4PivotKind Kind, decimal Price,
    DateTimeOffset ExtremeOpenUtc, DateTimeOffset ExtremeNominalCloseUtc, DateTimeOffset ConfirmedAtUtc,
    string ProfileId, int LeftBars, int RightBars, string SourceProvenance)
{
    public TimeSpan ConfirmationDelay => ConfirmedAtUtc - ExtremeNominalCloseUtc;
}

public sealed record H4MarketStructureRequest(CandleSeries Series, DateTimeOffset RequestedStartUtc,
    DateTimeOffset AsOfUtc, H4PivotProfile Profile, string SourceProvenance,
    IReadOnlyList<H4DocumentedClosure>? DocumentedClosures = null);

public sealed record H4MarketStructureObservation(DateTimeOffset AsOfUtc, H4PivotProfile Profile,
    string ProviderId, string Instrument, string OriginalTimeframe, string SourceProvenance,
    DateTimeOffset RequestedStartUtc, int ClosedCandleCount, DateTimeOffset? FirstOpenUtc,
    DateTimeOffset? LastNominalCloseUtc, IReadOnlyList<H4SourceGap> SourceGaps,
    IReadOnlyList<H4ConfirmedPivot> ConfirmedPivots,
    H4ConfirmedPivot? PreviousHigh, H4ConfirmedPivot? CurrentHigh,
    H4ConfirmedPivot? PreviousLow, H4ConfirmedPivot? CurrentLow,
    H4HighRelation HighRelation, H4LowRelation LowRelation,
    H4StructureClassification Classification, H4ResearchStatus Status, string ReasonCode)
{
    public string CandidateId => "EXP-NQ-H4-MARKET-STRUCTURE-V1";
    public string CandidateVersion => "1.0.0";
    public string HistoricalAvailability => "Documentary_NotAuthenticatedAtHistoricalCutoff";
    public bool MentorRuleConfirmed => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Pure, strict local-extrema research over original closed H4 candles.</summary>
public sealed class EvaluateH4MarketStructureCandidateUseCase
{
    private static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);

    public H4MarketStructureObservation Execute(H4MarketStructureRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Series);
        ArgumentNullException.ThrowIfNull(request.Profile);
        if (request.Series.Timeframe != H4 || request.Series.Symbol.Value != "US100" ||
            request.Series.ProviderId.Value != "oanda-global-markets-mt5-demo")
            throw new ArgumentException("Original OANDA demo US100 H4 series required.", nameof(request));
        if (request.Profile.LeftBars <= 0 || request.Profile.RightBars <= 0 ||
            string.IsNullOrWhiteSpace(request.Profile.Id) || string.IsNullOrWhiteSpace(request.SourceProvenance) ||
            request.AsOfUtc.Offset != TimeSpan.Zero || request.RequestedStartUtc.Offset != TimeSpan.Zero ||
            request.RequestedStartUtc >= request.AsOfUtc)
            throw new ArgumentException("Explicit valid profile, provenance and UTC interval required.", nameof(request));

        var visible = request.Series.Candles.Where(c => c.OpenTimeUtc >= request.RequestedStartUtc &&
                                                       c.CloseTimeUtc <= request.AsOfUtc).ToArray();
        var nominalDuration = TimeSpan.FromHours(4);
        if (visible.Any(c => c.CloseTimeUtc - c.OpenTimeUtc != nominalDuration))
            throw new ArgumentException("Visible original H4 candles require a four-hour nominal interval.", nameof(request));
        var closures = request.DocumentedClosures ?? [];
        foreach (var closure in closures)
            if (closure.StartUtc.Offset != TimeSpan.Zero || closure.EndUtc.Offset != TimeSpan.Zero ||
                closure.StartUtc >= closure.EndUtc || string.IsNullOrWhiteSpace(closure.EvidenceReference))
                throw new ArgumentException("Documented closures require valid UTC bounds and evidence.", nameof(request));
        if (closures.GroupBy(c => (c.StartUtc, c.EndUtc)).Any(group => group.Count() > 1))
            throw new ArgumentException("Duplicate documented closure intervals are not supported.", nameof(request));
        var gaps = new List<H4SourceGap>();
        if (visible.Length > 0 && visible[0].OpenTimeUtc > request.RequestedStartUtc)
        {
            var leading = closures.SingleOrDefault(c => c.StartUtc == request.RequestedStartUtc &&
                                                        c.EndUtc == visible[0].OpenTimeUtc);
            gaps.Add(new(request.RequestedStartUtc, visible[0].OpenTimeUtc,
                leading is not null, leading?.EvidenceReference));
        }
        foreach (var pair in visible.Zip(visible.Skip(1)))
        {
            if (pair.First.CloseTimeUtc >= pair.Second.OpenTimeUtc) continue;
            var closure = closures.SingleOrDefault(c => c.StartUtc == pair.First.CloseTimeUtc && c.EndUtc == pair.Second.OpenTimeUtc);
            gaps.Add(new(pair.First.CloseTimeUtc, pair.Second.OpenTimeUtc, closure is not null, closure?.EvidenceReference));
        }
        if (visible.Length > 0 && request.AsOfUtc - visible[^1].CloseTimeUtc >= nominalDuration)
        {
            var lastClose = visible[^1].CloseTimeUtc;
            var closure = closures.SingleOrDefault(c => c.StartUtc == lastClose && c.EndUtc == request.AsOfUtc);
            gaps.Add(new(lastClose, request.AsOfUtc, closure is not null, closure?.EvidenceReference));
        }
        var leadingUncovered = gaps.Count > 0 && gaps[0].StartUtc == request.RequestedStartUtc &&
                               !gaps[0].DocumentedClosure;
        var insufficientHistory = visible.Length == 0 || leadingUncovered ||
                                  visible.Length < (long)request.Profile.LeftBars + request.Profile.RightBars + 1;
        var unknownGap = gaps.Any(g => !g.DocumentedClosure);
        var pivots = new List<H4ConfirmedPivot>();
        if (!unknownGap && !insufficientHistory)
            foreach (var (extreme, confirmedAt, high, low) in StrictConfirmedPivots.Find(
                         visible, request.Profile.LeftBars, request.Profile.RightBars))
            {
                if (high)
                    pivots.Add(Pivot(H4PivotKind.High, extreme.High));
                if (low)
                    pivots.Add(Pivot(H4PivotKind.Low, extreme.Low));

                H4ConfirmedPivot Pivot(H4PivotKind kind, decimal price) => new(
                    $"{request.Profile.Id}:{kind}:{extreme.OpenTimeUtc:yyyyMMddTHHmmssZ}", kind, price,
                    extreme.OpenTimeUtc, extreme.CloseTimeUtc, confirmedAt, request.Profile.Id,
                    request.Profile.LeftBars, request.Profile.RightBars, request.SourceProvenance);
            }
        var highs = pivots.Where(p => p.Kind == H4PivotKind.High).ToArray();
        var lows = pivots.Where(p => p.Kind == H4PivotKind.Low).ToArray();
        var previousHigh = highs.Length >= 2 ? highs[^2] : null;
        var currentHigh = highs.LastOrDefault();
        var previousLow = lows.Length >= 2 ? lows[^2] : null;
        var currentLow = lows.LastOrDefault();
        var highRelation = previousHigh is null ? H4HighRelation.InsufficientEvidence :
            currentHigh!.Price > previousHigh.Price ? H4HighRelation.HigherHigh :
            currentHigh.Price < previousHigh.Price ? H4HighRelation.LowerHigh : H4HighRelation.EqualHigh;
        var lowRelation = previousLow is null ? H4LowRelation.InsufficientEvidence :
            currentLow!.Price > previousLow.Price ? H4LowRelation.HigherLow :
            currentLow.Price < previousLow.Price ? H4LowRelation.LowerLow : H4LowRelation.EqualLow;
        var status = insufficientHistory ? H4ResearchStatus.InsufficientHistory :
            unknownGap ? H4ResearchStatus.MissingSourceIntervals :
            previousHigh is null || previousLow is null ? H4ResearchStatus.InsufficientConfirmedPivots :
            highRelation == H4HighRelation.EqualHigh || lowRelation == H4LowRelation.EqualLow ||
            currentHigh!.ExtremeOpenUtc == currentLow!.ExtremeOpenUtc ? H4ResearchStatus.AmbiguousStructure :
            H4ResearchStatus.Classified;
        var classification = status != H4ResearchStatus.Classified ?
            status == H4ResearchStatus.AmbiguousStructure ? H4StructureClassification.Indeterminate : H4StructureClassification.InsufficientEvidence :
            highRelation == H4HighRelation.HigherHigh && lowRelation == H4LowRelation.HigherLow ? H4StructureClassification.Bullish :
            highRelation == H4HighRelation.LowerHigh && lowRelation == H4LowRelation.LowerLow ? H4StructureClassification.Bearish :
            H4StructureClassification.Mixed;
        return new(request.AsOfUtc, request.Profile, request.Series.ProviderId.Value, request.Series.Symbol.Value,
            request.Series.Timeframe.ToString(), request.SourceProvenance, request.RequestedStartUtc, visible.Length,
            visible.FirstOrDefault()?.OpenTimeUtc, visible.LastOrDefault()?.CloseTimeUtc, gaps.AsReadOnly(),
            pivots.AsReadOnly(), previousHigh, currentHigh, previousLow, currentLow, highRelation, lowRelation,
            classification, status, status.ToString());
    }
}
