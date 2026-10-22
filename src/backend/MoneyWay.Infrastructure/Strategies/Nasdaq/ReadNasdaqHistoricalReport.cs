using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Reads persisted evidence only. Bounded memory, one concurrent read, no cache or replay.</summary>
public sealed class ReadNasdaqHistoricalReport : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<NasdaqHistoricalReportSummary> ExecuteAsync(string path, string? expectedSha256,
        CancellationToken cancellationToken = default, long? frameStep = null)
    {
        await gate.WaitAsync(cancellationToken);
        try { return await Task.Run(() => Read(path, expectedSha256, cancellationToken, frameStep), cancellationToken); }
        finally { gate.Release(); }
    }

    private static NasdaqHistoricalReportSummary Read(string path, string? expectedSha256, CancellationToken cancellationToken, long? frameStep)
    {
        // FileShare.Read prevents cooperating writers from changing the source during this read.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        using var json = new BoundedReportJsonReader(stream, cancellationToken);
        json.Token(JsonTokenType.StartObject);
        var headers = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var properties = new HashSet<string>(StringComparer.Ordinal);
        long frames = 0;
        DateTimeOffset? previous = null, first = null;
        NasdaqHistoricalFrameSummary? selected = null;
        long? firstSessionStep = null, lastSessionStep = null, previousSelectedStep = null, nextSelectedStep = null;
        long sessionFrameCount = 0;
        string? lastVerdict = null;
        bool framesSeen = false;
        bool? hasCanonical = null;
        while (json.Peek() == JsonTokenType.PropertyName)
        {
            var name = json.Token(JsonTokenType.PropertyName)!;
            Require(properties.Add(name) && properties.Count <= 32);
            if (name == "Frames")
            {
                Require(headers["SchemaVersion"].GetInt32() == 2);
                Require(hasCanonical.HasValue);
                var session = headers["Session"];
                Require(Id(session, "StrategyId") == "moneyway-nasdaq");
                var day = DateOnly.ParseExact(Text(session.GetProperty("TradingDay")), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                json.Token(JsonTokenType.StartArray);
                while (json.Peek() != JsonTokenType.EndArray)
                {
                    using var document = json.Value();
                    var frame = document.RootElement;
                    var step = frame.GetProperty("Step").GetInt64();
                    var at = frame.GetProperty("AsOfUtc").GetDateTimeOffset();
                    Require(step == ++frames && at.Offset == TimeSpan.Zero && (previous is null || at > previous));
                    first ??= at;
                    previous = at;
                    var projection = Frame(frame, step, at);
                    Require((projection.Verdict is not null) == hasCanonical);
                    lastVerdict = projection.Verdict;
                    if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).DateTime) == day)
                    {
                        firstSessionStep ??= step;
                        sessionFrameCount++;
                        if (frameStep is null || step == frameStep)
                        {
                            selected = projection;
                            previousSelectedStep = lastSessionStep;
                        }
                        else if (selected is not null && nextSelectedStep is null) nextSelectedStep = step;
                        lastSessionStep = step;
                    }
                }
                json.Token(JsonTokenType.EndArray);
                framesSeen = true;
            }
            else
            {
                using var document = json.Value();
                if (name == "Canonical")
                {
                    Require(document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Null);
                    hasCanonical = document.RootElement.ValueKind == JsonValueKind.Object;
                }
                if (name is "SchemaVersion" or "SessionId" or "Session" or "FrameCount" or "FinalVerdict"
                    or "ReplayStartUtc" or "ReplayEndUtc" or "InputDiagnostics" or "BindingDiagnostics")
                    headers.Add(name, document.RootElement.Clone());
            }
        }
        json.Token(JsonTokenType.EndObject);
        Require(json.Peek() is null && framesSeen && headers["SchemaVersion"].GetInt32() == 2);
        Require(frames == headers["FrameCount"].GetInt64());
        var finalVerdict = EnumValue<StrategyVerdict>(headers["FinalVerdict"]);
        Require(hasCanonical.HasValue && finalVerdict == lastVerdict);
        var start = Timestamp(headers["ReplayStartUtc"]);
        var end = Timestamp(headers["ReplayEndUtc"]);
        Require(start == first && end == previous);
        var digest = json.Digest();
        if (!string.IsNullOrWhiteSpace(expectedSha256))
            Require(expectedSha256.Length == 64 && string.Equals(expectedSha256, digest, StringComparison.OrdinalIgnoreCase));
        var identity = headers["Session"];
        return new(Text(headers["SessionId"]), Id(identity, "StrategyId"), Id(identity, "StrategyVersion"),
            Id(identity, "ProviderId"), Id(identity, "Symbol"),
            DateOnly.ParseExact(Text(identity.GetProperty("TradingDay")), "yyyy-MM-dd", CultureInfo.InvariantCulture), 2, frames, finalVerdict,
            start, end, digest, json.BytesRead,
            string.IsNullOrWhiteSpace(expectedSha256) ? "SchemaChronologyAndCountChecked" : "SchemaChronologyCountAndExpectedHashChecked",
            "NotRevalidated", Codes(headers["InputDiagnostics"]), Codes(headers["BindingDiagnostics"]),
            frameStep is null ? selected : null, selected,
            new(firstSessionStep, lastSessionStep, selected?.Step, previousSelectedStep, nextSelectedStep, sessionFrameCount));
    }

    private static NasdaqHistoricalFrameSummary Frame(JsonElement frame, long step, DateTimeOffset at)
    {
        var rules = new List<NasdaqHistoricalRuleSummary>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sequence = -1;
        foreach (var rule in Values(frame.GetProperty("Evaluations")))
        {
            var id = Id(rule, "RuleId");
            var next = rule.GetProperty("Sequence").GetInt32();
            Require(ids.Add(id) && rules.Count < 128 && next > sequence && rule.GetProperty("EvaluatedAtUtc").GetDateTimeOffset() == at);
            sequence = next;
            rules.Add(new(id, EnumValue<RuleDefinitionStatus>(rule.GetProperty("DefinitionStatus")) ?? throw new InvalidDataException(),
                EnumValue<RuleEvaluationResult>(rule.GetProperty("Result")) ?? throw new InvalidDataException(), next,
                rule.GetProperty("IsRequired").GetBoolean(), Text(rule.GetProperty("Reason")), at, OptionalText(rule, "EvidenceId")));
        }
        var diagnostic = frame.GetProperty("Diagnostic");
        var hasDiagnostic = diagnostic.ValueKind == JsonValueKind.Object;
        if (hasDiagnostic)
            Require(diagnostic.GetProperty("Step").GetInt64() == step && diagnostic.GetProperty("AsOfUtc").GetDateTimeOffset() == at);
        var missing = hasDiagnostic ? Values(diagnostic.GetProperty("MissingRequiredRuleIds")).Select(e => Text(e.GetProperty("Value"))).ToArray() : [];
        Require(missing.Length <= 128);
        var factual = frame.TryGetProperty("FactualEvaluation", out var value) ? value : default;
        var snapshotState = OptionalState(frame, "SnapshotState");
        var factualState = OptionalState(frame, "FactualEvaluationState");
        if (snapshotState == "Available")
            Require(frame.TryGetProperty("Snapshot", out var snapshot) && snapshot.ValueKind == JsonValueKind.Object);
        if (factualState == "Available") Require(factual.ValueKind == JsonValueKind.Object);
        return new(step, at, hasDiagnostic ? EnumValue<StrategyVerdict>(diagnostic.GetProperty("Verdict")) : null,
            hasDiagnostic ? OptionalText(diagnostic, "Reason") : null,
            hasDiagnostic && diagnostic.TryGetProperty("BlockingRuleId", out var blocking) && blocking.ValueKind == JsonValueKind.Object ? Text(blocking.GetProperty("Value")) : null,
            hasDiagnostic ? EnumValue<RuleEvaluationResult>(diagnostic.GetProperty("BlockingResult")) : null,
            hasDiagnostic ? diagnostic.GetProperty("HasCompleteRequiredCoverage").GetBoolean() : null, missing,
            snapshotState, OptionalText(frame, "SnapshotDiagnostic"), factualState,
            factual.ValueKind == JsonValueKind.Object && factual.TryGetProperty("RequiresHumanValidation", out var human) ? human.GetBoolean() : null, rules);
    }

    private static string? OptionalState(JsonElement element, string name) => OptionalText(element, name) is "Available" or "Unavailable" ? OptionalText(element, name) : null;
    private static DateTimeOffset? Timestamp(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetDateTimeOffset();
    private static string Id(JsonElement element, string name) => Text(element.GetProperty(name).GetProperty("Value"));
    private static string? OptionalText(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? Text(value) : null;
    private static string Text(JsonElement value)
    {
        var text = value.GetString();
        Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 4096);
        return text!;
    }
    private static JsonElement.ArrayEnumerator Values(JsonElement element) =>
        (element.ValueKind == JsonValueKind.Array ? element : element.GetProperty("$values")).EnumerateArray();
    private static string[] Codes(JsonElement element)
    {
        var codes = Values(element).Select(e => Text(e.GetProperty("Code"))).Distinct(StringComparer.Ordinal).Take(129).ToArray();
        Require(codes.Length <= 128);
        return codes;
    }
    private static string? EnumValue<T>(JsonElement value) where T : struct, Enum
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var parsed = (T)Enum.ToObject(typeof(T), value.GetInt32());
        Require(Enum.IsDefined(parsed));
        return parsed.ToString();
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Invalid or unsupported canonical report.");
    }
    public void Dispose() => gate.Dispose();
}
