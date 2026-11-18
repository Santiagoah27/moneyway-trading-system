using System.Globalization;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
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
                var reviewSession = new NasdaqDemoSessionIdentity(new(Id(session, "StrategyId")), new(Id(session, "StrategyVersion")),
                    new(Id(session, "ProviderId")), new(Id(session, "Symbol")), day);
                var reviews = headers.TryGetValue("HistoricalEntryEvidence", out var reviewJson)
                    ? NasdaqHistoricalEntryReportProjection.Evidence(reviewJson, reviewSession) : [];
                var entryFrames = new List<NasdaqHistoricalEntryFrame>();
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
                    var projection = Frame(frame, step, at, Id(session, "ProviderId"), Id(session, "Symbol"));
                    if (reviews.Any(e => e.EarliestEventUtc == at && e.LatestEventUtc == at))
                        entryFrames.Add(NasdaqHistoricalEntryCausalSelector.Frame(projection));
                    var entryAssessment = new NasdaqHistoricalEntryCausalSelector().Select(reviewSession, at, reviews, entryFrames);
                    JsonElement? storedEntry = frame.TryGetProperty("TradeLifecycle", out var trade)
                        && trade.TryGetProperty("HistoricalEntry", out var entry) ? entry : null;
                    NasdaqHistoricalEntryReportProjection.Validate(storedEntry, entryAssessment);
                    if (projection.TradeLifecycle is { } lifecycle)
                    {
                        lifecycle = new NasdaqSupervisedHistoricalTradeSnapshotAssembler().Project(reviewSession, step, at, projection.Verdict,
                            projection.Rules, lifecycle with { HistoricalEntry = entryAssessment, SupervisedSnapshot = null });
                        NasdaqHistoricalEntryReportProjection.ValidateSnapshot(trade.ValueKind == JsonValueKind.Object
                            && trade.TryGetProperty("SupervisedSnapshot", out var storedSnapshot) ? storedSnapshot : null, lifecycle.SupervisedSnapshot);
                        projection = projection with { TradeLifecycle = lifecycle };
                    }
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
                    or "ReplayStartUtc" or "ReplayEndUtc" or "InputDiagnostics" or "BindingDiagnostics" or "HistoricalEntryEvidence")
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

    private static NasdaqHistoricalFrameSummary Frame(JsonElement frame, long step, DateTimeOffset at, string providerId, string symbol)
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
            TradeLifecycle(frame, rules, at, snapshotState, providerId, symbol));
    }

    private static NasdaqHistoricalTradeLifecycleSummary TradeLifecycle(JsonElement frame,
        IReadOnlyList<NasdaqHistoricalRuleSummary> rules, DateTimeOffset at, string? snapshotState, string providerId, string symbol)
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
                && (stage.Status == original.Status
                    || stage.Stage == "StopLoss" && stage.Status == "NotImplemented" && result.Path == "WickfillScenario2"
                        && stage.RuleResult != "Passed" && result.Scenario2StopLoss is null
                    || stage.Stage == "Risk" && stage.Status == "NotImplemented" && result.Path == "WickfillScenario2"
                        && stage.RuleResult != "Passed" && result.Scenario2RiskExposure is null
                    || stage.Stage == "TakeProfit" && stage.Status == "NotImplemented" && result.Path == "WickfillScenario2"
                        && stage.RuleResult != "Passed" && result.Scenario2TakeProfit is null
                    || stage.Status == "RecordedArtifact" && snapshotState == "Available"
                    && stage.Stage is "Entry" or "StopLoss" or "Risk" or "TakeProfit" or "DocumentedExit"));
        }
        var sources = result.Sources.AsEnumerable();
        if (result.Scenario2StopLoss is { } stop)
        {
            Require(result.Path == "WickfillScenario2" && rules.SingleOrDefault(r => r.RuleId == "NQ-SL-001")?.Result == "Passed"
                && !string.IsNullOrWhiteSpace(stop.ReferenceId) && stop.ReferenceId.Length <= 4096
                && stop.StopPrice > stop.ProtectivePrice && stop.EffectiveAtUtc.Offset == TimeSpan.Zero
                && stop.ObservedAtUtc.Offset == TimeSpan.Zero && stop.EffectiveAtUtc <= stop.ObservedAtUtc && stop.ObservedAtUtc <= at
                && stop.Source is not null && stop.ProtectiveSource is not null
                && stop.Source.ObservedAtUtc == stop.ObservedAtUtc && stop.ProtectiveSource.ObservedAtUtc <= stop.ObservedAtUtc);
            sources = sources.Concat([stop.Source!, stop.ProtectiveSource!]);
        }
        if (result.Scenario2RiskExposure is { } risk)
        {
            // Validate the published projection against its recorded rule, without running an evaluator on read.
            var riskResult = rules.SingleOrDefault(r => r.RuleId == "NQ-RISK-001")?.Result;
            Require(result.Path == "WickfillScenario2" && result.Scenario2StopLoss is { } selectedStop
                && risk.StopPrice == selectedStop.StopPrice && riskResult is "Passed" or "Failed"
                && risk.IsWithinLimit == (riskResult == "Passed") && risk.Limit == NasdaqRiskExposureRuleFact.MaximumRiskRatio
                && risk.Exposure is not null && risk.EffectiveAtUtc.Offset == TimeSpan.Zero
                && risk.ObservedAtUtc.Offset == TimeSpan.Zero && risk.EffectiveAtUtc >= selectedStop.EffectiveAtUtc
                && risk.EffectiveAtUtc >= selectedStop.ObservedAtUtc && risk.EffectiveAtUtc <= risk.ObservedAtUtc
                && risk.ObservedAtUtc <= at && risk.Source is not null && risk.Source.ObservedAtUtc == risk.ObservedAtUtc
                && risk.RatioExceedsDecimalRange == (risk.RiskRatio is null) && risk.RiskRatio is null or >= 0);
            sources = sources.Append(risk.Source!);
        }
        if (result.Scenario2TakeProfit is { } target)
        {
            Require(result.Path == "WickfillScenario2" && result.Scenario2StopLoss is { } selectedStop
                && target.StopPrice == selectedStop.StopPrice && rules.SingleOrDefault(r => r.RuleId == "NQ-TP-001")?.Result == "Passed"
                && target.ProviderId == providerId && target.Symbol == symbol && target.Direction == "Sell"
                && target.TargetReferencePrice == target.TakeProfitPrice && target.ReferenceKind is "SessionLevel" or "Structural"
                && !string.IsNullOrWhiteSpace(target.ReferenceDescription) && target.ReferenceDescription.Length <= 4096
                && target.ReferenceSha256 is { Length: 64 } && target.ReferenceSha256.All(char.IsAsciiHexDigit)
                && target.EffectiveAtUtc.Offset == TimeSpan.Zero && target.ObservedAtUtc.Offset == TimeSpan.Zero
                && target.EffectiveAtUtc <= target.ObservedAtUtc && selectedStop.ObservedAtUtc <= target.ObservedAtUtc
                && target.ObservedAtUtc <= at && target.Source is not null && target.ReferenceSource is not null
                && target.Source.ObservedAtUtc == target.ObservedAtUtc && target.ReferenceSource.ObservedAtUtc <= target.EffectiveAtUtc);
            sources = sources.Concat([target.Source!, target.ReferenceSource!]);
        }
        foreach (var source in sources)
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
