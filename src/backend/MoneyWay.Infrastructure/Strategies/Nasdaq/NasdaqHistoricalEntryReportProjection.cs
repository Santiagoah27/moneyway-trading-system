using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Validates persisted diagnostic annotations against the frozen source frames. No strategy reevaluation.</summary>
internal static class NasdaqHistoricalEntryReportProjection
{
    internal static NasdaqHistoricalExitEvidence[] Exits(JsonElement element, NasdaqDemoSessionIdentity session)
    {
        var values = JsonSerializer.Deserialize<NasdaqHistoricalExitEvidence[]>(element) ?? throw new InvalidDataException();
        if (values.Length > 16 || values.Any(v => v is null || v.Session != session)
            || values.Select(v => v.ReviewId).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidDataException();
        foreach (var value in values) value.Validate();
        return values;
    }

    internal static void ValidateExit(JsonElement? stored, NasdaqHistoricalExitAssessment? expected)
    {
        if (stored is null || stored.Value.ValueKind == JsonValueKind.Null)
        { if (expected is not null) throw new InvalidDataException("Missing exit diagnostic."); return; }
        var value = stored.Value;
        if (value.GetProperty("Scope").GetString() != "HistoricalExitAuditNotStrategyInput"
            || value.GetProperty("HumanValidationRequired").ValueKind != JsonValueKind.True
            || new[] { "NativeExitIndependentlyVerified", "EntryExitRelationshipIndependentlyVerified", "EconomicOutcomeSupported", "CompleteSupervisedTrade" }
                .Any(p => value.GetProperty(p).ValueKind != JsonValueKind.False)
            || JsonSerializer.Serialize(JsonSerializer.Deserialize<NasdaqHistoricalExitAssessment>(value)) != JsonSerializer.Serialize(expected))
            throw new InvalidDataException("Exit diagnostic contradicts its source evidence or claims unsupported verification.");
    }
    internal static NasdaqHistoricalEntryEvidence[] Evidence(JsonElement element, NasdaqDemoSessionIdentity session)
    {
        var values = JsonSerializer.Deserialize<NasdaqHistoricalEntryEvidence[]>(element) ?? throw new InvalidDataException();
        if (values.Length > 16 || values.Any(v => v is null || v.Session != session)
            || values.Select(v => v.ReviewId).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new InvalidDataException();
        foreach (var value in values) value.Validate();
        return values;
    }

    internal static void Validate(JsonElement? stored, NasdaqHistoricalEntryAssessment? expected)
    {
        if (stored is null || stored.Value.ValueKind == JsonValueKind.Null)
        { if (expected is not null) throw new InvalidDataException("Missing historical-entry diagnostic."); return; }
        var value = stored.Value;
        if (value.GetProperty("EntryAuthorization").GetString() != "NotGrantedByDiagnostic"
            || value.GetProperty("NativeExecutionIndependentlyVerified").ValueKind != JsonValueKind.False
            || value.GetProperty("FailureClassification").GetString() != "inconclusive"
            || value.GetProperty("Scope").GetString() != "HistoricalEvidenceAuditNotStrategyInput"
            || JsonSerializer.Serialize(JsonSerializer.Deserialize<NasdaqHistoricalEntryAssessment>(value)) != JsonSerializer.Serialize(expected))
            throw new InvalidDataException("Historical-entry diagnostic contradicts source evidence or frozen frames.");
    }

    internal static void ValidateSnapshot(JsonElement? stored, NasdaqSupervisedHistoricalTradeSnapshot? expected)
    {
        // Legacy artifacts have no grouped snapshot. Their original values/statuses remain readable.
        if (stored is null || stored.Value.ValueKind == JsonValueKind.Null) return;
        var value = stored.Value;
        // Prior grouped snapshots explicitly reported WF2 exit composition as NotImplemented.
        // Validate that historical shape against the same original constituents, then expose the new view.
        var comparison = expected;
        if (!value.TryGetProperty("DocumentaryExit", out _) && expected is { DocumentaryExit: null })
            comparison = expected with
            {
                Components = expected.Components.Select(c => c.Stage == "DocumentedExit"
                    ? new NasdaqHistoricalTradeStage("DocumentedExit", "NotImplemented", null, null, "Existing exit types require a Standard snapshot; no WF2 exit is synthesized.") : c).ToArray(),
                MissingEvidence = expected.MissingEvidence.Where(m => m != "HistoricalExitEvidenceMissing")
                    .Select(m => m == "IndependentNativeExitAndRelationshipVerificationNotPerformed" ? "Wf2DocumentedExitNotIntegrated" : m).ToArray()
            };
        if (value.GetProperty("CompleteSupervisedTrade").ValueKind != JsonValueKind.False
            || new[] { "NativeExitIndependentlyVerified", "EntryExitRelationshipIndependentlyVerified" }
                .Any(p => value.TryGetProperty(p, out var flag) && flag.ValueKind != JsonValueKind.False)
            || value.TryGetProperty("HistoricalExitObserved", out var observed) && observed.GetBoolean() != (expected?.HistoricalExitObserved ?? false)
            || value.GetProperty("Scope").GetString() != "SupervisedHistoricalEvidenceSnapshot"
            || value.GetProperty("Scenario").GetString() != "WickfillScenario2" || value.GetProperty("Direction").GetString() != "Sell"
            || value.GetProperty("FailureClassification").GetString() != "inconclusive"
            || value.GetProperty("Exit").ValueKind != JsonValueKind.Null || value.GetProperty("EconomicOutcome").ValueKind != JsonValueKind.Null
            || value.GetProperty("NativeExecutionIndependentlyVerified").ValueKind != JsonValueKind.False
            || value.GetProperty("ExitSupported").ValueKind != JsonValueKind.False || value.GetProperty("EconomicOutcomeSupported").ValueKind != JsonValueKind.False
            || value.GetProperty("EntryAuthorization").GetString() != "NotGrantedBySnapshot"
            || JsonSerializer.Serialize(JsonSerializer.Deserialize<NasdaqSupervisedHistoricalTradeSnapshot>(value)) != JsonSerializer.Serialize(comparison))
            throw new InvalidDataException("Supervised snapshot contradicts existing lifecycle evidence or claims an unsupported trade.");
        if (value.TryGetProperty("DocumentaryExit", out var exit)) ValidateExit(exit, expected?.DocumentaryExit);
    }
}
