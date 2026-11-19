using System.Globalization;
using System.Text;

namespace MoneyWay.Application.Research.Nasdaq;

/// <summary>Human-readable projection of the experiment, never a strategy verdict calculation.</summary>
public static class M5SwingComparisonMarkdown
{
    public static string Render(M5SwingComparisonReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        static string Cell(object? value) => value is null ? "—" : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")
            .Replace("\\", "\\\\").Replace("|", "\\|").Replace("`", "\\`").Replace("[", "\\[").Replace("]", "\\]")
            .Replace("*", "\\*").Replace("_", "\\_").Replace("<", "&lt;").Replace(">", "&gt;").Replace('\r', ' ').Replace('\n', ' ');
        static string Time(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture) ?? "—";
        var b = new StringBuilder();
        b.AppendLine("# Experimental M5 swing comparison V1\n");
        b.AppendLine("**MoneyWay research hypotheses. Not mentor-confirmed rules, admitted data, operational signals or strategy verdicts. No winner selected.**\n");
        b.AppendLine($"Series: {Cell(report.Input.SeriesId)} · {Cell(report.Input.ProviderId)}/{Cell(report.Input.Symbol)} · {Cell(report.Input.Timeframe)} · {report.Input.Direction} · {report.Input.DataOrigin}\n");
        b.AppendLine($"Cutoff: {Time(report.Input.EvaluationCutoffUtc)}. Initialization: {Cell(report.Input.Initialization?.ReferenceId)} / {Time(report.Input.Initialization?.AnchorOpenTimeUtc)}; declared availability {Time(report.Input.Initialization?.AvailableAtUtc)}; fixed extreme {Cell(report.Input.InitialImpulsePrice)}.\n");
        b.AppendLine($"Provenance: {Cell(report.Input.Provenance)}. Historical availability unauthenticated.\n");
        b.AppendLine("## Shared experimental assumptions\n");
        foreach (var a in report.SharedAssumptions) b.AppendLine("- " + Cell(a));
        b.AppendLine("\n## Results\n\n| Algorithm/version | State | Pullback start (open UTC) | Proposed price | All reference candles (open UTC) | Confirmed at UTC | Anchor latency | Start latency |\n| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var r in report.Results)
        {
            b.AppendLine($"| {r.Algorithm.AlgorithmId}/{r.Algorithm.AlgorithmVersion} | {r.Status} | {Time(r.PullbackStart?.OpenTimeUtc)} | {Cell(r.ProposedReferencePrice)} | {Cell(string.Join(", ", r.ReferenceCandles.Select(c => Time(c.OpenTimeUtc))))} | {Time(r.ConfirmationAtUtc)} | {Cell(r.ConfirmationLatencyFromAnchor)} | {Cell(r.PullbackToConfirmationLatency)} |");
        }
        b.AppendLine("\nUnconfirmed proposed prices are provisional. Missing latency is not zero.\n");
        foreach (var r in report.Results) b.AppendLine($"- {r.Algorithm.AlgorithmId}: {Cell(r.Algorithm.StartCondition)}. {Cell(r.Reason)}");
        b.AppendLine("\n## Pairwise comparison\n\n| Pair | Full agreement | Confirmed reference/time agreement | Confirmation latency difference (left minus right) | Start latency difference |\n| --- | --- | --- | --- | --- |");
        foreach (var p in report.Comparisons)
        {
            b.AppendLine($"| {p.LeftAlgorithm} / {p.RightAlgorithm} | {p.FullAgreement} | {Cell(p.ConfirmedReferenceAgreement)} | {Cell(p.ConfirmationLatencyDifference)} | {Cell(p.PullbackLatencyDifference)} |");
        }
        b.AppendLine(); foreach (var p in report.Comparisons) b.AppendLine($"- {p.LeftAlgorithm} / {p.RightAlgorithm}: {Cell(p.Explanation)}");
        b.AppendLine("\n## Visible supplied evidence\n\nPrevious means the previous supplied closed candle. No missing bars were reconstructed. Rows after the first continuation are context only, not another cycle.\n\n| Open UTC | Close UTC | Open | High | Low | Close |\n| --- | --- | --- | --- | --- | --- |");
        foreach (var c in report.EvidenceCandles) b.AppendLine($"| {Time(c.OpenTimeUtc)} | {Time(c.CloseTimeUtc)} | {Cell(c.Open)} | {Cell(c.High)} | {Cell(c.Low)} | {Cell(c.Close)} |");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
