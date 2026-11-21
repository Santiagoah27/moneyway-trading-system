using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum StructuralTargetPolicy { NearestConfirmedM5Swing, NearestAuditedSessionExtreme }
public enum StructuralTargetReason
{
    Eligible, WrongSide, EqualToReference, NotYetObservable, SourceGap, InsufficientEvidence
}
public enum StructuralTargetOutcome
{
    WaitingForM1Realignment, TargetIdentified, TiedTargetsIdentified, NoHistoricalReference,
    NoFavorableReference, ReferencesUnavailable, InsufficientEvidence
}
public enum StructuralTargetInteraction { ReachedInClosedM5, NotReachedInCoveredM5, UnknownDueToSourceGap, NoClosedM5Interval }

public sealed record StructuralTargetProfile(StructuralTargetPolicy Policy, M5StructuralPivotProfile? PivotProfile)
{
    public string Id => PivotProfile is null ? Policy.ToString() : $"{Policy}/{PivotProfile.Id}";
}

public sealed record StructuralTargetInteractionEvidence(StructuralTargetInteraction Status,
    DateTimeOffset StartUtc, DateTimeOffset CoveredThroughUtc, Candle? FirstObservedReach,
    IReadOnlyList<M5FvgSourceGap> Gaps);

public sealed record StructuralSessionEndpoint(string Session, string StrategyVersion, DateOnly OperatingDate,
    DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset? EvaluatedAtUtc, string CoverageStatus,
    string AvailabilityMeaning, IReadOnlyList<DateTimeOffset> MissingObservableOpensUtc,
    IReadOnlyList<Candle> ExtremeCandles);

public sealed record StructuralTargetReference(string Id, string Category, string Provider, string Instrument,
    string Timeframe, M5StructuralPivotKind Kind, DateTimeOffset SourceTimestampUtc, DateTimeOffset? AvailableAtUtc,
    decimal? Price, bool EligibleAtCutoff, bool? FavorableDirection, decimal? DistancePoints, StructuralTargetReason Reason,
    string SourceProvenance, StructuralTargetInteractionEvidence? PriorInteraction,
    M5StructuralPivot? Pivot, StructuralSessionEndpoint? Session);

public sealed record StructuralTargetEvaluation(M1RealignmentCloseReference CloseReference, M5FvgDirection Direction,
    StructuralTargetProfile Profile, string SourceProvenance, IReadOnlyList<M5StructuralSourceGap> PivotSourceGaps,
    IReadOnlyList<StructuralTargetReference> Candidates, IReadOnlyList<string> SelectedIds,
    decimal? TargetPrice, decimal? RewardDistancePoints, StructuralTargetOutcome Outcome, string ReasonCode)
{
    public DateTimeOffset AsOfUtc => CloseReference.ObservedAtUtc;
}

/// <summary>Compares independent target pools using already observed research evidence.</summary>
internal static class NasdaqStructuralTakeProfitCandidate
{
    private static readonly TimeSpan M5Interval = TimeSpan.FromMinutes(5);

    internal static StructuralTargetEvaluation Swings(M5StructuralStopObservation stop, CandleSeries m5,
        IReadOnlyList<M5FvgClosure> closures)
    {
        var kind = TargetKind(stop.Direction);
        var profile = new StructuralTargetProfile(StructuralTargetPolicy.NearestConfirmedM5Swing, stop.Profile);
        var candidates = stop.ConfirmedSwings.Select(s => s.Pivot)
            .Where(p => p.Kind == kind && p.ConfirmedAtUtc <= stop.AsOfUtc)
            .Select(p => Reference(stop.CloseReference, stop.Direction, m5, closures, p.Id, "M5Swing",
                m5.Timeframe.ToString(), p.ExtremeOpenUtc, p.ConfirmedAtUtc, p.Price, p.ExtremeCloseUtc,
                stop.SourceProvenance, pivot: p));
        var limited = stop.Outcome is M5StructuralStopOutcome.MissingSourceEvidence or M5StructuralStopOutcome.InsufficientM5History;
        return Evaluate(stop.CloseReference, stop.Direction, profile, stop.SourceProvenance, stop.SourceGaps, candidates,
            limited ? stop.ReasonCode : null);
    }

    internal static StructuralTargetEvaluation Sessions(M1RealignmentCloseReference close, M5FvgDirection direction,
        CandleSeries m5, IReadOnlyList<NasdaqSessionReference> sessions, string provenance, IReadOnlyList<M5FvgClosure> closures)
    {
        var kind = TargetKind(direction);
        var candidates = sessions.Select(s => Endpoint(s));
        return Evaluate(close, direction, new(StructuralTargetPolicy.NearestAuditedSessionExtreme, null),
            provenance, [], candidates);

        StructuralTargetReference Endpoint(NasdaqSessionReference source)
        {
            var cutoff = close.ObservedAtUtc;
            var high = kind == M5StructuralPivotKind.High;
            var id = FormattableString.Invariant($"{source.Provider}/{source.Instrument}/{source.StrategyVersion}/{source.OperatingDate:yyyy-MM-dd}/{source.Session}/{kind}");
            var missing = source.MissingOpensUtc.Where(t => cutoff - t >= TimeSpan.FromHours(1)).ToArray();
            var exact = source.SourceAuthority == "AuditedExactH1" &&
                        source.Provider == m5.ProviderId.Value && source.Instrument == m5.Symbol.Value;
            var sourcePrice = high ? source.High : source.Low;
            var extremes = high ? source.HighCandles : source.LowCandles;
            var reason = source.EndUtc > cutoff || source.AvailableAtUtc > cutoff || source.EvaluatedAtUtc > cutoff
                ? StructuralTargetReason.NotYetObservable
                : !exact || source.StartUtc.Offset != TimeSpan.Zero || source.EndUtc.Offset != TimeSpan.Zero ||
                  source.EvaluatedAtUtc.Offset != TimeSpan.Zero || source.StartUtc == default ||
                  source.StartUtc >= source.EndUtc || source.EvaluatedAtUtc < source.EndUtc ||
                  source.CoverageStatus == "InvalidSourceIntervals"
                    ? StructuralTargetReason.InsufficientEvidence
                    : missing.Length > 0 ? StructuralTargetReason.SourceGap
                    : !source.IsComplete || sourcePrice is null || extremes.Count == 0 ||
                      extremes.Any(c => c.OpenTimeUtc < source.StartUtc || c.CloseTimeUtc > source.EndUtc)
                        ? StructuralTargetReason.InsufficientEvidence : StructuralTargetReason.Eligible;
            var available = reason == StructuralTargetReason.Eligible;
            var session = new StructuralSessionEndpoint(source.Session, source.StrategyVersion, source.OperatingDate,
                source.StartUtc, source.EndUtc, source.EvaluatedAtUtc <= cutoff && source.EvaluatedAtUtc != default ? source.EvaluatedAtUtc : null,
                source.EvaluatedAtUtc <= cutoff ? source.CoverageStatus : "SourceSnapshotNotYetObservable",
                source.AvailabilityMeaning, source.EvaluatedAtUtc <= cutoff ? Array.AsReadOnly(missing) : [],
                available ? extremes : []);
            return Reference(close, direction, m5, closures, id, $"{source.Session}SessionExtreme",
                source.SourceTimeframe, source.EndUtc, available ? source.AvailableAtUtc : null,
                available ? sourcePrice : null, source.EndUtc, provenance, session: session, unavailable: reason)
                with
            { Provider = source.Provider, Instrument = source.Instrument };
        }
    }

    private static StructuralTargetReference Reference(M1RealignmentCloseReference close, M5FvgDirection direction,
        CandleSeries m5, IReadOnlyList<M5FvgClosure> closures, string id, string category, string timeframe,
        DateTimeOffset sourceTimestamp, DateTimeOffset? availableAt, decimal? price, DateTimeOffset interactionStart,
        string provenance, M5StructuralPivot? pivot = null, StructuralSessionEndpoint? session = null,
        StructuralTargetReason unavailable = StructuralTargetReason.Eligible)
    {
        var distance = direction == M5FvgDirection.Bullish ? price - close.Price : close.Price - price;
        var reason = distance switch
        {
            null => unavailable,
            < 0 => StructuralTargetReason.WrongSide,
            0 => StructuralTargetReason.EqualToReference,
            _ => StructuralTargetReason.Eligible
        };
        return new(id, category, m5.ProviderId.Value, m5.Symbol.Value, timeframe, TargetKind(direction), sourceTimestamp,
            availableAt, price, price is not null, distance is null ? null : distance > 0, distance, reason, provenance,
            price is { } level ? PriorInteraction(m5, closures, interactionStart, close.ObservedAtUtc, direction, level) : null,
            pivot, session);
    }

    private static StructuralTargetEvaluation Evaluate(M1RealignmentCloseReference close, M5FvgDirection direction,
        StructuralTargetProfile profile, string provenance, IReadOnlyList<M5StructuralSourceGap> gaps,
        IEnumerable<StructuralTargetReference> source, string? limitation = null)
    {
        var candidates = source.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var selected = candidates.Where(c => c.Reason == StructuralTargetReason.Eligible)
            .GroupBy(c => c.DistancePoints).MinBy(g => g.Key)?.ToArray() ?? [];
        var target = selected.FirstOrDefault();
        var outcome = selected.Length > 1 ? StructuralTargetOutcome.TiedTargetsIdentified :
            target is not null ? StructuralTargetOutcome.TargetIdentified :
            limitation is not null ? StructuralTargetOutcome.InsufficientEvidence :
            candidates.Length == 0 ? StructuralTargetOutcome.NoHistoricalReference :
            candidates.Any(c => c.EligibleAtCutoff) ? StructuralTargetOutcome.NoFavorableReference :
            StructuralTargetOutcome.ReferencesUnavailable;
        return new(close, direction, profile, provenance, gaps, Array.AsReadOnly(candidates),
            Array.AsReadOnly(selected.Select(c => c.Id).ToArray()), target?.Price, target?.DistancePoints,
            outcome, limitation ?? outcome.ToString());
    }

    private static M5StructuralPivotKind TargetKind(M5FvgDirection direction) =>
        direction == M5FvgDirection.Bullish ? M5StructuralPivotKind.High : M5StructuralPivotKind.Low;

    private static StructuralTargetInteractionEvidence PriorInteraction(CandleSeries m5, IReadOnlyList<M5FvgClosure> closures,
        DateTimeOffset start, DateTimeOffset cutoff, M5FvgDirection direction, decimal price)
    {
        var through = cutoff.AddTicks(-(cutoff.Ticks % M5Interval.Ticks));
        var previous = start;
        var gaps = new List<M5FvgSourceGap>();
        Candle? firstReach = null;
        foreach (var candle in m5.Candles.Where(c => c.OpenTimeUtc >= start && c.CloseTimeUtc <= through &&
                     c.CloseTimeUtc - c.OpenTimeUtc == M5Interval && c.OpenTimeUtc.Ticks % M5Interval.Ticks == 0))
        {
            AddGap(candle.OpenTimeUtc);
            previous = candle.CloseTimeUtc;
            if (direction == M5FvgDirection.Bullish ? candle.High >= price : candle.Low <= price)
                firstReach ??= candle;
        }
        AddGap(through);
        var status = firstReach is not null ? StructuralTargetInteraction.ReachedInClosedM5 :
            gaps.Any(g => g.ClosureEvidence is null) ? StructuralTargetInteraction.UnknownDueToSourceGap :
            through <= start || gaps.Count == 1 && gaps[0].StartUtc == start && gaps[0].EndUtc == through
                ? StructuralTargetInteraction.NoClosedM5Interval : StructuralTargetInteraction.NotReachedInCoveredM5;
        return new(status, start, through, firstReach, gaps.AsReadOnly());

        void AddGap(DateTimeOffset end)
        {
            if (previous >= end) return;
            var evidence = closures.FirstOrDefault(c => c.StartUtc == previous && c.EndUtc == end)?.EvidenceReference;
            gaps.Add(new(previous, end, previous + M5Interval, evidence));
        }
    }
}
