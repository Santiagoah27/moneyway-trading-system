using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M5ContextualSequenceTests
{
    private static readonly DateTimeOffset Open = new(2026, 6, 23, 13, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static DateTimeOffset At(int minute) => Open.AddMinutes(minute);
    private static Candle Bar(int close) => new(Provider, Symbol, M5, At(close - 5), At(close), 100, 110, 90, 100, null);

    private static M5SequenceEpisode Episode(int? sweep, bool high = false, string status = "Selected")
    {
        var reference = new ContextualLiquidityReference("ref", "Synthetic", 101, high, Day,
            Provider.Value, Symbol.Value, "1h", [], null, At(-60), "Complete", "fixture");
        ContextualLiquidityObservation? observation = sweep is { } minute ? new("sweep", "ref", Bar(minute), At(minute),
            "Fixture", "Fixture", new(101, false, true, false, true, false, 1), false, 0, 0, false, null, null) : null;
        return new("session/ref/E1", Day, new("E1", "1.0.0", reference, status, status, observation, observation,
            null, null, [], "Complete"));
    }

    private static M5SequenceZone Zone(string id, int formed, M5FvgDirection direction = M5FvgDirection.Bullish,
        int? inversion = null, int? invalidation = null, M5FvgProfile profile = M5FvgProfile.GeometryOnly) =>
        new(new(id, profile, direction, Bar(formed - 10), Bar(formed - 5), Bar(formed), 95, 105, true, "fixture"),
            inversion is { } i ? At(i) : null, invalidation is { } v ? At(v) : null, null);

    private static M5SequenceConfirmation Event(string id, int close, M5ConfirmationKind kind = M5ConfirmationKind.StructuralChange,
        M5FvgDirection direction = M5FvgDirection.Bullish, M5SequenceZone? origin = null) =>
        new(id, kind, direction, At(close), "candidate", "config", id, "fixture", "verified", true, Bar(close), origin);

    private static M5SequenceResult Run(int? sweep, M5SequenceConfirmation[] events, M5SequenceZone[]? zones = null,
        M5SequenceWindowPolicy window = M5SequenceWindowPolicy.PostSweepObservation,
        M5SweepPolicy ordering = M5SweepPolicy.StrictAfterSweep,
        M5SequenceFvgPolicy fvg = M5SequenceFvgPolicy.FormationAfterConfirmation,
        int cutoff = 180, bool high = false, M5FvgSourceGap[]? gaps = null, string status = "Selected",
        M5FvgProfile profile = M5FvgProfile.GeometryOnly) =>
        EvaluateM5ContextualSequenceUseCase.EvaluateEpisode(Episode(sweep, high, status),
            new(ordering, window, fvg, profile, new(8, 30), new(11, 30)), At(cutoff), true, events, zones ?? [], gaps ?? []);

    [Fact]
    public void AbsentEpisodeWaitsAndMissingCoverageIsExplicit()
    {
        var waiting = Run(null, [Event("first", 30)], status: "NoQualifyingEventObserved");
        Assert.Equal("WaitingForLiquidityTake", Assert.Single(waiting.Lifecycle).State);
        Assert.Null(waiting.Direction);
        Assert.Equal("NoObservableSweep", Assert.Single(waiting.Confirmations).Reason);
        Assert.Equal(M5FirstConfirmation.InsufficientEvidence, Run(null, [], status: "AbstainedEventCoverage").FirstOutcome);
    }

    [Theory]
    [InlineData(false, M5FvgDirection.Bullish)]
    [InlineData(true, M5FvgDirection.Bearish)]
    public void ReversalHypothesisFiltersOppositeDirection(bool high, M5FvgDirection expected)
    {
        var opposite = high ? M5FvgDirection.Bullish : M5FvgDirection.Bearish;
        var result = Run(0, [Event("wrong", 5, direction: opposite), Event("right", 10, direction: expected)], high: high);
        Assert.Equal(expected, result.Direction);
        Assert.Equal("DirectionMismatch", result.Confirmations[0].Reason);
        Assert.Equal("right", Assert.Single(result.FirstEligibleIds));
    }

    [Theory]
    [InlineData(5, 10, M5FirstConfirmation.StructuralChangeFirst)]
    [InlineData(10, 5, M5FirstConfirmation.IFVGFirst)]
    [InlineData(5, 5, M5FirstConfirmation.SimultaneousConfirmation)]
    public void FirstIsEarliestAndEveryTiedEventSurvives(int sc, int inversion, M5FirstConfirmation outcome)
    {
        var events = new[] { Event("z", sc), Event("b", inversion, M5ConfirmationKind.IFVG), Event("a", inversion, M5ConfirmationKind.IFVG) };
        var result = Run(0, events);
        Assert.Equal(outcome, result.FirstOutcome);
        Assert.Equal(At(Math.Min(sc, inversion)), result.FirstEligibleUtc);
        Assert.Equal(sc == inversion ? ["a", "b", "z"] : sc < inversion ? ["z"] : new[] { "a", "b" }, result.FirstEligibleIds);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(0, events.Reverse().ToArray())));
    }

    [Fact]
    public void SameSweepCloseIsDiagnosticAndNeverQualifies()
    {
        var events = new[] { Event("before", -5), Event("same", 0, M5ConfirmationKind.IFVG) };
        var strict = Run(0, events);
        Assert.Equal(["BeforeSweep", "SameSweepClose"], strict.Confirmations.Select(c => c.Reason));
        Assert.Equal(M5FirstConfirmation.WaitingForConfirmation, strict.FirstOutcome);
        var diagnostic = Run(0, events, ordering: M5SweepPolicy.SameCloseDiagnostic);
        Assert.Equal(M5FirstConfirmation.AmbiguousSweepOrdering, diagnostic.FirstOutcome);
        Assert.Empty(diagnostic.FirstEligibleIds);
        var later = Run(0, [.. events, Event("later", 5)], ordering: M5SweepPolicy.SameCloseDiagnostic);
        Assert.Equal("later", Assert.Single(later.FirstEligibleIds));
        Assert.Contains(later.Lifecycle, e => e.State == "Ambiguous" && e.ObservedAtUtc == At(0));
    }

    [Fact]
    public void ExplicitBogotaWindowIncludesOpeningAndExcludesClosing()
    {
        var events = new[] { Event("before", -5, M5ConfirmationKind.IFVG), Event("opens", 0), Event("closes", 180) };
        var chronology = Run(-20, events);
        Assert.Equal("before", Assert.Single(chronology.FirstEligibleIds));
        var strict = Run(-20, events, window: M5SequenceWindowPolicy.WindowEventOnly);
        Assert.Equal("SweepOutsideWindow", strict.SweepReason);
        Assert.Empty(strict.FirstEligibleIds);
        var prior = Run(-20, events, window: M5SequenceWindowPolicy.WindowConfirmationWithPriorSweep);
        Assert.Equal("before", Assert.Single(prior.ChronologicalFirstIds));
        Assert.Equal("opens", Assert.Single(prior.FirstEligibleIds));
        Assert.Equal(At(0), prior.WindowOpenUtc);
        Assert.Equal(At(180), prior.WindowCloseUtc);
        Assert.Equal("ConfirmationOutsideWindow", prior.Confirmations[^1].Reason);
        Assert.Equal("Eligible", Run(0, [Event("later", 5)], window: M5SequenceWindowPolicy.WindowEventOnly).SweepReason);
    }

    [Fact]
    public void DirectionalFormationPoliciesKeepTiesAndRejectOriginAndEquality()
    {
        var origin = Zone("origin", -20, M5FvgDirection.Bearish, inversion: 10);
        var events = new[] { Event("ifvg", 10, M5ConfirmationKind.IFVG, origin: origin) };
        M5SequenceZone[] zones = [origin, Zone("before", 5), Zone("same", 10), Zone("opposite", 15, M5FvgDirection.Bearish),
            Zone("wide", 15), Zone("a", 25), Zone("b", 25), Zone("profile-b", 20, profile: M5FvgProfile.MiddleCloseConfirmation)];
        var wide = Run(0, events, zones);
        Assert.Equal("wide", Assert.Single(wide.FirstFvgs).Zone.Formation.Id);
        Assert.Contains(wide.FvgDiagnostics, f => f.Reason == "OriginatingInvertedZone");
        Assert.Contains(wide.FvgDiagnostics, f => f.Reason == "SameConfirmationClose");
        Assert.Contains(wide.FvgDiagnostics, f => f.Reason == "BeforeConfirmation");
        Assert.Contains(wide.FvgDiagnostics, f => f.Reason == "DirectionMismatch");
        Assert.True(origin.Formation.FormationUtc < At(0));
        var narrow = Run(0, events, zones, fvg: M5SequenceFvgPolicy.FormationSequenceAfterConfirmation);
        Assert.Equal(["a", "b"], narrow.FirstFvgs.Select(f => f.Zone.Formation.Id));
        Assert.Contains(narrow.FvgDiagnostics, f => f.Zone.Formation.Id == "wide" && f.Reason == "SequenceStartedBeforeConfirmation");
        Assert.Equal("profile-b", Assert.Single(Run(0, events, zones, profile: M5FvgProfile.MiddleCloseConfirmation).FirstFvgs).Zone.Formation.Id);
    }

    [Fact]
    public void HistoricalCompletionSurvivesLaterInvalidationWithoutRestart()
    {
        var origin = Zone("origin", -20, M5FvgDirection.Bearish, 10, 25);
        var events = new[] { Event("ifvg", 10, M5ConfirmationKind.IFVG, origin: origin), Event("late-sc", 30) };
        var first = Zone("confirm", 20, inversion: 35);
        var result = Run(0, events, [first, Zone("late-zone", 40)]);
        Assert.Equal(At(20), result.CompletedAtUtc);
        Assert.Equal("Formed", Assert.Single(result.FirstFvgs).ZoneStateAtFormation);
        Assert.Equal("Inverted", result.FirstFvgs[0].ZoneStateAtCutoff);
        Assert.Contains(result.Lifecycle, l => l.State == "ConfirmationInvalidated" && l.ObservedAtUtc == At(25));
        Assert.Contains(result.Lifecycle, l => l.State == "FVGInvalidated" && l.ObservedAtUtc == At(35));
        var blocked = Run(0, events, [Zone("equality", 25), Zone("too-late", 30)]);
        Assert.Equal("ifvg", Assert.Single(blocked.FirstEligibleIds));
        Assert.Null(blocked.CompletedAtUtc);
        Assert.All(blocked.FvgDiagnostics, x => Assert.Equal("TriggerInvalidatedByFvgClose", x.Reason));
    }

    [Fact]
    public void UnexplainedGapBlocksEarlierStagesAndFutureTransitionsStayHidden()
    {
        var origin = Zone("origin", -20, M5FvgDirection.Bearish, 10, 25);
        var events = new[] { Event("ifvg", 10, M5ConfirmationKind.IFVG, origin: origin), Event("future", 30) };
        var early = Run(0, events, [Zone("future-fvg", 20)], cutoff: 10);
        Assert.Equal("ifvg", Assert.Single(early.FirstEligibleIds));
        Assert.Null(Assert.Single(early.Confirmations).Event.OriginZone!.InvalidationUtc);
        Assert.Empty(early.FvgDiagnostics);
        Assert.Equal(M5FirstConfirmation.WaitingForConfirmation, Run(0, events, cutoff: 9).FirstOutcome);
        var pre = Run(0, events, gaps: [new(At(0), At(5), At(5), null)]);
        Assert.Equal(M5FirstConfirmation.InsufficientEvidence, pre.FirstOutcome);
        var post = Run(0, events, [Zone("later", 20)], gaps: [new(At(10), At(15), At(15), null)]);
        Assert.Equal("MissingSourceBeforeFvg", Assert.Single(post.FvgDiagnostics).Reason);
        Assert.Null(post.CompletedAtUtc);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void OptionalFvgPathsUseTheirOwnAnchorsForScAndIfvg(bool inversion, bool continuation)
    {
        var origin = Zone("origin", -10, M5FvgDirection.Bearish, 5);
        var trigger = inversion ? Event("ifvg", 5, M5ConfirmationKind.IFVG, origin: origin) : Event("sc", 5);
        M5SequenceZone[] zones = continuation ? [origin, Zone("post-trigger", 10)] : [origin, Zone("pre-trigger", 0)];
        var result = Paths(Run(0, [trigger], zones, cutoff: 12), Minutes(5, 12, 5, 10));
        Assert.All(result.Comparisons, comparison =>
        {
            var anchored = comparison.TriggerAnchored;
            Assert.Equal(At(5), anchored.AnchorUtc);
            Assert.Equal(At(7), anchored.FirstRealignmentUtc);
            var evidence = Assert.Single(anchored.Triggers);
            Assert.Equal(trigger.Kind, evidence.Confirmation.Kind);
            Assert.Equal(inversion ? "NotInvalidatedWithinObservableEvidence" : "ScInvalidationUndefined", evidence.ValidityAtRealignment);
            Assert.Equal("NoEligibleFvgObservedByAssessmentTime", evidence.OptionalFvgStatus);
            Assert.Equal(continuation ? At(10) : null, comparison.FvgAnchored.AnchorUtc);
            Assert.Equal(continuation ? At(12) : null, comparison.FvgAnchored.FirstRealignmentUtc);
            if (continuation)
            {
                Assert.Equal("AfterRealignment", Assert.Single(evidence.OptionalFvgs).RelationToRealignment);
                Assert.Null(evidence.OptionalFvgs[0].StateAtRealignment);
                Assert.Equal("ObservableBeforeRealignment", Assert.Single(comparison.FvgAnchored.Triggers).OptionalFvgStatus);
            }
            else
            {
                Assert.Empty(evidence.OptionalFvgs);
            }
        });
        Assert.Equal(1, result.Summary.DistinctTriggerOnlyRealignments);
        Assert.Equal(2, result.Summary.TriggerOnlyAssociations);
    }

    [Theory]
    [InlineData(7, "AfterRealignment", null)]
    [InlineData(8, "SameCloseOrderingUnresolved", "NotInvalidatedWithinObservableEvidence")]
    [InlineData(9, "BeforeRealignment", "NotInvalidatedWithinObservableEvidence")]
    [InlineData(10, "BeforeRealignment", "NotInvalidatedWithinObservableEvidence")]
    public void OptionalFvgTimingRetainsCausalSupportAndSharedMarketEvents(int pullback, string relation, string? state)
    {
        var row = Run(0, [Event("sc", 5)], [Zone("fvg", 10)], cutoff: 13);
        var result = Paths(row, Minutes(5, 13, pullback));
        Assert.All(result.Comparisons, c =>
        {
            var optional = Assert.Single(Assert.Single(c.TriggerAnchored.Triggers).OptionalFvgs);
            Assert.Equal(relation, optional.RelationToRealignment);
            Assert.Equal(state, optional.StateAtRealignment);
        });
        Assert.Equal(pullback == 10 ? 1 : 0, result.Summary.DistinctSharedRealignments);
    }

    [Fact]
    public void FirstIfvgInvalidationAndDirectionalFvgInversionRetainGeometryAndCannotSelectLaterSc()
    {
        var origin = Zone("origin", -10, M5FvgDirection.Bearish, 5, 15);
        var row = Run(0, [Event("ifvg", 5, M5ConfirmationKind.IFVG, origin: origin), Event("later-sc", 20)],
            [Zone("fvg", 10, inversion: 15), Zone("later-fvg", 25)], cutoff: 30);
        var result = Paths(row, Minutes(5, 30, 14));
        Assert.All(result.Comparisons, c =>
        {
            Assert.Equal(At(16), c.TriggerAnchored.FirstRealignmentUtc);
            Assert.Equal(At(16), c.FvgAnchored.FirstRealignmentUtc);
            var first = Assert.Single(c.TriggerAnchored.Triggers);
            Assert.Equal("ifvg", first.Confirmation.Id);
            Assert.Equal("Invalidated", first.ValidityAtRealignment);
            Assert.Equal("Invalidated", Assert.Single(first.OptionalFvgs).StateAtRealignment);
            Assert.Contains(row.FvgDiagnostics, f => f.Reason == "TriggerInvalidatedByFvgClose");
        });
        Assert.Equal(2, result.Summary.InvalidatedIfvgAtTriggerRealignmentAssociations);
        Assert.Equal(2, result.Summary.InvalidatedFvgAtControlRealignmentAssociations);
    }

    [Fact]
    public void TiesKeepEveryIdentityAndNeverIncreaseDistinctMarketEvents()
    {
        var origin = Zone("origin", -10, M5FvgDirection.Bearish, 5);
        var a = Event("sc-a", 5);
        var b = Event("sc-b", 5);
        var inversion = Event("ifvg", 5, M5ConfirmationKind.IFVG, origin: origin);
        var mixed = Paths(Run(0, [b, inversion, a], cutoff: 7), Minutes(5, 7, 5));
        Assert.All(mixed.Comparisons, c =>
        {
            Assert.Equal("SimultaneousScAndIfvg", c.TriggerOrdering);
            Assert.Equal(["ifvg", "sc-a", "sc-b"], c.TriggerAnchored.Triggers.Select(t => t.Confirmation.Id));
        });
        Assert.Equal(2, mixed.Summary.DistinctFirstTriggers);
        var tied = Paths(Run(0, [b, a], cutoff: 7), Minutes(5, 7, 5));
        Assert.All(tied.Comparisons, c => Assert.Equal("TiedHypotheses", c.TriggerOrdering));
        Assert.Equal(1, tied.Summary.DistinctFirstTriggers);
        Assert.Throws<ArgumentException>(() => Paths(Run(0, [a, a], cutoff: 7), Minutes(5, 7, 5)));
    }

    [Fact]
    public void GapAndWindowEvidenceRemainOwnedByTheirExistingPolicies()
    {
        var before = new M5FvgSourceGap(At(0), At(5), At(5), null);
        var after = new M5FvgSourceGap(At(5), At(10), At(10), null);
        var blocked = Paths(Run(0, [Event("sc", 10)], cutoff: 12, gaps: [before]), Minutes(10, 12, 10));
        Assert.All(blocked.Comparisons, c => Assert.Equal("InsufficientEvidence", c.TriggerAnchored.Outcome));
        var missingM5 = Paths(Run(0, [Event("sc", 5)], cutoff: 12, gaps: [after]), Minutes(5, 12, 10));
        Assert.All(missingM5.Comparisons, c =>
        {
            var evidence = Assert.Single(c.TriggerAnchored.Triggers);
            Assert.Equal("UnknownDueToMissingM5Evidence", evidence.ValidityAtRealignment);
            Assert.Equal("AbsenceUndeterminedDueToM5Gap", evidence.OptionalFvgStatus);
        });
        var missingM1 = Paths(Run(0, [Event("sc", 5)], cutoff: 9), Minutes(7, 9, 7));
        Assert.All(missingM1.Comparisons, c => Assert.Equal("InsufficientEvidence", c.TriggerAnchored.Outcome));
        var limit = Paths(Run(170, [Event("sc", 175)], cutoff: 180), Minutes(175, 180, 178));
        Assert.All(limit.Comparisons, c => Assert.Null(c.TriggerAnchored.FirstRealignmentUtc));
        Assert.All(limit.Scans, s => Assert.Equal(M1WindowPosition.AtExclusiveEnd, Assert.Single(s.Attempts).WindowPosition));

        var filters = Run(0, [Event("wrong", 5, direction: M5FvgDirection.Bearish), Event("other-session", 1000)],
            cutoff: 1005, window: M5SequenceWindowPolicy.WindowEventOnly);
        Assert.Equal(["DirectionMismatch", "ConfirmationOutsideSession"], filters.Confirmations.Select(c => c.Reason));
        Assert.All(Paths(filters, []).Comparisons, c => Assert.Null(c.TriggerAnchored.ScanId));
        var preOpen = Paths(Run(-5, [Event("sc", 5)], cutoff: 7,
            window: M5SequenceWindowPolicy.WindowConfirmationWithPriorSweep), Minutes(5, 7, 5));
        Assert.All(preOpen.Comparisons, c => Assert.True(c.PreWindowSweep));
        var sameSweep = Paths(Run(0, [Event("same", 0)], cutoff: 7, ordering: M5SweepPolicy.SameCloseDiagnostic), []);
        Assert.All(sameSweep.Comparisons, c => Assert.Equal("AmbiguousSweepOrdering", c.TriggerOrdering));
    }

    [Fact]
    public void CutoffPreventsEarlierM1FromCompletingPullbackAndIgnoresFutureM5AndM1()
    {
        var row = Run(0, [Event("first", 5)], cutoff: 6);
        var early = Paths(row, Minutes(2, 6, 2));
        Assert.All(early.Comparisons, c =>
        {
            Assert.Equal("WaitingForCountertrend", c.TriggerAnchored.Outcome);
            Assert.Null(c.TriggerAnchored.FirstPullbackId);
        });
        var pending = Paths(row, Minutes(5, 6, 5));
        Assert.All(pending.Comparisons, c =>
        {
            Assert.NotNull(c.TriggerAnchored.FirstPullbackId);
            Assert.Null(c.TriggerAnchored.FirstRealignmentUtc);
        });
        var expected = Snapshot(pending);
        var future = Run(0, [Event("first", 5), Event("future", 10)], [Zone("future-fvg", 10)], cutoff: 6);
        Assert.Equal(expected, Snapshot(Paths(future, Minutes(5, 8, 5))));
        var changed = Run(0, [Event("first", 5), Event("future", 15)], [Zone("moved-fvg", 15)], cutoff: 6);
        Assert.Equal(expected, Snapshot(Paths(changed, Minutes(5, 8, 5, 7))));
        var exact = Paths(Run(0, [Event("first", 5)], cutoff: 7), Minutes(5, 7, 5));
        Assert.All(exact.Comparisons, c => Assert.Equal(At(7), c.TriggerAnchored.FirstRealignmentUtc));

        static string Snapshot((IReadOnlyList<M1RealignmentScan> Scans, IReadOnlyList<NasdaqM1PathComparison> Comparisons,
            NasdaqPathCounts Summary, IReadOnlyList<NasdaqPathPolicyCounts> ByPolicy) result) =>
            JsonSerializer.Serialize(new { result.Scans, result.Comparisons, result.Summary, result.ByPolicy });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TriggerAnchoredDownstreamUsesExistingProtectiveAndFavorableGeometryWithoutFvg(bool sell)
    {
        var direction = sell ? M5FvgDirection.Bearish : M5FvgDirection.Bullish;
        var row = Run(0, [Event("sc", 5, direction: direction)], cutoff: 7, high: sell);
        var minutes = Minutes(5, 7, 5).Select(c => sell ? Mirror(c) : c).ToArray();
        var report = Downstream(row, minutes, StructuralBars(sell));
        Assert.All(report.Paths, path =>
        {
            Assert.True(path.M5TriggerObserved && path.M1PullbackObserved && path.M1RealignmentObserved);
            Assert.Equal(At(7), path.AssessmentUtc);
            Assert.Null(path.FvgAnchoredMarketRealignmentId);
            Assert.Empty(Assert.Single(path.Triggers).OptionalFvgs);
            Assert.True(path.StructuralSlCandidateAvailable && path.StructuralTpCandidateAvailable);
        });
        var geometry = Assert.Single(report.GeometryByMarketRealignmentId).Value;
        var stop = geometry.Stops.Single(s => s.Profile.LeftBars == 1);
        Assert.Equal(direction, stop.Direction);
        Assert.Equal(sell ? 108 : 92, stop.Selected!.Pivot.Price);
        Assert.Equal(sell ? M5StructuralPivotKind.High : M5StructuralPivotKind.Low, stop.Selected.Pivot.Kind);
        Assert.Contains(geometry.Targets, t => t.Profile.Policy == StructuralTargetPolicy.NearestConfirmedM5Swing &&
            t.SelectedIds.Count > 0 && (sell ? t.TargetPrice < t.CloseReference.Price : t.TargetPrice > t.CloseReference.Price));
        Assert.Equal(StructuralTargetOutcome.ReferencesUnavailable,
            geometry.Targets.Single(t => t.Profile.Policy == StructuralTargetPolicy.NearestAuditedSessionExtreme).Outcome);
        Assert.Equal(new NasdaqDownstreamEventCount(1, 1), report.Summary.Both);
        Assert.False(report.H4ContextAvailable || report.IndependentHumanValidation || report.AuthenticatedTemporalAvailability ||
            report.CanonicalAdmissionPermitted || report.OperationalSignal);
    }

    [Fact]
    public void DownstreamKeepsTiesAndInvalidatedHistoryWithoutBackfillingOptionalFvg()
    {
        var origin = Zone("origin", -10, M5FvgDirection.Bearish, 5, 10);
        var events = new[] { Event("sc", 5), Event("ifvg", 5, M5ConfirmationKind.IFVG, origin: origin), Event("later-sc", 20) };
        var candidate = Run(0, events, [Zone("late", 10)], cutoff: 12);
        var minutes = Minutes(5, 12, 5);
        var report = Downstream(candidate, minutes, StructuralBars(false));
        Assert.All(report.Paths, p =>
        {
            Assert.Equal("SimultaneousScAndIfvg", p.TriggerOrdering);
            Assert.Equal(["ifvg", "sc"], p.Triggers.Select(t => t.ConfirmationId));
            var ifvg = p.Triggers[0];
            Assert.Equal("NotInvalidatedWithinObservableEvidence", ifvg.ValidityAtAssessment);
            Assert.Equal("Invalidated", ifvg.ValidityAtReportCutoff);
            Assert.Equal(At(10), ifvg.OriginInvalidationAtReportCutoff);
            Assert.Null(ifvg.OriginZone!.InvalidationUtc);
            Assert.All(p.Triggers, t => Assert.Empty(t.OptionalFvgs));
            var late = Assert.Single(p.Triggers.Single(t => t.ConfirmationId == "sc").LaterOptionalFvgs);
            Assert.Equal("late", late.Formation.Id);
            Assert.Equal("AfterRealignment", late.RelationToRealignment);
            Assert.Null(late.StateAtRealignment);
        });
        var invalidBeforeM1 = Downstream(Run(0, [Event("ifvg", 5, M5ConfirmationKind.IFVG, origin: origin),
            Event("later-sc", 20)], cutoff: 25), Minutes(5, 25, 14), StructuralBars(false));
        Assert.All(invalidBeforeM1.Paths, p =>
        {
            var trigger = Assert.Single(p.Triggers);
            Assert.Equal("ifvg", trigger.ConfirmationId);
            Assert.Equal("Invalidated", trigger.ValidityAtAssessment);
            Assert.Equal(At(10), trigger.OriginZone!.InvalidationUtc);
        });
        var withSupport = Downstream(Run(0, [Event("sc", 5)], [Zone("support", 10)], cutoff: 16),
            Minutes(5, 16, 13), StructuralBars(false));
        Assert.All(withSupport.Paths, p =>
        {
            var support = Assert.Single(Assert.Single(p.Triggers).OptionalFvgs);
            Assert.Equal("support", support.Formation.Id);
            Assert.Equal(At(10), support.AvailableAtUtc);
            Assert.Equal("NotInvalidatedWithinObservableEvidence", support.StateAtRealignment);
        });
    }

    [Fact]
    public void DownstreamReportsCoverageGapsAndPreservesFixedCutoffWithFutureChanges()
    {
        var row = Run(0, [Event("sc", 5)], cutoff: 7);
        var original = Downstream(row, Minutes(5, 7, 5), StructuralBars(false));
        var future = Run(0, [Event("sc", 5), Event("future", 10)], [Zone("future", 10)], cutoff: 7);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(Downstream(future,
            Minutes(5, 11, 5, 9), StructuralBars(false).Append(Bar(10)).Append(Bar(15)).ToArray())));
        var gaps = Downstream(row, Minutes(6, 8, 6), StructuralBars(false));
        Assert.Empty(gaps.GeometryByMarketRealignmentId);
        Assert.All(gaps.Paths, p =>
        {
            Assert.Equal("InsufficientEvidence", p.M1Outcome);
            Assert.NotEmpty(p.M1SourceGaps);
            Assert.False(p.M1RealignmentObserved);
        });
        var missingM5 = Downstream(row, Minutes(5, 7, 5), StructuralBars(false)[..^1]);
        var references = Assert.Single(missingM5.GeometryByMarketRealignmentId).Value;
        Assert.All(references.Stops, s => Assert.Equal("ExpectedM5CloseMissingAtCutoff", s.ReasonCode));
        Assert.False(references.StructuralSlCandidateAvailable || references.StructuralTpCandidateAvailable);
    }

    private static NasdaqTriggerAnchoredDownstreamReport Downstream(M5SequenceResult row, Candle[] minutes, Candle[] bars)
    {
        var paths = Paths(row, minutes);
        var report = new NasdaqOptionalFvgPathReport("fixture", Day, Provider.Value, Symbol.Value, "fixture",
            new(row.AsOfUtc, null, null, [], []), NasdaqTradingWindowAlignment.Evaluate(row.AsOfUtc, row.AsOfUtc),
            NasdaqResearchChainOutcome.H4ContextUnavailable, 0, "Compared", [row], [], [],
            paths.Scans, paths.Comparisons, paths.Summary, paths.ByPolicy);
        return EvaluateNasdaqTriggerAnchoredDownstreamUseCase.Project(report, [],
            new(Provider, Symbol, M5, bars), new(Provider, Symbol, new(1, TimeframeUnit.Hour), []));
    }

    private static Candle[] StructuralBars(bool mirror)
    {
        int[] lows = [94, 80, 95, 85, 96, 92, 97];
        int[] highs = [108, 106, 112, 105, 115, 107, 109];
        return lows.Select((low, i) =>
        {
            var bar = new Candle(Provider, Symbol, M5, At(-30 + 5 * i), At(-25 + 5 * i), 100, highs[i], low, 100, null);
            return mirror ? Mirror(bar) : bar;
        }).ToArray();
    }

    private static Candle Mirror(Candle c) => new(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc,
        200 - c.Open, 200 - c.Low, 200 - c.High, 200 - c.Close, c.Volume);

    private static (IReadOnlyList<M1RealignmentScan> Scans, IReadOnlyList<NasdaqM1PathComparison> Comparisons,
        NasdaqPathCounts Summary, IReadOnlyList<NasdaqPathPolicyCounts> ByPolicy) Paths(M5SequenceResult row, Candle[] candles)
    {
        var series = new CandleSeries(Provider, Symbol, new(1, TimeframeUnit.Minute), candles);
        var sequence = new M5ContextualSequenceReport("session", row.AsOfUtc, "fixture", [row], [], []);
        var control = new EvaluateM1RealignmentCandidateUseCase().Execute(sequence, series, Enum.GetValues<M1RealignmentProfile>());
        return CompareNasdaqOptionalFvgPathsUseCase.Compare(sequence, control, series);
    }

    private static Candle[] Minutes(int start, int end, params int[] pullbacks) =>
        Enumerable.Range(start, end - start).Select(minute =>
        {
            var opposite = pullbacks.Contains(minute);
            return new Candle(Provider, Symbol, new(1, TimeframeUnit.Minute), At(minute), At(minute + 1),
                100, opposite ? 101 : 104, 97, opposite ? 98 : 103, null);
        }).ToArray();
}
