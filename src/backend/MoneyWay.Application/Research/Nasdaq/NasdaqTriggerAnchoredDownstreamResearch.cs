using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record NasdaqDownstreamZone(M5FvgFormation Formation, DateTimeOffset AvailableAtUtc,
    DateTimeOffset? InversionUtc, DateTimeOffset? InvalidationUtc, DateTimeOffset? EvidenceLimitedAtUtc,
    string? RelationToRealignment, string? StateAtRealignment);

public sealed record NasdaqDownstreamTrigger(string ConfirmationId, string MarketEventId, M5ConfirmationKind Kind,
    M5FvgDirection Direction, DateTimeOffset ConfirmedAtUtc, Candle ConfirmationCandle,
    string ValidityAtAssessment, string ValidityAtReportCutoff, DateTimeOffset? OriginInvalidationAtReportCutoff,
    ContextualStructuralSelection? FrozenStructuralReference, NasdaqDownstreamZone? OriginZone,
    string OptionalFvgStatus, IReadOnlyList<NasdaqDownstreamZone> OptionalFvgs,
    IReadOnlyList<NasdaqDownstreamZone> LaterOptionalFvgs);

public sealed record NasdaqDownstreamGeometry(IReadOnlyList<M5StructuralStopObservation> Stops,
    IReadOnlyList<StructuralTargetEvaluation> Targets)
{
    public bool StructuralSlCandidateAvailable => Stops.Any(s => s.OnProtectiveSide == true);
    public bool StructuralTpCandidateAvailable => Targets.Any(t => t.SelectedIds.Count > 0);
}

public sealed record NasdaqDownstreamPath(string EpisodeId, M5SequenceConfiguration Configuration, M1RealignmentProfile Profile,
    ContextualLiquidityObservation? LiquidityTake, string SweepReason, string TriggerOrdering, string M1Outcome,
    DateTimeOffset AssessmentUtc, IReadOnlyList<NasdaqDownstreamTrigger> Triggers, M1CountertrendAttempt? Pullback,
    string? ScanId, string? MarketRealignmentId, string? FvgAnchoredMarketRealignmentId,
    IReadOnlyList<M5FvgSourceGap> M5SourceGaps, IReadOnlyList<M1CoverageGap> M1SourceGaps,
    bool StructuralSlCandidateAvailable, bool StructuralTpCandidateAvailable)
{
    public bool M5TriggerObserved => Triggers.Count > 0;
    public bool M1PullbackObserved => Pullback is not null;
    public bool M1RealignmentObserved => MarketRealignmentId is not null;
}

public sealed record NasdaqDownstreamEventCount(int DistinctM5Triggers, int DistinctM1Realignments);
public sealed record NasdaqDownstreamSummary(NasdaqDownstreamEventCount Realignment, NasdaqDownstreamEventCount StructuralSl,
    NasdaqDownstreamEventCount StructuralTp, NasdaqDownstreamEventCount Both,
    IReadOnlyList<string> SharedM1WithFvgAnchored, IReadOnlyList<string> TriggerOnlyM1,
    int NoM5TriggerAssociations, int TriggerWithoutRealignmentAssociations, int RealignmentWithoutSlAssociations,
    int RealignmentWithoutTpAssociations);

public sealed record NasdaqTriggerAnchoredDownstreamReport(string DatasetId, DateOnly OperatingDate, string Provider,
    string Instrument, string SourceProvenance, NasdaqResearchReplayCutoff ReplayCutoff, NasdaqTradingWindowComparison Window,
    NasdaqResearchChainOutcome BaselineOutcome, IReadOnlyList<NasdaqResearchPreparationProfile> H4Profiles,
    NasdaqPathCounts V2Summary, IReadOnlyList<NasdaqDownstreamPath> Paths,
    IReadOnlyDictionary<string, NasdaqDownstreamGeometry> GeometryByMarketRealignmentId, NasdaqDownstreamSummary Summary)
{
    public string CandidateId => "EXP-NQ-TRIGGER-ANCHORED-DOWNSTREAM-RESEARCH-V1";
    public string Version => "1.0.0";
    public bool H4ContextAvailable => H4Profiles.Any(p => p.ContextAvailable);
    public bool IndependentHumanValidation => false;
    public bool AuthenticatedTemporalAvailability => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Projects the V2 trigger path into the existing structural candidates at each first M1 close.</summary>
public sealed class EvaluateNasdaqTriggerAnchoredDownstreamUseCase
{
    public NasdaqTriggerAnchoredDownstreamReport Execute(NasdaqResearchReplayRequest request)
    {
        var upstream = new CompareNasdaqOptionalFvgPathsUseCase().Execute(request, out var baseline);
        var m5 = request.OriginalSources.Single(s => s.Timeframe == new Timeframe(5, TimeframeUnit.Minute));
        var h1 = request.OriginalSources.Single(s => s.Timeframe == new Timeframe(1, TimeframeUnit.Hour));
        return Project(upstream, baseline.Preparation.Profiles, m5, h1);
    }

    internal static NasdaqTriggerAnchoredDownstreamReport Project(NasdaqOptionalFvgPathReport upstream,
        IReadOnlyList<NasdaqResearchPreparationProfile> h4, CandleSeries m5, CandleSeries h1)
    {
        var scans = upstream.Scans.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var m5Rows = upstream.M5Evidence.ToDictionary(s => (s.Episode.Id, s.Configuration.Id));
        var geometry = new SortedDictionary<string, NasdaqDownstreamGeometry>(StringComparer.Ordinal);
        var paths = new List<NasdaqDownstreamPath>();
        var stop = new EvaluateM5StructuralStopLossCandidateUseCase();
        var sessionLiquidity = new NasdaqSessionLiquidityCalculator();

        foreach (var comparison in upstream.Comparisons)
        {
            var row = m5Rows[(comparison.EpisodeId, comparison.Configuration.Id)];
            var path = comparison.TriggerAnchored;
            var scan = path.ScanId is { } scanId ? scans[scanId] : null;
            var attemptId = path.FirstRealignmentId ?? path.FirstPullbackId;
            var attempt = attemptId is null ? null : scan!.Attempts.Single(a => a.Id == attemptId);
            var assessment = path.FirstRealignmentUtc ?? upstream.ReplayCutoff.RequestedUtc;
            NasdaqDownstreamGeometry? references = null;
            if (path.MarketRealignmentId is { } id)
            {
                if (attempt?.Realignment is not { } candle || candle.CloseTimeUtc != assessment ||
                    assessment > upstream.ReplayCutoff.RequestedUtc || !attempt.WindowEligible ||
                    !scan!.FirstRealignmentIds.Contains(attempt.Id))
                    throw new ArgumentException("Downstream geometry requires the exact observable first M1 realignment.", nameof(upstream));
                if (!geometry.TryGetValue(id, out references))
                {
                    var direction = scan.Anchor.Direction;
                    var stops = M5StructuralPivotProfile.Initial
                        .Select(profile => stop.Execute(m5, candle, direction, profile, upstream.SourceProvenance)).ToArray();
                    var targets = stops.Select(s => NasdaqStructuralTakeProfitCandidate.Swings(s, m5, []))
                        .Append(NasdaqStructuralTakeProfitCandidate.Sessions(new(candle), direction, m5,
                            sessionLiquidity.CalculateExplicitSessions(h1, scan.Anchor.OperatingDate, assessment),
                            upstream.SourceProvenance, [])).ToArray();
                    geometry.Add(id, references = new(Array.AsReadOnly(stops), Array.AsReadOnly(targets)));
                }
            }

            var triggers = path.Triggers.Select(t => Trigger(t, assessment, upstream.ReplayCutoff.RequestedUtc)).ToArray();
            paths.Add(new(comparison.EpisodeId, comparison.Configuration, comparison.Profile, row.Episode.Selection.Event,
                comparison.SweepReason, comparison.TriggerOrdering, path.Outcome, assessment, Array.AsReadOnly(triggers),
                attempt, scan?.Id, path.MarketRealignmentId, comparison.FvgAnchored.MarketRealignmentId,
                Array.AsReadOnly(row.SourceGaps.Where(g => g.ObservedAtUtc <= assessment)
                    .Select(g => g.EndUtc > assessment ? g with { EndUtc = assessment } : g).ToArray()),
                Array.AsReadOnly(scan?.SourceGaps.Where(g => g.ObservableAtUtc <= assessment)
                    .Select(g => g.EndUtc > assessment ? g with { EndUtc = assessment } : g).ToArray() ?? []),
                references?.StructuralSlCandidateAvailable == true, references?.StructuralTpCandidateAvailable == true));
        }

        var realigned = paths.Where(p => p.M1RealignmentObserved).ToArray();
        var shared = upstream.Comparisons.Select(c => c.FvgAnchored.MarketRealignmentId).OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var summary = new NasdaqDownstreamSummary(Count(realigned),
            Count(realigned.Where(p => p.StructuralSlCandidateAvailable)), Count(realigned.Where(p => p.StructuralTpCandidateAvailable)),
            Count(realigned.Where(p => p.StructuralSlCandidateAvailable && p.StructuralTpCandidateAvailable)),
            Array.AsReadOnly(geometry.Keys.Where(shared.Contains).ToArray()),
            Array.AsReadOnly(geometry.Keys.Where(id => !shared.Contains(id)).ToArray()),
            paths.Count(p => !p.M5TriggerObserved), paths.Count(p => p.M5TriggerObserved && !p.M1RealignmentObserved),
            realigned.Count(p => !p.StructuralSlCandidateAvailable), realigned.Count(p => !p.StructuralTpCandidateAvailable));
        return new(upstream.DatasetId, upstream.OperatingDate, upstream.Provider, upstream.Instrument,
            upstream.SourceProvenance, upstream.ReplayCutoff, upstream.Window, upstream.BaselineChainOutcome, h4,
            upstream.Summary, paths.AsReadOnly(), new ReadOnlyDictionary<string, NasdaqDownstreamGeometry>(geometry), summary);
    }

    private static NasdaqDownstreamEventCount Count(IEnumerable<NasdaqDownstreamPath> paths)
    {
        var rows = paths.ToArray();
        return new(rows.SelectMany(p => p.Triggers).Select(t => t.MarketEventId).Distinct(StringComparer.Ordinal).Count(),
            rows.Select(p => p.MarketRealignmentId).Distinct(StringComparer.Ordinal).Count());
    }

    private static NasdaqDownstreamTrigger Trigger(NasdaqTriggerPathEvidence evidence, DateTimeOffset assessment, DateTimeOffset cutoff)
    {
        var confirmation = evidence.Confirmation;
        var origin = confirmation.OriginZone;
        var observed = evidence.OptionalFvgs.Where(f => f.Evidence.Zone.Formation.FormationUtc <= assessment)
            .Select(f => Snapshot(f.Evidence.Zone, assessment, f.RelationToRealignment, f.StateAtRealignment)).ToArray();
        var later = evidence.OptionalFvgs.Where(f => f.Evidence.Zone.Formation.FormationUtc > assessment &&
                f.Evidence.Zone.Formation.FormationUtc <= cutoff)
            .Select(f => Snapshot(f.Evidence.Zone, cutoff, f.RelationToRealignment, null)).ToArray();
        return new(confirmation.Id, evidence.MarketEventId, confirmation.Kind, confirmation.Direction,
            confirmation.ConfirmedAtUtc, confirmation.EvidenceCandle, evidence.ValidityAtRealignment ?? evidence.ValidityAtCutoff,
            evidence.ValidityAtCutoff, AtOrBefore(origin?.InvalidationUtc, cutoff), confirmation.FrozenStructuralReference,
            origin is null ? null : Snapshot(origin, assessment, null, null), evidence.OptionalFvgStatus,
            Array.AsReadOnly(observed), Array.AsReadOnly(later));
    }

    private static NasdaqDownstreamZone Snapshot(M5SequenceZone zone, DateTimeOffset at, string? relation, string? state) =>
        new(zone.Formation, zone.Formation.FormationUtc, AtOrBefore(zone.InversionUtc, at), AtOrBefore(zone.InvalidationUtc, at),
            AtOrBefore(zone.EvidenceLimitedAtUtc, at), relation, state);

    private static DateTimeOffset? AtOrBefore(DateTimeOffset? value, DateTimeOffset cutoff) => value <= cutoff ? value : null;
}
