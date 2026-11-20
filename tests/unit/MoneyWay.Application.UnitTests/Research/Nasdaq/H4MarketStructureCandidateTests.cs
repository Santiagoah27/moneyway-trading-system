using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class H4MarketStructureCandidateTests
{
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe Frame = new(4, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Origin = new(2026, 6, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly EvaluateH4MarketStructureCandidateUseCase useCase = new();

    private static Candle Bar(int index, decimal high, decimal low, int? hour = null) =>
        new(Provider, Symbol, Frame, Origin.AddHours(hour ?? index * 4), Origin.AddHours((hour ?? index * 4) + 4),
            8m, high, low, 8m, 1m);

    private static CandleSeries Series(decimal[] highs, decimal[] lows) => new(Provider, Symbol, Frame,
        highs.Select((high, index) => Bar(index, high, lows[index])));

    private H4MarketStructureObservation Evaluate(CandleSeries series, int left = 1, int right = 1,
        DateTimeOffset? cutoff = null, DateTimeOffset? start = null, IReadOnlyList<H4DocumentedClosure>? closures = null) =>
        useCase.Execute(new(series, start ?? Origin, cutoff ?? series.EndTimeUtc ?? Origin.AddHours(4),
            new($"TEST-{left}-{right}", left, right), "synthetic-test-source", closures));

    [Fact]
    public void StrictHighLowAndRightConfirmationAreCausal()
    {
        var series = Series([10, 15, 11, 12, 10], [5, 4, 2, 5, 6]);
        var before = Evaluate(series, cutoff: Origin.AddHours(7).AddMinutes(59));
        Assert.Empty(before.ConfirmedPivots);
        var at = Evaluate(series, cutoff: Origin.AddHours(12));
        var high = Assert.Single(at.ConfirmedPivots, p => p.Kind == H4PivotKind.High);
        Assert.Equal(15, high.Price);
        Assert.Equal(Origin.AddHours(4), high.ExtremeOpenUtc);
        Assert.Equal(Origin.AddHours(8), high.ExtremeNominalCloseUtc);
        Assert.Equal(Origin.AddHours(12), high.ConfirmedAtUtc);
        Assert.Equal(TimeSpan.FromHours(4), high.ConfirmationDelay);
        Assert.Equal("synthetic-test-source", high.SourceProvenance);
        Assert.Equal("TEST-1-1", high.ProfileId);
        Assert.Equal(1, high.LeftBars);
        Assert.DoesNotContain(at.ConfirmedPivots, p => p.Kind == H4PivotKind.Low);
        var later = Evaluate(series, cutoff: Origin.AddHours(16));
        Assert.Equal(2, later.ConfirmedPivots.Count);
        Assert.Contains(later.ConfirmedPivots, p => p.Kind == H4PivotKind.Low && p.Price == 2 && p.ConfirmedAtUtc == Origin.AddHours(16));
    }

    [Fact]
    public void EqualityDoesNotMakeStrictPivotAndSimultaneousExtremaRemainSeparate()
    {
        var tied = Series([10, 15, 15, 10], [5, 2, 2, 5]);
        Assert.Empty(Evaluate(tied).ConfirmedPivots);
        var dual = Series([10, 15, 10], [5, 2, 5]);
        var pivots = Evaluate(dual).ConfirmedPivots;
        Assert.Equal([H4PivotKind.High, H4PivotKind.Low], pivots.Select(p => p.Kind));
        Assert.Equal(pivots[0].ExtremeOpenUtc, pivots[1].ExtremeOpenUtc);
    }

    [Fact]
    public void ProfilesAreExplicitAndIndependent()
    {
        var series = Series([10, 15, 11, 16, 10, 17, 11], [5, 2, 5, 3, 5, 4, 5]);
        var one = Evaluate(series);
        var two = Evaluate(series, 2, 2);
        var three = Evaluate(series, 3, 3);
        Assert.True(one.ConfirmedPivots.Count > two.ConfirmedPivots.Count);
        Assert.All(three.ConfirmedPivots, p => Assert.Equal("TEST-3-3", p.ProfileId));
        Assert.Equal(3, H4PivotProfile.InitialResearchProfiles.Count);
    }

    [Fact]
    public void TwoRightBarsDelayConfirmationUntilSecondRightClose()
    {
        var series = Series([10, 11, 15, 12, 10], [5, 5, 5, 5, 5]);
        Assert.Empty(Evaluate(series, 2, 2, cutoff: Origin.AddHours(20).AddTicks(-1)).ConfirmedPivots);
        var high = Assert.Single(Evaluate(series, 2, 2, cutoff: Origin.AddHours(20)).ConfirmedPivots);
        Assert.Equal(Origin.AddHours(20), high.ConfirmedAtUtc);
        Assert.Equal(Origin.AddHours(8), high.ExtremeOpenUtc);
    }

    [Theory]
    [InlineData(true, H4StructureClassification.Bullish, H4HighRelation.HigherHigh, H4LowRelation.HigherLow)]
    [InlineData(false, H4StructureClassification.Bearish, H4HighRelation.LowerHigh, H4LowRelation.LowerLow)]
    public void IndependentHighAndLowSequencesClassifyStructure(bool rising,
        H4StructureClassification expected, H4HighRelation high, H4LowRelation low)
    {
        var highs = rising ? new decimal[] { 10, 15, 11, 12, 16, 11, 12, 17, 11, 12 } :
                             [10, 17, 11, 12, 16, 11, 12, 15, 11, 12];
        var lows = rising ? new decimal[] { 5, 4, 2, 5, 6, 3, 6, 7, 4, 8 } :
                            [8, 7, 4, 7, 6, 3, 6, 5, 2, 5];
        var result = Evaluate(Series(highs, lows));
        Assert.Equal(expected, result.Classification);
        Assert.Equal(high, result.HighRelation);
        Assert.Equal(low, result.LowRelation);
        Assert.Equal(H4ResearchStatus.Classified, result.Status);
        Assert.Equal(3, result.ConfirmedPivots.Count(p => p.Kind == H4PivotKind.High));
        Assert.Equal(3, result.ConfirmedPivots.Count(p => p.Kind == H4PivotKind.Low));
        Assert.NotNull(result.PreviousHigh);
        Assert.NotNull(result.CurrentLow);
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.False(result.OperationalSignal);
    }

    [Fact]
    public void OppositeRelationsAreMixedAndEqualRelationsIndeterminate()
    {
        var highs = new decimal[] { 10, 15, 11, 12, 16, 11, 12, 17, 11, 12 };
        var lowDescending = new decimal[] { 8, 7, 4, 7, 6, 3, 6, 5, 2, 5 };
        Assert.Equal(H4StructureClassification.Mixed, Evaluate(Series(highs, lowDescending)).Classification);
        var lowerHighs = new decimal[] { 10, 17, 11, 12, 16, 11, 12, 15, 11, 12 };
        var higherLows = new decimal[] { 5, 4, 2, 5, 6, 3, 6, 7, 4, 8 };
        Assert.Equal(H4StructureClassification.Mixed, Evaluate(Series(lowerHighs, higherLows)).Classification);
        var equalHighs = new decimal[] { 10, 15, 11, 12, 15, 11, 12, 15, 11, 12 };
        var equality = Evaluate(Series(equalHighs, lowDescending));
        Assert.Equal(H4HighRelation.EqualHigh, equality.HighRelation);
        Assert.Equal(H4StructureClassification.Indeterminate, equality.Classification);
        var equalLows = new decimal[] { 5, 4, 2, 5, 6, 2, 6, 7, 2, 8 };
        var lowEquality = Evaluate(Series(highs, equalLows));
        Assert.Equal(H4LowRelation.EqualLow, lowEquality.LowRelation);
        Assert.Equal(H4StructureClassification.Indeterminate, lowEquality.Classification);
        var dualLatestLows = new decimal[] { 5, 4, 2, 5, 6, 3, 6, 1, 4, 8 };
        var dualLatest = Evaluate(Series(highs, dualLatestLows));
        Assert.Equal(dualLatest.CurrentHigh!.ExtremeOpenUtc, dualLatest.CurrentLow!.ExtremeOpenUtc);
        Assert.Equal(H4StructureClassification.Indeterminate, dualLatest.Classification);
    }

    [Fact]
    public void InsufficientHistoryAndPivotsAreDistinct()
    {
        var tiny = Series([10, 11], [5, 4]);
        Assert.Equal(H4ResearchStatus.InsufficientHistory, Evaluate(tiny).Status);
        var flat = Series([10, 11, 12, 13, 14], [5, 5, 5, 5, 5]);
        Assert.Equal(H4ResearchStatus.InsufficientConfirmedPivots, Evaluate(flat).Status);
        Assert.Equal(H4StructureClassification.InsufficientEvidence, Evaluate(flat).Classification);
        Assert.Equal(H4ResearchStatus.InsufficientHistory, Evaluate(flat, start: Origin.AddHours(-4)).Status);
        var documentedLead = new H4DocumentedClosure(Origin.AddHours(-4), Origin, "documented-test-closure");
        var withClosure = Evaluate(flat, start: Origin.AddHours(-4), closures: [documentedLead]);
        Assert.Equal(H4ResearchStatus.InsufficientConfirmedPivots, withClosure.Status);
        Assert.True(Assert.Single(withClosure.SourceGaps).DocumentedClosure);
    }

    [Fact]
    public void UnknownGapBlocksButDocumentedClosureDoesNotBridgePivotNeighborhood()
    {
        var series = new CandleSeries(Provider, Symbol, Frame,
            [Bar(0, 10, 5), Bar(1, 15, 2), Bar(2, 11, 5, hour: 12), Bar(3, 10, 5, hour: 16)]);
        var blocked = Evaluate(series);
        Assert.Equal(H4ResearchStatus.MissingSourceIntervals, blocked.Status);
        Assert.Empty(blocked.ConfirmedPivots);
        var closure = new H4DocumentedClosure(Origin.AddHours(8), Origin.AddHours(12), "documented-test-closure");
        var permitted = Evaluate(series, closures: [closure]);
        Assert.Equal(H4ResearchStatus.InsufficientConfirmedPivots, permitted.Status);
        Assert.All(permitted.SourceGaps, gap => Assert.True(gap.DocumentedClosure));
        Assert.Empty(permitted.ConfirmedPivots);
    }

    [Fact]
    public void FutureAppendAndMutationCannotRewriteEarlierSnapshot()
    {
        var prefix = Series([10, 15, 11, 12], [5, 4, 2, 5]);
        var cutoff = Origin.AddHours(16);
        var first = Evaluate(prefix, cutoff: cutoff);
        var appended = new CandleSeries(Provider, Symbol, Frame, [.. prefix.Candles, Bar(4, 20, 1)]);
        var mutated = new CandleSeries(Provider, Symbol, Frame, [.. prefix.Candles, Bar(4, 30, 0)]);
        var snapshot = System.Text.Json.JsonSerializer.Serialize(first);
        Assert.Equal(snapshot, System.Text.Json.JsonSerializer.Serialize(Evaluate(appended, cutoff: cutoff)));
        Assert.Equal(snapshot, System.Text.Json.JsonSerializer.Serialize(Evaluate(mutated, cutoff: cutoff)));
        Assert.Equal(snapshot, System.Text.Json.JsonSerializer.Serialize(Evaluate(prefix, cutoff: cutoff)));
    }

    [Fact]
    public void OtherSourceAndInvalidConfigurationAreRejected()
    {
        var foreign = new CandleSeries(new("other"), Symbol, Frame,
            [new Candle(new("other"), Symbol, Frame, Origin, Origin.AddHours(4), 8, 10, 5, 8, 1)]);
        Assert.Throws<ArgumentException>(() => Evaluate(foreign));
        var series = Series([10, 11, 10], [5, 4, 5]);
        Assert.Throws<ArgumentException>(() => Evaluate(series, 0, 1));
    }

    [Fact]
    public void MissingTailBlocksOnceAFullNominalIntervalCouldHaveClosed()
    {
        var series = Series([10, 15, 11], [5, 4, 2]);
        Assert.Empty(Evaluate(series, cutoff: Origin.AddHours(16).AddTicks(-1)).SourceGaps);
        var result = Evaluate(series, cutoff: Origin.AddHours(16));
        Assert.Equal(H4ResearchStatus.MissingSourceIntervals, result.Status);
        Assert.Equal(Origin.AddHours(12), Assert.Single(result.SourceGaps).StartUtc);
    }

    [Fact]
    public void TimeframeLabelAloneDoesNotValidateNominalDuration()
    {
        var mislabeled = new Candle(Provider, Symbol, Frame, Origin, Origin.AddHours(1), 8, 10, 5, 8, 1);
        var series = new CandleSeries(Provider, Symbol, Frame, [mislabeled]);
        Assert.Throws<ArgumentException>(() => Evaluate(series));
    }
}
