using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanM5TriggerTests
{
    private readonly NasdaqHumanM5TriggerObservationSelector selector = new();
    private readonly NasdaqLiquidityTakeRuleFact take;
    private readonly StrategyReplayContextObservation[] history;
    public NasdaqHumanM5TriggerTests() => (take, history) = M5Fixture.Established();
    private NasdaqHumanM5TriggerObservation Observation(bool ifvg = false, int minute = 10, int? observed = null,
        string source = "review:trigger", NasdaqHumanM5TakeTriggerOrder order = NasdaqHumanM5TakeTriggerOrder.Unspecified,
        NasdaqHumanLiquidityTakeObservation? otherTake = null, decimal swingPrice = 100,
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy)
    {
        var confirmation = M5Fixture.Five(M5Fixture.At(14, minute));
        var reference = M5Fixture.Five(M5Fixture.At(14));
        NasdaqHumanM5TriggerEvent marketEvent = ifvg
            ? new NasdaqHumanM5TriggerEvent.Ifvg(confirmation, direction, [reference], [M5Fixture.Five(M5Fixture.At(13, 50)), M5Fixture.Five(M5Fixture.At(13, 55)), reference])
            : new NasdaqHumanM5TriggerEvent.StructuralChange(confirmation, direction, [reference], [reference], swingPrice);
        return new(otherTake ?? take.Take, marketEvent, M5Fixture.At(14, observed ?? minute), source, order);
    }
    private StrategyReplayContext Context(int minute, params NasdaqHumanM5TriggerObservation[] inputs) =>
        M5Fixture.Context(M5Fixture.At(14, minute), inputs.Cast<IStrategyReplayInputObservation>().ToArray()).WithPriorObservations(history);
    private NasdaqHumanM5TriggerSelection Select(int minute, params NasdaqHumanM5TriggerObservation[] inputs) => selector.Select(Context(minute, inputs), take);

    [Fact]
    public void MissingDoesNotInventTriggerOrRuleOutcome() => Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UniqueRetainsExactTakeEventSourcesDirectionAndProvenance(bool ifvg)
    {
        var observation = Observation(ifvg);
        var selected = Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(10, observation));
        Assert.Same(observation, selected.Fact);
        Assert.Same(take.Take, selected.Fact.DecisiveTake);
        Assert.Equal(ifvg ? NasdaqHumanM5TriggerKind.Ifvg : NasdaqHumanM5TriggerKind.StructuralChange, selected.Fact.Event.Kind);
        Assert.Equal(take.Direction, selected.Fact.Event.Direction);
        Assert.Equal(M5Fixture.At(14, 10), selected.Fact.EffectiveAtUtc);
        Assert.Equal("review:trigger", selected.Fact.SourceReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarketTimeSelectsFirstRegardlessOfKindArrivalOrInputOrder(bool firstIfvg)
    {
        var first = Observation(firstIfvg, 10, 30, "late annotation of first");
        var later = Observation(!firstIfvg, 20, 20, "earlier annotation of later");
        Assert.Same(later, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(20, first, later)).Fact);
        Assert.Same(first, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(30, later, first)).Fact);
        Assert.Same(first, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(30, first, later)).Fact);
        // The stored earlier result/context remains immutable after later human visibility.
        Assert.Same(later, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(20, later, first)).Fact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LaterDistinctTriggersNeverSupersedeFirstOrManufactureConflict(bool firstIfvg)
    {
        var first = Observation(firstIfvg);
        var later = Observation(!firstIfvg, 20);
        var sameKindLater = Observation(firstIfvg, 15);
        Assert.Same(first, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(30, later, sameKindLater, first)).Fact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameCandleRequiresTypedHumanOrderAndExactTakeSourceBinding(bool ifvg)
    {
        var ordered = Observation(ifvg, 5, order: NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation);
        Assert.Same(ordered, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(10, ordered)).Fact);
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(ifvg, 5)));
        var noSameCandleSource = new NasdaqHumanLiquidityTakeObservation(take.Take.Reference,
            new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol,
                "source:event", 89, take.Take.EffectiveAtUtc, "source:no candle"), take.Take.ReferenceEligibleAtUtc, take.Take.ObservedAtUtc, "review:take");
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(ifvg, 5,
            otherTake: noSameCandleSource, order: NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation)));
    }

    [Fact]
    public void BeforeTakeCannotBeReused() => Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(minute: 0, observed: 10)));

    [Fact]
    public void SameTimeScIfvgConflictHasNoTypeOrInputPriority()
    {
        var sc = Observation(); var ifvg = Observation(true);
        var first = Assert.IsType<NasdaqHumanM5TriggerSelection.Conflict>(Select(10, sc, ifvg));
        var reverse = Assert.IsType<NasdaqHumanM5TriggerSelection.Conflict>(Select(10, ifvg, sc));
        Assert.Equal(first.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal(2, first.SupportingObservations.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompatibleDuplicateSupportIsLosslessAndReadOnly(bool ifvg)
    {
        var a = Observation(ifvg); var b = Observation(ifvg, observed: 15, source: "second reviewer");
        var result = Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(20, b, a));
        Assert.Equal(new[] { a, b }, result.SupportingObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5TriggerObservation>)result.SupportingObservations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)a.Event.ReviewedSourceCandles).Clear());
    }

    [Fact]
    public void ContradictorySameSourceSwingClaimsPreserveConflict()
    {
        var a = Observation(swingPrice: 100); var b = Observation(swingPrice: 101);
        Assert.Equal(2, Assert.IsType<NasdaqHumanM5TriggerSelection.Conflict>(Select(10, a, b)).SupportingObservations.Count);
    }

    [Fact]
    public void TerminalSessionBlocksAnyTakeAndTriggerWithoutRevival()
    {
        var invalid = M5Fixture.Established(high: true);
        var context = M5Fixture.Context(M5Fixture.At(14, 10), [Observation()]).WithPriorObservations(invalid.History);
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(selector.Select(context, take));
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(selector.Select(context, invalid.Fact));
    }

    [Fact]
    public void WrongTakeSameSessionAndOppositeDirectionAreUnusable()
    {
        var other = M5Fixture.Take(false, eventId: "different event");
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(otherTake: other)));
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(direction: NasdaqHumanH4PermittedDirection.Sell)));
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(selector.Select(M5Fixture.Context(M5Fixture.At(14, 10), [Observation()]), take));
    }

    [Fact]
    public void AnotherTradingDayCannotUseThisCanonicalTake()
    {
        var context = M5Fixture.Context(M5Fixture.At(38, 5), [Observation()]).WithPriorObservations(history);
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(selector.Select(context, take));
    }

    [Fact]
    public void FutureHumanAvailabilityDoesNotChangeEarlierFrame()
    {
        var baseline = Observation(); var future = Observation(true, 5, 30, order: NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation);
        Assert.Same(baseline, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(10, baseline, future)).Fact);
        Assert.Same(future, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(30, baseline, future)).Fact);
    }

    [Fact]
    public void MissingNamedSourceRetainsUnavailableEvidenceAndMixedSupport()
    {
        // Use an exact absent historical source outside the supplied M5 range.
        var missingSource = M5Fixture.Five(M5Fixture.At(13, 20));
        var marketEvent = new NasdaqHumanM5TriggerEvent.StructuralChange(M5Fixture.Five(M5Fixture.At(14, 10)),
            take.Direction, [missingSource], [missingSource], 100);
        var missing = new NasdaqHumanM5TriggerObservation(take.Take, marketEvent, M5Fixture.At(14, 10), "retained missing source");
        var result = Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, missing));
        Assert.Same(missing, Assert.Single(result.UnavailableSourceObservations));
        var mixed = Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(10, Observation(), missing));
        Assert.Same(missing, Assert.Single(mixed.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanM5TriggerObservation>)mixed.UnavailableSourceObservations).Clear());
    }

    [Fact]
    public void ExactSourceOhlcMismatchIsUnusableRatherThanTrusted()
    {
        var mismatch = new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10), 101), take.Direction,
            [M5Fixture.Five(M5Fixture.At(14))], [M5Fixture.Five(M5Fixture.At(14))]);
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10,
            new NasdaqHumanM5TriggerObservation(take.Take, mismatch, M5Fixture.At(14, 10), "wrong OHLC")));
    }

    [Fact]
    public void AddingFutureCandlesAndAssertionsLeavesHistoricalSelectionAndStoredContextUnchanged()
    {
        var first = Observation(); var future = Observation(true, 20);
        var before = M5Fixture.Context(M5Fixture.At(14, 10), [first], includeFutureCandles: false).WithPriorObservations(history);
        var after = Context(10, future, first);
        var stored = Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(selector.Select(before, take));
        var expanded = Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(selector.Select(after, take));
        Assert.Equal(stored.SupportingObservations, expanded.SupportingObservations);
        Assert.Single(before.InputObservations);
        Assert.Single(after.InputObservations);
        Assert.Equal(stored.SupportingObservations, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(selector.Select(before, take)).SupportingObservations);
    }

    [Fact]
    public void ExactReferenceIdentityMattersEvenWhenSessionTimeAndPriceAgree()
    {
        var context = M5Fixture.Context(M5Fixture.At(13, 15), []);
        var otherReference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, context.AsOfUtc, "different selected endpoint");
        var other = new NasdaqHumanLiquidityTakeObservation(otherReference, take.Take.Event,
            take.Take.ReferenceEligibleAtUtc, take.Take.ObservedAtUtc, "same time and price, different reference");
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(Select(10, Observation(otherTake: other)));
    }

    [Fact]
    public void ForeignSourcesAndOpenConfirmationCannotEnterDrivingEvidence()
    {
        var source = M5Fixture.Five(M5Fixture.At(14));
        var foreign = new Candle(new("foreign"), LiquidityFixture.Symbol, NasdaqHumanM5TriggerEvent.M5,
            source.OpenTimeUtc, source.CloseTimeUtc, 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), take.Direction, [source], [foreign]));
        var eventFromForeignSource = new NasdaqHumanM5TriggerEvent.Ifvg(foreign, take.Direction, [foreign], [foreign]);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerObservation(take.Take, eventFromForeignSource, M5Fixture.At(14, 10), "foreign"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerObservation(take.Take,
            Observation(minute: 15).Event, M5Fixture.At(14, 10), "confirmation still open"));
    }

    [Fact]
    public void SourceCollectionsSnapshotMembershipAndIgnoreInputOrder()
    {
        var sources = new List<Candle> { M5Fixture.Five(M5Fixture.At(13, 55)), M5Fixture.Five(M5Fixture.At(14)) };
        var a = new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), take.Direction, sources, sources);
        var b = new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), take.Direction, sources.AsEnumerable().Reverse(), sources.AsEnumerable().Reverse());
        sources.Clear();
        Assert.Equal(2, a.ReviewedSourceCandles.Count);
        Assert.Equal(2, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(Select(10,
            new NasdaqHumanM5TriggerObservation(take.Take, a, M5Fixture.At(14, 10), "one"),
            new NasdaqHumanM5TriggerObservation(take.Take, b, M5Fixture.At(14, 10), "two"))).SupportingObservations.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlignedSellReusesCanonicalDirectionWithoutReadingH4OrM5Geometry(bool ifvg)
    {
        var sell = M5Fixture.Established(true, NasdaqHumanH4PermittedDirection.Sell);
        var review = Observation(ifvg, otherTake: sell.Fact.Take, direction: sell.Fact.Direction);
        var context = M5Fixture.Context(M5Fixture.At(14, 10), [review]).WithPriorObservations(sell.History);
        Assert.Same(review, Assert.IsType<NasdaqHumanM5TriggerSelection.Unique>(selector.Select(context, sell.Fact)).Fact);
    }

    [Fact]
    public void CanonicalTimeOwnerPreventsUseAtOperationalCutoff()
    {
        var context = M5Fixture.Context(M5Fixture.At(16), [Observation()]).WithPriorObservations(history);
        Assert.IsType<NasdaqHumanM5TriggerSelection.Missing>(selector.Select(context, take));
    }

    [Fact]
    public void ConflictAlsoRetainsUnavailableSourceProvenance()
    {
        var missingSource = M5Fixture.Five(M5Fixture.At(13, 20));
        var e = new NasdaqHumanM5TriggerEvent.Ifvg(M5Fixture.Five(M5Fixture.At(14, 10)), take.Direction, [missingSource], [missingSource]);
        var unavailable = new NasdaqHumanM5TriggerObservation(take.Take, e, M5Fixture.At(14, 10), "missing evidence");
        var result = Assert.IsType<NasdaqHumanM5TriggerSelection.Conflict>(Select(10, Observation(), Observation(true), unavailable));
        Assert.Equal(2, result.SupportingObservations.Count);
        Assert.Same(unavailable, Assert.Single(result.UnavailableSourceObservations));
    }

    [Fact]
    public void TypeInvariantsRejectWrongTimeframeIdentityFutureSourceAndInvalidAvailability()
    {
        var confirmation = M5Fixture.Five(M5Fixture.At(14, 10));
        var source = M5Fixture.Five(M5Fixture.At(14));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerEvent.Ifvg(LiquidityFixture.Candle(13), take.Direction, [source], [source]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerEvent.Ifvg(confirmation, take.Direction, [LiquidityFixture.Candle(13)], [source]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerEvent.Ifvg(confirmation, take.Direction, [source], [M5Fixture.Five(M5Fixture.At(14, 15))]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerEvent.Ifvg(confirmation, NasdaqHumanH4PermittedDirection.Unresolved, [source], [source]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerObservation(take.Take, Observation().Event, M5Fixture.At(14, 5), "early"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerObservation(take.Take, Observation().Event, M5Fixture.At(14, 10), " "));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanM5TriggerObservation(take.Take, Observation().Event, M5Fixture.At(14, 10).ToOffset(TimeSpan.FromHours(1)), "local"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NasdaqHumanM5TriggerObservation(take.Take, Observation().Event, M5Fixture.At(14, 10), "bad enum", (NasdaqHumanM5TakeTriggerOrder)42));
    }
}
