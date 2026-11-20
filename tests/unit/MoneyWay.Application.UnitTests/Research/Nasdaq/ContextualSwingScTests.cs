using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

// All OHLC and availability declarations here are synthetic software fixtures, not authentic market evidence.
public sealed class ContextualSwingScTests
{
    private static readonly MarketDataProviderId Provider = new("synthetic-context-sc");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour), M5 = new(5, TimeframeUnit.Minute);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly DateTimeOffset Prep = new(2026, 6, 23, 13, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int minute) => Prep.AddMinutes(minute);
    private static Candle Bar(int minute, decimal high = 105, decimal low = 96, decimal close = 100, decimal open = 100) => new(Provider, Symbol, M5, At(minute), At(minute + 5), open, high, low, close, null);
    private static CandleSeries Hours() => new(Provider, Symbol, H1, Enumerable.Range(0, 15).Select(i => new Candle(Provider, Symbol, H1, Prep.AddHours(i - 15), Prep.AddHours(i - 14), 100, 150, i < 9 ? 50 : 90, 100, null)));
    private static CandleSeries Source(params Candle[] custom) => new(Provider, Symbol, M5, Enumerable.Range(0, 85).Select(i => custom.FirstOrDefault(c => c.OpenTimeUtc == At(i * 5 - 365)) ?? Bar(i * 5 - 365)));
    private static CandleSeries ConfirmedSource(params Candle[] custom) => Source(new[] { Bar(0, 110, 95), Bar(5, 112, 96, 105), Bar(10, 108, 92, 94, 105), Bar(25, 106), Bar(30, 108, 89), Bar(35, 114, 96, 113) }.Where(c => !custom.Any(x => x.OpenTimeUtc == c.OpenTimeUtc)).Concat(custom).ToArray());
    private static CandleSeries Mirror(CandleSeries s) => new(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Select(c => new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, 200 - c.Open, 200 - c.Low, 200 - c.High, 200 - c.Close, c.Volume)));
    private readonly CompareContextualSwingScUseCase useCase = new();
    private ContextualSwingScReport Run(CandleSeries source, DateTimeOffset? cutoff = null, CandleSeries? hours = null) => useCase.Execute(hours ?? Hours(), source, Day, At(-360), cutoff ?? At(60), M5ResearchDataOrigin.Synthetic, "synthetic:contextual SC");
    private static ContextualSwingScComparison Row(ContextualSwingScReport r, string family, string policy = "E2", string reference = "LL", string? variant = null) => r.Comparisons.Single(x => x.HypothesisId == family && x.EventSelection.PolicyId.EndsWith(policy + "-V1") && x.EventSelection.LiquidityReference.Category == reference && x.SwingVariantId == variant);

    [Fact]
    public void EventPoliciesRetainHistoryAndDistinguishOpenAndCloseAt0830()
    {
        var r = Run(Source(Bar(5, 105, 89), Bar(25, 105, 88), Bar(30, 105, 87)));
        var e1 = Row(r, CompareContextualSwingScUseCase.Previous, "E1").EventSelection;
        var e2 = Row(r, CompareContextualSwingScUseCase.Previous).EventSelection;
        Assert.Equal(At(5), e1.Event!.Candle.OpenTimeUtc); Assert.Equal(At(30), e2.Event!.Candle.OpenTimeUtc);
        Assert.Equal(At(35), e2.Event.AvailableAtUtc); Assert.Equal(2, e2.Event.PriorPenetrationCount); Assert.True(e2.Event.PriorPenetrationBeforeOpen);
        Assert.NotEqual(At(25), e2.Event.Candle.OpenTimeUtc);
        Assert.Equal(72, r.Comparisons.Count); Assert.Equal(12, r.EventSelections.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void E3RequiresThreeDistinctChronologicalCandlesAndStrictOtherSideClose(bool equalityOnly)
    {
        var rows = new[] { Bar(5, 105, 89, 90), Bar(10, 105, 90, 90), Bar(15, 105, 90, 90), Bar(20, 105, 90, 90), Bar(25, 105, 90, equalityOnly ? 90 : 100), Bar(30, 105, 89, 90) };
        var r = Run(Source(rows)); var selection = Row(r, CompareContextualSwingScUseCase.Previous, "E3").EventSelection;
        if (equalityOnly) { Assert.Null(selection.Event); Assert.Equal("NoQualifyingEventObserved", selection.Status); }
        else
        {
            Assert.Equal(At(5), selection.EarlierPenetration!.Candle.OpenTimeUtc); Assert.Equal(At(25), selection.IntermediateReturn!.Candle.OpenTimeUtc); Assert.Equal(At(30), selection.Event!.Candle.OpenTimeUtc);
            Assert.Equal(3, new[] { selection.EarlierPenetration.Id, selection.IntermediateReturn.Id, selection.Event.Id }.Distinct().Count());
        }
    }

    [Fact]
    public void EventCandleReturnCannotSatisfyItsOwnE3IntermediateReturn()
    {
        var r = Run(Source(Bar(25, 105, 89, 90), Bar(30, 105, 89, 100)));
        Assert.Null(Row(r, CompareContextualSwingScUseCase.Previous, "E3").EventSelection.Event);
    }

    [Fact]
    public void MissingPrefixAbstainsForE1WhileE2VerifiesItsOwnPostStartSearch()
    {
        var s = Source(Bar(5, 105, 89), Bar(30, 105, 89)); var missing = new CandleSeries(Provider, Symbol, M5, s.Candles.Where(c => c.OpenTimeUtc != Prep));
        var r = Run(missing); var e1 = Row(r, CompareContextualSwingScUseCase.Previous, "E1");
        Assert.Equal("AbstainedEventCoverage", e1.Status); Assert.Null(e1.EventSelection.Event); Assert.NotNull(e1.EventSelection.ObservedCandidate);
        Assert.Equal(At(30), Row(r, CompareContextualSwingScUseCase.Previous).EventSelection.Event!.Candle.OpenTimeUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviousRangeAndAllFourConfirmedVariantsFeedStrictSubsequentScWithoutDirectionApproval(bool downward)
    {
        var source = ConfirmedSource(); var hours = Hours(); if (downward) { source = Mirror(source); hours = Mirror(hours); }
        var r = Run(source, hours: hours); var name = downward ? "LH" : "LL";
        var previous = Row(r, CompareContextualSwingScUseCase.Previous, reference: name); var range = Row(r, CompareContextualSwingScUseCase.Range, reference: name);
        Assert.Equal(downward ? 94 : 106, previous.StructuralReference!.Price); Assert.Equal(downward ? 88 : 112, range.StructuralReference!.Price);
        foreach (var variant in CompareM5SwingHypothesesUseCase.InclusiveAlgorithms)
        {
            var row = Row(r, CompareContextualSwingScUseCase.Confirmed, reference: name, variant: variant.AlgorithmId);
            Assert.Equal("ExperimentalScObserved", row.Status); Assert.Equal(downward ? 88 : 112, row.StructuralReference!.Price);
            Assert.Equal(At(15), row.StructuralReference.AvailableAtUtc); Assert.Equal(At(35), row.SubsequentSc!.Candle!.OpenTimeUtc); Assert.Equal(At(40), row.SubsequentSc.AsOfUtc);
            Assert.Equal(At(5), Assert.Single(row.StructuralReference.Sources).OpenTimeUtc); Assert.Equal("2.0.0", row.SwingVariantVersion);
            Assert.True(row.BoundedDetector!.IsExperimentalReferenceConfirmed); Assert.False(row.CanonicalAdmissionPermitted); Assert.False(row.OperationalSignal);
        }
        Assert.Equal(At(30), previous.StructuralReference.SelectedAtUtc); Assert.Equal(At(30), previous.StructuralReference.AvailableAtUtc);
        Assert.Equal("ExperimentalScObserved", previous.Status); Assert.Equal("ExperimentalScObserved", range.Status);
    }

    [Theory]
    [InlineData("112", false)]
    [InlineData("111", false)]
    [InlineData("112.00000000000000000000000001", true)]
    public void EqualityWickAndMarginalExactCloseUseTheExistingStrictPrimitive(string close, bool sc)
    {
        var price = decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture);
        var r = Run(ConfirmedSource(Bar(35, 115, 96, price)));
        var row = Row(r, CompareContextualSwingScUseCase.Confirmed, variant: "EXP-M5-SWING-A-V2");
        Assert.Equal(sc ? "ExperimentalScObserved" : "NoQualifyingSCObserved", row.Status);
        Assert.Equal(sc, row.SubsequentSc is not null);
    }

    [Fact]
    public void EventCloseBeyondIsPreservedAndLaterContinuationCannotBecomeANewSc()
    {
        var r = Run(ConfirmedSource(Bar(30, 114, 89, 113), Bar(35, 115, 96, 114)));
        var row = Row(r, CompareContextualSwingScUseCase.Confirmed, variant: "EXP-M5-SWING-A-V2");
        Assert.Equal("AbstainedSameCandleBreak", row.Status); Assert.True(row.EventCloseComparison!.IsConfirmed); Assert.Null(row.SubsequentSc);
        Assert.Equal(At(30), row.EventCloseComparison.Candle!.OpenTimeUtc);
    }

    [Fact]
    public void ConfirmedReferenceAlreadyBrokenBeforeEventIsStaleAndNeverReplaced()
    {
        var r = Run(ConfirmedSource(Bar(20, 114, 96, 113)));
        foreach (var variant in CompareM5SwingHypothesesUseCase.InclusiveAlgorithms)
        {
            var row = Row(r, CompareContextualSwingScUseCase.Confirmed, variant: variant.AlgorithmId);
            Assert.Equal("AbstainedStaleReference", row.Status); Assert.Equal(112, row.StructuralReference!.Price); Assert.Equal(At(20), row.PreEventBreak!.Candle!.OpenTimeUtc); Assert.Null(row.SubsequentSc);
        }
    }

    [Fact]
    public void MissingPreparationAndPreviousIntervalsCannotBeReplacedByTheEvent()
    {
        var early = Source(Bar(0, 105, 89)); var missingPrevious = new CandleSeries(Provider, Symbol, M5, early.Candles.Where(c => c.OpenTimeUtc != At(-5)));
        var first = Row(Run(missingPrevious), CompareContextualSwingScUseCase.Previous, "E1"); Assert.Equal("AbstainedPreviousIntervalMissing", first.Status);
        var s = ConfirmedSource(); var gap = new CandleSeries(Provider, Symbol, M5, s.Candles.Where(c => c.OpenTimeUtc != At(15)));
        Assert.Equal("AbstainedPreparationCoverage", Row(Run(gap), CompareContextualSwingScUseCase.Range).Status);
        Assert.Equal("AbstainedConfirmedStructureCoverage", Row(Run(gap), CompareContextualSwingScUseCase.Confirmed, variant: "EXP-M5-SWING-A-V2").Status);
    }

    [Fact]
    public void RangeRetainsAllEqualExtremeOwnersAndExcludesEventHigh()
    {
        var r = Run(ConfirmedSource(Bar(20, 112), Bar(30, 140, 89)));
        var row = Row(r, CompareContextualSwingScUseCase.Range);
        Assert.Equal(112, row.StructuralReference!.Price); Assert.Equal(new[] { At(5), At(20) }, row.StructuralReference.Sources.Select(c => c.OpenTimeUtc));
        Assert.DoesNotContain(row.StructuralReference.Sources, c => c.OpenTimeUtc == At(30));
    }

    [Fact]
    public void PrePreparationEventAndUnconfirmedV2RemainHonestAbstentions()
    {
        var r = Run(Source(Bar(-360, 120, 49)));
        Assert.Equal("AbstainedPreparationRangeUnavailable", Row(r, CompareContextualSwingScUseCase.Range, "E1", "AL").Status);
        var row = Row(r, CompareContextualSwingScUseCase.Confirmed, "E1", "AL", "EXP-M5-SWING-A-V2");
        Assert.Equal("AbstainedConfirmedStructure", row.Status); Assert.Equal(M5ExperimentalStatus.AbstainedMissingInitialization, row.BoundedDetector!.Status); Assert.Null(row.StructuralReference);
        var provisional = Row(Run(Source(Bar(30, 108, 89))), CompareContextualSwingScUseCase.Confirmed, variant: "EXP-M5-SWING-A-V2");
        Assert.Equal("AbstainedConfirmedStructure", provisional.Status); Assert.False(provisional.BoundedDetector!.IsExperimentalReferenceConfirmed);
    }

    [Fact]
    public void APostEventCoverageHolePreventsClaimingTheFirstObservedBreakAsVerifiedSc()
    {
        var s = ConfirmedSource(Bar(40, 115, 96, 113)); var gap = new CandleSeries(Provider, Symbol, M5, s.Candles.Where(c => c.OpenTimeUtc != At(35)));
        var row = Row(Run(gap), CompareContextualSwingScUseCase.Range);
        Assert.Equal("AbstainedSubsequentCoverage", row.Status); Assert.Null(row.SubsequentSc); Assert.NotNull(row.ObservedSubsequentBreak); Assert.Contains(At(35), row.MissingScOpensUtc);
    }

    [Fact]
    public void EventAndScAreOnlyAvailableAfterTheirOwnClosesAndAllReferencesArePreEvent()
    {
        var s = ConfirmedSource(); Assert.Null(Row(Run(s, At(35).AddTicks(-1)), CompareContextualSwingScUseCase.Range).EventSelection.Event);
        var beforeSc = Row(Run(s, At(40).AddTicks(-1)), CompareContextualSwingScUseCase.Range); Assert.Null(beforeSc.SubsequentSc);
        var after = Row(Run(s, At(40)), CompareContextualSwingScUseCase.Range); Assert.Equal(At(40), after.SubsequentSc!.AsOfUtc);
        Assert.All(Run(s).Comparisons.Where(r => r.StructuralReference is not null), r => Assert.True(r.StructuralReference!.AvailableAtUtc <= r.EventSelection.Event!.Candle.OpenTimeUtc));
    }

    [Fact]
    public void SharedOppositeSideEventsRemainDistinctReferencesWithoutPriority()
    {
        var r = Run(Source(Bar(30, 151, 49))); var selected = r.EventSelections.Where(e => e.PolicyId.EndsWith("E2-V1") && e.Event is not null).ToArray();
        Assert.Equal(4, selected.Length); Assert.Equal(4, selected.Select(e => e.LiquidityReference.Id).Distinct().Count());
        Assert.True(r.Tracking.CandleGroups.Single(g => g.Candle.OpenTimeUtc == At(30)).OppositeSidesPenetrated);
    }

    [Fact]
    public void WrongSourcesAndTimeframeNeverBecomeNewCanonicalFacts()
    {
        Assert.Throws<ArgumentException>(() => Run(new(new("other"), Symbol, M5, [])));
        Assert.Throws<ArgumentException>(() => Run(new(Provider, new("USTEC"), M5, [])));
        Assert.Throws<ArgumentException>(() => Run(new(Provider, Symbol, new(1, TimeframeUnit.Minute), [])));
        Assert.False(typeof(IReplayRuleEvaluator).IsAssignableFrom(typeof(CompareContextualSwingScUseCase)));
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(typeof(ContextualSwingScReport)));
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(ContextualSwingScReport)));
    }

    [Fact]
    public void EveryEarlierPrefixIsDeterministicAndLaterCandlesCannotChangeTheFrozenReference()
    {
        var s = ConfirmedSource(); var future = new CandleSeries(Provider, Symbol, M5, s.Candles.Append(Bar(60, 999, 1)));
        foreach (var cutoff in new[] { Prep, At(30), At(35).AddTicks(-1), At(35), At(40).AddTicks(-1), At(40), At(60) })
        {
            var prefix = new CandleSeries(Provider, Symbol, M5, future.Candles.Where(c => c.CloseTimeUtc <= cutoff));
            var expected = JsonSerializer.Serialize(Run(prefix, cutoff)); Assert.Equal(expected, JsonSerializer.Serialize(Run(future, cutoff))); Assert.Equal(expected, JsonSerializer.Serialize(Run(prefix, cutoff)));
        }
        Assert.Equal(JsonSerializer.Serialize(Row(Run(s, At(40)), CompareContextualSwingScUseCase.Range).StructuralReference), JsonSerializer.Serialize(Row(Run(s), CompareContextualSwingScUseCase.Range).StructuralReference));
    }
}
