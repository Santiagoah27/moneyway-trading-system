using System.Globalization;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Documentary entry evidence, never a replay input, rule fact, admitted candle or authorized execution.</summary>
public sealed record NasdaqHistoricalEntryEvidence(
    NasdaqDemoSessionIdentity Session, string Scenario, string Direction, string ReviewId,
    string SourceReference, string HumanValidationIdentity, string Origin, string EventKind,
    string SourcePlatform, string? SourceProviderId, string? SourceSymbol,
    string? OriginalTime, string TimePrecision, DateTimeOffset? EarliestEventUtc,
    DateTimeOffset? LatestEventUtc, string? TimeMappingReference, DateTimeOffset? HistoricalAvailableAtUtc,
    DateOnly? ReviewCreatedOn, decimal? SourceEntryPrice, string? SourcePriceUnit, string? ExecutionReference)
{
    public string? SourceEntryPriceText => SourceEntryPrice?.ToString(CultureInfo.InvariantCulture);
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Session);
        foreach (var s in new[] { Scenario, Direction, ReviewId, SourceReference, HumanValidationIdentity, Origin, EventKind, SourcePlatform, TimePrecision })
            if (string.IsNullOrWhiteSpace(s) || s.Length > 4096) throw new ArgumentException("Entry evidence requires bounded explicit provenance and identity.");
        foreach (var s in new[] { SourceProviderId, SourceSymbol, OriginalTime, TimeMappingReference, SourcePriceUnit, ExecutionReference })
            if (s is not null && (string.IsNullOrWhiteSpace(s) || s.Length > 4096)) throw new ArgumentException("Optional evidence cannot be a blank default.");
        if (Scenario is not ("WickfillScenario2" or "Standard") || Direction is not ("Sell" or "Buy")
            || Origin is not ("HumanReview" or "NativeExecutionRecord")
            || EventKind is not ("ReportedExecution" or "PositionOpeningField" or "OpenPositionObserved" or "OrderPlacement")
            || TimePrecision is not ("Unknown" or "Approximate" or "SourceReportedSecond" or "ExactNativeRecord"))
            throw new ArgumentException("Unsupported historical evidence classification.");
        if (new[] { EarliestEventUtc, LatestEventUtc, HistoricalAvailableAtUtc }.Any(t => t.HasValue && t.Value.Offset != TimeSpan.Zero)
            || EarliestEventUtc.HasValue != LatestEventUtc.HasValue || EarliestEventUtc > LatestEventUtc
            || EarliestEventUtc.HasValue && TimeMappingReference is null
            || TimePrecision == "Unknown" && EarliestEventUtc.HasValue
            || TimePrecision is "SourceReportedSecond" or "ExactNativeRecord" && EarliestEventUtc != LatestEventUtc
            || HistoricalAvailableAtUtc < EarliestEventUtc)
            throw new ArgumentException("UTC bounds and authentic availability require explicit source mapping; no native conversion is inferred.");
    }
    internal bool SameEvent(NasdaqHistoricalEntryEvidence other) =>
        Session == other.Session && Scenario == other.Scenario && Direction == other.Direction
        && Origin == other.Origin && EventKind == other.EventKind && SourcePlatform == other.SourcePlatform
        && SourceProviderId == other.SourceProviderId && SourceSymbol == other.SourceSymbol
        && OriginalTime == other.OriginalTime && TimePrecision == other.TimePrecision
        && EarliestEventUtc == other.EarliestEventUtc && LatestEventUtc == other.LatestEventUtc
        && SourceEntryPrice == other.SourceEntryPrice && SourcePriceUnit == other.SourcePriceUnit && ExecutionReference == other.ExecutionReference;
}

public sealed record NasdaqHistoricalEntryFrame(long Step, DateTimeOffset AsOfUtc, string? Verdict,
    bool? HasCompleteRequiredCoverage, IReadOnlyList<NasdaqHistoricalRuleSummary> Rules);
public sealed record NasdaqHistoricalEntryReviewAssessment(NasdaqHistoricalEntryEvidence Evidence,
    string Availability, string NativeExecutionStatus, NasdaqHistoricalEntryFrame? CanonicalAtEntry, IReadOnlyList<string> Issues);

/// <summary>Post-evaluation diagnostic projection. It cannot change StrategyVerdict or grant an order authorization.</summary>
public sealed record NasdaqHistoricalEntryAssessment(DateTimeOffset AsOfUtc, string SelectionStatus,
    IReadOnlyList<NasdaqHistoricalEntryReviewAssessment> Observations)
{
    public string Scope => "HistoricalEvidenceAuditNotStrategyInput";
    public string EntryAuthorization => "NotGrantedByDiagnostic";
    public bool NativeExecutionIndependentlyVerified => false;
    public string FailureClassification => "inconclusive";
}

/// <summary>Availability selection plus comparison with frozen original frames; no evaluation of a trading rule.</summary>
public sealed class NasdaqHistoricalEntryCausalSelector
{
    public NasdaqHistoricalEntryAssessment? Select(NasdaqDemoSessionIdentity session, DateTimeOffset asOfUtc,
        IEnumerable<NasdaqHistoricalEntryEvidence> evidence, IEnumerable<NasdaqHistoricalEntryFrame> frames)
    {
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Diagnostic boundary must be UTC.");
        var supplied = evidence.ToArray();
        if (supplied.Length > 16 || supplied.Any(e => e is null)) throw new ArgumentException("Entry reviews must be non-null and bounded to sixteen.");
        foreach (var item in supplied) item.Validate();
        // Unknown availability is a visibly retrospective annotation, never available historical input.
        // Known future evidence is excluded entirely, so it cannot change an earlier diagnostic.
        var visible = supplied.Where(e => e.Session == session && e.Scenario == "WickfillScenario2" && e.Direction == "Sell"
                && (e.HistoricalAvailableAtUtc is null || e.HistoricalAvailableAtUtc <= asOfUtc)
                && (e.EarliestEventUtc is null || e.EarliestEventUtc <= asOfUtc))
            .OrderBy(e => e.ReviewId, StringComparer.Ordinal).ThenBy(e => e.SourceReference, StringComparer.Ordinal).ToArray();
        if (visible.Length == 0) return null;
        var frozen = frames.Where(f => f.AsOfUtc <= asOfUtc).ToArray();
        var available = visible.Where(e => e.HistoricalAvailableAtUtc.HasValue).ToArray();
        var conflict = available.Length > 0 && available.Any(e => !e.SameEvent(available[0]));
        var reviews = visible.Select(e =>
        {
            var issues = new List<string>();
            var retrospective = e.HistoricalAvailableAtUtc is null;
            if (retrospective) issues.Add("HistoricalAvailabilityUnknown_NotAdmitted");
            if (e.SourceProviderId is null) issues.Add("OriginalProviderUnknown");
            else if (e.SourceProviderId != session.ProviderId.Value) issues.Add("OriginalProviderMismatch");
            if (e.SourceSymbol is null) issues.Add("OriginalInstrumentUnknown");
            else if (e.SourceSymbol != session.Symbol.Value) issues.Add("OriginalInstrumentMismatch_NoMapping");
            if (e.ExecutionReference is null) issues.Add("NativeExecutionReferenceMissing");
            if (e.SourceEntryPrice is null) issues.Add("OriginalEntryPriceMissing");
            if (e.SourcePriceUnit is null) issues.Add("OriginalPriceUnitUnknown");
            if (e.EventKind == "OrderPlacement") issues.Add("OrderPlacementIsNotExecution");
            if (e.TimePrecision == "Approximate") issues.Add("ApproximateEventTime_NoExactFill");
            if (e.EarliestEventUtc is null) issues.Add("EventUtcUnknown_NoImplicitConversion");
            var exact = e.TimePrecision is "SourceReportedSecond" or "ExactNativeRecord" && e.EarliestEventUtc.HasValue;
            var atEntry = exact ? frozen.SingleOrDefault(f => f.AsOfUtc == e.EarliestEventUtc) : null;
            if (atEntry is null) issues.Add("OriginalEntryFrameNotCaptured");
            else
            {
                if (atEntry.Rules.SingleOrDefault(r => r.RuleId == "NQ-WF2-002")?.Result != "Passed") issues.Add("Wf2NotPassedAtOriginalEntry");
                if (atEntry.Verdict != "Ready" || atEntry.HasCompleteRequiredCoverage != true
                    || atEntry.Rules.Any(r => r.IsRequired && r.Result is not ("Passed" or "NotApplicable")))
                    issues.Add("CanonicalRequirementsIncompleteAtOriginalEntry");
            }
            if (e.HistoricalAvailableAtUtc > e.EarliestEventUtc) issues.Add("EvidenceAvailableOnlyAfterEntry_NoBackdating");
            var peers = retrospective ? visible.Where(v => v.HistoricalAvailableAtUtc is null).ToArray() : available;
            if (peers.Any(v => !e.SameEvent(v))) issues.Add(retrospective ? "ConflictingRetrospectiveClaims_NotAdmitted" : "ConflictingHistoricalEntryClaims");
            var documentedNative = e.Origin == "NativeExecutionRecord" && e.EventKind == "ReportedExecution"
                && e.SourceProviderId == session.ProviderId.Value && e.SourceSymbol == session.Symbol.Value
                && e.TimePrecision == "ExactNativeRecord" && exact && e.SourceEntryPrice.HasValue
                && e.SourcePriceUnit is not null && e.ExecutionReference is not null && e.HistoricalAvailableAtUtc.HasValue;
            // This diagnostic does not authenticate/import broker deals. A complete documentary record is explicit, not a fill generated here.
            issues.Add("IndependentNativeExecutionVerificationNotPerformed");
            return new NasdaqHistoricalEntryReviewAssessment(e, retrospective ? "RetrospectiveOnly" : "AvailableAtSelectedFrame",
                documentedNative ? "NativeRecordDocumented" : "NativeExecutionUnverified", atEntry, issues.ToArray());
        }).ToArray();
        return new(asOfUtc, conflict ? "Conflict" : available.Length == 0 ? "RetrospectiveOnly" : "Unique", reviews);
    }

    public static NasdaqHistoricalEntryFrame Frame(NasdaqHistoricalFrameSummary frame) =>
        new(frame.Step, frame.AsOfUtc, frame.Verdict, frame.HasCompleteRequiredCoverage, frame.Rules.Select(r => r with { EvidenceId = null }).ToArray());

    public static NasdaqHistoricalEntryFrame Frame(NasdaqMentorSessionFrame frame, string? verdict, bool? coverage) =>
        new(frame.Strategy.Step, frame.Strategy.AsOfUtc, verdict, coverage, frame.Strategy.Evaluations.Select(r =>
            new NasdaqHistoricalRuleSummary(r.RuleId.Value, r.DefinitionStatus.ToString(), r.Result.ToString(), r.Sequence,
                r.IsRequired, r.Reason, r.EvaluatedAtUtc, null)).ToArray());
}
