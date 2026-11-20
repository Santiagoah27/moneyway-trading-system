using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class H4ContextCandidateTests
{
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe Frame = new(4, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int index) => Start.AddHours(4 * index);
    private static Candle Bar(int index, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, Frame, At(index), At(index + 1), open, high, low, close, null);

    private static Candle[] Prefix() =>
        [Bar(0, 100, 105, 95, 100), Bar(1, 100, 110, 90, 100), Bar(2, 100, 105, 95, 100)];

    private static H4ContextObservation Run(Candle[] candles, int horizon = 1, DateTimeOffset? cutoff = null,
        int legs = 1, IReadOnlyList<H4DocumentedClosure>? closures = null) =>
        new EvaluateH4ContextCandidateUseCase().Execute(
            new(new(Provider, Symbol, Frame, candles), Start, cutoff ?? candles[^1].CloseTimeUtc,
                new($"PROFILE-{legs}", legs, legs), "synthetic", closures), horizon);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BreakoutUsesPriorCloseOpenAndStrictCloseAndDoesNotRepeatOnContinuation(bool upper)
    {
        var first = upper ? Bar(3, 100, 115, 88, 111) : Bar(3, 100, 112, 85, 89);
        var next = upper ? Bar(4, 111, 114, 108, 112) : Bar(4, 89, 92, 86, 88);
        var result = Run([.. Prefix(), first, next]);
        var observed = result.CandleHistory[3];
        Assert.Equal(upper ? H4LevelOutcome.BullishBreakout : H4LevelOutcome.BearishBreakout,
            (upper ? observed.Upper : observed.Lower).Outcome);
        Assert.Equal(upper ? H4LevelOutcome.LowerImmediateRejection : H4LevelOutcome.UpperImmediateRejection,
            (upper ? observed.Lower : observed.Upper).Outcome);
        Assert.True(observed.IntrabarOrderingUnknown);
        Assert.Equal(H4WickOutcome.FullyReached, observed.UpperWick.Outcome);
        var continued = result.CandleHistory[4];
        Assert.Equal(H4LevelOutcome.ContinuedBeyond, (upper ? continued.Upper : continued.Lower).Outcome);
        Assert.Equal(H4DelayedStatus.HorizonCompleted, result.DelayedHistory[^1].Status);
        Assert.Single(result.DelayedHistory.Where(track => track.Status == H4DelayedStatus.Pending).Select(track => track.BreakoutOpenUtc).Distinct());
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.False(result.OperationalSignal);
    }

    [Fact]
    public void ConfirmationAtOpenIsEligibleButConfirmationAtCloseIsNot()
    {
        var result = Run([.. Prefix(), Bar(3, 100, 115, 95, 111)]);
        Assert.Null(result.CandleHistory[2].Upper.Reference);
        var reference = Assert.IsType<H4ConfirmedPivot>(result.CandleHistory[3].Upper.Reference);
        Assert.Equal(At(3), reference.ConfirmedAtUtc);
        Assert.Equal(At(1), reference.ExtremeOpenUtc);
        var highOnly = Run([Prefix()[0], Bar(1, 100, 110, 95, 100), Prefix()[2], Bar(3, 100, 115, 96, 111)]);
        Assert.Equal(H4LevelOutcome.BullishBreakout, highOnly.CandleHistory[3].Upper.Outcome);
        Assert.Equal(H4LevelOutcome.InsufficientEvidence, highOnly.CandleHistory[3].Lower.Outcome);
        Assert.Null(Run([.. Prefix(), Bar(3, 100, 115, 95, 111)], legs: 2).CandleHistory[3].Upper.Reference);
    }

    [Fact]
    public void TouchBoundaryAndOpeningGapAreDistinct()
    {
        var touch = Run([.. Prefix(), Bar(3, 100, 110, 90, 100)]).LatestClosedCandle!;
        Assert.Equal(H4LevelOutcome.Touch, touch.Upper.Outcome);
        Assert.Equal(H4LevelOutcome.Touch, touch.Lower.Outcome);
        Assert.False(touch.Upper.Penetrated);
        var at = Run([.. Prefix(), Bar(3, 100, 115, 96, 110)]).LatestClosedCandle!;
        Assert.Equal(H4LevelOutcome.CloseAtReference, at.Upper.Outcome);
        Assert.True(at.Upper.Penetrated);
        var gap = Run([.. Prefix(), Bar(3, 112, 116, 109, 115)]);
        Assert.Equal(H4LevelOutcome.OpeningGapBeyond, gap.LatestClosedCandle!.Upper.Outcome);
        Assert.Empty(gap.DelayedHistory);
        var low = Run([.. Prefix(), Bar(3, 88, 91, 85, 90)]).LatestClosedCandle!;
        Assert.Equal(H4LevelOutcome.OpeningGapBeyond, low.Lower.Outcome);
        Assert.True(low.Lower.ClosedAtReference);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    public void DelayedInvalidationResolvesOnlyAtCloseAgainstFrozenLevel(int horizon, bool firstBar)
    {
        var breakout = Bar(3, 100, 115, 96, 111);
        var hold = Bar(4, 111, 114, 109, 110);
        var reject = firstBar ? Bar(4, 111, 114, 105, 109) : Bar(5, 110, 112, 104, 109);
        Candle[] input = firstBar ? [.. Prefix(), breakout, reject] : [.. Prefix(), breakout, hold, reject];
        var before = Run(input, horizon, reject.CloseTimeUtc.AddTicks(-1));
        Assert.Equal(H4DelayedStatus.Pending, before.DelayedHistory[^1].Status);
        var after = Run(input, horizon);
        var invalidation = after.DelayedHistory[^1];
        Assert.Equal(H4DelayedStatus.Invalidated, invalidation.Status);
        Assert.Equal(reject.CloseTimeUtc, invalidation.InvalidationUtc);
        Assert.Equal(At(1), invalidation.Reference.ExtremeOpenUtc);
        if (!firstBar)
        {
            Assert.Equal(At(3), after.LatestClosedCandle!.Upper.Reference!.ExtremeOpenUtc);
            Assert.Equal(H4DelayedStatus.HorizonCompleted, Run(input, 1).DelayedHistory[^1].Status);
        }
    }

    [Fact]
    public void BearishBreakoutInvalidatesStrictlyAboveFrozenLow()
    {
        var result = Run([.. Prefix(), Bar(3, 100, 105, 85, 89), Bar(4, 89, 95, 86, 91)]);
        Assert.Equal(H4DelayedStatus.Invalidated, result.DelayedHistory[^1].Status);
        Assert.Equal(90m, result.DelayedHistory[^1].Reference.Price);
    }

    [Fact]
    public void WickGeometryLabelsPartialFullTouchNoneAndZeroLength()
    {
        var prior = Bar(0, 100, 112, 90, 102);
        var partial = Run([prior, Bar(1, 101, 107, 95, 101)]).LatestClosedCandle!;
        Assert.Equal(H4WickOutcome.PartiallyReached, partial.UpperWick.Outcome);
        Assert.Equal(102m, partial.UpperWick.Base);
        Assert.Equal(10m, partial.UpperWick.Length);
        Assert.Equal(5m, partial.UpperWick.MaximumReachFromBase);
        Assert.Equal(0.5m, partial.LowerWick.ReachRatio);
        Assert.Equal(95m, partial.LowerWick.IntersectionLow);
        var full = Run([prior, Bar(1, 101, 115, 88, 101)]).LatestClosedCandle!;
        Assert.True(full.UpperWick.BothBoundariesIncluded);
        Assert.Equal(H4WickOutcome.FullyReached, full.LowerWick.Outcome);
        var touch = Run([prior, Bar(1, 101, 102, 101, 101)]).LatestClosedCandle!;
        Assert.Equal(H4WickOutcome.BoundaryTouched, touch.UpperWick.Outcome);
        Assert.Equal(H4WickOutcome.NotReached, touch.LowerWick.Outcome);
        var zero = Run([Bar(0, 100, 102, 100, 102), Bar(1, 101, 115, 88, 101)]).LatestClosedCandle!;
        Assert.Equal(H4WickOutcome.NotApplicable, zero.UpperWick.Outcome);
        Assert.Equal(H4WickOutcome.NotApplicable, zero.LowerWick.Outcome);
    }

    [Fact]
    public void OpeningInsideOrBeyondWickKeepsOnlyGeometricIntersection()
    {
        var prior = Bar(0, 100, 112, 90, 102);
        var inside = Run([prior, Bar(1, 106, 114, 101, 111)]).LatestClosedCandle!.UpperWick;
        Assert.Equal(H4WickOutcome.GapAmbiguous, inside.Outcome);
        Assert.True(inside.BothBoundariesIncluded);
        Assert.True(inside.GapUncertainty);
        Assert.Null(inside.ReachRatio);
        var beyond = Run([prior, Bar(1, 88, 89, 85, 87)]).LatestClosedCandle!.LowerWick;
        Assert.Equal(H4WickOutcome.GapAmbiguous, beyond.Outcome);
        Assert.Null(beyond.IntersectionLow);
    }

    [Fact]
    public void UnknownGapLimitsTrackingWhileDocumentedClosurePreservesSubsequentCount()
    {
        Candle[] input = [.. Prefix(), Bar(3, 100, 115, 95, 111), Bar(5, 111, 112, 105, 109)];
        var unknown = Run(input);
        Assert.Equal(H4DelayedStatus.EvidenceLimited, unknown.DelayedHistory[^1].Status);
        Assert.Equal(At(5), unknown.DelayedHistory[^1].ObservedAtUtc);
        Assert.Equal("UnexplainedSourceGap", unknown.LatestClosedCandle!.CoverageReason);
        Assert.Equal(H4WickOutcome.InsufficientEvidence, unknown.LatestClosedCandle.UpperWick.Outcome);
        var tail = Run(input, cutoff: At(5));
        Assert.Equal(unknown.DelayedHistory[^1], tail.DelayedHistory[^1]);
        var documented = Run(input, closures: [new(At(4), At(5), "synthetic closure")]);
        Assert.Equal(H4DelayedStatus.Invalidated, documented.DelayedHistory[^1].Status);
        Assert.Equal(1, documented.DelayedHistory[^1].ObservedSubsequentCandles);
        Assert.Equal("DocumentedMarketClosure", documented.LatestClosedCandle!.CoverageReason);
    }

    [Fact]
    public void AppendingOrMutatingFutureCandlesCannotChangeEarlierReport()
    {
        Candle[] prefix = [.. Prefix(), Bar(3, 100, 115, 95, 111)];
        var cutoff = At(4).AddHours(1);
        var snapshot = Run(prefix, 2, cutoff);
        Assert.Equal(TimeSpan.FromHours(1), snapshot.LatestClosedCandleAge);
        var expected = JsonSerializer.Serialize(snapshot);
        Assert.Equal(expected, JsonSerializer.Serialize(Run([.. prefix, Bar(4, 110, 120, 108, 119)], 2, cutoff)));
        Assert.Equal(expected, JsonSerializer.Serialize(Run([.. prefix, Bar(4, 75, 82, 70, 80)], 2, cutoff)));
    }
}
