using System.Text.RegularExpressions;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>A literal source cost. No aggregation, sign reversal, currency conversion or fee defaults.</summary>
public sealed record NasdaqReportedMonetaryCost(string Category, string SourceReference)
{
    public string? Amount { get; init; }
    public string? Currency { get; init; }
    public string? OriginalSignMeaning { get; init; }
}

/// <summary>Operator/source-reported monetary evidence linked to an immutable execution-evidence revision, never calculated PnL.</summary>
public sealed record NasdaqReportedMonetaryOutcome(Guid ExecutionRecordId, int ExecutionEvidenceRevision,
    string ExecutionEvidenceRevisionHash, string SourceReference, string ReportedBy, string Provenance)
{
    public string? SourceSymbol { get; init; }
    public string? Amount { get; init; }
    public string? Currency { get; init; }
    public string AmountMeaning { get; init; } = "Unspecified";
    public string Basis { get; init; } = "Unspecified";
    public string? OriginalMeaningStatement { get; init; }
    public string? Narrative { get; init; }
    public string? SourceEventDescription { get; init; }
    public string Association { get; init; } = "RecordOnly";
    public string? AssociationSourceReference { get; init; }
    public string? DealReference { get; init; }
    public string? PositionReference { get; init; }
    public string? OriginalTime { get; init; }
    public string? ClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    public IReadOnlyList<NasdaqReportedMonetaryCost>? Costs { get; init; }

    public void Validate()
    {
        if (ExecutionRecordId == Guid.Empty || ExecutionEvidenceRevision is < 1 or > 100
            || ExecutionEvidenceRevisionHash is null || !Regex.IsMatch(ExecutionEvidenceRevisionHash, "^[a-f0-9]{64}$"))
            throw new ArgumentException("Reported outcome requires an existing MoneyWay record/revision/hash reference.");
        NasdaqSupervisedExecutionEvidenceDraft.Reference(SourceReference); Text(ReportedBy, true);
        foreach (var text in new[] { OriginalMeaningStatement, Narrative, SourceEventDescription, DealReference, PositionReference, OriginalTime, ClockDomain }) Text(text);
        if (Provenance is not ("HumanReported" or "DocumentReported") || SourceSymbol is not (null or "USTEC" or "US100")
            || Basis is not ("Unspecified" or "Gross" or "Net")
            || AmountMeaning is not ("Unspecified" or "SignedResult" or "ProfitMagnitude" or "LossMagnitude")
            || Association is not ("RecordOnly" or "HumanReported" or "DocumentarySupported")
            || TimePrecision is not ("Unknown" or "Approximate" or "SourceReportedSecond" or "ExactNativeRecord"))
            throw new ArgumentException("Unsupported reported monetary evidence classification; no native verification is available.");
        if (Amount is not null) ValidateAmount(Amount);
        ValidateCurrency(Currency);
        if (Association != "RecordOnly" && AssociationSourceReference is null) throw new ArgumentException("An outcome/trade association assertion needs its own source.");
        if (AssociationSourceReference is not null) NasdaqSupervisedExecutionEvidenceDraft.Reference(AssociationSourceReference);
        if (Costs?.Count > 16 || Costs?.Any(c => c is null) == true) throw new ArgumentException("Reported costs must be bounded, explicit source components.");
        foreach (var cost in Costs ?? [])
        {
            Text(cost.Category, true); NasdaqSupervisedExecutionEvidenceDraft.Reference(cost.SourceReference); Text(cost.OriginalSignMeaning);
            if (cost.Amount is not null) ValidateAmount(cost.Amount);
            ValidateCurrency(cost.Currency);
        }
    }
    public static void ValidateAmount(string value)
    {
        // Retain the original explicit '+' and scale. Reuse the exact decimal representability check; never write its numeric projection back.
        Text(value, true);
        _ = NasdaqSupervisedExecutionEvidenceDraft.Decimal(value.StartsWith('+') ? value[1..] : value);
    }
    private static void ValidateCurrency(string? value)
    {
        if (value is not null && !Regex.IsMatch(value, "^[A-Z]{3}$")) throw new ArgumentException("Use an explicit three-letter source currency or leave it unresolved.");
    }
    private static void Text(string? value, bool required = false)
    {
        if (value is null && !required) return;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("Reported monetary provenance must be explicit, bounded and trimmed; unknown values remain null.");
    }
    internal void AddIssues(NasdaqSupervisedExecutionEvidenceDraft draft, List<string> missing)
    {
        if (Amount is null) missing.Add("ReportedOutcomeAmountMissing_NoNumericInference");
        if (Currency is null) missing.Add("ReportedOutcomeCurrencyUnknown");
        if (AmountMeaning == "Unspecified") missing.Add("ReportedAmountSignMeaningUnresolved");
        if (Amount is not null && Amount.StartsWith('-') && AmountMeaning is "ProfitMagnitude" or "LossMagnitude") missing.Add("ReportedMagnitudeSignConflict_InterpretationUnresolved");
        if (Basis == "Unspecified") missing.Add("ReportedGrossNetMeaningUnresolved");
        if (SourceSymbol is null) missing.Add("ReportedOutcomeInstrumentUnknown");
        else if (SourceSymbol != draft.OriginalSymbol || draft.Entry?.Symbol is { } e && SourceSymbol != e || draft.Exit?.Symbol is { } x && SourceSymbol != x)
            missing.Add("ReportedOutcomeInstrumentConflict_NoMapping");
        if (Costs is null || Costs.Count == 0) missing.Add("ReportedCostsUnknown_NotZero");
        foreach (var cost in Costs ?? [])
        {
            if (cost.Amount is null) missing.Add("ReportedCostAmountMissing_NotZero");
            if (cost.Currency is null) missing.Add("ReportedCostCurrencyUnknown");
            if (cost.Currency is not null && Currency is null) missing.Add("ReportedCostCurrencyCompatibilityUnresolved_NoCombination");
            else if (cost.Currency is not null && cost.Currency != Currency) missing.Add("ReportedCostCurrencyMismatch_NoConversionOrCombination");
            if (cost.OriginalSignMeaning is null) missing.Add("ReportedCostSignMeaningUnresolved_NoCalculation");
        }
        var suppliedDeals = new[] { draft.Entry?.DealId, draft.Exit?.DealId }.Where(s => s is not null).ToArray();
        if (DealReference is not null && suppliedDeals.Length > 0 && !suppliedDeals.Contains(DealReference)) missing.Add("ReportedOutcomeDealAssociationConflict");
        var suppliedPositions = new[] { draft.Entry?.PositionId, draft.Exit?.PositionId }.Where(s => s is not null).ToArray();
        if (PositionReference is not null && suppliedPositions.Length > 0 && suppliedPositions.Any(p => p != PositionReference)) missing.Add("ReportedOutcomePositionAssociationConflict");
        missing.Add(Association == "RecordOnly" ? "OutcomeNativeTradeAssociationUnresolved" : "OutcomeTradeAssociationAsserted_NotIndependentlyVerified");
        missing.Add("ReportedOutcomeNotBrokerVerifiedOrMoneyWayCalculated");
    }
    internal void AddAnchorIssues(NasdaqSupervisedExecutionEvidenceDraft original, List<string> missing)
    {
        if (SourceSymbol is not null && (SourceSymbol != original.OriginalSymbol || original.Entry?.Symbol is { } e && SourceSymbol != e || original.Exit?.Symbol is { } x && SourceSymbol != x))
            missing.Add("ReportedOutcomeOriginalExecutionInstrumentConflict_NoMapping");
        var deals = new[] { original.Entry?.DealId, original.Exit?.DealId }.Where(s => s is not null).ToArray();
        if (DealReference is not null && deals.Length > 0 && !deals.Contains(DealReference)) missing.Add("ReportedOutcomeOriginalExecutionDealConflict");
        var positions = new[] { original.Entry?.PositionId, original.Exit?.PositionId }.Where(s => s is not null).ToArray();
        if (PositionReference is not null && positions.Any(p => p != PositionReference)) missing.Add("ReportedOutcomeOriginalExecutionPositionConflict");
    }
}
