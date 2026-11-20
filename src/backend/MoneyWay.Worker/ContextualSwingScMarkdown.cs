using System.Globalization;
using System.Text;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal static class ContextualSwingScMarkdown
{
    internal static string Render(ContextualSwingScReport report)
    {
        static string Time(DateTimeOffset? t) => t?.ToString("O", CultureInfo.InvariantCulture) ?? "unavailable";
        static string Cell(object? value) => value is null ? "unavailable" : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "unavailable").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        var b = new StringBuilder("# Contextual M5 structural reference / subsequent SC comparison\n\n");
        b.AppendLine($"{report.AlgorithmId} / {report.Version}. Exposed Development research, no winner, no operational direction or entry approval.\n");
        b.AppendLine($"Research range {Time(report.Tracking.RangeStartUtc)} through {Time(report.Tracking.CutoffUtc)}. H1 session references; M5 event and SC observations. Nominal documentary availability only.\n");
        b.AppendLine("## Event policies\n\n| Liquidity reference | Policy | Status | Event M5 open / close UTC | Prior strict count | Earlier penetration / intermediate return for E3 |\n| --- | --- | --- | --- | --- | --- |");
        foreach (var e in report.EventSelections) b.AppendLine($"| {Cell(e.LiquidityReference.Category)} {Cell(e.LiquidityReference.Price)} | {e.PolicyId} | {e.Status} | {Time(e.Event?.Candle.OpenTimeUtc)} / {Time(e.Event?.AvailableAtUtc)} | {Cell(e.Event?.PriorPenetrationCount)} | {Time(e.EarlierPenetration?.Candle.OpenTimeUtc)} / {Time(e.IntermediateReturn?.Candle.OpenTimeUtc)} |");
        b.AppendLine("\n## Grouped equal reported outcomes\n\nAll individual comparisons/variants and complete source evidence remain in JSON. Grouping is not equivalence of hypotheses or independent market samples.\n\n| Reference / policy | Family / variant identities | Structural native price | Reference availability / source opens UTC | Event close breaks reference | Outcome | First subsequent SC open / close UTC |\n| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var group in report.OutcomeGroups)
        {
            var rows = report.Comparisons.Where(r => group.ComparisonIds.Contains(r.Id)).ToArray(); var r = rows[0];
            b.AppendLine($"| {Cell(r.EventSelection.LiquidityReference.Category)} / {r.EventSelection.PolicyId} | {Cell(string.Join(", ", rows.Select(x => x.HypothesisId + (x.SwingVariantId is null ? "" : " / " + x.SwingVariantId))))} | {Cell(r.StructuralReference?.Price)} | {Time(r.StructuralReference?.AvailableAtUtc)} / {Cell(r.StructuralReference is null ? null : string.Join(", ", r.StructuralReference.Sources.Select(c => Time(c.OpenTimeUtc))))} | {Cell(r.EventCloseComparison?.IsConfirmed)} | {r.Status} | {Time(r.SubsequentSc?.Candle?.OpenTimeUtc)} / {Time(r.SubsequentSc?.AsOfUtc)} |");
        }
        b.AppendLine("\n## Counts and assumptions\n");
        foreach (var g in report.Comparisons.GroupBy(r => r.Status).OrderBy(g => g.Key, StringComparer.Ordinal)) b.AppendLine($"- {g.Key}: {g.Count()} combinations.");
        foreach (var assumption in report.Assumptions) b.AppendLine("\n- " + assumption);
        b.AppendLine("\nLiquidity penetration extreme, structural reference, continuation confirmation and subsequent reversal confirmation are separate evidence. Original continuation labels are not contextual SC ground truth; no accuracy or profitability inferred.\n\nCanonicalAdmissionPermitted=false; OperationalSignal=false. No ACTIVE/candidate mutation, canonical facts, Ready, orders, timeframe substitution or trading-window correction.");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
