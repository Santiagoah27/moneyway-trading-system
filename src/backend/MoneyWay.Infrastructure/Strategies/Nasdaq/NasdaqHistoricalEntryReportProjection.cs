using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Validates persisted diagnostic annotations against the frozen source frames. No strategy reevaluation.</summary>
internal static class NasdaqHistoricalEntryReportProjection
{
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
        if (value.GetProperty("CompleteSupervisedTrade").ValueKind != JsonValueKind.False
            || value.GetProperty("Scope").GetString() != "SupervisedHistoricalEvidenceSnapshot"
            || value.GetProperty("Scenario").GetString() != "WickfillScenario2" || value.GetProperty("Direction").GetString() != "Sell"
            || value.GetProperty("FailureClassification").GetString() != "inconclusive"
            || value.GetProperty("Exit").ValueKind != JsonValueKind.Null || value.GetProperty("EconomicOutcome").ValueKind != JsonValueKind.Null
            || value.GetProperty("NativeExecutionIndependentlyVerified").ValueKind != JsonValueKind.False
            || value.GetProperty("ExitSupported").ValueKind != JsonValueKind.False || value.GetProperty("EconomicOutcomeSupported").ValueKind != JsonValueKind.False
            || value.GetProperty("EntryAuthorization").GetString() != "NotGrantedBySnapshot"
            || JsonSerializer.Serialize(JsonSerializer.Deserialize<NasdaqSupervisedHistoricalTradeSnapshot>(value)) != JsonSerializer.Serialize(expected))
            throw new InvalidDataException("Supervised snapshot contradicts existing lifecycle evidence or claims an unsupported trade.");
    }
}
