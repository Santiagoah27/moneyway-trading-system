using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum H4ContinuityValidity { ContextValidityUnresolved, TrackedBreakoutInvalidated }
public enum H4CloseRelation { BelowReference, AtReference, AboveReference }

public sealed record H4ContextFollowUp(Candle Candle, H4CloseRelation CloseRelation);

public sealed record H4ContextContinuityEvidence(H4CausalContextEvent Event, bool AtLatestClosedCandle,
    H4ConfirmedPivot? LatestConfirmedSameSideReference, IReadOnlyList<H4ContextFollowUp> SubsequentCloses,
    IReadOnlyList<H4CausalContextEvent> LaterOpposingEvents, IReadOnlyList<H4SourceGap> SubsequentSourceGaps,
    IReadOnlyList<H4DelayedObservation> TrackedBreakoutHistory, H4ContinuityValidity Validity)
{
    public bool HumanValidationRequired => Validity == H4ContinuityValidity.ContextValidityUnresolved;
}

public sealed record H4ContextContinuityProfile(string ConfigurationId, H4PivotProfile PivotProfile, int Horizon,
    H4CausalStructureState? StructureState, H4ResearchDirection? ConfirmedStructureDirection,
    H4CausalContextState? V2ContextState, string V2Reason, H4ResearchDirection? V2ContextAlignedDirection,
    H4CandleContext? LatestClosedCandle, IReadOnlyList<H4CausalReference> ProvisionalReferences,
    IReadOnlyList<H4SourceGap> SourceGaps,
    IReadOnlyList<H4ContextContinuityEvidence> EventHistory, int V2CompleteAssociations)
{
    public IReadOnlyList<H4ContextContinuityEvidence> LatestPreviouslyObservedEvents => EventHistory
        .Where(e => !e.AtLatestClosedCandle).GroupBy(e => e.Event.ObservedAtUtc).OrderBy(g => g.Key)
        .LastOrDefault()?.ToArray() ?? [];

    public IReadOnlyList<H4ContextContinuityEvidence> EventsAtLatestClose => EventHistory.Where(e => e.AtLatestClosedCandle).ToArray();
}

public sealed record NasdaqH4ContextContinuityReport(string DatasetId, DateOnly OperatingDate, string Provider,
    string Instrument, string SourceProvenance, DateTimeOffset PreparationUtc, NasdaqResearchReplayCutoff ReportCutoff,
    NasdaqResearchReplayCutoff? PreparationCutoff, IReadOnlyList<H4ContextContinuityProfile> Profiles,
    int V2DistinctCompletedM1Events)
{
    public string CandidateId => "EXP-NQ-H4-CONTEXT-CONTINUITY-V3";
    public string Version => "3.0.0";
    public string BaselineCandidateId => H4CausalContextObservation.ResearchId;
    public string BaselineVersion => H4CausalContextObservation.ResearchVersion;
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public string ChainAssessment => "V2ReportedWithoutV3Reclassification";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Projects bounded V2 event evidence without assigning a session direction or an expiry.</summary>
public sealed class EvaluateH4ContextContinuityUseCase
{
    public NasdaqH4ContextContinuityReport Execute(NasdaqH4CausalContextComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        var profiles = comparison.Profiles.Select(profile =>
        {
            var evidence = profile.V2;
            return new H4ContextContinuityProfile(profile.ConfigurationId, profile.PivotProfile, profile.Horizon,
                evidence?.StructureState, evidence?.ConfirmedStructureDirection, evidence?.ContextState,
                evidence?.ContextReason ?? profile.V1GateReason, evidence?.ContextAlignedDirection,
                evidence?.LatestClosedCandle, evidence?.ProvisionalReferences ?? [], evidence?.StructureEvidence.SourceGaps ?? [],
                evidence is null ? [] : ProjectEvents(evidence), profile.V2CompleteAssociations);
        }).ToArray();
        return new(comparison.DatasetId, comparison.OperatingDate, comparison.Provider, comparison.Instrument,
            comparison.SourceProvenance, comparison.PreparationUtc, comparison.ReportCutoff, comparison.PreparationCutoff,
            Array.AsReadOnly(profiles), comparison.V2DistinctCompletedM1Events);
    }

    public IReadOnlyList<H4ContextContinuityEvidence> ProjectEvents(H4CausalContextObservation evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var events = evidence.ContextEvents.Where(e => e.ObservedAtUtc <= evidence.AsOfUtc).ToArray();
        return Array.AsReadOnly(events.Select(e =>
        {
            var reference = e.Reference.Kind == H4PivotKind.High ? evidence.StructureEvidence.CurrentHigh :
                evidence.StructureEvidence.CurrentLow;
            var followUps = evidence.ClosedSourceCandles.Where(c => c.CloseTimeUtc > e.ObservedAtUtc && c.CloseTimeUtc <= evidence.AsOfUtc)
                .Select(c => new H4ContextFollowUp(c, c.Close < e.Reference.Price ? H4CloseRelation.BelowReference :
                    c.Close > e.Reference.Price ? H4CloseRelation.AboveReference : H4CloseRelation.AtReference)).ToArray();
            var tracked = e.Kind == H4CausalContextKind.Breakout ? evidence.DelayedEvidence.Where(d =>
                d.Reference.CandidateId == e.Reference.CandidateId && d.BreakoutOpenUtc == e.ObservationCandle.OpenTimeUtc &&
                d.BreakoutConfirmedUtc == e.ObservedAtUtc && d.ObservedAtUtc <= evidence.AsOfUtc).ToArray() : [];
            var validity = tracked.Any(d => d.Status == H4DelayedStatus.Invalidated)
                ? H4ContinuityValidity.TrackedBreakoutInvalidated : H4ContinuityValidity.ContextValidityUnresolved;
            var opposing = events.Where(later => later.ObservedAtUtc > e.ObservedAtUtc && later.Direction != e.Direction).ToArray();
            var gaps = evidence.StructureEvidence.SourceGaps.Where(g => g.EndUtc > e.ObservedAtUtc &&
                g.EndUtc <= evidence.AsOfUtc).ToArray();
            return new H4ContextContinuityEvidence(e, e.ObservedAtUtc == evidence.LatestClosedCandle?.ObservedAtUtc,
                reference, Array.AsReadOnly(followUps), Array.AsReadOnly(opposing), Array.AsReadOnly(gaps),
                Array.AsReadOnly(tracked), validity);
        }).ToArray());
    }
}
