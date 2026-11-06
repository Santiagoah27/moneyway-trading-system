using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Explicit read-only projection of the bounded native diagnostic envelope, not a v2 harness report.
/// The whole source is limited to 8 MiB/128 frames; v2 retains its independent incremental reader.
/// Preserve references are resolved only backwards. No replay, verdict aggregation or evidence inference.</summary>
internal sealed class ReadNasdaqCanonicalPreEntryWindow
{
    internal const string Schema = "moneyway.canonical-preentry-window";
    private readonly Dictionary<string, JsonElement> references = new(StringComparer.Ordinal);
    private readonly CancellationToken cancellationToken;
    private ReadNasdaqCanonicalPreEntryWindow(CancellationToken token) => cancellationToken = token;

    internal static NasdaqHistoricalReportSummary Read(FileStream stream, string? expectedHash, CancellationToken token, long? selectedStep)
    {
        Require(stream.Length <= 8 * 1024 * 1024);
        token.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });
        var reader = new ReadNasdaqCanonicalPreEntryWindow(token);
        reader.Index(document.RootElement);
        stream.Position = 0;
        var digest = Convert.ToHexString(SHA256.HashData(stream));
        token.ThrowIfCancellationRequested();
        Require(string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length == 64 && string.Equals(expectedHash, digest, StringComparison.OrdinalIgnoreCase));
        return reader.Project(document.RootElement, digest, stream.Length, !string.IsNullOrWhiteSpace(expectedHash), selectedStep);
    }

    private void Index(JsonElement node)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (node.ValueKind == JsonValueKind.Object)
        {
            var properties = node.EnumerateObject().ToArray();
            Require(properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length);
            if (node.TryGetProperty("$ref", out var reference))
            {
                Require(properties.Length == 1 && references.ContainsKey(Text(reference)));
                return;
            }
            if (node.TryGetProperty("$id", out var id))
                Require(references.Count < 4096 && references.TryAdd(Text(id), node));
            foreach (var property in properties) Index(property.Value);
        }
        else if (node.ValueKind == JsonValueKind.Array)
            foreach (var item in node.EnumerateArray()) Index(item);
    }

    private JsonElement Resolve(JsonElement element) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty("$ref", out var reference)
        ? references[Text(reference)] : element;
    private JsonElement Property(JsonElement element, string name) => Resolve(Resolve(element).GetProperty(name));
    private JsonElement[] Values(JsonElement element)
    {
        element = Resolve(element);
        return (element.ValueKind == JsonValueKind.Array ? element : Property(element, "$values")).EnumerateArray().ToArray();
    }
    private string Id(JsonElement element, string name) => Text(Property(Property(element, name), "Value"));

    private NasdaqHistoricalReportSummary Project(JsonElement root, string hash, long bytes, bool expectedHash, long? requested)
    {
        Require(root.EnumerateObject().Count() <= 32 && Text(Property(root, "Schema")) == Schema && Property(root, "SchemaVersion").GetInt32() == 1);
        var session = Property(root, "Session");
        Require(Id(session, "StrategyId") == "moneyway-nasdaq");
        var day = DateOnly.ParseExact(Text(Property(session, "TradingDay")), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var prefixStart = Time(Property(root, "EvaluatedPrefixStartUtc"));
        var prefixEnd = Time(Property(root, "EvaluatedPrefixEndUtc"));
        var prefixCount = Property(root, "EvaluatedPrefixFrameCount").GetInt64();
        var start = Time(Property(root, "DiagnosticWindowStartUtc"));
        var end = Time(Property(root, "DiagnosticWindowEndUtc"));
        Require(prefixStart <= start && start <= end && end <= prefixEnd && prefixCount > 0);
        var frames = Values(Property(root, "Frames"));
        Require(frames.Length is > 0 and <= 128 && frames.Length <= prefixCount);
        var projections = new List<NasdaqHistoricalFrameSummary>();
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observation = Property(frame, "StrategyObservation");
            Require(new[] { "StrategyId", "StrategyVersion", "ProviderId", "Symbol" }.All(k => Id(observation, k) == Id(session, k)));
            var step = Property(observation, "Step").GetInt64();
            var at = Time(Property(observation, "AsOfUtc"));
            Require(step > 0 && step <= prefixCount && (projections.Count == 0 || step > projections[^1].Step && at > projections[^1].AsOfUtc)
                && at >= start && at <= end && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(at, "America/Bogota").DateTime) == day);
            var diagnostic = Property(frame, "CanonicalDiagnostic");
            Require(Property(diagnostic, "Step").GetInt64() == step && Time(Property(diagnostic, "AsOfUtc")) == at);
            var rules = new List<NasdaqHistoricalRuleSummary>();
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var evaluation in Values(Property(observation, "Evaluations")))
            {
                var id = Id(evaluation, "RuleId");
                var sequence = Property(evaluation, "Sequence").GetInt32();
                Require(rules.Count < 128 && ruleIds.Add(id) && sequence >= 0 && (rules.Count == 0 || sequence > rules[^1].Sequence)
                    && Time(Property(evaluation, "EvaluatedAtUtc")) == at);
                // Native EvidenceReference contains full bodies/local paths, not a persisted EvidenceId. Do not relabel it.
                rules.Add(new(id, EnumValue<RuleDefinitionStatus>(Property(evaluation, "DefinitionStatus"))!,
                    EnumValue<RuleEvaluationResult>(Property(evaluation, "Result"))!, sequence,
                    Property(evaluation, "IsRequired").GetBoolean(), Text(Property(evaluation, "Reason")), at, null));
            }
            Require(Property(observation, "EvaluationCount").GetInt32() == rules.Count);
            var missing = Values(Property(diagnostic, "MissingRequiredRuleIds")).Select(e => Text(Property(e, "Value"))).ToArray();
            Require(missing.Length <= 128 && missing.Distinct(StringComparer.Ordinal).Count() == missing.Length);
            var block = Property(diagnostic, "BlockingRuleId");
            var blockResult = EnumValue<RuleEvaluationResult>(Property(diagnostic, "BlockingResult"), allowNull: true);
            var blockId = block.ValueKind == JsonValueKind.Null ? null : Text(Property(block, "Value"));
            Require(blockId is null ? blockResult is null : rules.Any(r => r.RuleId == blockId && r.Result == blockResult));
            var snapshot = Property(frame, "Snapshot");
            var factual = Property(frame, "FactualEvaluation");
            Require(snapshot.ValueKind is JsonValueKind.Object or JsonValueKind.Null && factual.ValueKind is JsonValueKind.Object or JsonValueKind.Null);
            var snapshotDiagnostic = Property(frame, "SnapshotDiagnostic");
            projections.Add(new(step, at, EnumValue<StrategyVerdict>(Property(diagnostic, "Verdict"), allowNull: true),
                Text(Property(diagnostic, "Reason")), blockId, blockResult, Property(diagnostic, "HasCompleteRequiredCoverage").GetBoolean(), missing,
                snapshot.ValueKind == JsonValueKind.Null ? "Unavailable" : "Available",
                snapshotDiagnostic.ValueKind == JsonValueKind.String ? Text(snapshotDiagnostic) : null,
                factual.ValueKind == JsonValueKind.Null ? "Unavailable" : "Available",
                factual.ValueKind == JsonValueKind.Object && factual.TryGetProperty("RequiresHumanValidation", out var human) ? human.GetBoolean() : null, rules));
        }
        Require(projections[0].AsOfUtc == start && projections[^1].AsOfUtc == end);
        var selected = requested is null ? projections[^1] : projections.SingleOrDefault(f => f.Step == requested);
        var selectedIndex = selected is null ? -1 : projections.IndexOf(selected);
        // Only the current/earlier binding identifiers can accompany an earlier selected frame.
        var bindings = Values(Property(root, "BindingDiagnostics")).Where(b => selected is not null && Time(Property(b, "AsOfUtc")) <= selected.AsOfUtc).ToArray();
        var codes = bindings.Select(b => Text(Property(b, "Code"))).Distinct(StringComparer.Ordinal).ToArray();
        var records = bindings.Select(b => Text(Property(b, "RecordId"))).Distinct(StringComparer.Ordinal).ToArray();
        Require(codes.Length <= 128 && records.Length <= 128);
        return new(null, Id(session, "StrategyId"), Id(session, "StrategyVersion"), Id(session, "ProviderId"), Id(session, "Symbol"), day,
            1, projections.Count, null, start, end, hash, bytes,
            expectedHash ? "SchemaChronologyCountAndExpectedHashChecked" : "SchemaChronologyAndCountChecked", "NotRevalidated", [], codes,
            null, selected, new(projections[0].Step, projections[^1].Step, selected?.Step,
                selectedIndex > 0 ? projections[selectedIndex - 1].Step : null,
                selectedIndex >= 0 && selectedIndex + 1 < projections.Count ? projections[selectedIndex + 1].Step : null, projections.Count),
            "BoundedCanonicalDiagnostic", Schema, prefixCount, prefixStart, prefixEnd, Text(Property(root, "CanonicalRuntimeCommit")), records);
    }

    private static DateTimeOffset Time(JsonElement value)
    {
        var at = value.GetDateTimeOffset(); Require(at.Offset == TimeSpan.Zero); return at;
    }
    private static string Text(JsonElement value)
    {
        var text = value.GetString(); Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 4096); return text!;
    }
    private static string? EnumValue<T>(JsonElement value, bool allowNull = false) where T : struct, Enum
    {
        if (allowNull && value.ValueKind == JsonValueKind.Null) return null;
        var parsed = (T)Enum.ToObject(typeof(T), value.GetInt32()); Require(Enum.IsDefined(parsed)); return parsed.ToString();
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Invalid or unsupported bounded canonical diagnostic.");
    }
}
