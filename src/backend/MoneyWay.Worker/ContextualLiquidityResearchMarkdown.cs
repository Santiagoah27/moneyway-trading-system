using System.Globalization;
using System.Text;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal static class ContextualLiquidityResearchMarkdown
{
    internal static string Render(ContextualLiquidityTrackingResult result)
    {
        static string Number(decimal d) => d.ToString(CultureInfo.InvariantCulture);
        static string Time(DateTimeOffset? d) => d?.ToString("O", CultureInfo.InvariantCulture) ?? "unavailable";
        var b = new StringBuilder("# Nasdaq contextual liquidity tracking V1\n\n");
        b.AppendLine($"{result.AlgorithmId} / {result.AlgorithmVersion}. June 23 exposed Development only; no relevance ranking, direction, swing anchor, valid take or order inferred.\n");
        b.AppendLine($"Research interval [{Time(result.RangeStartUtc)}, {Time(result.CutoffUtc)}], not an operational end decision. Preparation {Time(result.PreparationUtc)}; observation start {Time(result.ObservationStartUtc)}. Availability is documentary, not authenticated.\n");
        b.AppendLine($"London historical H1 context: {result.LondonHistoricalContext.CompletedClassification}; upper {(result.LondonHistoricalContext.CompletedUpperMagnitude is { } upper ? Number(upper) : "unavailable")}, lower {(result.LondonHistoricalContext.CompletedLowerMagnitude is { } lower ? Number(lower) : "unavailable")}. Independent from M5 reference tracking.\n");
        b.AppendLine("| Reference | Native price | Available UTC | M5 coverage | First strict penetration interval UTC | Strict observations before open | At/after open |\n| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var t in result.References)
        {
            var first = t.FirstObservedPenetration;
            b.AppendLine($"| {t.Reference.Category} | {Number(t.Reference.Price)} | {Time(t.Reference.AvailableAtUtc)} | {t.ObservationCoverage} | {(first is null ? "not observed" : Time(first.Candle.OpenTimeUtc) + " / " + Time(first.Candle.CloseTimeUtc))} | {t.Observations.Count(o => o.Relationship.StrictPenetration && o.AvailableAtUtc < result.ObservationStartUtc)} | {t.PenetrationsAvailableAtOrAfterOpen} |");
        }
        b.AppendLine("\nSession endpoints retain separate stable identities and all source ties, including price coincidences. AH/AL are unavailable before Asia end; LH/LL before London end. Candles opening before reference availability are not retrospectively used. Formation interval and evidence availability phase are distinct. The candle ending exactly 08:30 is available at the boundary, not before it.\n");
        b.AppendLine("## London Low complete closed-M5 sequence\n\n| M5 open UTC | Close UTC | O / H / L / C | Interval phase / availability phase | Equality | Strict | Open beyond | Close beyond | Prior strict count | Prior pre-open strict | Later-other-side-close support |\n| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var o in result.References.Where(r => r.Reference.Category == "LL").SelectMany(r => r.Observations))
            b.AppendLine($"| {Time(o.Candle.OpenTimeUtc)} | {Time(o.Candle.CloseTimeUtc)} | {Number(o.Candle.Open)} / {Number(o.Candle.High)} / {Number(o.Candle.Low)} / {Number(o.Candle.Close)} | {o.IntervalPhase} / {o.AvailabilityPhase} | {o.Relationship.ExactExtremeContact} | {o.Relationship.StrictPenetration} | {o.Relationship.OpensBeyondReference} | {o.Relationship.CloseBeyond} | {o.PriorPenetrationCount} | {o.PriorPenetrationBeforeOpen} | {o.PriorLaterOtherSideCloseId is not null} |");
        b.AppendLine("\nAll references/rows, exact OHLC, IDs, prior-event links, missing slots, H1 context and same-candle ambiguity groups are in liquidity-context.json. Returning closes and further exceedances do not establish a new sweep or a crossing path. First means first observable supplied interval, not an event in missing history. No reset at 08:30.\n");
        b.AppendLine("CanonicalAdmissionPermitted=false; OperationalSignal=false. Replacement, invalidation, reuse and operational relevance remain unresolved. No independent validation sample count is inferred from multiple references in one Development episode.");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
