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
}
