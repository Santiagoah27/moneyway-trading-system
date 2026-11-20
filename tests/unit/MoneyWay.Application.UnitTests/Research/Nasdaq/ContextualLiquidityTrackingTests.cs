using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class ContextualLiquidityTrackingTests
{
    private static readonly MarketDataProviderId Provider = new("synthetic-context");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe Hour = new(1, TimeframeUnit.Hour), Five = new(5, TimeframeUnit.Minute);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly DateTimeOffset Prep = new(2026, 6, 23, 13, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int minute) => Prep.AddMinutes(minute);
    private static Candle M5(int minute, decimal high = 105, decimal low = 95, decimal close = 100, decimal open = 100) => new(Provider, Symbol, Five, At(minute), At(minute + 5), open, high, low, close, null);
    private static CandleSeries H1() => new(Provider, Symbol, Hour, Enumerable.Range(0, 15).Select(i => new Candle(Provider, Symbol, Hour, Prep.AddHours(i - 15), Prep.AddHours(i - 14), 100, 110, 90, 100, null)));
    private static CandleSeries M5Source(params Candle[] replacements) => new(Provider, Symbol, Five, Enumerable.Range(0, 84).Select(i => replacements.FirstOrDefault(c => c.OpenTimeUtc == At(i * 5 - 360)) ?? M5(i * 5 - 360)));
    private readonly TrackContextualLiquidityUseCase useCase = new();
    private ContextualLiquidityTrackingResult Run(CandleSeries m5, DateTimeOffset? cutoff = null, CandleSeries? h1 = null, IReadOnlyList<ContextualStructuralReference>? structural = null) =>
        useCase.Execute(h1 ?? H1(), m5, Day, At(-360), cutoff ?? At(60), M5ResearchDataOrigin.Synthetic, "synthetic:tracking fixture", structural);
    private static ContextualReferenceTracking Ref(ContextualLiquidityTrackingResult r, string category) => r.References.Single(t => t.Reference.Category == category);

    [Fact]
    public void InventoryPreservesSourceAuthorityCoincidentIdentitiesAllTiesAndFormationVersusAvailability()
    {
        var r = Run(M5Source()); Assert.Equal(4, r.References.Count); Assert.Equal(4, r.References.Select(t => t.Reference.Id).Distinct().Count());
        Assert.Equal(Ref(r, "AH").Reference.Price, Ref(r, "LH").Reference.Price); Assert.NotEqual(Ref(r, "AH").Reference.Id, Ref(r, "LH").Reference.Id);
        Assert.Equal(9, Ref(r, "AH").Reference.SourceCandles.Count); Assert.Equal(6, Ref(r, "LL").Reference.SourceCandles.Count);
        Assert.Equal(At(-360), Ref(r, "AL").Reference.AvailableAtUtc); Assert.Equal(Prep, Ref(r, "LL").Reference.AvailableAtUtc);
        Assert.All(r.References, t => { Assert.Equal("1h", t.Reference.SourceTimeframe); Assert.Equal(Day, t.Reference.OperatingDate); Assert.Equal(Provider.Value, t.Reference.Provider); Assert.Equal(Symbol.Value, t.Reference.Instrument); });
        Assert.Equal(r.References.Select(t => t.Reference.Id), Run(M5Source(), At(40)).References.Select(t => t.Reference.Id));
    }

    [Theory]
    [InlineData(110, 95, 100, 100, true, false, false, false)]
    [InlineData(111, 95, 100, 100, false, true, false, false)]
    [InlineData(113, 111, 112, 112, false, true, true, true)]
    [InlineData(105, 90, 100, 100, true, false, false, false)]
    [InlineData(105, 89, 100, 100, false, true, false, false)]
    [InlineData(89, 87, 88, 88, false, true, true, true)]
    public void ExactOhlcPredicatesAreSharedAndRetainEveryCoincidentReference(int high, int low, int close, int open, bool equality, bool strict, bool openBeyond, bool closeBeyond)
    {
        var r = Run(M5Source(M5(0, high, low, close, open))); var upper = high >= 110;
        foreach (var name in upper ? new[] { "AH", "LH" } : new[] { "AL", "LL" })
        {
            var o = Ref(r, name).Observations.Single(e => e.Candle.OpenTimeUtc == Prep);
            Assert.Equal(equality, o.Relationship.ExactExtremeContact); Assert.Equal(strict, o.Relationship.StrictPenetration);
            Assert.Equal(openBeyond, o.Relationship.OpensBeyondReference); Assert.Equal(closeBeyond, o.Relationship.CloseBeyond);
        }
        Assert.Equal(2, r.CandleGroups.Single(g => g.Candle.OpenTimeUtc == Prep).InteractingReferenceIds.Count);
    }

    [Fact]
    public void MultipleOppositePenetrationsPreserveAllIdsAndUnknownIntrabarOrder()
    {
        var r = Run(M5Source(M5(0, 111, 89))); var group = r.CandleGroups.Single(g => g.Candle.OpenTimeUtc == Prep);
        Assert.Equal(4, group.PenetratedReferenceIds.Count); Assert.True(group.OppositeSidesPenetrated); Assert.Equal("MultipleReferences_SameCandleOrderUnknown", group.Ordering);
    }

    [Fact]
    public void PreOpenHistoryReturnSequenceAndAdditionalPenetrationsAreNeverResetAtOpen()
    {
        var r = Run(M5Source(M5(5, 105, 89), M5(35, 105, 88)));
        var t = Ref(r, "LL"); Assert.True(t.PenetrationObservedBeforeOpen); Assert.Equal(1, t.PenetrationsAvailableAtOrAfterOpen);
        var later = t.Observations.Single(o => o.Candle.OpenTimeUtc == At(35));
        Assert.True(later.PriorPenetrationBeforeOpen); Assert.Equal(1, later.PriorPenetrationCount);
        Assert.NotNull(later.PriorPenetrationId); Assert.NotNull(later.PriorLaterOtherSideCloseId); Assert.True(later.AdditionalPenetrationAfterLaterOtherSideClose);
        Assert.Contains(t.Observations, o => o.Id == later.PriorLaterOtherSideCloseId && o.AvailableAtUtc <= later.Candle.OpenTimeUtc && o.CloseOnOtherSide);
    }

    [Fact]
    public void CandleEndingAt0830BelongsToPreparationButWasNotObservableBeforeOpen()
    {
        var s = M5Source(M5(25, 105, 89)); var before = Run(s, At(30).AddTicks(-1)); var at = Run(s, At(30));
        Assert.Null(Ref(before, "LL").FirstObservedPenetration); Assert.False(Ref(at, "LL").PenetrationObservedBeforeOpen);
        var o = Ref(at, "LL").FirstObservedPenetration!;
        Assert.Equal("Preparation", o.IntervalPhase); Assert.Equal("Observation", o.AvailabilityPhase); Assert.Equal(At(30), o.AvailableAtUtc);
    }

    [Fact]
    public void LondonReferencesCannotConsumeTheirFormationCandleOrEarlierLondonHistory()
    {
        var s = M5Source(M5(-5, 110, 90)); var before = Run(s, Prep.AddTicks(-1)); var after = Run(s);
        Assert.DoesNotContain(before.References, t => t.Reference.Category.StartsWith("L"));
        Assert.All(Ref(after, "LL").Observations, o => Assert.True(o.Candle.OpenTimeUtc >= Prep));
        Assert.Contains(Ref(after, "AL").Observations, o => o.Candle.OpenTimeUtc == At(-5) && o.Relationship.ExactExtremeContact);
    }

    [Theory]
    [InlineData(0, "Asia")]
    [InlineData(14, "London")]
    public void IncompleteSessionReferencesNeverEnterTheCandidateInventory(int missing, string name)
    {
        var source = H1(); var r = Run(M5Source(), h1: new(Provider, Symbol, Hour, source.Candles.Where((_, i) => i != missing)));
        Assert.Contains(r.UnavailableReferences, x => x.StartsWith(name + ":"));
        Assert.DoesNotContain(r.References, t => t.Reference.Category.StartsWith(name == "Asia" ? "A" : "L"));
    }

    [Fact]
    public void ObservationHolesAreExplicitAndCannotProveNoPenetration()
    {
        var full = M5Source(); var r = Run(new(Provider, Symbol, Five, full.Candles.Where(c => c.OpenTimeUtc != At(10))));
        var t = Ref(r, "LL"); Assert.Equal("MissingClosedIntervals", t.ObservationCoverage); Assert.Equal("InsufficientHistoricalCoverage", t.HistoryStatus); Assert.Equal(At(10), Assert.Single(t.MissingClosedM5OpensUtc));
        var empty = Run(new(Provider, Symbol, Five, [])); Assert.All(empty.References, t => Assert.Equal("InsufficientHistoricalCoverage", t.HistoryStatus));
    }

    [Fact]
    public void UnfinishedCandleAndInvalidIntervalsAreNotConsumed()
    {
        var s = M5Source(M5(55, 111, 89)); var r = Run(s, At(60).AddTicks(-1));
        Assert.DoesNotContain(r.References.SelectMany(t => t.Observations), o => o.Candle.OpenTimeUtc == At(55));
        var malformed = new Candle(Provider, Symbol, Five, At(0), At(4), 100, 999, 1, 100, null);
        var invalid = Run(new(Provider, Symbol, Five, s.Candles.Where(c => c.OpenTimeUtc != Prep).Append(malformed).OrderBy(c => c.OpenTimeUtc)));
        Assert.Equal("InvalidObservationIntervals", Ref(invalid, "LL").ObservationCoverage); Assert.DoesNotContain(Ref(invalid, "LL").Observations, o => o.Candle.OpenTimeUtc == Prep);
    }

    [Fact]
    public void ProviderInstrumentTimeframeAndUtcIdentityCannotBeSubstituted()
    {
        Assert.Throws<ArgumentException>(() => Run(new(new("other"), Symbol, Five, [])));
        Assert.Throws<ArgumentException>(() => Run(new(Provider, new("USTEC"), Five, [])));
        Assert.Throws<ArgumentException>(() => Run(new(Provider, Symbol, new(1, TimeframeUnit.Minute), [])));
        Assert.Throws<ArgumentException>(() => Run(M5Source(), h1: new(Provider, Symbol, new(60, TimeframeUnit.Minute), [])));
        Assert.Throws<ArgumentException>(() => Run(M5Source(), At(60).ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void StrictDecimalPrecisionHasNoToleranceAndNoCanonicalInterfaces()
    {
        const decimal tiny = 0.00000000000000000000000001m;
        var r = Run(M5Source(M5(0, 110 + tiny, 90 - tiny)));
        Assert.All(r.CandleGroups.Single(g => g.Candle.OpenTimeUtc == Prep).PenetratedReferenceIds, id => Assert.Contains(r.References, t => t.Reference.Id == id));
        Assert.Equal(4, r.CandleGroups.Single(g => g.Candle.OpenTimeUtc == Prep).PenetratedReferenceIds.Count);
        Assert.False(r.CanonicalAdmissionPermitted); Assert.False(r.OperationalSignal);
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(typeof(ContextualLiquidityTrackingResult)));
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(ContextualLiquidityTrackingResult)));
        Assert.False(typeof(IReplayRuleEvaluator).IsAssignableFrom(typeof(TrackContextualLiquidityUseCase)));
    }

    [Fact]
    public void EveryEarlierPrefixAndRepeatedEvaluationRemainsStableAfterFutureExtension()
    {
        var s = M5Source(M5(5, 105, 89), M5(35, 111, 88)); var future = new CandleSeries(Provider, Symbol, Five, s.Candles.Append(M5(60, 999, 1)));
        foreach (var cutoff in new[] { At(-300), Prep.AddTicks(-1), Prep, At(10), At(30).AddTicks(-1), At(30), At(40), At(60) })
        {
            var prefix = new CandleSeries(Provider, Symbol, Five, future.Candles.Where(c => c.CloseTimeUtc <= cutoff));
            var h1 = new CandleSeries(Provider, Symbol, Hour, H1().Candles.Where(c => c.CloseTimeUtc <= cutoff));
            var expected = JsonSerializer.Serialize(Run(prefix, cutoff, h1));
            Assert.Equal(expected, JsonSerializer.Serialize(Run(future, cutoff))); Assert.Equal(expected, JsonSerializer.Serialize(Run(prefix, cutoff, h1)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void OptionalStructuralMembersRequireExistingValidatedSourceResolutionAndAreNotBackdated(int structuralHours)
    {
        var source = new CandleSeries(Provider, Symbol, Hour, H1().Candles.Select(c => c.OpenTimeUtc == Prep.AddHours(-1)
            ? new Candle(Provider, Symbol, Hour, c.OpenTimeUtc, c.CloseTimeUtc, 100, 110, 90, 99, null) : c));
        var structuralSource = structuralHours == 1 ? source : new CandleSeries(Provider, Symbol, new(4, TimeframeUnit.Hour), [
            new(Provider, Symbol, new(4, TimeframeUnit.Hour), Prep.AddHours(-9), Prep.AddHours(-5), 100, 110, 90, 100, null),
            new(Provider, Symbol, new(4, TimeframeUnit.Hour), Prep.AddHours(-5), Prep.AddHours(-1), 100, 110, 90, 99, null)]);
        var member = structuralSource.Candles[^2]; var validation = new StructuralBodyCloseBreakCalculator().Evaluate(structuralSource.Candles[^1], 100, StructuralBreakDirection.Lower);
        var reference = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.LowerHigh, [member], "synthetic:structural member", validation);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var session = new NasdaqDemoSessionIdentity(definition.StrategyId, definition.Version, Provider, Symbol, Day);
        var review = new NasdaqHumanStructuralLiquidityObservation(session, [reference], Prep, Prep, "synthetic:validated selection");
        var cursor = new MultiTimeframeCandleReplayCursor(structuralHours == 1 ? [source, M5Source()] : new[] { source, structuralSource, M5Source() }); StrategyReplayContext? context = null;
        while (cursor.TryAdvance(out var f)) if (f!.AsOfUtc == Prep) { context = new CreateStrategyReplayContextUseCase().Execute(definition, f, [review]); break; }
        var supplied = ContextualStructuralReference.FromSelection(context!, session, reference);
        var r = Run(M5Source(), h1: source, structural: [supplied]); var t = r.References.Single(t => t.Reference.Category.StartsWith("Structural"));
        Assert.Equal(Prep, t.Reference.AvailableAtUtc); Assert.All(t.Observations, o => Assert.True(o.Candle.OpenTimeUtc >= Prep));
        var early = Run(M5Source(), Prep.AddTicks(-1), source, [supplied]); Assert.DoesNotContain(early.References, t => t.Reference.Category.StartsWith("Structural"));
        Assert.Equal(JsonSerializer.Serialize(Run(M5Source(), Prep.AddTicks(-1), source)), JsonSerializer.Serialize(early));
        Assert.Throws<ArgumentException>(() => Run(M5Source(), h1: source, structural: [supplied, supplied]));
        var unvalidated = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.LowerHigh, [member], "synthetic:unvalidated");
        Assert.Throws<ArgumentException>(() => ContextualStructuralReference.FromSelection(context!, session, unvalidated));
    }
}
