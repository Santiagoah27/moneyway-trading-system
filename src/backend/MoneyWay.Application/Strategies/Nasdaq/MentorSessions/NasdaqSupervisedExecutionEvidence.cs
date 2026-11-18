using System.Globalization;
using System.Text.RegularExpressions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

public sealed record NasdaqSupervisedExecutionEvent(string SourceReference, string EventKind)
{
    public string? Symbol { get; init; }
    public string? Direction { get; init; }
    public string? Price { get; init; }
    public string? PriceUnit { get; init; }
    public string? Currency { get; init; }
    public string? OriginalTime { get; init; }
    public string? ClockDomain { get; init; }
    public string TimePrecision { get; init; } = "Unknown";
    public DateTimeOffset? DocumentedUtc { get; init; }
    public string? TimeMappingReference { get; init; }
    public string? DealId { get; init; }
    public string? PositionId { get; init; }
    public string? CloseMechanism { get; init; }
    public string? CloseMechanismSource { get; init; }
}

/// <summary>Operator assertions only. No caller field can confer trust or historical availability.</summary>
public sealed record NasdaqSupervisedExecutionEvidenceDraft(string SessionId, DateOnly TradingDay, string SourcePlatform,
    string OriginalSymbol, string Reviewer, string SourceReference, string RevisionReason)
{
    public string? SourceProviderId { get; init; }
    public string? Direction { get; init; }
    public string? Scenario { get; init; }
    public NasdaqSupervisedExecutionEvent? Entry { get; init; }
    public NasdaqSupervisedExecutionEvent? Exit { get; init; }
    public string Association { get; init; } = "None";
    public string? AssociationSourceReference { get; init; }
    public NasdaqReportedMonetaryOutcome? ReportedOutcome { get; init; }

    public void Validate()
    {
        foreach (var s in new[] { SessionId, SourcePlatform, OriginalSymbol, Reviewer, SourceReference, RevisionReason })
            Text(s, required: true);
        Text(SourceProviderId); Text(AssociationSourceReference);
        if (TradingDay == default || OriginalSymbol is not ("US100" or "USTEC") || Direction is not (null or "Buy" or "Sell")
            || Scenario is not (null or "WickfillScenario2") || Scenario == "WickfillScenario2" && Direction != "Sell"
            || Association is not ("None" or "HumanReported" or "DocumentarySupported"))
            throw new ArgumentException("Unsupported session, instrument, direction, scenario or association.");
        Reference(SourceReference);
        if (Entry is null && Exit is null) throw new ArgumentException("Supply at least an entry or exit observation; prices and native identifiers may remain absent.");
        if (Association != "None" && (Entry is null || Exit is null || AssociationSourceReference is null))
            throw new ArgumentException("An asserted association needs both observations and its explicit source.");
        if (AssociationSourceReference is not null) Reference(AssociationSourceReference);
        Event(Entry, true); Event(Exit, false);
        ReportedOutcome?.Validate();
    }
    private static void Event(NasdaqSupervisedExecutionEvent? e, bool entry)
    {
        if (e is null) return;
        Reference(e.SourceReference);
        foreach (var s in new[] { e.Symbol, e.Direction, e.Price, e.PriceUnit, e.Currency, e.OriginalTime, e.ClockDomain,
            e.TimePrecision, e.TimeMappingReference, e.DealId, e.PositionId, e.CloseMechanism, e.CloseMechanismSource }) Text(s);
        if (e.Symbol is not (null or "US100" or "USTEC") || e.Direction is not (null or "Buy" or "Sell")
            || e.TimePrecision is not ("Unknown" or "Approximate" or "SourceReportedSecond" or "ExactNativeRecord")
            || (entry ? e.EventKind is not ("ReportedExecution" or "PositionOpeningField" or "OpenPositionObserved" or "OrderPlacement")
                : e.EventKind is not ("ReportedClosure" or "PositionClosingField" or "ClosedPositionObserved" or "ReportedStopProtection")))
            throw new ArgumentException("Unsupported original event classification.");
        if (e.Price is not null && e.PriceUnit != "NativeInstrumentPrice" || e.Price is null && e.PriceUnit is not null)
            throw new ArgumentException("A supplied price requires the explicit NativeInstrumentPrice unit.");
        if (e.Price is not null) _ = Decimal(e.Price);
        if (e.Currency is not null && !Regex.IsMatch(e.Currency, "^[A-Z]{3}$")) throw new ArgumentException("Currency must be an explicit three-letter source code.");
        if (e.DocumentedUtc is { } utc && (utc.Offset != TimeSpan.Zero || e.TimeMappingReference is null || e.TimePrecision == "Unknown"))
            throw new ArgumentException("Documentary UTC requires an explicit mapping and precision; no native conversion is inferred.");
        if (e.CloseMechanism is not null && e.CloseMechanismSource is null) throw new ArgumentException("A close mechanism requires its own source.");
        if (e.CloseMechanismSource is not null) Reference(e.CloseMechanismSource);
        if (e.TimeMappingReference is not null) Reference(e.TimeMappingReference);
    }
    public static decimal Decimal(string text)
    {
        static string Canonical(string value) => value.Contains('.') ? value.TrimEnd('0').TrimEnd('.') : value;
        if (!Regex.IsMatch(text, "^-?(0|[1-9][0-9]*)(\\.[0-9]{1,28})?$")
            || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || Canonical(text).Replace("-0", "0", StringComparison.Ordinal) != Canonical(value.ToString(CultureInfo.InvariantCulture)).Replace("-0", "0", StringComparison.Ordinal))
            throw new ArgumentException("Price must be an exact decimal string representable without rounding; no exponents, separators or tolerances.");
        return value;
    }
    private static void Text(string? s, bool required = false)
    {
        if (s is null && !required) return;
        if (string.IsNullOrWhiteSpace(s) || s.Length > 2048 || s != s.Trim() || s.Any(char.IsControl))
            throw new ArgumentException("Evidence text must be explicit, bounded and trimmed; unknown fields remain null.");
    }
    public static void Reference(string s)
    {
        Text(s, true);
        if (s.StartsWith("note:", StringComparison.Ordinal) && s.Length > 5) return;
        if (Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)) return;
        throw new ArgumentException("Source references must be note: statements or HTTPS references. Local paths and automatic downloads are not supported.");
    }
}

public sealed record NasdaqSupervisedExecutionEvidenceRecord(Guid RecordId, int Revision, Guid SubmissionId,
    DateTimeOffset RegisteredAtUtc, string? PreviousRevision, NasdaqSupervisedExecutionEvidenceDraft Evidence);
public sealed record NasdaqSupervisedExecutionRecordReference(Guid RecordId, int Revision, string RevisionHash);
public sealed record NasdaqSupervisedExecutionEvidenceView(NasdaqSupervisedExecutionEvidenceRecord Record, string RevisionHash,
    IReadOnlyList<string> MissingEvidence, NasdaqHistoricalTradeLifecycleSummary? DocumentaryLifecycle)
{
    public string Scope => "LocalOperatorAssertion_NotHistoricalReplayInput";
    public string Integrity => "RegisteredPayloadSha256_NotOriginalArtifactHash";
    public string ArtifactAvailability => "NotInspected_ReferenceOnly";
    public string? OriginalArtifactSha256 => null;
    public bool NativeExecutionVerified => false;
    public bool EntryExitRelationshipVerified => false;
    public bool CompleteSupervisedTrade => false;
    public string? EconomicOutcome => null;
    public NasdaqReportedMonetaryOutcome? ReportedOutcome => Record.Evidence.ReportedOutcome;
    public bool BrokerRealizedOutcomeVerified => false;
    public string? MoneyWayCalculatedPnl => null;
    public string? StrategyClassification => null;
    public bool SessionClosureEstablished => false;
    public string ReentryEligibility => "NotEvaluated_UnresolvedPolicy";
}

/// <summary>Application-level retrospective consumer; never merges manual captures into a historical report/frame.</summary>
public static class NasdaqSupervisedExecutionEvidenceProjection
{
    public static NasdaqSupervisedExecutionEvidenceView View(NasdaqSupervisedExecutionEvidenceRecord record, string hash,
        NasdaqSupervisedExecutionEvidenceRecord? outcomeAnchor = null)
    {
        var d = record.Evidence; d.Validate(); var missing = new List<string>();
        if (d.SourceProviderId is null) missing.Add("OriginalProviderUnknown");
        if (d.Direction is null) missing.Add("DirectionUnknown");
        if (d.Entry is null) missing.Add("EntryMissing");
        if (d.Entry?.Symbol is { } entrySymbol && entrySymbol != d.OriginalSymbol) missing.Add("EntryDeclaredInstrumentConflict_NoMapping");
        if (d.Exit is null) missing.Add("ExitMissing");
        foreach (var (name, e) in new[] { ("Entry", d.Entry), ("Exit", d.Exit) })
        {
            if (e is null) continue;
            if (e.Price is null) missing.Add(name + "PriceMissing");
            if (e.DealId is null) missing.Add(name + "DealMissing");
            if (e.PositionId is null) missing.Add(name + "PositionMissing");
            if (e.DocumentedUtc is null || e.TimePrecision is "Approximate" or "Unknown") missing.Add(name + "ChronologyUnresolved");
            if (e.OriginalTime is not null && e.ClockDomain is null) missing.Add(name + "OriginalClockUnknown");
            if (e.Direction is not null && d.Direction is not null && e.Direction != d.Direction) missing.Add("ConflictingDirections");
        }
        if (d.Exit is { } exit)
        {
            if (exit.Symbol is null) missing.Add("ExitInstrumentUnknown_NoImplicitMapping");
            else if (exit.Symbol != (d.Entry?.Symbol ?? d.OriginalSymbol)) missing.Add("EntryExitInstrumentConflict_NoMapping");
            if (exit.PositionId is not null && d.Entry?.PositionId is not null && exit.PositionId != d.Entry.PositionId) missing.Add("NativePositionConflict");
            else if (exit.PositionId is not null && d.Entry?.PositionId is not null) missing.Add("SuppliedPositionIdsMatch_NotIndependentlyVerified");
            if (exit.DocumentedUtc is null && exit.TimePrecision is "SourceReportedSecond" or "ExactNativeRecord") missing.Add("ExitUtcMappingMissing_OriginalPrecisionPreservedInRecord");
            if (exit.DocumentedUtc < d.Entry?.DocumentedUtc && exit.TimePrecision != "Approximate" && d.Entry?.TimePrecision != "Approximate") missing.Add("ExitPrecedesEntry");
        }
        missing.Add(d.Association == "None" ? "EntryExitAssociationUnresolved" : d.Association + "AssociationAsserted_NotIndependentlyVerified");
        missing.AddRange(["OriginalArtifactsNotInspected", "NativeExecutionAndRelationshipUnverified", "HistoricalAvailabilityNotEstablished", "SlRiskTpNotEstablishedByCapture", "EconomicValuationNotImplemented"]);
        if (d.ReportedOutcome is null) missing.Add("ReportedMonetaryOutcomeMissing");
        else d.ReportedOutcome.AddIssues(d, missing);
        if (d.ReportedOutcome is { } report)
        {
            if (outcomeAnchor is null) missing.Add("ReportedOutcomeExecutionAnchorNotSuppliedToProjection");
            else report.AddAnchorIssues(outcomeAnchor.Evidence, missing);
        }
        NasdaqHistoricalTradeLifecycleSummary? lifecycle = null;
        if (d.Scenario == "WickfillScenario2" && d.Direction == "Sell")
        {
            var definition = MoneyWayNasdaqStrategyDefinition.Instance;
            var session = new NasdaqDemoSessionIdentity(definition.StrategyId, definition.Version, new(d.SourceProviderId ?? "documentary-provider-unknown"), new(d.OriginalSymbol), d.TradingDay);
            var e = d.Entry;
            var exact = e?.TimePrecision is "SourceReportedSecond" or "ExactNativeRecord";
            var entry = e is null ? null : new NasdaqHistoricalEntryEvidence(session, "WickfillScenario2", "Sell", "capture-entry", e.SourceReference,
                d.Reviewer, "HumanReview", e.EventKind, d.SourcePlatform, d.SourceProviderId, e.Symbol ?? d.OriginalSymbol,
                e.OriginalTime, e.TimePrecision, exact ? e.DocumentedUtc : null, exact ? e.DocumentedUtc : null,
                e.TimeMappingReference, null, null, e.Price is null ? null : NasdaqSupervisedExecutionEvidenceDraft.Decimal(e.Price), e.PriceUnit, e.DealId)
            { PositionReference = e.PositionId };
            var entries = new NasdaqHistoricalEntryCausalSelector().Select(session, record.RegisteredAtUtc, entry is null ? [] : [entry], []);
            var x = d.Exit;
            var exactExit = x?.TimePrecision is "SourceReportedSecond" or "ExactNativeRecord";
            var exitEvidence = x is null ? null : new NasdaqHistoricalExitEvidence(session, "capture-exit", x.SourceReference, d.Reviewer, "HumanReview", x.EventKind, d.SourcePlatform)
            {
                SourceSymbol = x.Symbol,
                SourceProviderId = d.SourceProviderId,
                OriginalTime = x.OriginalTime,
                OriginalClockDomain = x.ClockDomain,
                TimePrecision = x.DocumentedUtc is null && exactExit ? "Unknown" : x.TimePrecision,
                NominalEventUtc = x.TimePrecision == "Approximate" ? x.DocumentedUtc : null,
                EarliestEventUtc = exactExit ? x.DocumentedUtc : null,
                LatestEventUtc = exactExit ? x.DocumentedUtc : null,
                TimeMappingReference = x.TimeMappingReference,
                ExitPrice = x.Price is null ? null : NasdaqSupervisedExecutionEvidenceDraft.Decimal(x.Price),
                PriceUnit = x.PriceUnit,
                ExecutionReference = x.DealId,
                PositionReference = x.PositionId,
                LinkedEntryReviewId = d.Association == "DocumentarySupported" && e is not null ? "capture-entry" : null,
                AssociationSourceReference = d.AssociationSourceReference,
                DocumentedCloseMechanism = x.CloseMechanism,
                CloseMechanismSourceReference = x.CloseMechanismSource,
                Limitations = "Manually registered assertion; no native authentication or historical availability established."
            };
            var exits = new NasdaqHistoricalExitCausalSelector().Select(session, record.RegisteredAtUtc, exitEvidence is null ? [] : [exitEvidence], entries);
            lifecycle = NasdaqHistoricalTradeLifecycleSummary.FromRules([]) with
            {
                Path = "WickfillScenario2",
                HistoricalEntry = entries,
                HistoricalExit = exits,
                SupervisedExecutionRecord = new(record.RecordId, record.Revision, hash)
            };
        }
        return new(record, hash, missing.AsReadOnly(), lifecycle);
    }
}
