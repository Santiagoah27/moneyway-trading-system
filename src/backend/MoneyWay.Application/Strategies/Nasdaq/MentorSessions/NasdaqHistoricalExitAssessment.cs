using System.Globalization;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Documentary close evidence. Unlike the Standard-owned documented exit, price and execution may be unknown.</summary>
public sealed record NasdaqHistoricalExitEvidence(NasdaqDemoSessionIdentity Session, string ReviewId,
    string SourceReference, string HumanValidationIdentity, string Origin, string EventKind, string SourcePlatform)
{
    public string Scenario => "WickfillScenario2";
    public string Direction => "Sell";
    public string? SourceProviderId { get; init; }
    public string? SourceSymbol { get; init; }
    public string? OriginalTime { get; init; }
    public string? OriginalClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    // A nominal approximation is not an invented uncertainty interval or an exact execution instant.
    public DateTimeOffset? NominalEventUtc { get; init; }
    public DateTimeOffset? EarliestEventUtc { get; init; }
    public DateTimeOffset? LatestEventUtc { get; init; }
    public string? TimeMappingReference { get; init; }
    public DateTimeOffset? HistoricalAvailableAtUtc { get; init; }
    public DateTimeOffset? SourceCreatedAtUtc { get; init; }
    public DateOnly? ReviewCreatedOn { get; init; }
    public decimal? ExitPrice { get; init; }
    public string? PriceUnit { get; init; }
    public decimal? Quantity { get; init; }
    public string? QuantityUnit { get; init; }
    public string? ExitSide { get; init; }
    public string? ExecutionReference { get; init; }
    public string? PositionReference { get; init; }
    public string? LinkedEntryReviewId { get; init; }
    public string? LinkedEntryExecutionReference { get; init; }
    public string? AssociationSourceReference { get; init; }
    public string? DocumentedCloseMechanism { get; init; }
    public string? CloseMechanismSourceReference { get; init; }
    public string? ReportedStopProtection { get; init; }
    public string? StopProtectionSourceReference { get; init; }
    public string? Limitations { get; init; }
    public string? ExitPriceText => ExitPrice?.ToString(CultureInfo.InvariantCulture);
    public string? QuantityText => Quantity?.ToString(CultureInfo.InvariantCulture);
    public DateTimeOffset? EffectiveAtUtc => TimePrecision is "ExactNativeRecord" or "SourceReportedSecond" ? EarliestEventUtc : null;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Session);
        foreach (var value in new[] { ReviewId, SourceReference, HumanValidationIdentity, Origin, EventKind, SourcePlatform, TimePrecision })
            if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) throw new ArgumentException("Exit evidence needs explicit bounded provenance.");
        foreach (var value in new[] { SourceProviderId, SourceSymbol, OriginalTime, OriginalClockDomain, TimeMappingReference, PriceUnit,
            QuantityUnit, ExitSide, ExecutionReference, PositionReference, LinkedEntryReviewId, LinkedEntryExecutionReference,
            AssociationSourceReference, DocumentedCloseMechanism, CloseMechanismSourceReference, ReportedStopProtection, StopProtectionSourceReference, Limitations })
            if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > 4096)) throw new ArgumentException("Unknown exit fields must remain null.");
        if (Origin is not ("HumanReview" or "NativeExecutionRecord")
            || EventKind is not ("ReportedClosure" or "PositionClosingField" or "ClosedPositionObserved" or "ReportedStopProtection")
            || TimePrecision is not ("Unknown" or "Approximate" or "SourceReportedSecond" or "ExactNativeRecord")
            || ExitSide is not (null or "Buy" or "Sell")) throw new ArgumentException("Unsupported exit classification.");
        if (new[] { NominalEventUtc, EarliestEventUtc, LatestEventUtc, HistoricalAvailableAtUtc, SourceCreatedAtUtc }.Any(t => t.HasValue && t.Value.Offset != TimeSpan.Zero)
            || EarliestEventUtc.HasValue != LatestEventUtc.HasValue || EarliestEventUtc > LatestEventUtc
            || (NominalEventUtc.HasValue || EarliestEventUtc.HasValue) && TimeMappingReference is null
            || TimePrecision == "Unknown" && (NominalEventUtc.HasValue || EarliestEventUtc.HasValue)
            || TimePrecision is "ExactNativeRecord" or "SourceReportedSecond" && (!EarliestEventUtc.HasValue || EarliestEventUtc != LatestEventUtc)
            || NominalEventUtc.HasValue && EarliestEventUtc.HasValue && (NominalEventUtc < EarliestEventUtc || NominalEventUtc > LatestEventUtc)
            || HistoricalAvailableAtUtc < LatestEventUtc || HistoricalAvailableAtUtc < SourceCreatedAtUtc
            || Quantity is <= 0 || Quantity.HasValue != (QuantityUnit is not null)
            || ExitPrice.HasValue != (PriceUnit is not null)
            || DocumentedCloseMechanism is not null && CloseMechanismSourceReference is null
            || ReportedStopProtection is not null && StopProtectionSourceReference is null
            || LinkedEntryReviewId is not null && AssociationSourceReference is null
            || LinkedEntryExecutionReference is not null && AssociationSourceReference is null)
            throw new ArgumentException("Exit times, values and associations require explicit source support; no conversion or linkage is inferred.");
    }
}

public sealed record NasdaqHistoricalExitReviewAssessment(NasdaqHistoricalExitEvidence Evidence, string Availability,
    string ObservationStatus, string EntryAssociation, string NativeExitStatus, bool NativeIdentityConsistent, IReadOnlyList<string> Issues);
public sealed record NasdaqHistoricalExitAssessment(DateTimeOffset AsOfUtc, IReadOnlyList<NasdaqHistoricalExitReviewAssessment> Observations)
{
    public bool HumanValidationRequired => true;
    public string Scope => "HistoricalExitAuditNotStrategyInput";
    public bool NativeExitIndependentlyVerified => false;
    public bool EntryExitRelationshipIndependentlyVerified => false;
    public bool EconomicOutcomeSupported => false;
    public bool CompleteSupervisedTrade => false;
}

/// <summary>Pure documentary projection. No rule evaluation, native authentication, fills, stop management or monetary calculation.</summary>
public sealed class NasdaqHistoricalExitCausalSelector
{
    public NasdaqHistoricalExitAssessment? Select(NasdaqDemoSessionIdentity session, DateTimeOffset asOfUtc,
        IEnumerable<NasdaqHistoricalExitEvidence> evidence, NasdaqHistoricalEntryAssessment? entry)
    {
        if (asOfUtc.Offset != TimeSpan.Zero || entry is not null && entry.AsOfUtc != asOfUtc) throw new ArgumentException("Exit and entry must share the diagnostic UTC boundary.");
        var supplied = evidence.ToArray();
        if (supplied.Length > 16 || supplied.Any(e => e is null) || supplied.Select(e => e.ReviewId).Distinct(StringComparer.Ordinal).Count() != supplied.Length)
            throw new ArgumentException("Exit reviews must be uniquely identified and bounded to sixteen.");
        foreach (var e in supplied) e.Validate();
        var visible = supplied.Where(e => e.Session == session && (e.HistoricalAvailableAtUtc is null || e.HistoricalAvailableAtUtc <= asOfUtc)
            && (e.NominalEventUtc is null || e.NominalEventUtc <= asOfUtc) && (e.LatestEventUtc is null || e.LatestEventUtc <= asOfUtc))
            .OrderBy(e => e.ReviewId, StringComparer.Ordinal).ToArray();
        if (visible.Length == 0) return null;
        var result = visible.Select(e =>
        {
            var issues = new List<string>();
            if (e.HistoricalAvailableAtUtc is null) issues.Add("RetrospectiveExit_NotAdmitted");
            if (e.TimePrecision is "Unknown" or "Approximate") issues.Add("ExitTimeNotExact");
            if (e.ExitPrice is null) issues.Add("ExitPriceMissing");
            if (e.Quantity is null) issues.Add("ExitQuantityMissing");
            if (e.ExecutionReference is null) issues.Add("NativeExitReferenceMissing");
            if (e.DocumentedCloseMechanism is null) issues.Add("CloseMechanismUnknown");
            if (e.ReportedStopProtection is not null) issues.Add("ReportedProtectionIsNotExecutionOrRealizedReturn");
            var entries = entry?.Observations.Where(o => o.Evidence.ReviewId == e.LinkedEntryReviewId).ToArray() ?? [];
            var opening = entries.Length == 1 ? entries[0].Evidence : null;
            var openingConflict = entry?.SelectionStatus == "Conflict" || entries.Any(o => o.Issues.Any(i => i.StartsWith("Conflicting", StringComparison.Ordinal)));
            if (openingConflict) issues.Add("ConflictingOpeningEvidence_NoAssociation");
            var association = "Unresolved";
            if (opening is null) issues.Add("ExplicitEntryAssociationMissingOrEntryUnavailable");
            var sameInstrument = opening is not null && e.SourceSymbol is not null && opening.SourceSymbol == e.SourceSymbol
                && opening.SourcePlatform == e.SourcePlatform && (opening.SourceProviderId is null || e.SourceProviderId is null || opening.SourceProviderId == e.SourceProviderId);
            if (opening is not null && !sameInstrument) issues.Add("EntryExitSourceIdentityConflictOrUnknown");
            var ordered = opening?.LatestEventUtc is { } entryEnd && e.EarliestEventUtc is { } exitStart && exitStart > entryEnd;
            if (!ordered) issues.Add("EntryExitCausalOrderUnresolved");
            var reversed = opening?.EarliestEventUtc is { } entryStart && e.LatestEventUtc is { } exitEnd && exitEnd < entryStart;
            if (reversed) issues.Add("ExitPrecedesEntry");
            var refConflict = e.LinkedEntryExecutionReference is not null && opening?.ExecutionReference is not null
                && e.LinkedEntryExecutionReference != opening.ExecutionReference;
            if (refConflict) issues.Add("OpeningExecutionReferenceConflict");
            if (e.LinkedEntryExecutionReference is null || opening?.ExecutionReference is null) issues.Add("OpeningExecutionAssociationReferenceMissing");
            if (e.PositionReference is null || opening?.PositionReference is null || e.PositionReference != opening.PositionReference)
                issues.Add("NativePositionIdentityMissingOrConflicting");
            var conflict = visible.Any(v => v != e && v.LinkedEntryReviewId == e.LinkedEntryReviewId && e.LinkedEntryReviewId is not null
                && (v.SourceSymbol != e.SourceSymbol || v.SourceProviderId is not null && e.SourceProviderId is not null && v.SourceProviderId != e.SourceProviderId
                    || v.ExitPrice.HasValue && e.ExitPrice.HasValue && v.ExitPrice != e.ExitPrice
                    || v.ExecutionReference is not null && e.ExecutionReference is not null && v.ExecutionReference != e.ExecutionReference
                    || v.EffectiveAtUtc.HasValue && e.EffectiveAtUtc.HasValue && v.EffectiveAtUtc != e.EffectiveAtUtc));
            if (conflict) issues.Add("ConflictingExitClaims_NoWinnerSelected");
            var closure = e.EventKind != "ReportedStopProtection";
            if (!closure) issues.Add("StopProtectionDoesNotEstablishClosure");
            if (opening is not null && !openingConflict && sameInstrument && !reversed && !refConflict && !conflict && closure && e.AssociationSourceReference is not null)
                association = ordered ? "DocumentaryAssociation" : "DocumentaryCandidate_OrderingRequiresValidation";
            var native = e.Origin == "NativeExecutionRecord" && e.EventKind == "ReportedClosure" && e.TimePrecision == "ExactNativeRecord"
                && e.ExitPrice.HasValue && e.ExecutionReference is not null && e.SourceProviderId is not null && e.SourceSymbol is not null
                && e.HistoricalAvailableAtUtc.HasValue && !conflict;
            var identity = native && ordered && sameInstrument && !openingConflict && !refConflict && !conflict
                && entries.SingleOrDefault()?.NativeExecutionStatus == "NativeRecordDocumented"
                && e.SourceProviderId == opening!.SourceProviderId && e.LinkedEntryExecutionReference == opening.ExecutionReference
                && e.LinkedEntryExecutionReference is not null && e.PositionReference is not null && e.PositionReference == opening.PositionReference;
            if (identity) association = "NativeIdentityConsistent_NotIndependentlyVerified";
            issues.Add("IndependentNativeExitAndRelationshipVerificationNotPerformed");
            return new NasdaqHistoricalExitReviewAssessment(e, e.HistoricalAvailableAtUtc is null ? "RetrospectiveOnly" : "AvailableAtSelectedFrame",
                conflict ? "Conflict" : closure ? "ReportedClosure_NotIndependentlyVerified" : "StopProtectionOnly", association,
                native ? "NativeExitRecordDocumented_NotIndependentlyVerified" : "NativeExitUnverified", identity, Array.AsReadOnly(issues.ToArray()));
        }).ToArray();
        return new(asOfUtc, Array.AsReadOnly(result));
    }
}
