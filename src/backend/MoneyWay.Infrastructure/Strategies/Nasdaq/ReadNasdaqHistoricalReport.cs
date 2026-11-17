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
                if (name == "Schema")
                {
                    Require(document.RootElement.ValueKind == JsonValueKind.String
                        && document.RootElement.GetString() == ReadNasdaqCanonicalPreEntryWindow.Schema);
                    stream.Position = 0;
                    return ReadNasdaqCanonicalPreEntryWindow.Read(stream, expectedSha256, cancellationToken, frameStep);
                }
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
            factual.ValueKind == JsonValueKind.Object && factual.TryGetProperty("RequiresHumanValidation", out var human) ? human.GetBoolean() : null, rules,
            TradeLifecycle(frame, rules, at, snapshotState));
    }

    private static NasdaqHistoricalTradeLifecycleSummary TradeLifecycle(JsonElement frame,
        IReadOnlyList<NasdaqHistoricalRuleSummary> rules, DateTimeOffset at, string? snapshotState)
    {
        if (!frame.TryGetProperty("TradeLifecycle", out var element))
        {
            var legacy = NasdaqHistoricalTradeLifecycleSummary.FromRules(rules);
            if (snapshotState != "Available") return legacy;
            return legacy with
            {
                EvidenceScope = "PublishedArtifactsWithoutDetails",
                Stages = legacy.Stages.Select(s => s.Stage is "Entry" or "StopLoss" or "Risk" or "TakeProfit"
                    ? s with { Status = "RecordedArtifactWithoutDetails", Reason = "An existing snapshot is recorded; regenerate the report with the trade projection to expose its values. No missing-input claim is made." }
                    : s.Stage == "DocumentedExit" ? s with { Status = "NotProjected", Reason = "Legacy factual execution details are not projected; inspect the original report or regenerate it." } : s).ToArray()
            };
        }
        var result = JsonSerializer.Deserialize<NasdaqHistoricalTradeLifecycleSummary>(element) ?? throw new InvalidDataException("Missing trade lifecycle.");
        var fallback = NasdaqHistoricalTradeLifecycleSummary.FromRules(rules);
        Require(result.Path == (snapshotState == "Available" ? "Standard" : fallback.Path)
            && result.EvidenceScope == "CanonicalFrameArtifacts" && result.FailureClassification == "inconclusive"
            && element.GetProperty("CompleteSupervisedTrade").ValueKind == JsonValueKind.False
            && result.Stages is { Count: 8 } && result.Stages.All(s => s is not null) && result.Stages.Select(s => s.Stage).SequenceEqual(fallback.Stages.Select(s => s.Stage))
            && result.Sources is { Count: <= 16 } && result.OmittedSources >= 0
            && result.FailedRuleIds is not null && result.FailedRuleIds.SequenceEqual(fallback.FailedRuleIds));
        foreach (var stage in result.Stages)
        {
            var original = fallback.Stages.Single(s => s.Stage == stage.Stage);
            Require(!string.IsNullOrWhiteSpace(stage.Reason) && stage.Reason.Length <= 4096
                && stage.RuleId == original.RuleId && stage.RuleResult == original.RuleResult
                && (stage.Status == original.Status || stage.Status == "RecordedArtifact" && snapshotState == "Available"
                    && stage.Stage is "Entry" or "StopLoss" or "Risk" or "TakeProfit" or "DocumentedExit"));
        }
        foreach (var source in result.Sources)
            Require(source is not null && source.ObservedAtUtc.Offset == TimeSpan.Zero && source.ObservedAtUtc <= at
                && !string.IsNullOrWhiteSpace(source.SourceReference) && source.SourceReference.Length <= 512
                && !string.IsNullOrWhiteSpace(source.InputType) && source.InputType.Length <= 256
                && source.SourceReferenceSha256 is { Length: 64 } && source.SourceReferenceSha256.All(char.IsAsciiHexDigit));
        Require(result.Entry is null || snapshotState == "Available" && result.Entry.EffectiveAtUtc.Offset == TimeSpan.Zero && result.Entry.EffectiveAtUtc <= at);
        Require(result.Exit is null || result.Entry is not null && result.Exit.EffectiveAtUtc.Offset == TimeSpan.Zero
            && result.Exit.EffectiveAtUtc >= result.Entry.EffectiveAtUtc && result.Exit.EffectiveAtUtc <= at);
        Require((result.Stages.Single(s => s.Stage == "Entry").Status == "RecordedArtifact") == (result.Entry is not null)
            && (result.Stages.Single(s => s.Stage == "DocumentedExit").Status == "RecordedArtifact") == (result.Exit is not null)
            && result.ExitScope is null or "Full" or "Partial" && result.Direction is null or "Buy" or "Sell");
        Require(snapshotState == "Available" || result.StopPrice is null && result.TakeProfitPrice is null && result.RiskRatio is null
            && result.Direction is null && result.AccountBasisAmount is null && result.ReportedMaximumLoss is null && result.RiskCurrency is null);
        return result;
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
