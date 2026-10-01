using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionEvidencePrimitiveTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void DerivesUniversalIdentityAndRetainsExactCompletion(int branch)
    {
        var completion = Completed(branch);
        var episode = NasdaqPostCompletionEpisode.FromCompleted(completion);
        var equivalent = NasdaqPostCompletionEpisode.FromCompleted(Completed(branch));
        Assert.Equal(episode, equivalent);
        Assert.Equal(episode.GetHashCode(), equivalent.GetHashCode());
        Assert.Same(completion, episode.Completion);
        Assert.Same(completion.Episode, episode.PreviousCompletedEpisode);
        Assert.Same(completion.MarketCursor, episode.ConfirmingCandle);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Upper, episode.ActiveExtremeSide);
        Assert.IsNotType<NasdaqHumanOriginVertexEpisode>(episode);
    }

    [Fact]
    public void IdentityDistinguishesPredecessorAndConfirmation()
    {
        var original = NasdaqPostCompletionEpisode.FromCompleted(Completed(0));
        Assert.NotEqual(original, NasdaqPostCompletionEpisode.FromCompleted(Completed(0, 4)));
        Assert.NotEqual(original, NasdaqPostCompletionEpisode.FromCompleted(Completed(0, confirmingHour: 24)));
        Assert.Throws<ArgumentNullException>(() => NasdaqPostCompletionEpisode.FromCompleted(null!));
    }

    [Fact]
    public void BearishCompletionDerivesLowerExtremeAndAcceptsBullishTurn()
    {
        var candidate = Candidate(bearish: true);
        var completion = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, Candle(20, 70, 130, 60, 69)));
        var episode = NasdaqPostCompletionEpisode.FromCompleted(completion);
        Assert.Equal(StructuralTurnBodyCoordinateSide.Lower, episode.ActiveExtremeSide);
        var turn = Candle(28, 60, 90, 50, 80);
        Assert.Same(turn, new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, turn).CorrectionStartCandle);
    }

    [Fact]
    public void EventSupportsSameCandleAndLaterNonOverlappingTurnWithValueIdentity()
    {
        var sameEpisode = NasdaqPostCompletionEpisode.FromCompleted(Completed(8));
        var immediate = new NasdaqPostCompletionActiveExtremeMembershipEvent(sameEpisode, sameEpisode.ConfirmingCandle);
        Assert.Equal(sameEpisode.ConfirmingCandleOpenTimeUtc, immediate.CorrectionStartCandleOpenTimeUtc);
        var episode = NasdaqPostCompletionEpisode.FromCompleted(Completed(0));
        var first = new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, Candle(32, 140, 150, 120, 130));
        var equal = new NasdaqPostCompletionActiveExtremeMembershipEvent(
            NasdaqPostCompletionEpisode.FromCompleted(Completed(0)), Candle(32, 140, 150, 120, 130));
        Assert.Equal(first, equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, Candle(36, 140, 150, 120, 130)));
    }

    [Fact]
    public void EventRejectsEarlierOverlappingAndNonCorrectiveCandles()
    {
        var episode = NasdaqPostCompletionEpisode.FromCompleted(Completed(0));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, Candle(16, 140, 150, 120, 130)));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, Candle(22, 140, 150, 120, 130)));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, episode.ConfirmingCandle));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, Candle(28, 130, 150, 120, 130)));
        var sameEpisode = NasdaqPostCompletionEpisode.FromCompleted(Completed(8));
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(sameEpisode, Candle(20, 141, 150, 50, 131)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(null!, Candle(28, 140, 150, 120, 130)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EventRejectsDifferentMarketSeries(int mismatch)
    {
        var episode = NasdaqPostCompletionEpisode.FromCompleted(Completed(0));
        var turn = new Candle(mismatch == 0 ? new("other") : Provider,
            mismatch == 1 ? new("other") : Symbol, mismatch == 2 ? new(1, TimeframeUnit.Hour) : H4,
            Start.AddHours(28), Start.AddHours(32), 140, 150, 120, 130, null);
        Assert.Throws<ArgumentException>(() => new NasdaqPostCompletionActiveExtremeMembershipEvent(episode, turn));
    }

    [Fact]
    public void ObservationCopiesNormalizesMembersAndPreservesProvenanceWithValueEquality()
    {
        var evt = Event();
        var members = new[] { Start.AddHours(20), Start.AddHours(16) };
        var observation = new NasdaqHumanPostCompletionActiveExtremeObservation(evt, members, Start.AddHours(40), "review:vertex");
        members[0] = Start;
        var equal = new NasdaqHumanPostCompletionActiveExtremeObservation(Event(),
            [Start.AddHours(16), Start.AddHours(20)], Start.AddHours(40), "review:vertex");
        Assert.Equal(observation, equal);
        Assert.Equal(observation.GetHashCode(), equal.GetHashCode());
        Assert.Equal([Start.AddHours(16), Start.AddHours(20)], observation.SelectedMemberOpenTimesUtc);
        Assert.Same(evt, observation.MembershipEvent);
        Assert.Equal("review:vertex", observation.SourceReference);
        Assert.Equal(Start.AddHours(40), observation.ObservedAtUtc);
        Assert.Equal(evt.Episode.PreviousCompletedEpisode.StrategyId, observation.StrategyId);
        Assert.Equal(evt.Episode.PreviousCompletedEpisode.StrategyVersion, observation.StrategyVersion);
        Assert.Equal(Provider, observation.ProviderId);
        Assert.Equal(Symbol, observation.Symbol);
        Assert.Equal(H4, observation.Timeframe);
        Assert.Throws<NotSupportedException>(() => ((IList<DateTimeOffset>)observation.SelectedMemberOpenTimesUtc)[0] = Start);
        Assert.NotEqual(observation, new NasdaqHumanPostCompletionActiveExtremeObservation(evt,
            [Start.AddHours(16)], Start.AddHours(40), "review:vertex"));
        Assert.NotEqual(observation, new NasdaqHumanPostCompletionActiveExtremeObservation(evt,
            equal.SelectedMemberOpenTimesUtc, Start.AddHours(44), "review:vertex"));
        Assert.NotEqual(observation, new NasdaqHumanPostCompletionActiveExtremeObservation(evt,
            equal.SelectedMemberOpenTimesUtc, Start.AddHours(40), "review:other"));
    }

    [Fact]
    public void ObservationRejectsMalformedMembersTimeAndSource()
    {
        var evt = Event();
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [], Start.AddHours(40), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start, Start], Start.AddHours(40), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start.ToOffset(TimeSpan.FromHours(1))], Start.AddHours(40), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start], Start.AddHours(40).ToOffset(TimeSpan.FromHours(1)), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start], Start.AddHours(28), "review"));
        foreach (var source in new[] { "", " ", " review" })
            Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start], Start.AddHours(40), source));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(null!, [Start], Start.AddHours(40), "review"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, null!, Start.AddHours(40), "review"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionActiveExtremeObservation(evt, [Start], Start.AddHours(40), null!));
    }

    private static NasdaqPostCompletionActiveExtremeMembershipEvent Event() => new(
        NasdaqPostCompletionEpisode.FromCompleted(Completed(0)), Candle(28, 140, 150, 120, 130));

    [Fact]
    public void ReplayEnvelopeBoundsVisibilityWithoutChangingTheEarlierContext()
    {
        var observation = new NasdaqHumanPostCompletionActiveExtremeObservation(Event(),
            [Start.AddHours(20)], Start.AddHours(40), "review:later");
        var early = Context([Candle(28, 140, 150, 120, 130)], observation);
        var later = Context([Candle(40, 100, 110, 90, 100)], observation);
        Assert.Empty(early.InputObservations);
        Assert.Same(observation, Assert.Single(later.InputObservations));
        Assert.Empty(early.InputObservations);
    }

    private static NasdaqH4ReconstructionSnapshot.Completed Completed(int branch, int offset = 0, int confirmingHour = 20)
    {
        var candidate = Candidate(offset);
        if (branch == 0)
            return new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(new NasdaqDirectCandidateBreakoutCompletionCalculator()
                .Evaluate(candidate, Candle(confirmingHour + offset, 130, 140, 60, 131)));
        if (branch == 7)
        {
            var breakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, Candle(20 + offset, 100, 140, 50, 131));
            return new NasdaqH4DirectionalBreakoutCompletionSnapshotReducer().Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(breakout));
        }
        NasdaqPostInvalidationCandidateRebuildBreakoutState state;
        IStrategyReplayInputObservation observation;
        Candle[] candles;
        if (branch == 8)
        {
            state = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(candidate, Candle(20 + offset, 140, 150, 50, 131));
            observation = new NasdaqHumanCollisionStructuralPriceObservation(state.Episode, state.ValidatingCandle.OpenTimeUtc,
                state.CandidateSide, 111, Start.AddHours(40 + offset), "review:price");
            candles = [state.ValidatingCandle];
        }
        else
        {
            var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, Candle(20 + offset, 100, 120, 50, 90));
            var turn = Candle(24 + offset, 80, 120, 60, 100);
            var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, turn).Tracking!;
            state = new NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator().Evaluate(tracking, Candle(28 + offset, 100, 140, 55, 131)).Breakout!;
            observation = new NasdaqHumanRebuiltCandidateVertexObservation(state.Episode.StrategyId, state.Episode.StrategyVersion,
                Provider, Symbol, state.Episode.InvalidatingCandleOpenTimeUtc, pending.MigrationCandle.OpenTimeUtc,
                state.CandidateSide, [pending.MigrationCandle.OpenTimeUtc], Start.AddHours(40 + offset), "review:members");
            candles = [pending.MigrationCandle, turn, state.ValidatingCandle];
        }
        var trigger = Candle(40 + offset, 100, 110, 90, 100);
        var context = Context([.. candles, trigger], observation);
        return (NasdaqH4ReconstructionSnapshot.Completed)new NasdaqH4BreakoutAwaitingCompletionReducer()
            .Reduce(new NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion(state), context).Snapshot;
    }

    private static NasdaqPostInvalidationCandidateState Candidate(int offset = 0, bool bearish = false)
    {
        var origin = bearish ? Candle(offset, 100, 110, 95, 110) : Candle(offset, 100, 110, 85, 90);
        var prior = bearish ? Candle(4 + offset, 95, 100, 90, 96) : Candle(4 + offset, 105, 115, 100, 110);
        var invalidating = bearish ? Candle(8 + offset, 100, 110, 80, 90) : Candle(8 + offset, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 140 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var start = bearish ? Candle(12 + offset, 70, 105, 65, 95) : Candle(12 + offset, 130, 145, 95, 115);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction,
            bearish ? Candle(16 + offset, 100, 130, 90, 99) : Candle(16 + offset, 80, 100, 60, 90)).Candidate!;
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);
}
