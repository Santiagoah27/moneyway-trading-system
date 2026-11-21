using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M5StructuralStopLossCandidateTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 6, 23, 13, 25, 0, TimeSpan.Zero);
    private static readonly (decimal Low, decimal High)[] Prices =
        [(10, 20), (8, 23), (11, 20), (9, 22), (12, 20), (10, 21), (13, 20)];

    [Theory]
    [InlineData(M5FvgDirection.Bullish, M5StructuralRelation.HigherLow, 10, 9)]
    [InlineData(M5FvgDirection.Bearish, M5StructuralRelation.LowerHigh, 21, 22)]
    public void SelectsLatestStrictSameSideStructureAndMeasuresExactDistance(M5FvgDirection direction,
        M5StructuralRelation relation, int selected, int previous)
    {
        var reference = Reference(35, direction, direction == M5FvgDirection.Bullish ? 10.125m : 20.875m);
        var result = Evaluate(Fixture(), reference, direction);
        Assert.Equal(M5StructuralStopOutcome.StructuralReferenceIdentified, result.Outcome);
        Assert.Equal(relation, result.Selected!.Relation);
        Assert.Equal(selected, result.Selected.Pivot.Price);
        Assert.Equal(previous, Assert.Single(result.Selected.Previous).Price);
        Assert.Equal(Start.AddMinutes(25), result.Selected.Pivot.ExtremeOpenUtc);
        Assert.Equal(Start.AddMinutes(30), result.Selected.Pivot.ExtremeCloseUtc);
        Assert.Equal(Start.AddMinutes(35), result.Selected.Pivot.ConfirmedAtUtc);
        Assert.Equal(TimeSpan.Zero, result.ConfirmationAge);
        Assert.Same(reference, result.CloseReference.Candle);
        Assert.Equal(reference.Close, result.CloseReference.Price);
        Assert.True(result.OnProtectiveSide);
        Assert.Equal(0.125m, result.DistancePoints);
    }

    [Theory]
    [InlineData(33, 9)]
    [InlineData(34, 9)]
    [InlineData(35, 10)]
    public void RightNeighborMustBeClosedAtTheM1Cutoff(int cutoff, int selected)
    {
        var result = Evaluate(Fixture(), Reference(cutoff));
        Assert.Equal(selected, result.Selected!.Pivot.Price);
        Assert.All(result.ConfirmedSwings, p => Assert.True(p.Pivot.ConfirmedAtUtc <= result.AsOfUtc));
        Assert.Equal(cutoff / 5, result.ClosedM5Count);
        if (cutoff < 35)
            Assert.DoesNotContain(result.ConfirmedSwings, s => s.Pivot.ExtremeOpenUtc == Start.AddMinutes(25));
    }

    [Fact]
    public void FutureAppendMutationAndUnclosedBarDoNotChangeTheObservation()
    {
        var reference = Reference(33);
        var full = Fixture();
        var prefix = full[..6];
        var mutated = prefix.Concat([Bar(6, 1, 90), Bar(7, 2, 99)]).ToArray();
        var expected = JsonSerializer.Serialize(Evaluate(prefix, reference));
        Assert.Equal(expected, JsonSerializer.Serialize(Evaluate(full, reference)));
        Assert.Equal(expected, JsonSerializer.Serialize(Evaluate(mutated, reference)));
        Assert.Equal(expected, JsonSerializer.Serialize(Evaluate(prefix, reference)));
    }

    [Theory]
    [InlineData(1, 1, M5StructuralStopOutcome.StructuralReferenceIdentified)]
    [InlineData(1, 2, M5StructuralStopOutcome.StructuralReferenceIdentified)]
    [InlineData(2, 1, M5StructuralStopOutcome.InsufficientConfirmedPivots)]
    [InlineData(2, 2, M5StructuralStopOutcome.InsufficientConfirmedPivots)]
    [InlineData(3, 3, M5StructuralStopOutcome.InsufficientConfirmedPivots)]
    [InlineData(4, 4, M5StructuralStopOutcome.InsufficientM5History)]
    public void IndependentlyAppliesBothNeighborhoodSizes(int left, int right, M5StructuralStopOutcome outcome)
    {
        var result = Evaluate(Fixture(), Reference(35), profile: new(left, right));
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(new(left, right), result.Profile);
        Assert.All(result.ConfirmedSwings, s => Assert.Equal(TimeSpan.FromMinutes(right * 5),
            s.Pivot.ConfirmedAtUtc - s.Pivot.ExtremeCloseUtc));
    }

    [Fact]
    public void EqualNeighborsRejectExtremaAndEqualConfirmedLevelsRemainSeparate()
    {
        Candle[] series = [Bar(0, 10, 20), Bar(1, 8, 23), Bar(2, 11, 20), Bar(3, 8, 23), Bar(4, 12, 20)];
        var result = Evaluate(series, Reference(25));
        Assert.Equal(M5StructuralStopOutcome.NoConfirmedHigherLow, result.Outcome);
        Assert.Equal(M5StructuralStopOutcome.NoConfirmedLowerHigh,
            Evaluate(series, Reference(25, M5FvgDirection.Bearish), M5FvgDirection.Bearish).Outcome);
        Assert.Contains(result.ConfirmedSwings, s => s.Relation == M5StructuralRelation.EqualHigh);
        Assert.Contains(result.ConfirmedSwings, s => s.Relation == M5StructuralRelation.EqualLow);
        Assert.Equal(2, result.ConfirmedSwings.Count(s => s.Relation == M5StructuralRelation.InsufficientEvidence));
        Assert.All(result.ConfirmedSwings.Where(s => s.Pivot.ExtremeOpenUtc == Start.AddMinutes(5)),
            s => Assert.True(s.Pivot.SimultaneousHighAndLow));
        Assert.Equal(2, result.ConfirmedSwings.Count(s => s.Pivot.ExtremeOpenUtc == Start.AddMinutes(5)));

        var tiedNeighbors = Evaluate([Bar(0, 10, 20), Bar(1, 8, 23), Bar(2, 8, 23), Bar(3, 10, 20)], Reference(20));
        Assert.Empty(tiedNeighbors.ConfirmedSwings);
    }

    [Fact]
    public void SameSideEqualConfirmationTimePreservesCandidatesAndPredecessors()
    {
        M5StructuralPivot Low(string id, int minute, decimal price) =>
            new(id, M5StructuralPivotKind.Low, price, Start.AddMinutes(minute - 10), Start.AddMinutes(minute - 5),
                Start.AddMinutes(minute), "tie", false);
        var first = Low("first", 10, 8);
        var olderHl = Low("older-hl", 15, 9);
        var a = Low("a", 20, 11);
        var b = Low("b", 20, 12);
        var tied = EvaluateM5StructuralStopLossCandidateUseCase.Classify([b, first, a, olderHl]);
        var latest = EvaluateM5StructuralStopLossCandidateUseCase.Latest(tied, M5StructuralRelation.HigherLow);
        Assert.Equal(new[] { "a", "b" }, latest.Select(s => s.Pivot.Id));
        Assert.All(latest, s => Assert.Equal(M5StructuralRelation.Ambiguous, s.Relation));
        var next = EvaluateM5StructuralStopLossCandidateUseCase.Classify([b, first, a, olderHl, Low("next", 25, 13)])[^1];
        Assert.Equal(M5StructuralRelation.Ambiguous, next.Relation);
        Assert.Equal(new[] { "a", "b" }, next.Previous.Select(p => p.Id));
    }

    [Theory]
    [InlineData(M5FvgDirection.Bullish, 10, 10)]
    [InlineData(M5FvgDirection.Bullish, 9.5, 10)]
    [InlineData(M5FvgDirection.Bearish, 21, 21)]
    [InlineData(M5FvgDirection.Bearish, 21.5, 21)]
    public void LatestNonprotectiveReferenceNeverFallsBack(M5FvgDirection direction, double reference, int pivot)
    {
        var result = Evaluate(Fixture(), Reference(35, direction, (decimal)reference), direction);
        Assert.Equal(M5StructuralStopOutcome.ReferenceNotProtective, result.Outcome);
        Assert.Equal(pivot, result.Selected!.Pivot.Price);
        Assert.False(result.OnProtectiveSide);
        Assert.Null(result.DistancePoints);
    }

    [Fact]
    public void MissingIntervalsCannotSupplyNeighborsOrPrecedingStructure()
    {
        var candles = Fixture();
        var afterGap = Evaluate([.. candles[..3], .. candles[4..]], Reference(35));
        var gap = Assert.Single(afterGap.SourceGaps);
        Assert.Equal(Start.AddMinutes(15), gap.StartUtc);
        Assert.Equal(Start.AddMinutes(20), gap.EndUtc);
        Assert.Equal(M5StructuralStopOutcome.InsufficientConfirmedPivots, afterGap.Outcome);
        Assert.Equal(M5StructuralRelation.InsufficientEvidence,
            Assert.Single(afterGap.ConfirmedSwings, s => s.Pivot.Kind == M5StructuralPivotKind.Low).Relation);
        Assert.Null(afterGap.Selected);

        var missingTail = Evaluate(candles[..6], Reference(35));
        Assert.Equal(M5StructuralStopOutcome.MissingSourceEvidence, missingTail.Outcome);
        Assert.Equal(Start.AddMinutes(35), Assert.Single(missingTail.SourceGaps).EndUtc);
        Assert.Equal(M5StructuralStopOutcome.InsufficientM5History, Evaluate([], Reference(35)).Outcome);
    }

    [Fact]
    public void RealignmentBodyMustBeConsistentWithTheSequenceDirection()
    {
        Assert.Throws<ArgumentException>(() => Evaluate(Fixture(), Reference(35, M5FvgDirection.Bearish)));
    }

    private static M5StructuralStopObservation Evaluate(Candle[] candles, Candle reference,
        M5FvgDirection direction = M5FvgDirection.Bullish, M5StructuralPivotProfile? profile = null) =>
        new EvaluateM5StructuralStopLossCandidateUseCase().Execute(new(Provider, Symbol, M5, candles),
            reference, direction, profile ?? new(1, 1), "fixture");

    private static Candle[] Fixture() => Prices.Select((p, i) => Bar(i, p.Low, p.High)).ToArray();

    private static Candle Bar(int index, decimal low, decimal high) =>
        new(Provider, Symbol, M5, Start.AddMinutes(index * 5), Start.AddMinutes((index + 1) * 5),
            low + 1, high, low, low + 1, null);

    private static Candle Reference(int closeMinute, M5FvgDirection direction = M5FvgDirection.Bullish, decimal close = 15)
    {
        var open = direction == M5FvgDirection.Bullish ? close - 1 : close + 1;
        return new(Provider, Symbol, M1, Start.AddMinutes(closeMinute - 1), Start.AddMinutes(closeMinute),
            open, Math.Max(open, close), Math.Min(open, close), close, null);
    }
}
