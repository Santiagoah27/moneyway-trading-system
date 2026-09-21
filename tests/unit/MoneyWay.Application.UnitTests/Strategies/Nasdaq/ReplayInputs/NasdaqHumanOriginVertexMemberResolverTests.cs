using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanOriginVertexMemberResolverTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanOriginVertexMemberResolver resolver = new();

    [Fact]
    public void ResolvesExactInvalidationAndHumanSelectedMembersInChronologicalOrder()
    {
        var first = Candle(0, 4, 100, 102, 98, 101);
        var selected = Candle(4, 8, 101, 115, 97, 100);
        var unselected = Candle(8, 12, 100, 120, 96, 101);
        var invalidating = Candle(12, 16, 101, 125, 95, 99);
        var observation = Observation(12, 4, 0);
        var context = Context(Series(H4, first, selected, unselected, invalidating), observation, 16);

        var result = resolver.Evaluate(observation, context);
        var repeated = resolver.Evaluate(observation, context);

        Assert.Same(invalidating, result.InvalidatingCandle);
        Assert.Equal([first, selected], result.SelectedMembers);
        Assert.Same(first, result.SelectedMembers[0]);
        Assert.Same(selected, result.SelectedMembers[1]);
        Assert.DoesNotContain(unselected, result.SelectedMembers);
        Assert.DoesNotContain(invalidating, result.SelectedMembers);
        Assert.Equal(result.SelectedMembers, repeated.SelectedMembers);
        Assert.Same(result.InvalidatingCandle, repeated.InvalidatingCandle);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Candle>)result.SelectedMembers).Add(unselected));
    }

    [Fact]
    public void ResolvesOneMemberWithoutAddingExactEqualOrMoreExtremeNeighbors()
    {
        var selected = Candle(0, 4, 100, 102, 98, 101);
        var equalBodyNeighbor = Candle(4, 8, 100, 120, 80, 101);
        var invalidating = Candle(8, 12, 101, 125, 95, 99);
        var observation = Observation(8, 0, observedAtHour: 12);

        var result = resolver.Evaluate(observation,
            Context(Series(H4, selected, equalBodyNeighbor, invalidating), observation, 12));

        Assert.Equal([selected], result.SelectedMembers);
        Assert.Same(invalidating, result.InvalidatingCandle);
    }

    [Fact]
    public void RejectsMismatchedIdentityAndNonH4Snapshot()
    {
        var observation = Observation(12, 0);
        var wrongProvider = Context(Series(H4, new MarketDataProviderId("other"), Symbol,
            Candle(0, 4, provider: new("other")), Candle(12, 16, provider: new("other"))), null, 16);
        var wrongSymbol = Context(Series(H4, Provider, new MarketSymbol("OTHER"),
            Candle(0, 4, symbol: new("OTHER")), Candle(12, 16, symbol: new("OTHER"))), null, 16);
        var hour = new Timeframe(1, TimeframeUnit.Hour);
        var wrongTimeframe = Context(Series(hour, new Candle(Provider, Symbol, hour, Start.AddHours(16),
            Start.AddHours(17), 100, 101, 99, 100, null)), observation, 17);

        Assert.Throws<ArgumentException>(() => resolver.Evaluate(observation, wrongProvider));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(observation, wrongSymbol));
        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(observation, wrongTimeframe));
    }

    [Fact]
    public void MissingInvalidationOrAnySelectedMemberFailsWithoutNearestSubstitution()
    {
        var missingInvalidation = Observation(12, 0, observedAtHour: 15);
        var prior = Candle(0, 4);
        var near = Candle(11, 15);
        var missingMember = Observation(12, 0, 4);
        var invalidating = Candle(12, 16);

        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(missingInvalidation,
            Context(Series(H4, prior, near), missingInvalidation, 15)));
        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(missingMember,
            Context(Series(H4, prior, invalidating), missingMember, 16)));
    }

    [Fact]
    public void CandleSeriesPreventsAnOverlappingMemberFromReachingTheResolver()
    {
        var member = Candle(0, 13);
        var invalidating = Candle(12, 16);
        var observation = Observation(12, 0);

        Assert.Throws<ArgumentException>(() => Series(H4, member, invalidating));
        Assert.Equal(Start.AddHours(13), member.CloseTimeUtc);
        Assert.Equal(Start.AddHours(12), invalidating.OpenTimeUtc);
        Assert.Equal([Start], observation.SelectedMemberOpenTimesUtc);
    }

    [Fact]
    public void RequiresVisibleObservationAndClosedInvalidationAtAsOf()
    {
        var member = Candle(0, 4);
        var invalidating = Candle(12, 16);
        var series = Series(H4, member, invalidating);
        var observation = Observation(12, 0);
        var earlier = Context(series, observation, 4);
        var hidden = Observation(12, 0, observedAtHour: 20);
        var atInvalidationClose = Context(series, hidden, 16);

        Assert.Throws<ArgumentException>(() => resolver.Evaluate(observation, earlier));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(hidden, atInvalidationClose));
        var earlyClaim = Observation(12, 0, observedAtHour: 4);
        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(earlyClaim,
            Context(series, earlyClaim, 4)));
    }

    [Fact]
    public void AppendingFutureCandlesDoesNotChangeEarlierResolution()
    {
        var member = Candle(0, 4);
        var invalidating = Candle(12, 16);
        var later = Candle(16, 20, 100, 200, 1, 150);
        var observation = Observation(12, 0);
        var earlier = Context(Series(H4, member, invalidating), observation, 16);
        var extended = Context(Series(H4, member, invalidating, later), observation, 16);

        var first = resolver.Evaluate(observation, earlier);
        var second = resolver.Evaluate(observation, extended);

        Assert.Same(member, first.SelectedMembers.Single());
        Assert.Same(member, second.SelectedMembers.Single());
        Assert.Same(invalidating, first.InvalidatingCandle);
        Assert.Same(invalidating, second.InvalidatingCandle);
        Assert.DoesNotContain(later, second.SelectedMembers);
    }

    [Fact]
    public void ExplicitObservationIsResolvedWithoutChoosingBetweenConflictingSelections()
    {
        var first = Candle(0, 4);
        var second = Candle(4, 8);
        var invalidating = Candle(12, 16);
        var firstReview = Observation(12, 0);
        var conflictingReview = Observation(12, 4);
        var context = Context(Series(H4, first, second, invalidating), firstReview, 16, conflictingReview);

        Assert.Equal([first], resolver.Evaluate(firstReview, context).SelectedMembers);
        Assert.Equal([second], resolver.Evaluate(conflictingReview, context).SelectedMembers);
        Assert.Equal(2, context.InputObservations.Count);
    }

    private static NasdaqHumanOriginVertexObservation Observation(int invalidationHour, int firstMemberHour,
        int? secondMemberHour = null, int? observedAtHour = null) => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId,
            MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, Start.AddHours(invalidationHour),
            secondMemberHour is int second
                ? [Start.AddHours(firstMemberHour), Start.AddHours(second)]
                : [Start.AddHours(firstMemberHour)],
            Start.AddHours(observedAtHour ?? Math.Max(invalidationHour + 4, 16)), "review:origin");

    private static Candle Candle(int openHour, int closeHour, decimal open = 100, decimal high = 101,
        decimal low = 99, decimal close = 100, MarketDataProviderId? provider = null, MarketSymbol? symbol = null) =>
        new(provider ?? Provider, symbol ?? Symbol, H4, Start.AddHours(openHour), Start.AddHours(closeHour),
            open, high, low, close, null);

    private static CandleSeries Series(Timeframe timeframe, params Candle[] candles) =>
        new(Provider, Symbol, timeframe, candles);

    private static CandleSeries Series(Timeframe timeframe, MarketDataProviderId provider, MarketSymbol symbol,
        params Candle[] candles) => new(provider, symbol, timeframe, candles);

    private static StrategyReplayContext Context(CandleSeries series, NasdaqHumanOriginVertexObservation? observation,
        int asOfHour, params IStrategyReplayInputObservation[] additionalInputs)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([series]);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == Start.AddHours(asOfHour))
            {
                IStrategyReplayInputObservation[] inputs = observation is null
                    ? additionalInputs
                    : [observation, .. additionalInputs];
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance,
                    frame, inputs);
            }
        }
        throw new InvalidOperationException("Test replay frame was not found.");
    }
}
