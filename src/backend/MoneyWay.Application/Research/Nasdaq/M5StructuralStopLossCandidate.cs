using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum M5StructuralPivotKind { High, Low }
public enum M5StructuralRelation { HigherHigh, LowerHigh, EqualHigh, HigherLow, LowerLow, EqualLow, InsufficientEvidence, Ambiguous }
public enum M5StructuralStopOutcome
{
    WaitingForM1Realignment, StructuralReferenceIdentified, InsufficientM5History,
    InsufficientConfirmedPivots, NoConfirmedHigherLow, NoConfirmedLowerHigh,
    AmbiguousStructuralReference, ReferenceNotProtective, MissingSourceEvidence, InsufficientEvidence
}

public sealed record M5StructuralPivotProfile(int LeftBars, int RightBars)
{
    public string Id => FormattableString.Invariant($"EXP-NQ-M5-SL-STRICT-{LeftBars}-{RightBars}-V1");
    public static IReadOnlyList<M5StructuralPivotProfile> Initial { get; } =
        Array.AsReadOnly(new M5StructuralPivotProfile[] { new(1, 1), new(2, 2), new(3, 3) });
}

public sealed record M5StructuralPivot(string Id, M5StructuralPivotKind Kind, decimal Price,
    DateTimeOffset ExtremeOpenUtc, DateTimeOffset ExtremeCloseUtc, DateTimeOffset ConfirmedAtUtc,
    string ProfileId, bool SimultaneousHighAndLow);

public sealed record M5StructuralClassification(M5StructuralPivot Pivot,
    IReadOnlyList<M5StructuralPivot> Previous, M5StructuralRelation Relation);

public sealed record M1RealignmentCloseReference(Candle Candle)
{
    public decimal Price => Candle.Close;
    public DateTimeOffset ObservedAtUtc => Candle.CloseTimeUtc;
}

public sealed record M5StructuralSourceGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

public sealed record M5StructuralStopObservation(M5FvgDirection Direction, M1RealignmentCloseReference CloseReference,
    M5StructuralPivotProfile Profile, string SourceProvenance, int ClosedM5Count, DateTimeOffset? ContiguousStartUtc,
    IReadOnlyList<M5StructuralSourceGap> SourceGaps, IReadOnlyList<M5StructuralClassification> ConfirmedSwings,
    IReadOnlyList<M5StructuralClassification> LatestCandidateSet, M5StructuralClassification? Selected,
    bool? OnProtectiveSide, decimal? DistancePoints, M5StructuralStopOutcome Outcome, string ReasonCode)
{
    public DateTimeOffset AsOfUtc => CloseReference.ObservedAtUtc;
    public TimeSpan? ConfirmationAge => AsOfUtc - Selected?.Pivot.ConfirmedAtUtc;
}

/// <summary>Evaluates geometric references at the observable close of the supplied M1 evidence.</summary>
public sealed class EvaluateM5StructuralStopLossCandidateUseCase
{
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);

    public M5StructuralStopObservation Execute(CandleSeries series, Candle realignment,
        M5FvgDirection direction, M5StructuralPivotProfile profile, string provenance)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(realignment);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.LeftBars <= 0 || profile.RightBars <= 0)
            throw new ArgumentException("Positive left and right M5 neighborhoods required.", nameof(profile));
        if (series.Timeframe != M5 || string.IsNullOrWhiteSpace(provenance) || !Enum.IsDefined(direction) ||
            realignment.Timeframe != M1 || realignment.ProviderId != series.ProviderId || realignment.Symbol != series.Symbol ||
            !Aligned(realignment, OneMinute) || (direction == M5FvgDirection.Bullish
                ? realignment.Close <= realignment.Open : realignment.Close >= realignment.Open))
            throw new ArgumentException("Original synchronized M5 and directional M1 evidence with provenance required.");

        var cutoff = realignment.CloseTimeUtc;
        var visible = series.Candles.TakeWhile(c => c.CloseTimeUtc <= cutoff).ToArray();
        var gaps = new List<M5StructuralSourceGap>();
        var swings = new List<M5StructuralClassification>();
        var segmentStart = 0;
        IReadOnlyList<M5StructuralClassification> latest = [];
        var reference = new M1RealignmentCloseReference(realignment);

        if (visible.Length == 0)
            return Result(M5StructuralStopOutcome.InsufficientM5History, "NoClosedM5Candles");
        if (visible.Any(c => !Aligned(c, FiveMinutes)))
            return Result(M5StructuralStopOutcome.MissingSourceEvidence, "InvalidClosedM5Interval");

        for (var index = 1; index < visible.Length; index++)
        {
            if (visible[index - 1].CloseTimeUtc == visible[index].OpenTimeUtc) continue;
            gaps.Add(new(visible[index - 1].CloseTimeUtc, visible[index].OpenTimeUtc));
            segmentStart = index;
        }

        var lastClose = visible[^1].CloseTimeUtc;
        var missingIntervals = (cutoff - lastClose).Ticks / FiveMinutes.Ticks;
        if (missingIntervals > 0)
        {
            gaps.Add(new(lastClose, lastClose.AddTicks(missingIntervals * FiveMinutes.Ticks)));
            segmentStart = visible.Length;
            return Result(M5StructuralStopOutcome.MissingSourceEvidence, "ExpectedM5CloseMissingAtCutoff");
        }
        if (visible.Length - segmentStart < (long)profile.LeftBars + profile.RightBars + 1)
            return gaps.Count > 0
                ? Result(M5StructuralStopOutcome.MissingSourceEvidence, "InsufficientContiguousM5AfterGap")
                : Result(M5StructuralStopOutcome.InsufficientM5History, "InsufficientClosedM5ForProfile");

        var pivots = new List<M5StructuralPivot>();
        foreach (var (extreme, confirmedAt, high, low) in StrictConfirmedPivots.Find(
                     visible[segmentStart..], profile.LeftBars, profile.RightBars))
        {
            if (high) pivots.Add(Pivot(M5StructuralPivotKind.High, extreme.High));
            if (low) pivots.Add(Pivot(M5StructuralPivotKind.Low, extreme.Low));

            M5StructuralPivot Pivot(M5StructuralPivotKind kind, decimal price) => new(
                FormattableString.Invariant($"{series.ProviderId.Value}/{series.Symbol.Value}/{profile.Id}/{kind}/{extreme.OpenTimeUtc:O}"),
                kind, price, extreme.OpenTimeUtc, extreme.CloseTimeUtc, confirmedAt, profile.Id, high && low);
        }
        swings.AddRange(Classify(pivots));
        var bullish = direction == M5FvgDirection.Bullish;
        var relevant = swings.Where(s => s.Pivot.Kind == (bullish ? M5StructuralPivotKind.Low : M5StructuralPivotKind.High)).ToArray();
        if (relevant.Length < 2)
            return Result(M5StructuralStopOutcome.InsufficientConfirmedPivots, "InsufficientEvidence_PrecedingSameSidePivot");

        latest = Latest(relevant, bullish ? M5StructuralRelation.HigherLow : M5StructuralRelation.LowerHigh);
        if (latest.Count == 0)
            return Result(bullish ? M5StructuralStopOutcome.NoConfirmedHigherLow : M5StructuralStopOutcome.NoConfirmedLowerHigh,
                bullish ? "NoStrictHigherLowInContiguousM5" : "NoStrictLowerHighInContiguousM5");
        if (latest.Count > 1 || latest[0].Relation == M5StructuralRelation.Ambiguous)
            return Result(M5StructuralStopOutcome.AmbiguousStructuralReference, "SameSideConfirmationOrderingAmbiguous");

        var selected = latest[0];
        var price = selected.Pivot.Price;
        var protective = bullish ? price < reference.Price : price > reference.Price;
        return protective
            ? Result(M5StructuralStopOutcome.StructuralReferenceIdentified, "LatestConfirmedStructuralReference",
                selected, true, bullish ? reference.Price - price : price - reference.Price)
            : Result(M5StructuralStopOutcome.ReferenceNotProtective, "LatestReferenceEqualToOrBeyondM1Close", selected, false);

        M5StructuralStopObservation Result(M5StructuralStopOutcome outcome, string reason,
            M5StructuralClassification? selected = null, bool? protective = null, decimal? distance = null) =>
            new(direction, reference, profile, provenance, visible.Length,
                segmentStart < visible.Length ? visible[segmentStart].OpenTimeUtc : null,
                gaps.AsReadOnly(), swings.AsReadOnly(), latest, selected, protective, distance, outcome, reason);
    }

    internal static IReadOnlyList<M5StructuralClassification> Classify(IEnumerable<M5StructuralPivot> pivots)
    {
        var results = new List<M5StructuralClassification>();
        foreach (var side in pivots.GroupBy(p => p.Kind))
        {
            IReadOnlyList<M5StructuralPivot> previous = [];
            foreach (var confirmation in side.GroupBy(p => p.ConfirmedAtUtc).OrderBy(g => g.Key))
            {
                var current = Array.AsReadOnly(confirmation.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray());
                foreach (var pivot in current)
                    results.Add(new(pivot, previous, current.Count > 1 || previous.Count > 1
                        ? M5StructuralRelation.Ambiguous
                        : previous.Count == 0 ? M5StructuralRelation.InsufficientEvidence : Relation(pivot, previous[0])));
                previous = current;
            }
        }
        return Array.AsReadOnly(results.OrderBy(c => c.Pivot.ConfirmedAtUtc).ThenBy(c => c.Pivot.Kind)
            .ThenBy(c => c.Pivot.Id, StringComparer.Ordinal).ToArray());
    }

    internal static IReadOnlyList<M5StructuralClassification> Latest(IEnumerable<M5StructuralClassification> observations,
        M5StructuralRelation target) => Array.AsReadOnly(observations
        .Where(s => s.Relation == target || s.Relation == M5StructuralRelation.Ambiguous)
        .GroupBy(s => s.Pivot.ConfirmedAtUtc).MaxBy(g => g.Key)?.ToArray() ?? []);

    private static M5StructuralRelation Relation(M5StructuralPivot current, M5StructuralPivot previous) =>
        (current.Kind, current.Price.CompareTo(previous.Price)) switch
        {
            (M5StructuralPivotKind.High, > 0) => M5StructuralRelation.HigherHigh,
            (M5StructuralPivotKind.High, < 0) => M5StructuralRelation.LowerHigh,
            (M5StructuralPivotKind.High, _) => M5StructuralRelation.EqualHigh,
            (_, > 0) => M5StructuralRelation.HigherLow,
            (_, < 0) => M5StructuralRelation.LowerLow,
            _ => M5StructuralRelation.EqualLow
        };

    private static bool Aligned(Candle candle, TimeSpan period) =>
        candle.CloseTimeUtc - candle.OpenTimeUtc == period && candle.OpenTimeUtc.Ticks % period.Ticks == 0;
}
