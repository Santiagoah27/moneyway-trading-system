using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class H4CausalContextResearchTests
{
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);
    private static readonly DateTimeOffset Start = new(2026, 6, 22, 5, 0, 0, TimeSpan.Zero);
    private static readonly H4PivotProfile Profile = H4PivotProfile.InitialResearchProfiles[0];
    private static readonly EvaluateH4CausalContextResearchUseCase Subject = new();

    [Theory]
    [InlineData(false, H4ResearchDirection.Buy, H4HighRelation.HigherHigh, H4LowRelation.HigherLow)]
    [InlineData(true, H4ResearchDirection.Sell, H4HighRelation.LowerHigh, H4LowRelation.LowerLow)]
    public void ConfirmedStructureAndSingleBreakoutCanAlignWithBothDirections(bool mirror,
        H4ResearchDirection expected, H4HighRelation highs, H4LowRelation lows)
    {
        var bars = Trend(mirror);
        var result = Execute(bars);
        Assert.Equal(H4CausalStructureState.Confirmed, result.StructureState);
        Assert.Equal(expected, result.ConfirmedStructureDirection);
        Assert.Equal(highs, result.StructureEvidence.HighRelation);
        Assert.Equal(lows, result.StructureEvidence.LowRelation);
        Assert.Equal(H4CausalContextState.Observed, result.ContextState);
        Assert.Equal(expected, result.ContextAlignedDirection);
        var current = Assert.Single(result.ContextEvents, e => e.ObservedAtUtc == result.AsOfUtc);
        Assert.Equal(H4CausalContextKind.Breakout, current.Kind);
        Assert.Equal(mirror ? H4PivotKind.Low : H4PivotKind.High, current.Reference.Kind);
        Assert.Same(bars[^1], current.ObservationCandle);
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.False(result.OperationalSignal);
    }

    [Fact]
    public void AReferenceWithoutAQualifiedEventDoesNotPassTheV2ContextGate()
    {
        var bars = Trend();
        bars[^1] = Bar(7, low: 13, high: 24, open: 21, close: 24);
        var report = Compare(bars);
        var shortProfile = report.Profiles[0];
        Assert.True(shortProfile.V1ContextAvailable);
        Assert.NotNull(shortProfile.V2);
        Assert.Equal(H4CausalStructureState.Confirmed, shortProfile.V2.StructureState);
        Assert.Equal(H4ResearchDirection.Buy, shortProfile.V2.ConfirmedStructureDirection);
        Assert.Equal(H4LevelOutcome.CloseAtReference, shortProfile.V2.LatestClosedCandle!.Upper.Outcome);
        Assert.Equal(H4CausalContextState.NoQualifiedEvent, shortProfile.V2.ContextState);
        Assert.Null(shortProfile.V2.ContextAlignedDirection);
        Assert.Equal(0, shortProfile.V2CompleteAssociations);
    }

    [Fact]
    public void AProvisionalExtremeCannotBecomeConfirmedBeforeTheExactRightClose()
    {
        var bars = new[] { Bar(0, 10, 20), Bar(1, 9, 21), Bar(2, 10, 20) };
        var awaiting = Execute(bars, At(3).AddTicks(-1));
        Assert.Empty(awaiting.ConfirmedReferences);
        Assert.Equal(H4CausalStructureState.Provisional, awaiting.StructureState);
        Assert.Equal(2, awaiting.ProvisionalReferences.Count);
        Assert.All(awaiting.ProvisionalReferences, p =>
        {
            Assert.Equal(2, p.EvidenceCandles.Count);
            Assert.Null(p.ConfirmedAtUtc);
        });
        var confirmed = Execute(bars, At(3));
        Assert.Equal(At(1), Assert.Single(confirmed.SimultaneousExtremeOpensUtc));
        Assert.Equal(new[] { H4PivotKind.High, H4PivotKind.Low }, confirmed.ConfirmedReferences.Select(p => p.Side));
        Assert.All(confirmed.ConfirmedReferences, p =>
        {
            Assert.Equal(At(1), p.ExtremeOpenUtc);
            Assert.Equal(At(2), p.ExtremeNominalCloseUtc);
            Assert.Equal(At(3), p.ConfirmedAtUtc);
            Assert.Equal(TimeSpan.FromHours(4), p.ConfirmationDelay);
            Assert.Equal(bars, p.EvidenceCandles);
        });
        Assert.Empty(confirmed.ProvisionalReferences);
        Assert.Equal(H4CausalContextState.Unavailable, confirmed.ContextState);
        Assert.Null(confirmed.ConfirmedStructureDirection);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ProfileSensitivityChangesTheEarliestPossibleConfirmation(int neighbors)
    {
        var bars = Enumerable.Range(0, 2 * neighbors + 1)
            .Select(i => i == neighbors ? Bar(i, 5, 25) : Bar(i, 10, 20)).ToArray();
        var profile = H4PivotProfile.InitialResearchProfiles[neighbors - 1];
        Assert.Empty(Execute(bars, bars[^1].CloseTimeUtc.AddTicks(-1), profile: profile).ConfirmedReferences);
        var atClose = Execute(bars, profile: profile);
        Assert.Equal(2, atClose.ConfirmedReferences.Count);
        Assert.All(atClose.ConfirmedReferences, p => Assert.Equal(TimeSpan.FromHours(neighbors * 4), p.ConfirmationDelay));
    }

    [Fact]
    public void TwoSidedPenetrationKeepsBothEventsWithUnknownOrdering()
    {
        var bars = Trend();
        bars[^1] = Bar(7, low: 6, high: 27, open: 21, close: 20);
        var report = Execute(bars);
        Assert.True(report.LatestClosedCandle!.IntrabarOrderingUnknown);
        Assert.Equal(H4CausalContextState.Ambiguous, report.ContextState);
        Assert.Null(report.ContextAlignedDirection);
        Assert.Equal(2, report.ContextEvents.Count(e => e.ObservedAtUtc == report.AsOfUtc));
    }

    [Fact]
    public void DelayedInvalidationIsAPostBreakoutEventAndConflictingDirectionIsRetained()
    {
        var bars = Trend().Append(Bar(8, low: 17, high: 27, open: 25, close: 22)).ToArray();
        var before = Execute(bars, At(8));
        var after = Execute(bars);
        Assert.DoesNotContain(before.ContextEvents, e => e.Kind == H4CausalContextKind.DelayedFakeout);
        var rejection = Assert.Single(after.ContextEvents, e => e.Kind == H4CausalContextKind.DelayedFakeout);
        Assert.Equal(At(7), rejection.OriginalBreakoutOpenUtc);
        Assert.Equal(At(9), rejection.ObservedAtUtc);
        Assert.Equal(H4ResearchDirection.Sell, rejection.Direction);
        Assert.Equal(H4ResearchDirection.Buy, after.ConfirmedStructureDirection);
        Assert.Null(after.ContextAlignedDirection);
    }

    [Fact]
    public void EqualNeighborhoodHasNoStrictPivotAndEqualConfirmedHighsAreAmbiguous()
    {
        var flat = Execute(Enumerable.Range(0, 5).Select(i => Bar(i, 10, 20)).ToArray());
        Assert.Empty(flat.ConfirmedReferences);
        Assert.Empty(flat.ProvisionalReferences);
        var bars = Trend();
        bars[4] = Bar(4, low: 11, high: 20, open: 18, close: 19);
        bars[5] = Bar(5, low: 7, high: 19);
        bars[6] = Bar(6, low: 12, high: 19);
        var equal = Execute(bars);
        Assert.Equal(H4HighRelation.EqualHigh, equal.StructureEvidence.HighRelation);
        Assert.Equal(H4CausalStructureState.Ambiguous, equal.StructureState);
        Assert.Null(equal.ConfirmedStructureDirection);
    }

    [Fact]
    public void UnexplainedGapAndDocumentedClosureRemainDistinctWithoutCrossGapPivots()
    {
        var bars = new[] { Bar(0, 10, 20), Bar(1, 9, 21), Bar(2, 10, 20), Bar(4, 7, 30), Bar(5, 8, 29) };
        var unknown = Execute(bars);
        Assert.Equal(H4CausalContextState.Unavailable, unknown.ContextState);
        Assert.False(Assert.Single(unknown.StructureEvidence.SourceGaps).DocumentedClosure);
        Assert.Empty(unknown.ConfirmedReferences);
        var known = Execute(bars, closures: [new(At(3), At(4), "synthetic:documented closure")]);
        Assert.True(Assert.Single(known.StructureEvidence.SourceGaps).DocumentedClosure);
        Assert.Equal(2, known.ConfirmedReferences.Count);
        Assert.All(known.ConfirmedReferences, p => Assert.Equal(At(1), p.ExtremeOpenUtc));
        Assert.DoesNotContain(known.ConfirmedReferences, p => p.ExtremeOpenUtc == At(4));
    }

    [Fact]
    public void BoundedHistoryDistinguishesAbsentShortAndExtendedPrefixes()
    {
        Assert.Equal(H4CausalStructureState.Unavailable, Execute([], At(1)).StructureState);
        var bars = Trend();
        var tooShort = Execute(bars[^2..], start: bars[^2].OpenTimeUtc);
        Assert.Equal(H4ResearchStatus.InsufficientHistory, tooShort.StructureEvidence.Status);
        Assert.Null(tooShort.ConfirmedStructureDirection);
        Assert.Equal(H4CausalStructureState.Confirmed, Execute(bars).StructureState);
        Assert.Equal(H4ResearchStatus.InsufficientHistory, Execute(bars[1..]).StructureEvidence.Status);
    }

    [Fact]
    public void FutureAppendAndMutationIncludingAnOpenCandleCannotRewriteThePrefix()
    {
        var bars = Trend();
        var asOf = At(7).AddHours(2);
        var initial = JsonSerializer.Serialize(Execute(bars[..^1], asOf));
        Assert.Equal(initial, JsonSerializer.Serialize(Execute(bars, asOf)));
        bars[7] = Bar(7, 0, 500, 300, 400);
        Assert.Equal(initial, JsonSerializer.Serialize(Execute([.. bars, Bar(8, 1, 550)], asOf)));
    }

    private static Candle[] Trend(bool mirror = false)
    {
        var bars = new[] { Bar(0, 8, 15), Bar(1, 10, 20), Bar(2, 5, 16), Bar(3, 9, 18),
            Bar(4, 11, 24), Bar(5, 7, 22), Bar(6, 12, 23, 20, 21), Bar(7, 13, 27, 21, 25) };
        return mirror ? bars.Select((b, i) => Bar(i, 100 - b.High, 100 - b.Low, 100 - b.Open, 100 - b.Close)).ToArray() : bars;
    }

    private static DateTimeOffset At(int i) => Start.AddHours(4 * i);
    private static Candle Bar(int i, decimal low, decimal high, decimal? open = null, decimal? close = null) =>
        new(Provider, Symbol, H4, At(i), At(i + 1), open ?? (low + high) / 2, high, low, close ?? (low + high) / 2, null);
    private static H4CausalContextObservation Execute(Candle[] bars, DateTimeOffset? cutoff = null,
        DateTimeOffset? start = null, H4PivotProfile? profile = null, H4DocumentedClosure[]? closures = null) =>
        Subject.Execute(new(new(Provider, Symbol, H4, bars), start ?? Start, cutoff ?? bars[^1].CloseTimeUtc,
            profile ?? Profile, "synthetic:causal H4", closures), 2);

    private static NasdaqH4CausalContextComparison Compare(Candle[] bars)
    {
        CandleSeries Empty(int amount, TimeframeUnit unit) => new(Provider, Symbol, new(amount, unit), []);
        return new CompareNasdaqH4CausalContextUseCase().Execute(new("synthetic", new(2026, 6, 23),
            new(2026, 6, 23, 16, 30, 0, TimeSpan.Zero),
            [new(Provider, Symbol, H4, bars), Empty(1, TimeframeUnit.Hour), Empty(5, TimeframeUnit.Minute), Empty(1, TimeframeUnit.Minute)],
            Start, "synthetic:causal H4"));
    }
}
