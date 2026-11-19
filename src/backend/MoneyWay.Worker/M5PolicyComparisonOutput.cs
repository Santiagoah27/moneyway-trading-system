using System.Globalization;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal sealed record M5PolicyHumanAgreement(string AlgorithmId, string AlgorithmVersion, bool? PriceAgreement,
    bool? CandleIdentityAgreement, bool? ExactReferenceAgreement, decimal? AlgorithmMinusHumanPrice,
    decimal? AbsolutePriceDifference, DateTimeOffset? AlgorithmAvailabilityUtc)
{
    public DateTimeOffset? IndependentHumanConfirmationUtc => null;
    public TimeSpan? HumanConfirmationLatency => null;
}
internal sealed record M5PolicyHumanEvidence(string EvidenceFileSha256, string OriginalExportSha256, string EpisodeId,
    string DatasetId, int DatasetVersion, string InputSha256, string OriginalExclusiveReportSha256,
    int OriginalLabelRevision, string OriginalLabelClassification, string OriginalExposure, string Extreme,
    decimal Price, IReadOnlyList<DateTimeOffset> ReferenceCandleOpensUtc, DateTimeOffset ReviewCutoffUtc,
    IReadOnlyList<M5PolicyHumanAgreement> Agreements)
{
    public string Scope => "UnauthenticatedRetrospectiveHumanResearchComparison_NotAlgorithmInput";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

internal static class M5PolicyComparisonOutput
{
    internal static M5PolicyHumanEvidence Bind(JsonElement projection, string evidenceHash, string inputHash,
        string legacyReportHash, M5PairedPolicyReport report)
    {
        static string Text(JsonElement e, string k) => e.GetProperty(k).GetString() ?? throw new ArgumentException("Human research metadata required.");
        if (projection.GetProperty("schemaVersion").GetInt32() != 1 || projection.GetProperty("canonicalAdmissionPermitted").GetBoolean() || projection.GetProperty("operationalSignal").GetBoolean()) throw new ArgumentException("Only isolated original human research evidence is supported.");
        var context = report.ExclusiveV1.Input;
        var matches = projection.GetProperty("reviews").EnumerateArray().Where(r => Text(r, "episodeId") == context.SeriesId).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Exactly one original human label for this episode is required.");
        var r = matches[0]; var label = r.GetProperty("label");
        var cutoff = label.GetProperty("cutoffUtc").GetDateTimeOffset();
        if (Text(r, "inputSha256") != inputHash || Text(r, "reportSha256") != legacyReportHash || cutoff != context.EvaluationCutoffUtc || cutoff.Offset != TimeSpan.Zero)
            throw new ArgumentException("Original human input/report hash or evaluation cutoff mismatch; no earlier-frame authorization.");
        if (r.GetProperty("originalRevealRevision").GetInt32() != 1 || label.GetProperty("revision").GetInt32() != 1 || Text(r, "originalExposure") != "SelfDeclaredIndependent" || Text(label, "classification") != "IndependentRetrospectiveResearch" || Text(label, "kind") != "Reference"
            || label.GetProperty("canonicalAdmissionPermitted").GetBoolean() || label.GetProperty("operationalSignal").GetBoolean()
            || label.GetProperty("confirmationOpenUtc").ValueKind != JsonValueKind.Null || label.GetProperty("confirmationCloseUtc").ValueKind != JsonValueKind.Null)
            throw new ArgumentException("Original revision/exposure/scope or unsupported human-confirmation contract.");
        var expectedExtreme = context.Direction == M5ImpulseDirection.Ascending ? "Low" : "High";
        if (Text(label, "extreme") != expectedExtreme) throw new ArgumentException("Human structural extreme does not match the source impulse direction.");
        var price = label.GetProperty("price").GetDecimal();
        var opens = label.GetProperty("selectedOpens").EnumerateArray().Select(t => t.GetDateTimeOffset()).Order().ToArray();
        if (!opens.Any() || opens.Distinct().Count() != opens.Length || opens.Any(t => t.Offset != TimeSpan.Zero)) throw new ArgumentException("Exact original human candle identities required.");
        foreach (var open in opens)
        {
            var candle = report.ExclusiveV1.EvidenceCandles.SingleOrDefault(c => c.OpenTimeUtc == open)
                ?? throw new ArgumentException("Human reference candle is not visible at the cutoff.");
            if ((expectedExtreme == "Low" ? candle.Low : candle.High) != price) throw new ArgumentException("Human reference price differs from original native OHLC.");
        }
        var agreements = report.ExclusiveV1.Results.Concat(report.InclusiveV2.Results).Select(result =>
        {
            var comparable = result.IsExperimentalReferenceConfirmed;
            var priceSame = result.ProposedReferencePrice == price;
            var identitiesSame = result.ReferenceCandles.Select(c => c.OpenTimeUtc).Order().SequenceEqual(opens);
            decimal? delta = comparable ? result.ProposedReferencePrice!.Value - price : null;
            return new M5PolicyHumanAgreement(result.Algorithm.AlgorithmId, result.Algorithm.AlgorithmVersion,
                comparable ? priceSame : null, comparable ? identitiesSame : null, comparable ? priceSame && identitiesSame : null,
                delta, delta is { } d ? Math.Abs(d) : null, result.ConfirmationAtUtc);
        }).ToArray();
        var originalHash = Text(projection, "sourceSha256");
        if (originalHash.Length != 64 || !originalHash.All(Uri.IsHexDigit)) throw new ArgumentException("Original evidence byte identity required; this is not authentication.");
        return new(evidenceHash, originalHash, context.SeriesId, Text(r, "datasetId"), r.GetProperty("datasetVersion").GetInt32(), inputHash, legacyReportHash,
            1, Text(label, "classification"), Text(r, "originalExposure"), expectedExtreme, price, Array.AsReadOnly(opens), cutoff, Array.AsReadOnly(agreements));
    }

    internal static string Render(M5PairedPolicyReport report, M5PolicyHumanEvidence? human)
    {
        static string Cell(object? value) => value is null ? "—" : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace("<", "&lt;").Replace(">", "&gt;").Replace("[", "\\[").Replace("]", "\\]").Replace("`", "\\`").Replace('*', ' ').Replace('_', ' ').Replace('\r', ' ').Replace('\n', ' ');
        static string Time(DateTimeOffset? t) => t?.ToString("O", CultureInfo.InvariantCulture) ?? "—";
        var b = new StringBuilder("# Experimental M5 exclusive V1 / inclusive V2 comparison\n\n");
        b.AppendLine("**MoneyWay research hypotheses, not mentor-confirmed rules or operational signals. No winner.**\n");
        b.AppendLine($"Series {Cell(report.ExclusiveV1.Input.SeriesId)} · {Cell(report.ExclusiveV1.Input.ProviderId)}/{Cell(report.ExclusiveV1.Input.Symbol)} · {Cell(report.ExclusiveV1.Input.DataOrigin)} · cutoff {Time(report.ExclusiveV1.Input.EvaluationCutoffUtc)}.\n");
        b.AppendLine("Only the confirmation-candle aggregation differs. Strict-close threshold, start hypotheses, fixed anchor and cutoff are identical. Inclusion requires an already established pullback; no intrabar order is inferred. NativeInstrumentPrice only. Documentary availability remains unauthenticated.\n");
        b.AppendLine("| Algorithm / version | Policy | State | Start open UTC | Reference | All reference opens UTC | Confirmation open UTC | Known at close UTC |\n| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var row in report.ExclusiveV1.Results.Concat(report.InclusiveV2.Results))
            b.AppendLine($"| {row.Algorithm.AlgorithmId} / {row.Algorithm.AlgorithmVersion} | {row.SharedPolicyId} | {row.Status} | {Time(row.PullbackStart?.OpenTimeUtc)} | {Cell(row.ProposedReferencePrice)} | {Cell(string.Join(", ", row.ReferenceCandles.Select(c => Time(c.OpenTimeUtc))))} | {Time(row.ContinuationCandle?.OpenTimeUtc)} | {Time(row.ConfirmationAtUtc)} |");
        b.AppendLine("\n| Pair | Confirmed-comparable | Reference changed (price or identities) | Price changed | V2 minus V1 | Absolute difference |\n| --- | --- | --- | --- | --- | --- |");
        foreach (var pair in report.Differences) b.AppendLine($"| {pair.ExclusiveAlgorithmId} / {pair.InclusiveAlgorithmId} | {pair.ComparableConfirmedReferences} | {Cell(pair.ReferenceChanged)} | {Cell(pair.PriceChanged)} | {Cell(pair.SignedPriceDifference)} | {Cell(pair.AbsolutePriceDifference)} |");
        foreach (var row in report.ExclusiveV1.Results.Concat(report.InclusiveV2.Results)) b.AppendLine($"\n- {row.Algorithm.AlgorithmId}: {Cell(row.Reason)}");
        if (human is not null)
        {
            b.AppendLine($"\n## Original retrospective human reference\n\nRevision {human.OriginalLabelRevision} · {Cell(human.OriginalLabelClassification)} · {Cell(human.OriginalExposure)} · {human.Extreme} {Cell(human.Price)} · cutoff {Time(human.ReviewCutoffUtc)}. Source export SHA256: {human.OriginalExportSha256}. Not an algorithm input or historical authorization.\n");
            b.AppendLine("| Algorithm | Exact reference agreement | Price agreement | Candle-set agreement | Algorithm minus human | Absolute difference |\n| --- | --- | --- | --- | --- | --- |");
            foreach (var a in human.Agreements) b.AppendLine($"| {a.AlgorithmId} | {Cell(a.ExactReferenceAgreement)} | {Cell(a.PriceAgreement)} | {Cell(a.CandleIdentityAgreement)} | {Cell(a.AlgorithmMinusHumanPrice)} | {Cell(a.AbsolutePriceDifference)} |");
            b.AppendLine("\nIndependent human confirmation and relative latency: unavailable. This is exposed Development evidence, not independent validation or profitability.\n");
        }
        else b.AppendLine("\nNo human reference supplied. Synthetic data is a software fixture, excluded from authentic Development agreement statistics.\n");
        b.AppendLine("CanonicalAdmissionPermitted=false · OperationalSignal=false. No contextual structural relevance, strategy activation, orders, profitability or winner inferred.");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
