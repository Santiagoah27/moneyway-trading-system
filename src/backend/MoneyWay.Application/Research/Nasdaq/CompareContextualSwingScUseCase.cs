using System.Text.Json;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

/// <summary>Pure composition of existing research outputs and strict-close primitives; never a canonical evaluator.</summary>
public sealed class CompareContextualSwingScUseCase
{
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    public const string Previous = "EXP-NQ-CONTEXT-SWING-PREVIOUS-V1";
    public const string Range = "EXP-NQ-CONTEXT-SWING-RANGE-V1";
    public const string Confirmed = "EXP-NQ-CONTEXT-SWING-CONFIRMED-V1";
    public static IReadOnlyList<string> EventPolicies { get; } = Array.AsReadOnly(new[] { "E1", "E2", "E3" });
    public static IReadOnlyList<string> SwingFamilies { get; } = Array.AsReadOnly(new[] { Previous, Range, Confirmed });
    public static string EventPolicyId(string policy) => $"EXP-NQ-CONTEXT-EVENT-{policy}-V1";
    private static DateTimeOffset[] Missing(IReadOnlyList<Candle> rows, DateTimeOffset start, DateTimeOffset end)
    {
        var expected = new List<DateTimeOffset>();
        for (var t = start; t.AddMinutes(5) <= end; t = t.AddMinutes(5)) expected.Add(t);
        return expected.Except(rows.Where(c => c.CloseTimeUtc == c.OpenTimeUtc.AddMinutes(5)).Select(c => c.OpenTimeUtc)).ToArray();
    }
    private static DateTimeOffset EligibleStart(ContextualLiquidityTrackingResult tracking, ContextualLiquidityReference reference)
    {
        var start = tracking.RangeStartUtc;
        while (start < reference.AvailableAtUtc) start = start.AddMinutes(5);
        return start;
    }
    private static ContextualScEventSelection Select(ContextualLiquidityTrackingResult tracking, ContextualReferenceTracking reference, string policy)
    {
        var id = EventPolicyId(policy);
        var rows = reference.Observations;
        ContextualLiquidityObservation? candidate = null, earlier = null, returned = null;
        foreach (var o in rows.Where(o => o.Relationship.StrictPenetration && (policy == "E1" || o.Candle.OpenTimeUtc >= tracking.ObservationStartUtc)))
        {
            if (policy == "E3")
            {
                var ret = rows.FirstOrDefault(r => r.Id == o.PriorLaterOtherSideCloseId && r.CloseOnOtherSide && r.Candle.CloseTimeUtc <= o.Candle.OpenTimeUtc);
                var pen = ret is null ? null : rows.FirstOrDefault(p => p.Id == ret.PriorPenetrationId && p.Relationship.StrictPenetration && p.Candle.CloseTimeUtc <= ret.Candle.OpenTimeUtc);
                if (pen is null || ret is null || pen.Candle.OpenTimeUtc >= ret.Candle.OpenTimeUtc || ret.Candle.OpenTimeUtc >= o.Candle.OpenTimeUtc) continue;
                earlier = pen; returned = ret;
            }
            candidate = o; break;
        }
        var start = EligibleStart(tracking, reference.Reference);
        if (policy == "E2" && start < tracking.ObservationStartUtc) start = tracking.ObservationStartUtc;
        var end = candidate?.Candle.CloseTimeUtc ?? tracking.CutoffUtc;
        var missing = reference.MissingClosedM5OpensUtc.Where(t => t >= start && t.AddMinutes(5) <= end).ToArray();
        var status = missing.Length > 0 ? "AbstainedEventCoverage" : candidate is null ? "NoQualifyingEventObserved" : "Selected";
        return new(id, "1.0.0", reference.Reference, status,
            status == "Selected" ? "First qualifying closed event under this explicit policy; prior history retained, no take approval."
                : status == "AbstainedEventCoverage" ? "Incomplete required search/prefix coverage cannot verify a first qualifying event." : "No qualifying event is observed in the covered range at this cutoff.",
            status == "Selected" ? candidate : null, candidate, earlier, returned, Array.AsReadOnly(missing), reference.ObservationCoverage);
    }

    public ContextualSwingScReport Execute(CandleSeries h1, CandleSeries m5, DateOnly day, DateTimeOffset startUtc, DateTimeOffset cutoffUtc,
        M5ResearchDataOrigin origin, string provenance, IReadOnlyList<ContextualStructuralReference>? structural = null)
    {
        var tracking = new TrackContextualLiquidityUseCase().Execute(h1, m5, day, startUtc, cutoffUtc, origin, provenance, structural);
        var visible = m5.Candles.Where(c => c.CloseTimeUtc <= cutoffUtc && c.CloseTimeUtc == c.OpenTimeUtc.AddMinutes(5)
            && c.OpenTimeUtc.Ticks % TimeSpan.FromMinutes(5).Ticks == 0).ToArray();
        var events = tracking.References.SelectMany(r => EventPolicies.Select(p => Select(tracking, r, p))).ToArray();
        var results = new List<ContextualSwingScComparison>(); var breaks = new StructuralBodyCloseBreakCalculator();
        foreach (var selected in events)
        {
            var direction = selected.LiquidityReference.HighSide ? StructuralBreakDirection.Lower : StructuralBreakDirection.Upper;
            var upward = direction == StructuralBreakDirection.Upper;
            M5SwingHypothesisResult[]? detector = null;
            if (selected.Event is { } ev)
            {
                // Snapshot frozen at event OPEN, including only fully closed, exactly eligible prior M5.
                var prefix = new CandleSeries(m5.ProviderId, m5.Symbol, M5, visible.Where(c => c.OpenTimeUtc >= tracking.PreparationUtc && c.CloseTimeUtc <= ev.Candle.OpenTimeUtc));
                var anchor = prefix.Candles.SingleOrDefault(c => c.OpenTimeUtc == tracking.PreparationUtc && c.CloseTimeUtc == tracking.PreparationUtc.AddMinutes(5));
                var init = anchor is null ? null : new M5ImpulseInitialization("EXP-NQ-CONTEXT-SWING-CONFIRMED-V1/preparation-anchor", m5.ProviderId, m5.Symbol, M5,
                    anchor.OpenTimeUtc, anchor.CloseTimeUtc, "Explicit experimental 08:00 anchor, not a mentor-selected structural seed.");
                detector = new CompareM5SwingHypothesesUseCase().ExecuteInclusive(new("contextual-confirmed-prefix", prefix,
                    upward ? M5ImpulseDirection.Descending : M5ImpulseDirection.Ascending, ev.Candle.OpenTimeUtc, init, origin, provenance)).Results.ToArray();
            }
            foreach (var family in SwingFamilies)
                foreach (var variant in family == Confirmed ? CompareM5SwingHypothesesUseCase.InclusiveAlgorithms.Select(a => (M5SwingHypothesisDefinition?)a) : new M5SwingHypothesisDefinition?[] { null })
                {
                    var actualDetector = family == Confirmed && detector is not null ? detector.Single(d => d.Algorithm.AlgorithmId == variant!.AlgorithmId) : null;
                    var id = selected.LiquidityReference.Id + "/" + selected.PolicyId + "/" + family + (variant is null ? "" : "/" + variant.AlgorithmId);
                    ContextualStructuralSelection? reference = null;
                    ContextualSwingScComparison Row(string status, string reason, IReadOnlyList<DateTimeOffset>? missingStructural = null, IReadOnlyList<DateTimeOffset>? missingSc = null,
                        StructuralBodyCloseBreakResult? stale = null, StructuralBodyCloseBreakResult? eventComparison = null, StructuralBodyCloseBreakResult? sc = null, StructuralBodyCloseBreakResult? seen = null) =>
                        new(id, selected, family, "1.0.0", variant?.AlgorithmId, variant?.AlgorithmVersion, upward ? "UpwardResearchBreak" : "DownwardResearchBreak",
                            status, reason, reference, actualDetector, stale, eventComparison, sc, seen, missingStructural ?? [], missingSc ?? []);
                    if (selected.Event is null) { results.Add(Row(selected.Status, selected.Reason)); continue; }
                    var e = selected.Event; var open = e.Candle.OpenTimeUtc;
                    var before = visible.Where(c => c.CloseTimeUtc <= open).ToArray();
                    var formation = new List<Candle>(); DateTimeOffset available;
                    if (family == Previous)
                    {
                        var previous = before.SingleOrDefault(c => c.OpenTimeUtc == open.AddMinutes(-5) && c.CloseTimeUtc == open);
                        if (previous is null) { results.Add(Row("AbstainedPreviousIntervalMissing", "The immediately preceding exact closed M5 interval is unavailable.")); continue; }
                        formation.Add(previous); available = previous.CloseTimeUtc;
                    }
                    else if (family == Range)
                    {
                        if (open <= tracking.PreparationUtc) { results.Add(Row("AbstainedPreparationRangeUnavailable", "No nonempty closed preparation range exists before this event.")); continue; }
                        var missing = Missing(before, tracking.PreparationUtc, open);
                        if (missing.Length > 0) { results.Add(Row("AbstainedPreparationCoverage", "Preparation range must be continuous; no event/future candle is substituted.", missing)); continue; }
                        formation.AddRange(before.Where(c => c.OpenTimeUtc >= tracking.PreparationUtc)); available = open;
                    }
                    else
                    {
                        var missing = open <= tracking.PreparationUtc ? [] : Missing(before, tracking.PreparationUtc, open);
                        if (missing.Length > 0) { results.Add(Row("AbstainedConfirmedStructureCoverage", "The bounded preparation episode has missing exact M5 intervals; detector output retained without promotion.", missing)); continue; }
                        if (actualDetector is not { IsExperimentalReferenceConfirmed: true, ProposedReferencePrice: not null, ConfirmationAtUtc: not null })
                        { results.Add(Row("AbstainedConfirmedStructure", "The unchanged V2 bounded detector has not established a confirmed, pre-event structural reference: " + actualDetector?.Status)); continue; }
                        formation.AddRange(actualDetector.ReferenceCandles); available = actualDetector.ConfirmationAtUtc.Value;
                    }
                    if (available > open) { results.Add(Row("AbstainedReferenceUnavailable", "Structural reference was not known at event open.")); continue; }
                    decimal price;
                    if (family == Confirmed) price = actualDetector!.ProposedReferencePrice!.Value;
                    else
                    {
                        price = upward ? formation[0].High : formation[0].Low;
                        foreach (var c in formation.Skip(1)) price = new StructuralCandidateExtremeCalculator().Evaluate(price, upward ? c.High : c.Low,
                            upward ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower).ResultingExtreme;
                    }
                    var owners = formation.Where(c => (upward ? c.High : c.Low) == price).ToArray();
                    reference = new(price, upward ? "High" : "Low", Array.AsReadOnly(owners), owners.Max(c => c.CloseTimeUtc), available, open);
                    var stale = before.Where(c => c.OpenTimeUtc >= available).Select(c => breaks.Evaluate(c, price, direction)).FirstOrDefault(r => r.IsConfirmed);
                    var same = breaks.Evaluate(e.Candle, price, direction);
                    if (stale is not null) { results.Add(Row("AbstainedStaleReference", "A strict closed break already exists after reference availability and before the event; no replacement.", stale: stale, eventComparison: same)); continue; }
                    var preMissing = Missing(before, available, open);
                    if (preMissing.Length > 0) { results.Add(Row("AbstainedStalenessCoverage", "Missing pre-event coverage cannot verify that the reference is unbroken.", preMissing, eventComparison: same)); continue; }
                    if (same.IsConfirmed) { results.Add(Row("AbstainedSameCandleBreak", "The event candle itself closes beyond the reference. Intrabar sequencing unresolved; later continuation is not a new SC.", eventComparison: same)); continue; }
                    var subsequent = visible.Where(c => c.OpenTimeUtc >= e.Candle.CloseTimeUtc && c.CloseTimeUtc == c.OpenTimeUtc.AddMinutes(5)).ToArray();
                    var observed = subsequent.Select(c => breaks.Evaluate(c, price, direction)).FirstOrDefault(r => r.IsConfirmed);
                    var scanMissing = Missing(subsequent, e.Candle.CloseTimeUtc, observed?.AsOfUtc ?? cutoffUtc);
                    if (scanMissing.Length > 0) { results.Add(Row("AbstainedSubsequentCoverage", "Missing intervals prevent a verified first subsequent SC.", missingSc: scanMissing, eventComparison: same, seen: observed)); continue; }
                    results.Add(observed is null ? Row("NoQualifyingSCObserved", "No strict subsequent close is observed by cutoff; equality/wick-only do not confirm.", eventComparison: same)
                        : Row("ExperimentalScObserved", "First qualifying strict close on a later candle; research only, frozen reference, no operational authorization.", eventComparison: same, sc: observed, seen: observed));
                }
        }
        var groups = results.GroupBy(r => JsonSerializer.Serialize(new
        {
            r.EventSelection.PolicyId,
            Ref = r.EventSelection.LiquidityReference.Id,
            Event = r.EventSelection.Event?.Id,
            r.StructuralReference,
            r.Status,
            Stale = r.PreEventBreak?.Candle?.OpenTimeUtc,
            Same = r.EventCloseComparison?.IsConfirmed,
            Sc = r.SubsequentSc?.Candle?.OpenTimeUtc,
            Detector = r.BoundedDetector?.Status,
            Provisional = r.BoundedDetector?.ProposedReferencePrice,
            r.Reason
        }))
            .Select(g => new ContextualScOutcomeGroup(Array.AsReadOnly(g.Select(r => r.Id).ToArray()), g.First().Status)).ToArray();
        return new(tracking, Array.AsReadOnly(events), results.AsReadOnly(), Array.AsReadOnly(groups));
    }
}
