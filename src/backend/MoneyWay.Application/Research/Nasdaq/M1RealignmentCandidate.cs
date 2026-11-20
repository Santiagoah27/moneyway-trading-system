using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum M1RealignmentProfile { BodyDirectionTurn, PullbackRangeReclaim }
public enum M1AttemptStatus { CountertrendObserved, WaitingForRealignment, RealignmentObserved, InterruptedPullback, InsufficientEvidence }
public enum M1WindowPosition { BeforeM5Anchor, BeforeWindow, InsideWindow, AtExclusiveEnd, AfterWindow }

public sealed record M1ResearchAnchor(DateOnly OperatingDate, DateTimeOffset AtUtc, M5FvgDirection Direction,
    DateTimeOffset WindowOpenUtc, DateTimeOffset WindowCloseUtc)
{
    public string Id => FormattableString.Invariant($"{OperatingDate:yyyy-MM-dd}/{AtUtc:O}/{Direction}/{WindowOpenUtc:O}/{WindowCloseUtc:O}");
    public M1WindowPosition Position(DateTimeOffset open, DateTimeOffset close) =>
        open < AtUtc ? M1WindowPosition.BeforeM5Anchor : close < WindowOpenUtc ? M1WindowPosition.BeforeWindow :
        close < WindowCloseUtc ? M1WindowPosition.InsideWindow :
        close == WindowCloseUtc ? M1WindowPosition.AtExclusiveEnd : M1WindowPosition.AfterWindow;
}

public sealed record M1CoverageGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateTimeOffset ObservableAtUtc);
public sealed record M1CountertrendAttempt(string Id, Candle First, Candle LastIncluded, int OppositeBodies,
    int NeutralBodies, decimal Low, decimal High, Candle? FirstAligned, Candle? Realignment,
    M1AttemptStatus Status, DateTimeOffset? InterruptedAtUtc, bool ContinuousFromAnchor, M1WindowPosition? WindowPosition)
{
    public DateTimeOffset StartUtc => First.OpenTimeUtc;
    public DateTimeOffset ObservedAtUtc => First.CloseTimeUtc;
    public DateTimeOffset EndUtc => LastIncluded.CloseTimeUtc;
    public DateTimeOffset? ConfirmedAtUtc => Realignment?.CloseTimeUtc;
    public TimeSpan? CountertrendToRealignment => ConfirmedAtUtc - ObservedAtUtc;
    public bool WindowEligible => ContinuousFromAnchor && WindowPosition == M1WindowPosition.InsideWindow;
}

public sealed record M1RealignmentScan(M1ResearchAnchor Anchor, M1RealignmentProfile Profile, DateTimeOffset AsOfUtc, int ClosedM1Count,
    DateTimeOffset? FirstM1OpenUtc, DateTimeOffset? LastM1CloseUtc, int PreAnchorClosedM1Count,
    IReadOnlyList<M1CoverageGap> SourceGaps, IReadOnlyList<M1CountertrendAttempt> Attempts, string Outcome)
{
    public string Id => $"{Anchor.Id}/{Profile}";
    public DateTimeOffset? FirstRealignmentUtc => Attempts.Where(a => a.WindowEligible).Min(a => a.ConfirmedAtUtc);
    public IReadOnlyList<string> FirstRealignmentIds => Attempts.Where(a => a.WindowEligible &&
        a.ConfirmedAtUtc == FirstRealignmentUtc).Select(a => a.Id).ToArray();
    public TimeSpan? AnchorToCountertrend => Attempts.FirstOrDefault()?.ObservedAtUtc - Anchor.AtUtc;
    public bool WindowExpiredWithoutRealignment => AsOfUtc >= Anchor.WindowCloseUtc && FirstRealignmentUtc is null;
}

public sealed record M1M5ZoneEvidence(string ZoneId, string Role, DateTimeOffset? InvalidatedAtUtc,
    DateTimeOffset? EvidenceLimitedAtUtc)
{
    public string StateAt(DateTimeOffset time)
    {
        if (InvalidatedAtUtc <= time) return "Invalidated";
        return EvidenceLimitedAtUtc <= time ? "UnknownDueToMissingData" : "NotInvalidatedWithinObservableEvidence";
    }

    public string RelativeTo(M1CountertrendAttempt attempt)
    {
        if (InvalidatedAtUtc is not { } invalidated)
            return EvidenceLimitedAtUtc is null ? "NotInvalidatedWithinObservableEvidence" : "UnknownDueToMissingData";
        if (invalidated <= attempt.ObservedAtUtc) return "InvalidatedBeforeOrAtPullbackObservation";
        if (invalidated <= attempt.EndUtc) return "InvalidatedDuringPullback";
        if (attempt.ConfirmedAtUtc is not { } confirmation)
            return attempt.InterruptedAtUtc < invalidated ? "InvalidatedAfterAttemptEnded" : "InvalidatedWhileAwaitingRealignment";
        return invalidated <= confirmation ? "InvalidatedBeforeOrAtRealignment" : "InvalidatedAfterRealignment";
    }
}

public sealed record M1ZoneAssessment(string AttemptId, string StateAtPullback, string? StateAtRealignment, string InvalidationRelation);
public sealed record M1ZoneTimeline(string ScanId, M1M5ZoneEvidence Zone, IReadOnlyList<M1ZoneAssessment> Attempts);
public sealed record M1M5Binding(string FvgId, string ConfirmationId, string FvgZoneTimelineId, string? IfvgZoneTimelineId);
public sealed record M1RealignmentAssociation(string EpisodeId, M5SequenceConfiguration M5Configuration,
    M1RealignmentProfile Profile, string? ScanId, string Outcome, IReadOnlyList<string> FirstM5ConfirmationIds,
    IReadOnlyList<M1M5Binding> Bindings, string M5SweepReason, M5FirstConfirmation M5FirstOutcome,
    IReadOnlyList<M5FvgSourceGap> M5SourceGaps)
{
    public bool AmbiguousM5Ordering => M5FirstOutcome is M5FirstConfirmation.SimultaneousConfirmation or M5FirstConfirmation.AmbiguousSweepOrdering;
}

public sealed record M1RealignmentReport(string SessionId, DateTimeOffset AsOfUtc, string SourceProvenance,
    string Provider, string Instrument, IReadOnlyList<M1RealignmentScan> Scans,
    IReadOnlyList<M1RealignmentAssociation> Associations, IReadOnlyDictionary<string, M1ZoneTimeline> ZoneTimelines)
{
    public string CandidateId => "EXP-NQ-M1-REALIGNMENT-V1";
    public string Version => "1.0.0";
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public int DistinctEpisodes => Associations.Select(a => a.EpisodeId).Distinct(StringComparer.Ordinal).Count();
    public int DistinctCompletedEpisodes => Associations.Where(a => a.ScanId is not null).Select(a => a.EpisodeId).Distinct(StringComparer.Ordinal).Count();
    public int DistinctAnchorTimestamps => Scans.Select(s => s.Anchor.AtUtc).Distinct().Count();
    public int DistinctAnchorContexts => Scans.Select(s => s.Anchor.Id).Distinct(StringComparer.Ordinal).Count();
    public int M5ConfigurationCombinations => Associations.Select(a => (a.EpisodeId, a.M5Configuration.Id)).Distinct().Count();
    public int CompletedM5ConfigurationCombinations => Associations.Where(a => a.ScanId is not null)
        .Select(a => (a.EpisodeId, a.M5Configuration.Id)).Distinct().Count();
}

public sealed class EvaluateM1RealignmentCandidateUseCase
{
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    public M1RealignmentReport Execute(M5ContextualSequenceReport m5, CandleSeries m1,
        IReadOnlyList<M1RealignmentProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(m5);
        ArgumentNullException.ThrowIfNull(m1);
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count == 0 || profiles.Distinct().Count() != profiles.Count || profiles.Any(p => !Enum.IsDefined(p)))
            throw new ArgumentException("Select distinct explicit M1 profiles.", nameof(profiles));
        if (m1.Timeframe != M1 || m5.AsOfUtc.Offset != TimeSpan.Zero ||
            m1.Candles.Any(c => c.CloseTimeUtc <= m5.AsOfUtc &&
                (c.CloseTimeUtc - c.OpenTimeUtc != Minute || c.OpenTimeUtc.Ticks % Minute.Ticks != 0)))
            throw new ArgumentException("Original, aligned one-minute candles and a UTC cutoff are required.", nameof(m1));
        var candles = m1.Candles.TakeWhile(c => c.CloseTimeUtc <= m5.AsOfUtc).ToArray();
        var scans = new SortedDictionary<string, M1RealignmentScan>(StringComparer.Ordinal);
        var timelines = new SortedDictionary<string, M1ZoneTimeline>(StringComparer.Ordinal);
        var associations = new List<M1RealignmentAssociation>();
        foreach (var sequence in m5.Results.OrderBy(r => r.Episode.Id, StringComparer.Ordinal)
                     .ThenBy(r => r.Configuration.Id, StringComparer.Ordinal))
        {
            if (sequence.AsOfUtc != m5.AsOfUtc)
                throw new ArgumentException("M5 sequence and M1 evaluation must share a cutoff.", nameof(m5));
            var reference = sequence.Episode.Selection.LiquidityReference;
            if (reference.Provider != m1.ProviderId.Value || reference.Instrument != m1.Symbol.Value)
                throw new ArgumentException("M1 provider and instrument must match the M5 context.", nameof(m1));
            foreach (var profile in profiles.Order())
            {
                var bindings = new List<M1M5Binding>();
                M1RealignmentScan? scan = null;
                if (sequence.CompletedAtUtc is { } at && at <= m5.AsOfUtc && sequence.Direction is { } direction)
                {
                    var anchor = new M1ResearchAnchor(sequence.Episode.OperatingDate, at, direction,
                        sequence.WindowOpenUtc, sequence.WindowCloseUtc);
                    var key = $"{anchor.Id}/{profile}";
                    if (!scans.TryGetValue(key, out scan))
                        scans.Add(key, scan = Scan(anchor, profile, m5.AsOfUtc, candles));
                    var confirmations = sequence.Confirmations.ToDictionary(c => c.Event.Id, StringComparer.Ordinal);
                    foreach (var fvg in sequence.FirstFvgs.OrderBy(f => f.Zone.Formation.Id, StringComparer.Ordinal)
                                 .ThenBy(f => f.ConfirmationId, StringComparer.Ordinal))
                    {
                        if (!fvg.Eligible || fvg.Zone.Formation.FormationUtc != at ||
                            fvg.Zone.Formation.OriginalDirection != direction ||
                            fvg.Zone.Formation.C3.ProviderId != m1.ProviderId || fvg.Zone.Formation.C3.Symbol != m1.Symbol ||
                            !sequence.FirstEligibleIds.Contains(fvg.ConfirmationId) ||
                            !confirmations.TryGetValue(fvg.ConfirmationId, out var confirmation) ||
                            !confirmation.Eligible || confirmation.Event.ConfirmedAtUtc >= at)
                            throw new ArgumentException("Completed M5 sequence must retain its observable directional FVG and trigger.", nameof(m5));
                        var fvgTimeline = AddTimeline(scan, fvg.Zone, false);
                        var ifvgTimeline = confirmation.Event.OriginZone is { } origin ? AddTimeline(scan, origin, true) : null;
                        bindings.Add(new(fvg.Zone.Formation.Id, fvg.ConfirmationId, fvgTimeline, ifvgTimeline));
                    }
                    if (bindings.Count == 0) throw new ArgumentException("Completed M5 sequence has no eligible FVG bindings.", nameof(m5));
                }
                associations.Add(new(sequence.Episode.Id, sequence.Configuration, profile, scan?.Id,
                    scan?.Outcome ?? "WaitingForM5Sequence", sequence.FirstEligibleIds, bindings.AsReadOnly(),
                    sequence.SweepReason, sequence.FirstOutcome, sequence.SourceGaps));
            }
        }
        return new(m5.SessionId, m5.AsOfUtc, m5.SourceProvenance, m1.ProviderId.Value, m1.Symbol.Value,
            Array.AsReadOnly(scans.Values.ToArray()), associations.AsReadOnly(),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, M1ZoneTimeline>(timelines));

        string AddTimeline(M1RealignmentScan scan, M5SequenceZone zone, bool invertedRole)
        {
            DateTimeOffset? Visible(DateTimeOffset? t) => t <= m5.AsOfUtc ? t : null;
            var evidence = new M1M5ZoneEvidence(zone.Formation.Id, invertedRole ? "IFVG" : "DirectionalFVG",
                Visible(invertedRole ? zone.InvalidationUtc : zone.InversionUtc), Visible(zone.EvidenceLimitedAtUtc));
            var key = $"{scan.Id}/{evidence.Role}/{evidence.ZoneId}";
            if (timelines.TryGetValue(key, out var existing) && existing.Zone != evidence)
                throw new ArgumentException("Conflicting M5 evidence for a shared zone identity.", nameof(m5));
            if (existing is null)
                timelines.Add(key, new(scan.Id, evidence, Array.AsReadOnly(scan.Attempts.Select(a =>
                    new M1ZoneAssessment(a.Id, evidence.StateAt(a.ObservedAtUtc),
                        a.ConfirmedAtUtc is { } t ? evidence.StateAt(t) : null, evidence.RelativeTo(a))).ToArray())));
            return key;
        }
    }

    private static M1RealignmentScan Scan(M1ResearchAnchor anchor, M1RealignmentProfile profile,
        DateTimeOffset cutoff, IReadOnlyList<Candle> visible)
    {
        var dayEnd = (DateTimeOffset)TimeZoneInfo.ConvertTimeToUtc(anchor.OperatingDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            NasdaqSupervisedSessionPolicy.Zone);
        var end = cutoff < dayEnd ? cutoff : dayEnd;
        var candles = visible.Where(c => c.OpenTimeUtc >= anchor.AtUtc && c.CloseTimeUtc > anchor.AtUtc &&
                                         c.OpenTimeUtc < dayEnd && c.CloseTimeUtc <= end).ToArray();
        var previousClose = anchor.AtUtc;
        var aligned = anchor.Direction == M5FvgDirection.Bullish ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish;
        var classifier = new CandleBodyDirectionCalculator();
        var attempts = new List<M1CountertrendAttempt>();
        var gaps = new List<M1CoverageGap>();
        M1CountertrendAttempt? active = null;
        foreach (var candle in candles)
        {
            if (candle.OpenTimeUtc > previousClose)
                Gap(candle.OpenTimeUtc);
            previousClose = candle.CloseTimeUtc;
            var body = classifier.Evaluate(candle);
            var opposite = body != aligned && body != CandleBodyDirection.Neutral;
            if (active?.FirstAligned is not null && opposite)
            {
                attempts.Add(active with { Status = M1AttemptStatus.InterruptedPullback, InterruptedAtUtc = candle.CloseTimeUtc });
                active = null;
            }
            if (active is null)
            {
                if (opposite)
                    active = new(FormattableString.Invariant($"{anchor.Id}/{profile}/{candle.OpenTimeUtc:O}"),
                        candle, candle, 1, 0, candle.Low, candle.High, null, null, M1AttemptStatus.CountertrendObserved,
                        null, gaps.Count == 0, null);
                continue;
            }
            if (body != aligned)
            {
                if (active.FirstAligned is null)
                    active = active with
                    {
                        LastIncluded = candle,
                        Low = Math.Min(active.Low, candle.Low),
                        High = Math.Max(active.High, candle.High),
                        OppositeBodies = active.OppositeBodies + (opposite ? 1 : 0),
                        NeutralBodies = active.NeutralBodies + (opposite ? 0 : 1)
                    };
                continue;
            }

            active = active with { FirstAligned = active.FirstAligned ?? candle, Status = M1AttemptStatus.WaitingForRealignment };
            var reclaimed = aligned == CandleBodyDirection.Bullish ? candle.Close > active.High : candle.Close < active.Low;
            if (profile == M1RealignmentProfile.BodyDirectionTurn || reclaimed)
            {
                attempts.Add(active with
                {
                    Realignment = candle,
                    Status = M1AttemptStatus.RealignmentObserved,
                    WindowPosition = anchor.Position(candle.OpenTimeUtc, candle.CloseTimeUtc)
                });
                active = null;
            }
        }
        if (end - previousClose >= Minute)
            Gap(previousClose.AddTicks((end - previousClose).Ticks / Minute.Ticks * Minute.Ticks));
        if (active is not null) attempts.Add(active);

        var outcome = attempts.Any(a => a.WindowEligible) ? "RealignmentObserved" :
            gaps.Count > 0 ? "InsufficientEvidence" :
            cutoff >= anchor.WindowCloseUtc ? "WindowExpiredWithoutRealignment" :
            active is not null ? active.Status.ToString() :
            attempts.Count > 0 ? "WaitingForRealignment" : "WaitingForCountertrend";
        return new(anchor, profile, cutoff, candles.Length, candles.FirstOrDefault()?.OpenTimeUtc,
            candles.LastOrDefault()?.CloseTimeUtc, visible.Count(c => c.CloseTimeUtc <= anchor.AtUtc),
            gaps.AsReadOnly(), attempts.AsReadOnly(), outcome);

        void Gap(DateTimeOffset gapEnd)
        {
            var known = previousClose.AddMinutes(1);
            gaps.Add(new(previousClose, gapEnd, known));
            if (active is null) return;
            attempts.Add(active with { Status = M1AttemptStatus.InsufficientEvidence, InterruptedAtUtc = known });
            active = null;
        }
    }
}
