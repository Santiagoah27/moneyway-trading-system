using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum NasdaqM1AnchorMode { FvgAnchored, TriggerAnchored }

public sealed record NasdaqOptionalFvgEvidence(M5SequenceFvgDiagnostic Evidence, string RelationToRealignment,
    string? StateAtRealignment);

public sealed record NasdaqTriggerPathEvidence(M5SequenceConfirmation Confirmation, string MarketEventId,
    string ValidityAtCutoff, string? ValidityAtRealignment, string OptionalFvgStatus,
    IReadOnlyList<NasdaqOptionalFvgEvidence> OptionalFvgs);

public sealed record NasdaqM1Path(NasdaqM1AnchorMode AnchorMode, string Outcome, string? ScanId, DateTimeOffset? AnchorUtc,
    string? FirstPullbackId, string? FirstRealignmentId, DateTimeOffset? FirstRealignmentUtc, string? MarketRealignmentId,
    IReadOnlyList<NasdaqTriggerPathEvidence> Triggers);

public sealed record NasdaqM1PathComparison(string EpisodeId, M5SequenceConfiguration Configuration, M1RealignmentProfile Profile,
    bool PreWindowSweep, M5FirstConfirmation FirstOutcome, string SweepReason, string TriggerOrdering,
    NasdaqM1Path FvgAnchored, NasdaqM1Path TriggerAnchored)
{
    public TimeSpan? AnchorAdvance => FvgAnchored.AnchorUtc - TriggerAnchored.AnchorUtc;
}

public sealed record NasdaqPathCounts(int DistinctTakes, int DistinctTakeCandles, int DistinctObservedSc,
    int DistinctObservedIfvg, int DistinctEligibleSc, int DistinctEligibleIfvg, int DistinctFirstTriggers,
    int DistinctFirstTriggersWithoutEligibleFvg, int FirstTriggerAssociationsWithoutEligibleFvg,
    int FvgAnchoredAssociations, int TriggerAnchoredAssociations, int DistinctFvgAnchoredRealignments,
    int DistinctTriggerAnchoredRealignments, int DistinctSharedRealignments, int DistinctTriggerOnlyRealignments,
    int MatchedSharedAssociations, int TriggerOnlyAssociations,
    int InvalidatedIfvgAtControlRealignmentAssociations, int InvalidatedIfvgAtTriggerRealignmentAssociations,
    int InvalidatedFvgAtControlRealignmentAssociations, int InvalidatedOptionalFvgAtTriggerRealignmentAssociations,
    int AmbiguousComparisons, int ComparisonsWithM5Gaps,
    int ComparisonsWithM1Gaps);

public sealed record NasdaqPathPolicyCounts(string ConfigurationId, bool PreWindowSweep, NasdaqPathCounts Counts);

public sealed record NasdaqOptionalFvgPathReport(string DatasetId, DateOnly OperatingDate, string Provider, string Instrument,
    string SourceProvenance, NasdaqResearchReplayCutoff ReplayCutoff, NasdaqTradingWindowComparison Window,
    NasdaqResearchChainOutcome BaselineChainOutcome, int BaselineCompleteH4ToM1Associations,
    string InputStatus, IReadOnlyList<M5SequenceResult> M5Evidence,
    IReadOnlyList<M5SequenceAssociation> ConfirmationAssociations, IReadOnlyList<string> UnavailableLiquidityReferences,
    IReadOnlyList<M1RealignmentScan> Scans, IReadOnlyList<NasdaqM1PathComparison> Comparisons,
    NasdaqPathCounts Summary, IReadOnlyList<NasdaqPathPolicyCounts> ByPolicy)
{
    public string CandidateId => "EXP-NQ-M5-TO-M1-OPTIONAL-FVG-PATH-V2";
    public string Version => "2.0.0";
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public string H4Authorization => "CanonicalH4AuthorizationNotEstablished_ConditionalDocumentaryComparison";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

public sealed class CompareNasdaqOptionalFvgPathsUseCase
{
    public NasdaqOptionalFvgPathReport Execute(NasdaqResearchReplayRequest request)
    {
        var baseline = new EvaluateNasdaqEndToEndResearchUseCase().Execute(request, out var m1);
        var sequence = baseline.Sequence;
        var control = baseline.Realignment;
        var report = Compare(sequence, control, m1);
        return new(baseline.DatasetId, baseline.OperatingDate, baseline.Provider, baseline.Instrument,
            baseline.SourceProvenance, baseline.ReplayCutoff, baseline.Window, baseline.Outcome,
            baseline.Summary.CompleteH4ToM1Associations, sequence is null ? "AwaitingResearchFrame" : "Compared",
            sequence?.Results ?? [], sequence?.ConfirmationAssociations ?? [], sequence?.UnavailableLiquidityReferences ?? [],
            report.Scans, report.Comparisons, report.Summary, report.ByPolicy);
    }

    internal static (IReadOnlyList<M1RealignmentScan> Scans, IReadOnlyList<NasdaqM1PathComparison> Comparisons,
        NasdaqPathCounts Summary, IReadOnlyList<NasdaqPathPolicyCounts> ByPolicy) Compare(
        M5ContextualSequenceReport? sequence, M1RealignmentReport? control, CandleSeries m1)
    {
        var scans = new SortedDictionary<string, M1RealignmentScan>(StringComparer.Ordinal);
        var comparisons = new List<NasdaqM1PathComparison>();
        var evidence = sequence?.Results ?? [];
        if (sequence is not null && control is not null)
        {
            foreach (var scan in control.Scans) scans.Add(scan.Id, scan);
            var controls = control.Associations.ToDictionary(a => (a.EpisodeId, a.M5Configuration.Id, a.Profile));
            foreach (var row in evidence)
            {
                if (row.Confirmations.Select(c => c.Event.Id).Distinct(StringComparer.Ordinal).Count() != row.Confirmations.Count)
                    throw new ArgumentException("Duplicate M5 confirmation identity within an episode and configuration.", nameof(sequence));
                var first = row.Confirmations.Where(c => c.Eligible && row.FirstEligibleIds.Contains(c.Event.Id))
                    .Select(c => c.Event).ToArray();
                var ordering = row.FirstOutcome switch
                {
                    M5FirstConfirmation.SimultaneousConfirmation => "SimultaneousScAndIfvg",
                    M5FirstConfirmation.AmbiguousSweepOrdering => "AmbiguousSweepOrdering",
                    _ when first.Select(MarketTriggerId).Distinct(StringComparer.Ordinal).Skip(1).Any() => "TiedDistinctEvents",
                    _ when first.Length > 1 => "TiedHypotheses",
                    _ when first.Length == 1 => "Unique",
                    _ => "NoEligibleTrigger"
                };
                foreach (var profile in Enum.GetValues<M1RealignmentProfile>())
                {
                    var existing = controls[(row.Episode.Id, row.Configuration.Id, profile)];
                    var fvgScan = existing.ScanId is { } id ? scans[id] : null;
                    M1RealignmentScan? triggerScan = null;
                    // Retain every tied input. The scan is shared geometry and provides no winner.
                    if (row.FirstEligibleUtc is { } at && first.Length > 0 && row.Direction is { } direction)
                    {
                        var anchor = new M1ResearchAnchor(row.Episode.OperatingDate, at, direction,
                            row.WindowOpenUtc, row.WindowCloseUtc);
                        var key = $"{anchor.Id}/{profile}";
                        if (!scans.TryGetValue(key, out triggerScan))
                            scans.Add(key, triggerScan = EvaluateM1RealignmentCandidateUseCase.Scan(anchor, profile,
                                row.AsOfUtc, m1.Candles));
                    }
                    var controlTriggers = first.Where(c => existing.Bindings.Any(b => b.ConfirmationId == c.Id)).ToArray();
                    comparisons.Add(new(row.Episode.Id, row.Configuration, profile,
                        row.Episode.Selection.Event?.AvailableAtUtc < row.WindowOpenUtc, row.FirstOutcome, row.SweepReason, ordering,
                        Path(row, NasdaqM1AnchorMode.FvgAnchored, fvgScan, existing.Outcome, controlTriggers),
                        Path(row, NasdaqM1AnchorMode.TriggerAnchored, triggerScan, row.FirstOutcome.ToString(), first)));
                }
            }
        }

        var byEpisodeConfiguration = comparisons.ToLookup(c => (c.EpisodeId, c.Configuration.Id));
        var groups = evidence.GroupBy(row => (row.Configuration.Id,
                PreWindow: row.Episode.Selection.Event?.AvailableAtUtc < row.WindowOpenUtc))
            .OrderBy(g => g.Key.Id, StringComparer.Ordinal).ThenBy(g => g.Key.PreWindow)
            .Select(g => new NasdaqPathPolicyCounts(g.Key.Id, g.Key.PreWindow,
                Summarize(g, g.SelectMany(r => byEpisodeConfiguration[(r.Episode.Id, r.Configuration.Id)]).ToArray(), scans)))
            .ToArray();
        return (Array.AsReadOnly(scans.Values.ToArray()), comparisons.AsReadOnly(),
            Summarize(evidence, comparisons, scans), Array.AsReadOnly(groups));
    }

    private static NasdaqM1Path Path(M5SequenceResult row, NasdaqM1AnchorMode mode, M1RealignmentScan? scan,
        string pendingOutcome, IReadOnlyList<M5SequenceConfirmation> confirmations)
    {
        var realignment = scan?.FirstRealignmentUtc;
        var firstAttempt = scan?.Attempts.FirstOrDefault();
        var triggerEvidence = confirmations.Select(trigger =>
        {
            var eligibleFvgs = mode == NasdaqM1AnchorMode.FvgAnchored ? row.FirstFvgs : row.FvgDiagnostics;
            var fvgs = eligibleFvgs.Where(f => f.Eligible && f.ConfirmationId == trigger.Id)
                .Select(f =>
                {
                    var formed = f.Zone.Formation.FormationUtc;
                    var relation = realignment is null ? "NoRealignment" : formed < realignment ? "BeforeRealignment" :
                        formed == realignment ? "SameCloseOrderingUnresolved" : "AfterRealignment";
                    var state = formed <= realignment
                        ? new M1M5ZoneEvidence(f.Zone.Formation.Id, "DirectionalFVG", f.Zone.InversionUtc,
                            f.Zone.EvidenceLimitedAtUtc).StateAt(realignment!.Value) : null;
                    return new NasdaqOptionalFvgEvidence(f, relation, state);
                }).ToArray();
            var supportCutoff = realignment ?? row.AsOfUtc;
            var fvgStatus = fvgs.Any(f => f.RelationToRealignment == "BeforeRealignment" &&
                                          f.StateAtRealignment == "NotInvalidatedWithinObservableEvidence")
                ? "ObservableBeforeRealignment"
                : fvgs.Any(f => f.Evidence.Zone.Formation.FormationUtc <= supportCutoff)
                    ? "CandidateObserved_SeeTimingAndLifecycle"
                    : HasGap(row, trigger.ConfirmedAtUtc, supportCutoff) ? "AbsenceUndeterminedDueToM5Gap"
                    : "NoEligibleFvgObservedByAssessmentTime";
            return new NasdaqTriggerPathEvidence(trigger, MarketTriggerId(trigger), Validity(row, trigger, row.AsOfUtc),
                realignment is { } t ? Validity(row, trigger, t) : null, fvgStatus, Array.AsReadOnly(fvgs));
        }).ToArray();
        return new(mode, scan?.Outcome ?? pendingOutcome, scan?.Id, scan?.Anchor.AtUtc, firstAttempt?.Id,
            realignment is null ? null : scan!.FirstRealignmentIds.Single(), realignment,
            realignment is { } t ? FormattableString.Invariant($"{scan!.Anchor.Direction}/{t:O}") : null,
            Array.AsReadOnly(triggerEvidence));
    }

    private static string Validity(M5SequenceResult row, M5SequenceConfirmation trigger, DateTimeOffset at)
    {
        if (trigger.OriginZone is { } zone && zone.InvalidationUtc <= at) return "Invalidated";
        if (trigger.OriginZone?.EvidenceLimitedAtUtc <= at || HasGap(row, trigger.ConfirmedAtUtc, at))
            return "UnknownDueToMissingM5Evidence";
        return trigger.Kind == M5ConfirmationKind.StructuralChange ? "ScInvalidationUndefined" :
            "NotInvalidatedWithinObservableEvidence";
    }

    private static bool HasGap(M5SequenceResult row, DateTimeOffset from, DateTimeOffset to) =>
        row.SourceGaps.Any(g => g.ClosureEvidence is null && g.ObservedAtUtc <= to && g.EndUtc > from && g.StartUtc < to);

    private static string MarketTriggerId(M5SequenceConfirmation trigger) => trigger.OriginZone is { } zone
        ? FormattableString.Invariant($"IFVG/{trigger.EvidenceCandle.ProviderId.Value}/{trigger.EvidenceCandle.Symbol.Value}/{trigger.Direction}/{zone.Formation.C3.OpenTimeUtc:O}/{trigger.EvidenceCandle.OpenTimeUtc:O}")
        : trigger.AssociationKey;

    private static NasdaqPathCounts Summarize(IEnumerable<M5SequenceResult> rows,
        IReadOnlyList<NasdaqM1PathComparison> comparisons, IReadOnlyDictionary<string, M1RealignmentScan> scans)
    {
        var evidence = rows.ToArray();
        var observed = evidence.SelectMany(r => r.Confirmations).Where(c => c.Event.Verified).ToArray();
        var first = evidence.SelectMany(r => r.Confirmations.Where(c => c.Eligible && r.FirstEligibleIds.Contains(c.Event.Id))
            .Select(c => (Row: r, c.Event))).ToArray();
        var noFvg = first.Where(c => !c.Row.FvgDiagnostics.Any(f => f.Eligible && f.ConfirmationId == c.Event.Id)).ToArray();
        var control = comparisons.Select(c => c.FvgAnchored.MarketRealignmentId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var trigger = comparisons.Select(c => c.TriggerAnchored.MarketRealignmentId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var takes = evidence.Select(r => r.Episode.Selection.Event).OfType<ContextualLiquidityObservation>().ToArray();
        return new(takes.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count(),
            takes.Select(e => e.Candle.OpenTimeUtc).Distinct().Count(),
            Count(observed.Where(c => c.Event.Kind == M5ConfirmationKind.StructuralChange).Select(c => c.Event)),
            Count(observed.Where(c => c.Event.Kind == M5ConfirmationKind.IFVG).Select(c => c.Event)),
            Count(observed.Where(c => c.Eligible && c.Event.Kind == M5ConfirmationKind.StructuralChange).Select(c => c.Event)),
            Count(observed.Where(c => c.Eligible && c.Event.Kind == M5ConfirmationKind.IFVG).Select(c => c.Event)),
            Count(first.Select(c => c.Event)),
            Count(noFvg.Select(c => c.Event)), noFvg.Length,
            comparisons.Count(c => c.FvgAnchored.MarketRealignmentId is not null),
            comparisons.Count(c => c.TriggerAnchored.MarketRealignmentId is not null),
            control.Count, trigger.Count, trigger.Count(control.Contains), trigger.Count(id => !control.Contains(id)),
            comparisons.Count(c => c.TriggerAnchored.MarketRealignmentId is { } id && id == c.FvgAnchored.MarketRealignmentId),
            comparisons.Count(c => c.TriggerAnchored.MarketRealignmentId is { } id && id != c.FvgAnchored.MarketRealignmentId),
            comparisons.Count(c => IfvgInvalidated(c.FvgAnchored)), comparisons.Count(c => IfvgInvalidated(c.TriggerAnchored)),
            comparisons.Count(c => FvgInvalidated(c.FvgAnchored)), comparisons.Count(c => FvgInvalidated(c.TriggerAnchored)),
            comparisons.Count(c => c.TriggerOrdering is not ("NoEligibleTrigger" or "Unique")),
            evidence.Count(r => r.SourceGaps.Any(g => g.ClosureEvidence is null)) * Enum.GetValues<M1RealignmentProfile>().Length,
            comparisons.Count(c => new[] { c.FvgAnchored.ScanId, c.TriggerAnchored.ScanId }.OfType<string>()
                .Any(id => scans[id].SourceGaps.Count > 0)));

        static int Count(IEnumerable<M5SequenceConfirmation> events) =>
            events.Select(MarketTriggerId).Distinct(StringComparer.Ordinal).Count();
        static bool IfvgInvalidated(NasdaqM1Path path) => path.Triggers.Any(t => t.ValidityAtRealignment == "Invalidated");
        static bool FvgInvalidated(NasdaqM1Path path) => path.Triggers.SelectMany(t => t.OptionalFvgs)
            .Any(f => f.StateAtRealignment == "Invalidated");
    }
}
