using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Assembled view of existing documentary and canonical artifacts, not a second strategy or execution engine.</summary>
public sealed record NasdaqSupervisedHistoricalTradeSnapshot(NasdaqDemoSessionIdentity Session, long FrameStep, DateTimeOffset AsOfUtc,
    string? CanonicalVerdict, IReadOnlyList<NasdaqHistoricalTradeStage> Components, IReadOnlyList<string> MissingEvidence,
    string InstrumentCorrespondence, bool HistoricalEntryObserved, bool CanonicalEligibilityRecordedAtEntry,
    bool StopLossSupported, bool RiskExposureSupported, bool TakeProfitSupported, bool NativeExecutionDocumented,
    bool CanonicalBindingsConsistent, bool DocumentedParameterSetAvailable,
    NasdaqHistoricalEntryAssessment? DocumentaryEntry, NasdaqHistoricalScenario2Stop? StopLoss,
    NasdaqHistoricalScenario2Risk? Risk, NasdaqHistoricalScenario2Target? TakeProfit)
{
    public string Scenario => "WickfillScenario2";
    public string Direction => "Sell";
    public string Scope => "SupervisedHistoricalEvidenceSnapshot";
    public string EntryAuthorization => "NotGrantedBySnapshot";
    public bool NativeExecutionIndependentlyVerified => false;
    public bool ExitSupported => false;
    public NasdaqHistoricalRecordedExecution? Exit => null;
    public bool EconomicOutcomeSupported => false;
    public string? EconomicOutcome => null;
    public bool CompleteSupervisedTrade => false;
    public string FailureClassification => "inconclusive";
}

/// <summary>Stateless composition of the existing lifecycle projection. Preserves independent component support and original entry time.</summary>
public sealed class NasdaqSupervisedHistoricalTradeSnapshotAssembler
{
    public NasdaqSupervisedHistoricalTradeSnapshot Assemble(NasdaqDemoSessionIdentity session, long step, DateTimeOffset asOfUtc,
        string? canonicalVerdict, NasdaqHistoricalTradeLifecycleSummary lifecycle)
    {
        ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(lifecycle);
        if (asOfUtc.Offset != TimeSpan.Zero || step <= 0 || lifecycle.Path == "Standard") throw new ArgumentException("An explicit WF2 diagnostic frame is required.");
        var entry = lifecycle.HistoricalEntry;
        if (entry is not null && (entry.AsOfUtc != asOfUtc || entry.Observations.Any(o => o.Evidence.Session != session)))
            throw new ArgumentException("Entry assessment must belong to the same original session and selected frame.");
        if (entry?.Observations.Any(o => o.Evidence.HistoricalAvailableAtUtc > asOfUtc || o.Evidence.EarliestEventUtc > asOfUtc
                || o.CanonicalAtEntry?.AsOfUtc > asOfUtc) == true)
            throw new ArgumentException("Future documentary evidence or canonical frames cannot be assembled.");
        var observations = entry?.Observations ?? [];
        var conflict = entry?.SelectionStatus == "Conflict" || observations.Any(o => o.Issues.Any(i => i.StartsWith("Conflicting", StringComparison.Ordinal)));
        var observed = !conflict && observations.Any(o => o.Evidence.EventKind != "OrderPlacement");
        var eligibility = !conflict && observations.Any(o => o.CanonicalAtEntry is { HasCompleteRequiredCoverage: true } at
            && at.AsOfUtc <= asOfUtc && at.Rules.SingleOrDefault(r => r.RuleId == "NQ-WF2-002")?.Result == "Passed");
        string? Result(string id) => lifecycle.Stages.SingleOrDefault(s => s.RuleId == id)?.RuleResult;
        bool Binding(NasdaqHistoricalScenario2Binding? b) => b is not null && b.Session == session
            && b.StartStep > 0 && b.StartStep <= step && b.StartAsOfUtc.Offset == TimeSpan.Zero && b.StartAsOfUtc <= asOfUtc;
        bool Visible(DateTimeOffset effective, DateTimeOffset available) => effective.Offset == TimeSpan.Zero
            && available.Offset == TimeSpan.Zero && effective <= available && available <= asOfUtc;
        var sl = lifecycle.Scenario2StopLoss;
        var risk = lifecycle.Scenario2RiskExposure;
        var tp = lifecycle.Scenario2TakeProfit;
        if (sl is not null && !Visible(sl.EffectiveAtUtc, sl.ObservedAtUtc)
            || risk is not null && !Visible(risk.EffectiveAtUtc, risk.ObservedAtUtc)
            || tp is not null && !Visible(tp.EffectiveAtUtc, tp.ObservedAtUtc))
            throw new ArgumentException("Canonical constituents must already be observable at the selected frame.");
        var slSupported = sl is not null && Result("NQ-SL-001") == "Passed" && Binding(sl.Binding)
            && Visible(sl.EffectiveAtUtc, sl.ObservedAtUtc) && sl.StopPrice > sl.ProtectivePrice;
        // Failed risk still documents exposure. Its actual failed comparison remains visible and never becomes approval.
        var riskSupported = risk is not null && Result("NQ-RISK-001") is "Passed" or "Failed" && Binding(risk.Binding)
            && Visible(risk.EffectiveAtUtc, risk.ObservedAtUtc);
        var tpSupported = tp is not null && Result("NQ-TP-001") == "Passed" && Binding(tp.Binding)
            && tp.ProviderId == session.ProviderId.Value && tp.Symbol == session.Symbol.Value && tp.Direction == "Sell"
            && tp.TakeProfitPrice == tp.TargetReferencePrice && Visible(tp.EffectiveAtUtc, tp.ObservedAtUtc);
        var bindings = slSupported && riskSupported && tpSupported && sl!.Binding == risk!.Binding && sl.Binding == tp!.Binding
            && risk.StopPrice == sl.StopPrice && tp.StopPrice == sl.StopPrice;
        var sourceIdentity = observations.Count > 0 && observations.All(o => o.Evidence.SourceProviderId == session.ProviderId.Value
            && o.Evidence.SourceSymbol == session.Symbol.Value);
        var native = !conflict && observations.Any(o => o.Availability == "AvailableAtSelectedFrame"
            && o.NativeExecutionStatus == "NativeRecordDocumented" && o.Evidence.SourceProviderId == session.ProviderId.Value
            && o.Evidence.SourceSymbol == session.Symbol.Value && o.Evidence.HistoricalAvailableAtUtc <= asOfUtc);
        // A documentary execution predating this canonical Start cannot be combined with a later setup's parameters.
        var entryAssociation = native && bindings && observations.Any(o => o.NativeExecutionStatus == "NativeRecordDocumented"
            && o.Availability == "AvailableAtSelectedFrame" && o.CanonicalAtEntry is { } original
            && original.Step >= sl!.Binding!.StartStep && original.AsOfUtc >= sl.Binding.StartAsOfUtc
            && o.Evidence.EarliestEventUtc >= sl.Binding.StartAsOfUtc);
        var correspondence = sourceIdentity && bindings ? "native_identity_matches" : "unresolved";
        var missing = new List<string>();
        if (!observed) missing.Add(conflict ? "ConflictingEntryEvidence" : "HistoricalEntryMissing");
        if (!eligibility) missing.Add("CanonicalEligibilityNotRecordedAtOriginalEntry");
        if (!slSupported) missing.Add("NativeInitialSlEvidenceMissingOrBlocked");
        if (!riskSupported) missing.Add("NativeExposureEvidenceMissingOrBlocked");
        if (!tpSupported) missing.Add("NativeHumanSelectedTpMissingOrBlocked");
        if (!bindings) missing.Add("CanonicalConstituentBindingUnresolved");
        if (correspondence == "unresolved") missing.Add("InstrumentCorrespondenceUnresolved");
        if (!native) missing.Add("NativeExecutionRecordMissingOrUnverified");
        if (!entryAssociation) missing.Add("EntrySetupAssociationUnresolved");
        missing.Add("IndependentNativeExecutionVerificationNotPerformed");
        missing.Add("Wf2DocumentedExitNotIntegrated"); missing.Add("EconomicOutcomeNotImplemented");
        if (observations.Any(o => o.Availability == "RetrospectiveOnly")) missing.Add("RetrospectiveEntryNotAdmitted");
        var components = new[]
        {
            new NasdaqHistoricalTradeStage("Entry", observed ? "RecordedArtifact" : "MissingInput", null, null,
                "Original documentary entry, instrument, price, precision and provenance remain in DocumentaryEntry; this is not an authorized native fill."),
            new("EntryEligibility", eligibility ? "RecordedEvaluation" : "MissingOrBlocked", "NQ-WF2-002", eligibility ? "Passed" : null,
                "Only the frozen original entry-frame WF2 result is displayed. Current/later Passed cannot replace it or authorize execution."),
            Component("StopLoss", "NQ-SL-001", slSupported, "Exact original native SL/reference and provenance required."),
            Component("Risk", "NQ-RISK-001", riskSupported, "Source-documented exposure; recorded comparison is preserved, no size is inferred."),
            Component("TakeProfit", "NQ-TP-001", tpSupported, "Exact human-selected native Low/TP and provenance required."),
            new("Execution", native ? "RecordedArtifact" : "MissingInput", null, null,
                "Documentary native record support is separate from independent verification, strategy authorization and order sending."),
            new("DocumentedExit", "NotImplemented", null, null, "Existing exit types require a Standard snapshot; no WF2 exit is synthesized."),
            new("EconomicOutcome", "NotImplemented", null, null, "No WF2 monetary outcome or loss/violation classification is implemented.")
        };
        NasdaqHistoricalTradeStage Component(string name, string rule, bool supported, string reason) =>
            new(name, supported ? "RecordedArtifact" : "MissingOrBlocked", rule, Result(rule),
                (lifecycle.Stages.SingleOrDefault(s => s.RuleId == rule)?.Reason ?? "No owning-rule evaluation recorded.") + " " + reason);
        return new(session, step, asOfUtc, canonicalVerdict, Array.AsReadOnly(components), Array.AsReadOnly(missing.ToArray()), correspondence,
            observed, eligibility, slSupported, riskSupported, tpSupported, native, bindings,
            observed && native && bindings && entryAssociation && correspondence == "native_identity_matches", entry, sl, risk, tp);
    }

    public NasdaqHistoricalTradeLifecycleSummary Project(NasdaqDemoSessionIdentity session, long step, DateTimeOffset asOfUtc,
        string? verdict, IReadOnlyList<NasdaqHistoricalRuleSummary> rules, NasdaqHistoricalTradeLifecycleSummary lifecycle)
    {
        if (lifecycle.Path == "Standard" || lifecycle.Path != "WickfillScenario2" && lifecycle.HistoricalEntry is null
            && !rules.Any(r => r.RuleId is "NQ-WF2-001" or "NQ-WF2-002" && r.Result == "Passed")) return lifecycle;
        return lifecycle with { SupervisedSnapshot = Assemble(session, step, asOfUtc, verdict, lifecycle) };
    }
}
