using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum H4LevelOutcome
{
    InsufficientEvidence, NoInteraction, Touch, Penetration, CloseAtReference,
    OpeningGapBeyond, ContinuedBeyond, BullishBreakout, BearishBreakout,
    UpperImmediateRejection, LowerImmediateRejection
}

public enum H4DelayedStatus { Pending, Invalidated, HorizonCompleted, EvidenceLimited }
public enum H4WickOutcome { InsufficientEvidence, NotApplicable, NotReached, BoundaryTouched, PartiallyReached, FullyReached, GapAmbiguous }

public sealed record H4LevelInteraction(H4PivotKind Side, H4ConfirmedPivot? Reference, H4LevelOutcome Outcome,
    decimal? PriorClose, decimal Open, decimal RangeExtreme, decimal Close, bool Touched, bool Penetrated,
    bool ClosedBeyond, bool ClosedAtReference, bool OpenedBeyond);

public sealed record H4DelayedObservation(H4ConfirmedPivot Reference, DateTimeOffset BreakoutOpenUtc,
    DateTimeOffset BreakoutConfirmedUtc, DateTimeOffset ObservedAtUtc, int Horizon, int ObservedSubsequentCandles,
    H4DelayedStatus Status)
{
    public DateTimeOffset? InvalidationUtc => Status == H4DelayedStatus.Invalidated ? ObservedAtUtc : null;
}

public sealed record H4WickObservation(H4PivotKind Side, H4WickOutcome Outcome, DateTimeOffset? ReferenceOpenUtc,
    DateTimeOffset? ReferenceCloseUtc, decimal? Base, decimal? Extreme, decimal? Length,
    decimal? IntersectionLow, decimal? IntersectionHigh, decimal? MaximumReachFromBase, decimal? ReachRatio,
    bool BothBoundariesIncluded, bool GapUncertainty);

public sealed record H4CandleContext(DateTimeOffset CandleOpenUtc, DateTimeOffset ObservedAtUtc,
    H4LevelInteraction Upper, H4LevelInteraction Lower, H4WickObservation UpperWick, H4WickObservation LowerWick,
    string CoverageReason)
{
    public bool IntrabarOrderingUnknown => Upper.Penetrated && Lower.Penetrated;
}

public sealed record H4ContextObservation(DateTimeOffset AsOfUtc, int Horizon, H4MarketStructureObservation MarketStructure,
    IReadOnlyList<H4CandleContext> CandleHistory, IReadOnlyList<H4DelayedObservation> DelayedHistory)
{
    public string CandidateId => "EXP-NQ-H4-CONTEXT-V1";
    public string CandidateVersion => "1.0.0";
    public string ConfigurationId => FormattableString.Invariant($"{MarketStructure.Profile.Id}:{MarketStructure.Profile.LeftBars}:{MarketStructure.Profile.RightBars}:H{Horizon}");
    public H4CandleContext? LatestClosedCandle => CandleHistory.LastOrDefault();
    public TimeSpan? LatestClosedCandleAge => LatestClosedCandle is { } candle ? AsOfUtc - candle.ObservedAtUtc : null;
    public IReadOnlyList<TimeSpan> CandleEvidenceAges => CandleHistory.Select(candle => AsOfUtc - candle.ObservedAtUtc).ToArray();
    public IReadOnlyList<TimeSpan> DelayedEvidenceAges => DelayedHistory.Select(update => AsOfUtc - update.ObservedAtUtc).ToArray();
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Experimental observations on nominally closed candles. Never supplies canonical replay facts.</summary>
public sealed class EvaluateH4ContextCandidateUseCase
{
    public H4ContextObservation Execute(H4MarketStructureRequest request, int horizon)
    {
        if (horizon is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(horizon), "Select an explicit horizon of one or two closed H4 candles.");

        var evaluator = new EvaluateH4MarketStructureCandidateUseCase();
        var structure = evaluator.Execute(request);
        var ongoingClosure = (request.DocumentedClosures ?? []).FirstOrDefault(c =>
            c.StartUtc == structure.LastNominalCloseUtc && c.EndUtc > request.AsOfUtc);
        if (ongoingClosure is not null && structure.SourceGaps.Any(g =>
                g.StartUtc == ongoingClosure.StartUtc && g.EndUtc == request.AsOfUtc && !g.DocumentedClosure))
            structure = evaluator.Execute(request with
            {
                DocumentedClosures = [.. request.DocumentedClosures!, ongoingClosure with { EndUtc = request.AsOfUtc }]
            });
        var visible = request.Series.Candles.Where(c => c.OpenTimeUtc >= request.RequestedStartUtc &&
                                                       c.CloseTimeUtc <= request.AsOfUtc).ToArray();
        var gaps = structure.SourceGaps.ToDictionary(g => g.StartUtc);
        var history = new List<H4CandleContext>();
        var delayed = new List<H4DelayedObservation>();
        var pending = new List<H4DelayedObservation>();
        H4ConfirmedPivot? high = null;
        H4ConfirmedPivot? low = null;
        Candle? prior = null;

        for (var start = 0; start < visible.Length;)
        {
            var end = start + 1;
            while (end < visible.Length && visible[end - 1].CloseTimeUtc == visible[end].OpenTimeUtc)
                end++;

            var segment = new CandleSeries(request.Series.ProviderId, request.Series.Symbol, request.Series.Timeframe,
                visible[start..end]);
            var pivots = evaluator.Execute(request with
            {
                Series = segment,
                RequestedStartUtc = visible[start].OpenTimeUtc,
                AsOfUtc = visible[end - 1].CloseTimeUtc,
                DocumentedClosures = null
            }).ConfirmedPivots;

            var nextPivot = 0;
            for (var index = start; index < end; index++)
            {
                var candle = visible[index];
                var coverage = prior is null ? "NoPriorClosedCandle" : "AdjacentClosedCandles";
                if (prior is not null && gaps.TryGetValue(prior.CloseTimeUtc, out var gap))
                {
                    coverage = gap.DocumentedClosure ? "DocumentedMarketClosure" : "UnexplainedSourceGap";
                    if (!gap.DocumentedClosure)
                    {
                        LimitPending(prior.CloseTimeUtc.AddHours(4) < candle.CloseTimeUtc
                            ? prior.CloseTimeUtc.AddHours(4) : candle.CloseTimeUtc);
                        high = low = null;
                        prior = null;
                    }
                }

                while (nextPivot < pivots.Count && pivots[nextPivot].ConfirmedAtUtc <= candle.OpenTimeUtc)
                {
                    var pivot = pivots[nextPivot++];
                    if (pivot.Kind == H4PivotKind.High) high = pivot;
                    else low = pivot;
                }

                for (var trackIndex = 0; trackIndex < pending.Count;)
                {
                    var track = pending[trackIndex];
                    var count = track.ObservedSubsequentCandles + 1;
                    var invalidated = track.Reference.Kind == H4PivotKind.High
                        ? candle.Close < track.Reference.Price : candle.Close > track.Reference.Price;
                    var status = invalidated ? H4DelayedStatus.Invalidated :
                        count == horizon ? H4DelayedStatus.HorizonCompleted : H4DelayedStatus.Pending;
                    var update = track with { ObservedAtUtc = candle.CloseTimeUtc, ObservedSubsequentCandles = count, Status = status };
                    delayed.Add(update);
                    if (status == H4DelayedStatus.Pending) pending[trackIndex++] = update;
                    else pending.RemoveAt(trackIndex);
                }

                var upper = Level(candle, prior, H4PivotKind.High, high);
                var lower = Level(candle, prior, H4PivotKind.Low, low);
                foreach (var observation in new[] { upper, lower })
                {
                    if (observation.Outcome is not (H4LevelOutcome.BullishBreakout or H4LevelOutcome.BearishBreakout))
                        continue;
                    var reference = observation.Reference!;
                    var track = new H4DelayedObservation(reference, candle.OpenTimeUtc, candle.CloseTimeUtc,
                        candle.CloseTimeUtc, horizon, 0, H4DelayedStatus.Pending);
                    pending.Add(track);
                    delayed.Add(track);
                }

                history.Add(new(candle.OpenTimeUtc, candle.CloseTimeUtc, upper, lower,
                    Wick(candle, prior, H4PivotKind.High), Wick(candle, prior, H4PivotKind.Low), coverage));
                prior = candle;
            }
            // A pivot confirmed at the final close can become a reference after a documented closure.
            while (nextPivot < pivots.Count)
            {
                var pivot = pivots[nextPivot++];
                if (pivot.Kind == H4PivotKind.High) high = pivot;
                else low = pivot;
            }
            start = end;
        }

        if (prior is not null && gaps.TryGetValue(prior.CloseTimeUtc, out var tail) && !tail.DocumentedClosure)
            LimitPending(prior.CloseTimeUtc.AddHours(4));

        return new(request.AsOfUtc, horizon, structure, history.AsReadOnly(), delayed.AsReadOnly());

        void LimitPending(DateTimeOffset observedAt)
        {
            delayed.AddRange(pending.Select(track => track with { ObservedAtUtc = observedAt, Status = H4DelayedStatus.EvidenceLimited }));
            pending.Clear();
        }
    }

    private static H4LevelInteraction Level(Candle candle, Candle? prior, H4PivotKind side,
        H4ConfirmedPivot? reference)
    {
        var upper = side == H4PivotKind.High;
        var extreme = upper ? candle.High : candle.Low;
        if (reference is null || prior is null)
            return new(side, reference, H4LevelOutcome.InsufficientEvidence, prior?.Close, candle.Open, extreme,
                candle.Close, false, false, false, false, false);

        var price = reference.Price;
        bool Beyond(decimal value) => upper ? value > price : value < price;
        var touched = candle.Low <= price && price <= candle.High;
        var penetrated = Beyond(extreme);
        var closedBeyond = Beyond(candle.Close);
        var openedBeyond = Beyond(candle.Open);
        var closedAt = candle.Close == price;
        var priorBeyond = Beyond(prior.Close);
        var outcome = H4LevelOutcome.NoInteraction;
        if (openedBeyond && !priorBeyond) outcome = H4LevelOutcome.OpeningGapBeyond;
        else if (closedAt) outcome = H4LevelOutcome.CloseAtReference;
        else if (closedBeyond && priorBeyond) outcome = H4LevelOutcome.ContinuedBeyond;
        else if (!priorBeyond && !openedBeyond && penetrated)
        {
            if (closedBeyond) outcome = upper ? H4LevelOutcome.BullishBreakout : H4LevelOutcome.BearishBreakout;
            else outcome = upper ? H4LevelOutcome.UpperImmediateRejection : H4LevelOutcome.LowerImmediateRejection;
        }
        else if (penetrated) outcome = H4LevelOutcome.Penetration;
        else if (touched) outcome = H4LevelOutcome.Touch;
        return new(side, reference, outcome, prior.Close, candle.Open, extreme, candle.Close, touched, penetrated,
            closedBeyond, closedAt, openedBeyond);
    }

    private static H4WickObservation Wick(Candle candle, Candle? prior, H4PivotKind side)
    {
        if (prior is null)
            return new(side, H4WickOutcome.InsufficientEvidence, null, null, null, null, null, null, null, null, null, false, false);

        var upper = side == H4PivotKind.High;
        var @base = upper ? Math.Max(prior.Open, prior.Close) : Math.Min(prior.Open, prior.Close);
        var extreme = upper ? prior.High : prior.Low;
        var length = Math.Abs(extreme - @base);
        var low = Math.Max(candle.Low, Math.Min(@base, extreme));
        var high = Math.Min(candle.High, Math.Max(@base, extreme));
        var intersects = low <= high;
        var bothBoundaries = intersects && high - low == length;
        var gap = length > 0 && (upper ? candle.Open > @base : candle.Open < @base);
        decimal? reach = null;
        decimal? ratio = null;
        var outcome = H4WickOutcome.NotApplicable;
        if (length > 0)
        {
            if (gap) outcome = H4WickOutcome.GapAmbiguous;
            else
            {
                reach = Math.Clamp(upper ? candle.High - @base : @base - candle.Low, 0, length);
                ratio = reach.Value / length;
                outcome = !intersects ? H4WickOutcome.NotReached : reach == 0 ? H4WickOutcome.BoundaryTouched :
                    reach == length ? H4WickOutcome.FullyReached : H4WickOutcome.PartiallyReached;
            }
        }
        return new(side, outcome, prior.OpenTimeUtc, prior.CloseTimeUtc, @base, extreme, length,
            intersects && length > 0 ? low : null, intersects && length > 0 ? high : null,
            reach, ratio, length > 0 && bothBoundaries, gap);
    }
}
