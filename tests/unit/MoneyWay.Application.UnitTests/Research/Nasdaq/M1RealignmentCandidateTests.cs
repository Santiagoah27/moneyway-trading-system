using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M1RealignmentCandidateTests
{
    private static readonly DateTimeOffset Opening = new(2026, 6, 23, 13, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static DateTimeOffset At(int minute) => Opening.AddMinutes(minute);

    private static Candle Bar(int minute, decimal open, decimal close, decimal high = 110, decimal low = 90, bool mirror = false) =>
        mirror ? new(Provider, Symbol, M1, At(minute), At(minute + 1), 200 - open, 200 - low, 200 - high, 200 - close, null)
               : new(Provider, Symbol, M1, At(minute), At(minute + 1), open, high, low, close, null);
    private static Candle Five(int close) => new(Provider, Symbol, M5, At(close - 5), At(close), 100, 110, 90, 100, null);

    private static M5SequenceResult Sequence(DateTimeOffset cutoff, int? anchor = 0, bool bearish = false,
        int? fvgInvalidation = null, int? ifvgInvalidation = null, string episodeId = "ep",
        M5FvgProfile profile = M5FvgProfile.GeometryOnly, int? evidenceLimited = null)
    {
        var reference = new ContextualLiquidityReference("ref", "fixture", 100, bearish, Day, Provider.Value, Symbol.Value,
            "1h", [], null, At(-60), "complete", "fixture");
        var selection = new ContextualScEventSelection("E1", "1", reference, "Selected", "fixture", null, null, null, null, [], "complete");
        var direction = bearish ? M5FvgDirection.Bearish : M5FvgDirection.Bullish;
        var config = new M5SequenceConfiguration(M5SweepPolicy.StrictAfterSweep, M5SequenceWindowPolicy.PostSweepObservation,
            M5SequenceFvgPolicy.FormationAfterConfirmation, profile, new(8, 30), new(11, 30));
        M5ConfirmationDiagnostic[] confirmations = [];
        M5SequenceFvgDiagnostic[] fvgs = [];
        if (anchor is { } a)
        {
            var opposite = bearish ? M5FvgDirection.Bullish : M5FvgDirection.Bearish;
            var origin = new M5SequenceZone(new($"origin/{profile}/{a}", profile, opposite, Five(a - 25), Five(a - 20),
                Five(a - 15), 95, 105, true, "fixture"), At(a - 5), ifvgInvalidation is { } v ? At(v) : null, null);
            confirmations = [new(new("trigger/" + a, M5ConfirmationKind.IFVG, direction, At(a - 5), "candidate", "config",
                origin.Formation.Id, "fixture", "verified", true, Five(a - 5), origin), true, true, "Eligible", "Inverted")];
            var fvg = new M5SequenceZone(new($"zone/{profile}/{a}", profile, direction, Five(a - 10), Five(a - 5),
                Five(a), 101, 102, true, "fixture"), fvgInvalidation is { } f ? At(f) : null, null,
                evidenceLimited is { } limited ? At(limited) : null);
            fvgs = [new(confirmations[0].Event.Id, fvg, "Eligible", "Formed", "Formed", "Inverted")];
        }
        var ids = confirmations.Select(c => c.Event.Id).ToArray();
        return new(new(episodeId, Day, selection), config, cutoff, Opening, At(180), direction, "Eligible", confirmations,
            ids, null, M5FirstConfirmation.IFVGFirst, ids, null, fvgs, fvgs, anchor is { } time ? At(time) : null, [], [], []);
    }

    private static M1RealignmentReport Run(DateTimeOffset cutoff, Candle[] candles, params M5SequenceResult[] sequences) =>
        new EvaluateM1RealignmentCandidateUseCase().Execute(new("session", cutoff, "fixture", sequences, [], []),
            new(Provider, Symbol, M1, candles), Enum.GetValues<M1RealignmentProfile>());

    [Fact]
    public void MissingOrFutureM5CompletionDoesNotCreateAnchor()
    {
        var result = Run(At(1), [Bar(0, 102, 99)], Sequence(At(1), null), Sequence(At(1), 5, episodeId: "future"));
        Assert.Empty(result.Scans);
        Assert.All(result.Associations, x => Assert.Equal("WaitingForM5Sequence", x.Outcome));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProfilesFreezeOppositeAndNeutralRangeAndRequireStrictReclaim(bool mirror)
    {
        Candle[] candles = [Bar(-2, 103, 101, mirror: mirror), Bar(-1, 100, 105, mirror: mirror),
            Bar(0, 104, 101, mirror: mirror), Bar(1, 101, 100, mirror: mirror), Bar(2, 100, 100, high: 112, low: 88, mirror: mirror),
            Bar(3, 100, 112, high: 120, mirror: mirror), Bar(4, 107, 111, high: 115, mirror: mirror),
            Bar(5, 111, 113, high: 115, mirror: mirror)];
        var result = Run(At(6), candles, Sequence(At(6), bearish: mirror));
        var body = Assert.Single(result.Scans, s => s.Profile == M1RealignmentProfile.BodyDirectionTurn);
        var reclaim = Assert.Single(result.Scans, s => s.Profile == M1RealignmentProfile.PullbackRangeReclaim);
        var attempt = Assert.Single(reclaim.Attempts);
        Assert.Equal(At(0), attempt.StartUtc);
        Assert.Equal(At(1), attempt.ObservedAtUtc);
        Assert.Equal(At(3), attempt.EndUtc);
        Assert.Equal(88m, attempt.Low);
        Assert.Equal(112m, attempt.High);
        Assert.Equal(2, attempt.OppositeBodies);
        Assert.Equal(1, attempt.NeutralBodies);
        Assert.Equal(At(4), body.FirstRealignmentUtc);
        Assert.Equal(At(6), reclaim.FirstRealignmentUtc);
        Assert.Equal(2, body.PreAnchorClosedM1Count);
        var before = At(5).AddTicks(-1);
        var pending = Run(before, candles, Sequence(before, bearish: mirror)).Scans.Single(s => s.Profile == reclaim.Profile);
        Assert.Equal(M1AttemptStatus.WaitingForRealignment, Assert.Single(pending.Attempts).Status);
        Assert.Null(pending.FirstRealignmentUtc);
    }

    [Fact]
    public void InterruptedRangeGetsNewIdentityAndHistoricalFirstSurvivesLaterAttempts()
    {
        Candle[] candles = [Bar(0, 104, 100), Bar(1, 100, 106), Bar(2, 108, 108, high: 120),
            Bar(3, 108, 100, high: 109), Bar(4, 101, 111, high: 112),
            Bar(5, 110, 100), Bar(6, 101, 113, high: 115)];
        var report = Run(At(7), candles, Sequence(At(7)));
        var reclaim = report.Scans.Single(s => s.Profile == M1RealignmentProfile.PullbackRangeReclaim);
        Assert.Equal(3, reclaim.Attempts.Count);
        Assert.Equal(M1AttemptStatus.InterruptedPullback, reclaim.Attempts[0].Status);
        Assert.Equal(At(4), reclaim.Attempts[0].InterruptedAtUtc);
        Assert.Equal(110m, reclaim.Attempts[0].High);
        Assert.Equal(109m, reclaim.Attempts[1].High);
        Assert.Equal(3, reclaim.Attempts.Select(x => x.Id).Distinct().Count());
        Assert.Equal(At(5), reclaim.FirstRealignmentUtc);
        Assert.Equal(At(7), reclaim.Attempts[^1].ConfirmedAtUtc);
    }

    [Fact]
    public void WindowIsHalfOpenWithOutsideObservationsPreserved()
    {
        var opening = Run(At(0), [Bar(-2, 101, 99), Bar(-1, 100, 102)], Sequence(At(0), -2)).Scans[0];
        Assert.Equal(At(0), opening.FirstRealignmentUtc);
        var end = Run(At(182), [Bar(178, 102, 98), Bar(179, 100, 105), Bar(180, 102, 98), Bar(181, 100, 105)],
            Sequence(At(182), 178)).Scans[0];
        Assert.Equal([M1WindowPosition.AtExclusiveEnd, M1WindowPosition.AfterWindow], end.Attempts.Select(a => a.WindowPosition));
        Assert.True(end.WindowExpiredWithoutRealignment);
        Assert.Null(end.FirstRealignmentUtc);
    }

    [Fact]
    public void GapsRetainIncompleteAttemptsAndBlockProofOfFirstRealignment()
    {
        var result = Run(At(4), [Bar(0, 102, 98), Bar(2, 102, 98), Bar(3, 100, 105)], Sequence(At(4))).Scans[0];
        Assert.Equal(M1AttemptStatus.InsufficientEvidence, result.Attempts[0].Status);
        Assert.Equal(At(2), Assert.Single(result.SourceGaps).ObservableAtUtc);
        Assert.Equal(At(4), result.Attempts[1].ConfirmedAtUtc);
        Assert.False(result.Attempts[1].ContinuousFromAnchor);
        Assert.Null(result.FirstRealignmentUtc);
        var noCountertrend = Run(At(1), [Bar(0, 100, 105)], Sequence(At(1))).Scans[0];
        Assert.Empty(noCountertrend.Attempts);
        Assert.Equal("WaitingForCountertrend", noCountertrend.Outcome);
    }

    [Theory]
    [InlineData(1, "InvalidatedBeforeOrAtPullbackObservation", "Invalidated")]
    [InlineData(2, "InvalidatedDuringPullback", "Invalidated")]
    [InlineData(3, "InvalidatedBeforeOrAtRealignment", "Invalidated")]
    [InlineData(4, "InvalidatedAfterRealignment", "NotInvalidatedWithinObservableEvidence")]
    public void M5ValidityIsEvaluatedAtEventCloseWithoutErasingM1(int invalidated, string phase, string state)
    {
        Candle[] candles = [Bar(0, 105, 102), Bar(1, 104, 102), Bar(2, 103, 106), Bar(3, 103, 106)];
        var report = Run(At(4), candles, Sequence(At(4), fvgInvalidation: invalidated, ifvgInvalidation: 4),
            Sequence(At(4), episodeId: "other", profile: M5FvgProfile.MiddleCloseConfirmation, evidenceLimited: 2));
        Assert.Equal(2, report.Scans.Count);
        Assert.Equal(4, report.Associations.Count);
        Assert.Equal(1, report.DistinctAnchorTimestamps);
        var body = report.Scans[0];
        Assert.Equal(At(3), body.FirstRealignmentUtc);
        var timelines = report.ZoneTimelines.Values.Where(t => t.ScanId == body.Id && t.Zone.Role == "DirectionalFVG").ToArray();
        var assessment = Assert.Single(Assert.Single(timelines, t => t.Zone.InvalidatedAtUtc is not null).Attempts);
        Assert.Equal(phase, assessment.InvalidationRelation);
        Assert.Equal(state, assessment.StateAtRealignment);
        Assert.Contains(timelines, t => t.Attempts.Any(x => x.StateAtRealignment == "UnknownDueToMissingData"));
    }

    [Fact]
    public void FixedCutoffIgnoresFutureM1AndCanReproduceIncompletePullback()
    {
        var cutoff = At(1);
        Candle[] prefix = [Bar(0, 105, 102)];
        var m5 = Sequence(cutoff, fvgInvalidation: 2);
        var first = Run(cutoff, prefix, m5);
        Assert.Equal(M1AttemptStatus.CountertrendObserved, Assert.Single(first.Scans[0].Attempts).Status);
        Assert.Null(first.Scans[0].FirstRealignmentUtc);
        Assert.All(first.ZoneTimelines.Values, z => Assert.Null(z.Zone.InvalidatedAtUtc));
        var expected = JsonSerializer.Serialize(first);
        Assert.Equal(expected, JsonSerializer.Serialize(Run(cutoff, prefix, m5)));
        Assert.Equal(expected, JsonSerializer.Serialize(Run(cutoff, [.. prefix, Bar(1, 101, 109)], m5)));
        Assert.Equal(expected, JsonSerializer.Serialize(Run(cutoff, [.. prefix, Bar(1, 109, 98)], m5)));
    }
}
