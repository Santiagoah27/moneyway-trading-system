using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum M5FvgProfile { GeometryOnly, MiddleCloseConfirmation }
public enum M5FvgDirection { Bullish, Bearish }
public enum M5FvgReach { NotReached, BoundaryTouched, PartiallyReached, FullyReached, GapAmbiguous }
public enum M5FvgEvent { Formed, Inverted, InversionInvalidated, InsufficientEvidence }
public enum M5ZonePosition { Below, AtLower, Inside, AtUpper, Above }

public sealed record M5FvgClosure(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string EvidenceReference);
public sealed record M5FvgSourceGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset ObservedAtUtc,
    string? ClosureEvidence);

public sealed record M5FvgRequest(CandleSeries Series, DateTimeOffset RequestedStartUtc, DateTimeOffset AsOfUtc,
    M5FvgProfile Profile, string SourceProvenance, IReadOnlyList<M5FvgClosure>? DocumentedClosures = null);

public sealed record M5FvgFormation(string Id, M5FvgProfile Profile, M5FvgDirection OriginalDirection,
    Candle C1, Candle C2, Candle C3, decimal LowerBoundary, decimal UpperBoundary, bool MiddleCloseConfirmed,
    string SourceProvenance)
{
    public DateTimeOffset FormationUtc => C3.CloseTimeUtc;
    public decimal GapSize => UpperBoundary - LowerBoundary;
    public decimal OpeningDiscontinuityC2 => C2.Open - C1.Close;
    public decimal OpeningDiscontinuityC3 => C3.Open - C2.Close;
    public M5FvgDirection OppositeDirection => OriginalDirection == M5FvgDirection.Bullish
        ? M5FvgDirection.Bearish : M5FvgDirection.Bullish;
}

public sealed record M5FvgInteraction(M5FvgReach Reach, decimal? IntersectionLower, decimal? IntersectionUpper,
    bool NearBoundaryContact, bool FarBoundaryContact, decimal? MaximumDirectionalReach, decimal? ReachRatio,
    M5ZonePosition OpenPosition, M5ZonePosition ClosePosition, decimal OpeningDiscontinuity,
    bool OpeningGapUncertainty);

public sealed record M5FvgObservation(DateTimeOffset ObservedAtUtc, M5FvgEvent? Event, Candle? Candle,
    M5FvgInteraction? Interaction, M5FvgSourceGap? SourceGap);

public sealed record M5FvgLifecycle(M5FvgFormation Formation, IReadOnlyList<M5FvgObservation> Observations)
{
    public DateTimeOffset? InversionUtc => Observations.FirstOrDefault(o => o.Event == M5FvgEvent.Inverted)?.ObservedAtUtc;
    public M5FvgDirection? InvertedDirection => InversionUtc is null ? null : Formation.OppositeDirection;
    public DateTimeOffset? InvalidationUtc => Observations.FirstOrDefault(o => o.Event == M5FvgEvent.InversionInvalidated)?.ObservedAtUtc;
}

public sealed record M5FvgReport(DateTimeOffset RequestedStartUtc, DateTimeOffset AsOfUtc, M5FvgProfile Profile,
    string ProviderId, string Symbol, string Timeframe, string SourceProvenance, int ClosedCandles,
    int EligibleWindows, IReadOnlyList<M5FvgSourceGap> SourceGaps, IReadOnlyList<M5FvgLifecycle> Zones)
{
    public string CandidateId => "EXP-NQ-M5-FVG-IFVG-V1";
    public string Version => "1.0.0";
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public bool IntrabarOrderingKnown => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Deterministic nominal-close observations, independent from canonical human evidence.</summary>
public sealed class EvaluateM5FvgCandidateUseCase
{
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private sealed class Track(M5FvgFormation formation)
    {
        public M5FvgFormation Formation { get; } = formation;
        public List<M5FvgObservation> Observations { get; } =
            [new(formation.FormationUtc, M5FvgEvent.Formed, formation.C3, null, null)];
        public bool Inverted { get; set; }
    }

    public M5FvgReport Execute(M5FvgRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Series);
        var series = request.Series;
        if (series.Timeframe != M5 || series.ProviderId.Value != "oanda-global-markets-mt5-demo" ||
            series.Symbol.Value != "US100" || !Enum.IsDefined(request.Profile) ||
            request.RequestedStartUtc.Offset != TimeSpan.Zero || request.AsOfUtc.Offset != TimeSpan.Zero ||
            request.RequestedStartUtc >= request.AsOfUtc || string.IsNullOrWhiteSpace(request.SourceProvenance))
            throw new ArgumentException("Explicit Nasdaq US100 M5 source, profile, provenance and UTC interval required.", nameof(request));

        var closures = new Dictionary<(DateTimeOffset, DateTimeOffset), string>();
        foreach (var closure in request.DocumentedClosures ?? [])
        {
            if (closure.StartUtc.Offset != TimeSpan.Zero || closure.EndUtc.Offset != TimeSpan.Zero ||
                closure.StartUtc >= closure.EndUtc || string.IsNullOrWhiteSpace(closure.EvidenceReference) ||
                !closures.TryAdd((closure.StartUtc, closure.EndUtc), closure.EvidenceReference))
                throw new ArgumentException("Documented closures require distinct UTC intervals and evidence.", nameof(request));
        }

        var visible = series.Candles.Where(c => c.OpenTimeUtc >= request.RequestedStartUtc &&
                                               c.CloseTimeUtc <= request.AsOfUtc).ToArray();
        if (visible.Any(c => c.CloseTimeUtc - c.OpenTimeUtc != Interval))
            throw new ArgumentException("Visible M5 candles must have five-minute nominal intervals.", nameof(request));

        var tracks = new List<Track>();
        var active = new List<Track>();
        var gaps = new List<M5FvgSourceGap>();
        var windows = 0;
        var priorClose = request.RequestedStartUtc;
        var contiguous = 0;
        for (var i = 0; i < visible.Length; i++)
        {
            var current = visible[i];
            if (priorClose < current.OpenTimeUtc)
            {
                RecordGap(priorClose, current.OpenTimeUtc, current.CloseTimeUtc);
                contiguous = 0;
            }
            if (i > 0)
            {
                var survivors = 0;
                for (var index = 0; index < active.Count; index++)
                {
                    var track = active[index];
                    if (Observe(track, visible[i - 1], current))
                        active[survivors++] = track;
                }
                active.RemoveRange(survivors, active.Count - survivors);
            }
            priorClose = current.CloseTimeUtc;
            if (++contiguous < 3) continue;
            windows++;
            if (Detect(visible[i - 2], visible[i - 1], current, request) is not { } formation) continue;
            var formed = new Track(formation);
            tracks.Add(formed);
            active.Add(formed);
        }

        if (request.AsOfUtc - priorClose >= Interval)
            RecordGap(priorClose, request.AsOfUtc, request.AsOfUtc, terminal: true);
        return new(request.RequestedStartUtc, request.AsOfUtc, request.Profile, series.ProviderId.Value,
            series.Symbol.Value, series.Timeframe.ToString(), request.SourceProvenance, visible.Length, windows,
            gaps.AsReadOnly(), Array.AsReadOnly(tracks.Select(t => new M5FvgLifecycle(t.Formation,
                t.Observations.AsReadOnly())).ToArray()));

        void RecordGap(DateTimeOffset start, DateTimeOffset end, DateTimeOffset detection, bool terminal = false)
        {
            closures.TryGetValue((start, end), out var evidence);
            if (terminal)
                evidence ??= closures.Where(c => c.Key.Item1 == start && c.Key.Item2 >= end)
                    .OrderBy(c => c.Key.Item2).Select(c => c.Value).FirstOrDefault();
            var nominalMissingClose = start.Add(Interval);
            var observedAt = nominalMissingClose < detection ? nominalMissingClose : detection;
            var gap = new M5FvgSourceGap(start, end, observedAt, evidence);
            gaps.Add(gap);
            if (evidence is not null) return;
            var observedGap = gap with { EndUtc = end < observedAt ? end : observedAt };
            foreach (var track in active)
                track.Observations.Add(new(observedAt, M5FvgEvent.InsufficientEvidence, null, null, observedGap));
            active.Clear();
        }
    }

    private static M5FvgFormation? Detect(Candle first, Candle middle, Candle last, M5FvgRequest request)
    {
        var bullish = last.Low > first.High;
        if (!bullish && last.High >= first.Low) return null;
        var confirmed = bullish ? middle.Close > first.High : middle.Close < first.Low;
        if (!confirmed && request.Profile == M5FvgProfile.MiddleCloseConfirmation) return null;
        var direction = bullish ? M5FvgDirection.Bullish : M5FvgDirection.Bearish;
        var id = FormattableString.Invariant(
            $"EXP-NQ-M5-FVG-IFVG-V1/{first.ProviderId.Value}/{first.Symbol.Value}/{request.Profile}/{direction}/{first.OpenTimeUtc:O}");
        return new(id, request.Profile, direction, first, middle, last, bullish ? first.High : last.High,
            bullish ? last.Low : first.Low, confirmed, request.SourceProvenance);
    }

    private static bool Observe(Track track, Candle previous, Candle candle)
    {
        var formation = track.Formation;
        var bullish = formation.OriginalDirection == M5FvgDirection.Bullish;
        var lower = formation.LowerBoundary;
        var upper = formation.UpperBoundary;
        var open = Position(candle.Open, lower, upper);
        var close = Position(candle.Close, lower, upper);
        var intersectionLow = Math.Max(lower, candle.Low);
        var intersectionHigh = Math.Min(upper, candle.High);
        var intersects = intersectionLow <= intersectionHigh;
        var nearContact = bullish ? candle.Low <= upper && candle.High >= upper : candle.Low <= lower && candle.High >= lower;
        var farContact = bullish ? candle.Low <= lower && candle.High >= lower : candle.Low <= upper && candle.High >= upper;
        var openedAwayFromNearSide = bullish ? candle.Open < upper : candle.Open > lower;
        var openingDiscontinuity = candle.Open - previous.Close;
        decimal? reach = openedAwayFromNearSide ? null : Math.Clamp(
            bullish ? upper - candle.Low : candle.High - lower, 0, formation.GapSize);
        var geometry = openedAwayFromNearSide ? M5FvgReach.GapAmbiguous :
            !intersects ? M5FvgReach.NotReached :
            farContact ? M5FvgReach.FullyReached :
            reach == 0 ? M5FvgReach.BoundaryTouched : M5FvgReach.PartiallyReached;
        var interaction = new M5FvgInteraction(geometry, intersects ? intersectionLow : null,
            intersects ? intersectionHigh : null, nearContact, farContact, reach, reach / formation.GapSize,
            open, close, openingDiscontinuity, openedAwayFromNearSide && openingDiscontinuity != 0);

        M5FvgEvent? transition = null;
        if (!track.Inverted && (bullish ? close == M5ZonePosition.Below : close == M5ZonePosition.Above))
        {
            track.Inverted = true;
            transition = M5FvgEvent.Inverted;
        }
        else if (track.Inverted && (bullish ? close == M5ZonePosition.Above : close == M5ZonePosition.Below))
            transition = M5FvgEvent.InversionInvalidated;
        track.Observations.Add(new(candle.CloseTimeUtc, transition, candle, interaction, null));
        return transition != M5FvgEvent.InversionInvalidated;
    }

    private static M5ZonePosition Position(decimal price, decimal lower, decimal upper)
    {
        if (price < lower) return M5ZonePosition.Below;
        if (price == lower) return M5ZonePosition.AtLower;
        if (price > upper) return M5ZonePosition.Above;
        return price == upper ? M5ZonePosition.AtUpper : M5ZonePosition.Inside;
    }
}
