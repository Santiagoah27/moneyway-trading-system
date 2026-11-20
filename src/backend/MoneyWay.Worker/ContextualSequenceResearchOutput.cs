using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal static class ContextualSequenceResearchOutput
{
    // Store source evidence once and preserve all per-configuration associations by identity.
    public static object Project(M5ContextualSequenceReport report) => new
    {
        report.CandidateId,
        report.Version,
        report.SessionId,
        report.AsOfUtc,
        report.SourceProvenance,
        report.HistoricalAvailability,
        report.OperationalSignal,
        report.CanonicalAdmissionPermitted,
        report.UnavailableLiquidityReferences,
        report.ConfirmationAssociations,
        Episodes = report.Results.Select(r => r.Episode).DistinctBy(e => e.Id),
        ScEvidence = report.Results.SelectMany(r => r.ScEvidence).DistinctBy(e => e.ComparisonId),
        Zones = report.Results.SelectMany(r => r.FvgDiagnostics.Select(f => f.Zone)
            .Concat(r.Confirmations.Select(c => c.Event.OriginZone).OfType<M5SequenceZone>())).DistinctBy(z => z.Formation.Id),
        ConfirmationEvidence = report.Results.SelectMany(r => r.Confirmations.Select(c => c.Event)).DistinctBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.Direction,
                e.ConfirmedAtUtc,
                e.CandidateId,
                e.Configuration,
                e.OriginatingObservationId,
                e.SourceReference,
                e.EvidenceQuality,
                e.Verified,
                e.EvidenceCandle,
                e.FrozenStructuralReference,
                OriginZoneId = e.OriginZone?.Formation.Id
            }),
        Results = report.Results.Select(r => new
        {
            EpisodeId = r.Episode.Id,
            r.Configuration,
            r.WindowOpenUtc,
            r.WindowCloseUtc,
            r.Direction,
            r.SweepReason,
            Confirmations = r.Confirmations.Select(c => new { EventId = c.Event.Id, c.DirectionCompatible, c.WithinWindow, c.Reason, c.Eligible, c.StateAtCutoff }),
            r.ChronologicalFirstIds,
            r.ChronologicalFirstUtc,
            r.FirstOutcome,
            r.FirstEligibleIds,
            r.FirstEligibleUtc,
            FvgDiagnostics = r.FvgDiagnostics.Select(Fvg),
            FirstFvgs = r.FirstFvgs.Select(Fvg),
            r.CompletedAtUtc,
            r.HistoricallyCompleted,
            r.SweepToConfirmation,
            r.ConfirmationToFvg,
            r.Lifecycle,
            r.SourceGaps,
            ScEvidenceIds = r.ScEvidence.Select(e => e.ComparisonId)
        })
    };

    private static object Fvg(M5SequenceFvgDiagnostic diagnostic) => new
    {
        diagnostic.ConfirmationId,
        ZoneId = diagnostic.Zone.Formation.Id,
        diagnostic.Reason,
        diagnostic.Eligible,
        diagnostic.ZoneStateAtFormation,
        diagnostic.ZoneStateAtCutoff,
        diagnostic.TriggerStateAtFormation
    };
}
