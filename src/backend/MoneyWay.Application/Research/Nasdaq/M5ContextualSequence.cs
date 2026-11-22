using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum M5SweepPolicy { StrictAfterSweep, SameCloseDiagnostic }
public enum M5SequenceWindowPolicy { PostSweepObservation, WindowEventOnly, WindowConfirmationWithPriorSweep }
public enum M5SequenceFvgPolicy { FormationAfterConfirmation, FormationSequenceAfterConfirmation }
public enum M5ConfirmationKind { StructuralChange, IFVG }
public enum M5FirstConfirmation
{
    WaitingForConfirmation, StructuralChangeFirst, IFVGFirst, SimultaneousConfirmation, AmbiguousSweepOrdering, InsufficientEvidence
}

public sealed record M5SequenceConfiguration(M5SweepPolicy SweepPolicy, M5SequenceWindowPolicy WindowPolicy,
    M5SequenceFvgPolicy FormationPolicy, M5FvgProfile Profile, TimeOnly WindowOpen, TimeOnly WindowClose)
{
    public string Id => FormattableString.Invariant($"{SweepPolicy}/{WindowPolicy}/{FormationPolicy}/{Profile}/{WindowOpen:HH:mm:ss.fffffff}-{WindowClose:HH:mm:ss.fffffff}");
}

public sealed record M5SequenceEpisode(string Id, DateOnly OperatingDate, ContextualScEventSelection Selection);
public sealed record M5ScEvidence(string ComparisonId, string Status, string Reason,
    IReadOnlyList<DateTimeOffset> MissingStructuralOpensUtc, IReadOnlyList<DateTimeOffset> MissingScOpensUtc);
public sealed record M5SequenceConfirmation(string Id, M5ConfirmationKind Kind, M5FvgDirection Direction,
    DateTimeOffset ConfirmedAtUtc, string CandidateId, string Configuration, string OriginatingObservationId,
    string SourceReference, string EvidenceQuality, bool Verified, Candle EvidenceCandle, M5SequenceZone? OriginZone)
{
    public ContextualStructuralSelection? FrozenStructuralReference { get; init; }
    public string AssociationKey => Kind == M5ConfirmationKind.IFVG ? Id : FormattableString.Invariant(
        $"SC/{EvidenceCandle.ProviderId.Value}/{EvidenceCandle.Symbol.Value}/{Direction}/{EvidenceCandle.OpenTimeUtc:O}");
}

public sealed record M5SequenceZone(M5FvgFormation Formation, DateTimeOffset? InversionUtc,
    DateTimeOffset? InvalidationUtc, DateTimeOffset? EvidenceLimitedAtUtc)
{
    public string StateAt(DateTimeOffset cutoff, bool invertedRole)
    {
        if (EvidenceLimitedAtUtc <= cutoff) return "InsufficientEvidence";
        if (invertedRole && InvalidationUtc <= cutoff) return "InversionInvalidated";
        if (InversionUtc <= cutoff) return "Inverted";
        return "Formed";
    }
}

public sealed record M5ConfirmationDiagnostic(M5SequenceConfirmation Event, bool DirectionCompatible,
    bool WithinWindow, string Reason, string StateAtCutoff)
{
    public bool Eligible => Reason == "Eligible";
}

public sealed record M5SequenceFvgDiagnostic(string ConfirmationId, M5SequenceZone Zone, string Reason,
    string ZoneStateAtFormation, string ZoneStateAtCutoff, string? TriggerStateAtFormation)
{
    public bool Eligible => Reason == "Eligible";
}

public sealed record M5SequenceTransition(string State, DateTimeOffset? ObservedAtUtc, string? EvidenceId = null);

public sealed record M5SequenceResult(M5SequenceEpisode Episode, M5SequenceConfiguration Configuration,
    DateTimeOffset AsOfUtc, DateTimeOffset WindowOpenUtc, DateTimeOffset WindowCloseUtc, M5FvgDirection? Direction,
    string SweepReason, IReadOnlyList<M5ConfirmationDiagnostic> Confirmations,
    IReadOnlyList<string> ChronologicalFirstIds, DateTimeOffset? ChronologicalFirstUtc,
    M5FirstConfirmation FirstOutcome, IReadOnlyList<string> FirstEligibleIds, DateTimeOffset? FirstEligibleUtc,
    IReadOnlyList<M5SequenceFvgDiagnostic> FvgDiagnostics, IReadOnlyList<M5SequenceFvgDiagnostic> FirstFvgs,
    DateTimeOffset? CompletedAtUtc, IReadOnlyList<M5SequenceTransition> Lifecycle, IReadOnlyList<M5FvgSourceGap> SourceGaps,
    IReadOnlyList<M5ScEvidence> ScEvidence)
{
    public TimeSpan? SweepToConfirmation => FirstEligibleUtc - Episode.Selection.Event?.AvailableAtUtc;
    public TimeSpan? ConfirmationToFvg => CompletedAtUtc - FirstEligibleUtc;
    public bool HistoricallyCompleted => CompletedAtUtc is not null;
}

public sealed record M5SequenceAssociation(string ConfigurationId, string EvidenceKey, IReadOnlyList<string> EventIds,
    IReadOnlyList<string> EpisodeIds)
{
    public bool Ambiguous => EpisodeIds.Count > 1;
}

public sealed record M5ContextualSequenceReport(string SessionId, DateTimeOffset AsOfUtc, string SourceProvenance,
    IReadOnlyList<M5SequenceResult> Results, IReadOnlyList<M5SequenceAssociation> ConfirmationAssociations,
    IReadOnlyList<string> UnavailableLiquidityReferences)
{
    public const string ResearchId = "EXP-NQ-M5-CONTEXTUAL-SEQUENCE-V1";
    public const string ResearchVersion = "1.0.0";
    public string CandidateId => ResearchId;
    public string Version => ResearchVersion;
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Composes visible research observations at one explicit cutoff.</summary>
public sealed class EvaluateM5ContextualSequenceUseCase
{
    public M5ContextualSequenceReport Execute(string sessionId, ContextualSwingScReport sc, CandleSeries m5,
        IReadOnlyList<M5SequenceConfiguration> configurations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(sc);
        ArgumentNullException.ThrowIfNull(m5);
        ArgumentNullException.ThrowIfNull(configurations);
        if (configurations.Count == 0 || configurations.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != configurations.Count)
            throw new ArgumentException("Select unique explicit sequence configurations.", nameof(configurations));
        var tracking = sc.Tracking;
        var cutoff = tracking.CutoffUtc;
        var actual = m5.Candles.Where(c => c.CloseTimeUtc <= cutoff).ToDictionary(c => c.OpenTimeUtc);
        bool Visible(Candle candle) => actual.TryGetValue(candle.OpenTimeUtc, out var observed) &&
            candle.ProviderId == observed.ProviderId && candle.Symbol == observed.Symbol && candle.Timeframe == observed.Timeframe &&
            candle.CloseTimeUtc == observed.CloseTimeUtc && candle.Open == observed.Open && candle.High == observed.High &&
            candle.Low == observed.Low && candle.Close == observed.Close && candle.Volume == observed.Volume;

        var results = new List<M5SequenceResult>();
        foreach (var profile in configurations.GroupBy(c => c.Profile))
        {
            var fvg = new EvaluateM5FvgCandidateUseCase().Execute(new(m5, tracking.RangeStartUtc, cutoff, profile.Key, tracking.Provenance));
            var zones = fvg.Zones.Select(z => new M5SequenceZone(z.Formation, z.InversionUtc, z.InvalidationUtc,
                z.Observations.FirstOrDefault(o => o.Event == M5FvgEvent.InsufficientEvidence)?.ObservedAtUtc)).ToArray();
            var inversions = fvg.Zones.Zip(zones).Where(z => z.Second.InversionUtc is not null)
                .Select(z =>
                {
                    var evidence = z.First.Observations.Single(o => o.Event == M5FvgEvent.Inverted);
                    return new M5SequenceConfirmation(z.Second.Formation.Id + "/IFVG", M5ConfirmationKind.IFVG,
                        z.Second.Formation.OppositeDirection, evidence.ObservedAtUtc, fvg.CandidateId, profile.Key.ToString(),
                        z.Second.Formation.Id, fvg.SourceProvenance, "StrictOppositeClose", true, evidence.Candle!, z.Second);
                }).ToArray();

            foreach (var selection in sc.EventSelections.OrderBy(s => s.LiquidityReference.Id, StringComparer.Ordinal)
                         .ThenBy(s => s.PolicyId, StringComparer.Ordinal))
            {
                if (selection.LiquidityReference.AvailableAtUtc > cutoff || selection.Event?.AvailableAtUtc > cutoff) continue;
                var episode = new M5SequenceEpisode($"{sessionId}/{selection.LiquidityReference.Id}/{selection.PolicyId}",
                    tracking.OperatingDate, selection);
                var sweepVerified = selection.Event is { } sweep && sweep.Candle.CloseTimeUtc <= sweep.AvailableAtUtc &&
                    sweep.ReferenceId == selection.LiquidityReference.Id && sweep.Relationship.StrictPenetration && Visible(sweep.Candle) &&
                    selection.LiquidityReference.SourceCandles.All(c => c.CloseTimeUtc <= cutoff);
                var comparisons = sc.Comparisons.Where(c => c.EventSelection.LiquidityReference.Id == selection.LiquidityReference.Id &&
                        c.EventSelection.PolicyId == selection.PolicyId && c.EventSelection.Event?.Id == selection.Event?.Id)
                    .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
                var scEvidence = Array.AsReadOnly(comparisons.Select(c => new M5ScEvidence(c.Id, c.Status, c.Reason,
                    c.MissingStructuralOpensUtc, c.MissingScOpensUtc)).ToArray());
                var changes = comparisons
                    .Select(c =>
                    {
                        var observed = c.SubsequentSc ?? c.PreEventBreak ?? c.ObservedSubsequentBreak ??
                                       (c.EventCloseComparison is { IsConfirmed: true } ? c.EventCloseComparison : null);
                        if (observed is not { IsConfirmed: true, Candle: { } bar } || !Visible(bar) ||
                            c.StructuralReference?.Sources.Any(owner => !Visible(owner)) == true) return null;
                        return new M5SequenceConfirmation(c.Id + "/" + bar.OpenTimeUtc.ToString("O"), M5ConfirmationKind.StructuralChange,
                            observed.Direction == StructuralBreakDirection.Upper ? M5FvgDirection.Bullish : M5FvgDirection.Bearish,
                            observed.AsOfUtc, c.HypothesisId,
                            c.SwingVariantId is null ? c.HypothesisVersion : $"{c.HypothesisVersion}/{c.SwingVariantId}/{c.SwingVariantVersion}", c.Id,
                            tracking.Provenance, c.Status, c.SubsequentSc is not null || c.EventCloseComparison == observed, bar, null)
                        { FrozenStructuralReference = c.StructuralReference };
                    }).OfType<M5SequenceConfirmation>();
                var events = changes.Concat(inversions).OrderBy(e => e.ConfirmedAtUtc).ThenBy(e => e.Id, StringComparer.Ordinal).ToArray();
                foreach (var configuration in profile)
                    results.Add(EvaluateEpisode(episode, configuration, cutoff, sweepVerified, events, zones, fvg.SourceGaps, scEvidence));
            }
        }

        var associations = results.SelectMany(r => r.Confirmations.Where(c => c.Eligible)
                .Select(c => (r.Configuration.Id, Key: c.Event.AssociationKey, Event: c.Event.Id, Episode: r.Episode.Id)))
            .GroupBy(x => (x.Id, x.Key)).OrderBy(g => g.Key.Id, StringComparer.Ordinal).ThenBy(g => g.Key.Key, StringComparer.Ordinal)
            .Select(g => new M5SequenceAssociation(g.Key.Id, g.Key.Key,
                Array.AsReadOnly(g.Select(x => x.Event).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                Array.AsReadOnly(g.Select(x => x.Episode).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())))
            .ToArray();
        var ordered = results.OrderBy(r => r.Episode.Id, StringComparer.Ordinal)
            .ThenBy(r => r.Configuration.Id, StringComparer.Ordinal).ToArray();
        return new(sessionId, cutoff, tracking.Provenance, Array.AsReadOnly(ordered), Array.AsReadOnly(associations),
            tracking.UnavailableReferences);
    }

    private static DateTimeOffset DayBoundary(DateOnly day, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(time), NasdaqSupervisedSessionPolicy.Zone);

    /// <summary>Compares already source-verified research evidence for a single episode.</summary>
    public static M5SequenceResult EvaluateEpisode(M5SequenceEpisode episode, M5SequenceConfiguration configuration,
        DateTimeOffset cutoff, bool sweepVerified,
        IReadOnlyList<M5SequenceConfirmation> confirmations, IReadOnlyList<M5SequenceZone> zones,
        IReadOnlyList<M5FvgSourceGap> sourceGaps, IReadOnlyList<M5ScEvidence>? scEvidence = null)
    {
        if (cutoff.Offset != TimeSpan.Zero || configuration.WindowOpen >= configuration.WindowClose || !Enum.IsDefined(configuration.SweepPolicy) ||
            !Enum.IsDefined(configuration.WindowPolicy) || !Enum.IsDefined(configuration.FormationPolicy) || !Enum.IsDefined(configuration.Profile))
            throw new ArgumentException("Explicit ordered research window and recognized policies required.", nameof(configuration));
        var sessionStart = DayBoundary(episode.OperatingDate, TimeOnly.MinValue);
        var sessionEnd = DayBoundary(episode.OperatingDate.AddDays(1), TimeOnly.MinValue);
        if (episode.Selection.LiquidityReference.AvailableAtUtc > cutoff || episode.Selection.Event?.AvailableAtUtc > cutoff ||
            episode.Selection.Event?.Candle.CloseTimeUtc > cutoff)
            throw new ArgumentException("The episode must be produced at the requested research cutoff.", nameof(episode));
        var opening = DayBoundary(episode.OperatingDate, configuration.WindowOpen);
        var closing = DayBoundary(episode.OperatingDate, configuration.WindowClose);
        var evidenceCutoff = cutoff < sessionEnd ? cutoff : sessionEnd.AddTicks(-1);
        DateTimeOffset? VisibleTransition(DateTimeOffset? time) => time <= evidenceCutoff ? time : null;
        M5SequenceZone AtCutoff(M5SequenceZone zone) => zone with
        {
            InversionUtc = VisibleTransition(zone.InversionUtc),
            InvalidationUtc = VisibleTransition(zone.InvalidationUtc),
            EvidenceLimitedAtUtc = VisibleTransition(zone.EvidenceLimitedAtUtc)
        };
        var sweep = episode.Selection.Event;
        var sweptAt = sweep?.AvailableAtUtc;
        M5FvgDirection? direction = sweep is null ? null : episode.Selection.LiquidityReference.HighSide
            ? M5FvgDirection.Bearish : M5FvgDirection.Bullish;
        bool InWindow(DateTimeOffset at) => at >= opening && at < closing;
        bool InSession(DateTimeOffset at) => at >= sessionStart && at < sessionEnd;
        var gaps = sourceGaps.Where(g => g.ObservedAtUtc <= cutoff && g.ClosureEvidence is null).ToArray();
        bool Missing(DateTimeOffset from, DateTimeOffset through) =>
            gaps.Any(g => g.EndUtc > from && g.StartUtc < through && g.ObservedAtUtc <= through);
        var sweepReason = sweep is null ? episode.Selection.Status : !sweepVerified ? "SweepEvidenceUnavailable" :
            !InSession(sweptAt!.Value) ? "SweepOutsideSession" :
            configuration.WindowPolicy == M5SequenceWindowPolicy.WindowEventOnly && !InWindow(sweptAt.Value) ?
            "SweepOutsideWindow" : "Eligible";

        var diagnostics = confirmations.Where(e => e.ConfirmedAtUtc <= cutoff && e.EvidenceCandle.CloseTimeUtc <= cutoff)
            .OrderBy(e => e.ConfirmedAtUtc).ThenBy(e => e.Id, StringComparer.Ordinal)
            .Select(e =>
            {
                var compatible = direction == e.Direction;
                var within = InWindow(e.ConfirmedAtUtc);
                var reason = sweptAt is null ? "NoObservableSweep" :
                    !compatible ? "DirectionMismatch" :
                    !InSession(e.ConfirmedAtUtc) ? "ConfirmationOutsideSession" :
                    e.ConfirmedAtUtc < sweptAt ? "BeforeSweep" :
                    !e.Verified ? "ConfirmationEvidenceLimited" :
                    e.ConfirmedAtUtc == sweptAt ? "SameSweepClose" :
                    sweepReason != "Eligible" ? sweepReason :
                    configuration.WindowPolicy != M5SequenceWindowPolicy.PostSweepObservation && !within ?
                        "ConfirmationOutsideWindow" :
                    Missing(sweptAt.Value, e.ConfirmedAtUtc) ? "MissingSourceBeforeConfirmation" : "Eligible";
                var origin = e.OriginZone is { } zone ? AtCutoff(zone) : null;
                return new M5ConfirmationDiagnostic(e with { OriginZone = origin }, compatible, within, reason,
                    origin?.StateAt(evidenceCutoff, true) ?? (e.Verified ? "ScInvalidationUndefined" : "InsufficientEvidence"));
            }).ToArray();
        var chronological = First(diagnostics.Where(c => c.DirectionCompatible && c.Event.Verified &&
            c.Event.ConfirmedAtUtc > sweptAt && InSession(c.Event.ConfirmedAtUtc)), c => c.Event.ConfirmedAtUtc);
        var first = First(diagnostics.Where(c => c.Eligible), c => c.Event.ConfirmedAtUtc);
        var firstAt = first.FirstOrDefault()?.Event.ConfirmedAtUtc;
        var sameSweep = configuration.SweepPolicy == M5SweepPolicy.SameCloseDiagnostic && sweepReason == "Eligible" &&
            diagnostics.Any(c => c.Reason == "SameSweepClose" &&
                (configuration.WindowPolicy == M5SequenceWindowPolicy.PostSweepObservation || c.WithinWindow));
        var outcome = first.Length > 0
            ? first.Select(c => c.Event.Kind).Distinct().Count() > 1 ? M5FirstConfirmation.SimultaneousConfirmation :
                first[0].Event.Kind == M5ConfirmationKind.IFVG ? M5FirstConfirmation.IFVGFirst : M5FirstConfirmation.StructuralChangeFirst
            : sameSweep ? M5FirstConfirmation.AmbiguousSweepOrdering
            : sweepReason is "SweepEvidenceUnavailable" or "AbstainedEventCoverage" ||
              sweptAt is { } s && sweepReason == "Eligible" && Missing(s, cutoff)
                ? M5FirstConfirmation.InsufficientEvidence : M5FirstConfirmation.WaitingForConfirmation;

        var fvgDiagnostics = first.SelectMany(c => zones.Where(z => z.Formation.FormationUtc <= cutoff &&
                    z.Formation.Profile == configuration.Profile)
                .Select(AtCutoff)
                .Select(z =>
                {
                    var formed = z.Formation.FormationUtc;
                    var trigger = c.Event.OriginZone;
                    var reason = z.Formation.Id == trigger?.Formation.Id ? "OriginatingInvertedZone" :
                        z.Formation.OriginalDirection != c.Event.Direction ? "DirectionMismatch" :
                        formed < firstAt ? "BeforeConfirmation" :
                        formed == firstAt ? "SameConfirmationClose" :
                        !InSession(formed) ? "FvgOutsideSession" :
                        configuration.FormationPolicy == M5SequenceFvgPolicy.FormationSequenceAfterConfirmation &&
                        z.Formation.C1.OpenTimeUtc < firstAt ? "SequenceStartedBeforeConfirmation" :
                        trigger?.InvalidationUtc <= formed ? "TriggerInvalidatedByFvgClose" :
                        trigger?.EvidenceLimitedAtUtc <= formed || Missing(firstAt!.Value, formed) ?
                            "MissingSourceBeforeFvg" : "Eligible";
                    return new M5SequenceFvgDiagnostic(c.Event.Id, z, reason, z.StateAt(formed, false),
                        z.StateAt(cutoff, false), trigger?.StateAt(formed, true));
                })).OrderBy(d => d.Zone.Formation.FormationUtc).ThenBy(d => d.Zone.Formation.Id, StringComparer.Ordinal)
            .ThenBy(d => d.ConfirmationId, StringComparer.Ordinal).ToArray();
        var firstFvgs = First(fvgDiagnostics.Where(f => f.Eligible), f => f.Zone.Formation.FormationUtc);
        var completed = firstFvgs.FirstOrDefault()?.Zone.Formation.FormationUtc;
        var lifecycle = new List<M5SequenceTransition>();
        if (sweptAt is null) lifecycle.Add(new("WaitingForLiquidityTake", null));
        else
        {
            lifecycle.Add(new("LiquidityTakeObserved", sweptAt, sweep!.Id));
            lifecycle.Add(new("WaitingForSCOrIFVG", sweptAt, sweep.Id));
        }
        if (sameSweep) lifecycle.Add(new("Ambiguous", sweptAt, "SameSweepClose"));
        if (outcome == M5FirstConfirmation.SimultaneousConfirmation)
            lifecycle.Add(new("Ambiguous", firstAt, "SimultaneousConfirmation"));
        if (outcome == M5FirstConfirmation.InsufficientEvidence)
            lifecycle.Add(new("InsufficientEvidence", gaps.FirstOrDefault(g => g.EndUtc > sweptAt)?.ObservedAtUtc));
        if (firstAt is not null)
        {
            lifecycle.Add(new("ConfirmationObserved", firstAt));
            lifecycle.Add(new("WaitingForDirectionalFVG", firstAt));
            foreach (var c in first)
            {
                if (c.Event.OriginZone?.InvalidationUtc is { } invalidated && invalidated <= cutoff)
                    lifecycle.Add(new("ConfirmationInvalidated", invalidated, c.Event.Id));
                if (c.Event.OriginZone?.EvidenceLimitedAtUtc is { } limited)
                    lifecycle.Add(new("InsufficientEvidence", limited, c.Event.Id));
            }
            if (completed is not null)
            {
                lifecycle.Add(new("DirectionalFVGObserved", completed));
                foreach (var zone in firstFvgs.Select(f => f.Zone).DistinctBy(z => z.Formation.Id))
                {
                    if (zone.InversionUtc is { } inverted && inverted <= cutoff)
                        lifecycle.Add(new("FVGInvalidated", inverted, zone.Formation.Id));
                    if (zone.EvidenceLimitedAtUtc is { } limited)
                        lifecycle.Add(new("InsufficientEvidence", limited, zone.Formation.Id));
                }
            }
            if (completed is null && gaps.FirstOrDefault(g => g.EndUtc > firstAt && g.ObservedAtUtc <= evidenceCutoff) is { } gap)
                lifecycle.Add(new("InsufficientEvidence", gap.ObservedAtUtc));
        }
        return new(episode, configuration, cutoff, opening, closing, direction, sweepReason, Array.AsReadOnly(diagnostics),
            Array.AsReadOnly(chronological.Select(c => c.Event.Id).ToArray()), chronological.FirstOrDefault()?.Event.ConfirmedAtUtc,
            outcome, Array.AsReadOnly(first.Select(c => c.Event.Id).ToArray()), firstAt, Array.AsReadOnly(fvgDiagnostics),
            Array.AsReadOnly(firstFvgs), completed, Array.AsReadOnly(lifecycle.OrderBy(e => e.ObservedAtUtc).ToArray()),
            Array.AsReadOnly(gaps), scEvidence ?? []);
    }

    private static T[] First<T>(IEnumerable<T> source, Func<T, DateTimeOffset> timestamp)
    {
        var ordered = source.OrderBy(timestamp).ToArray();
        return ordered.Length == 0 ? [] : ordered.TakeWhile(item => timestamp(item) == timestamp(ordered[0])).ToArray();
    }
}
