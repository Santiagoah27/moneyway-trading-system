using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelectorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector selector = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndLateUniqueRetainOriginalCollisionAndEveryVisibleSource(bool bearish)
    {
        var state = Awaiting(bearish);
        var first = Observation(state, 100.1234m, 88, "review:z");
        var duplicate = Observation(state, 100.1234m, 92, "review:a");
        var early = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Missing>(selector.Select(state, At(84, first, duplicate)));
        Assert.Empty(early.SupportingObservations);
        var single = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(88, first, duplicate)));
        Assert.Same(first, Assert.Single(single.SupportingObservations));
        var unique = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(92, duplicate, first)));
        Assert.Equal(100.1234m, unique.StructuralPrice);
        Assert.Equal(new[] { first, duplicate }, unique.SupportingObservations);
        foreach (var selection in new NasdaqHumanPostCompletionCollisionStructuralPriceSelection[] { early, single, unique })
        {
            Assert.Same(state, selection.AwaitingState);
            Assert.Same(state.EvidenceContext, selection.Context);
            Assert.Same(state.CollisionCandle, selection.AwaitingState.MarketCursor);
            Assert.Same(state.LifecycleResult, selection.AwaitingState.LifecycleResult);
        }
        Assert.Equal(Start.AddHours(92), unique.AsOfUtc);
        Assert.Same(state.EvidenceContext, first.Context);
        Assert.Equal(Start.AddHours(88), first.ObservedAtUtc);
        Assert.Equal("review:z", first.SourceReference);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>)unique.SupportingObservations).Clear());
        Assert.Equal(first, Observation(state, first.StructuralPrice, 88, first.SourceReference));
        Assert.Equal(first.GetHashCode(), Observation(state, first.StructuralPrice, 88, first.SourceReference).GetHashCode());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactConflictPersistsWithDuplicatesAndNoFutureWinner(bool bearish)
    {
        var state = Awaiting(bearish);
        var a = Observation(state, 100m, 88, "review:z");
        var b = Observation(state, 100.0001m, 88, "review:conflict");
        var duplicate = Observation(state, 100m, 92, "review:a");
        var future = Observation(state, 999m, 100, "review:future");
        var conflict = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Conflict>(selector.Select(state, At(88, a, b, duplicate)));
        Assert.Equal(new[] { 100m, 100.0001m }, conflict.ConflictingPrices);
        Assert.Equal(new[] { b, a }, conflict.SupportingObservations);
        var later = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Conflict>(selector.Select(state, At(92, duplicate, b, a, future)));
        Assert.Equal(conflict.ConflictingPrices, later.ConflictingPrices);
        Assert.Equal(new[] { b, a, duplicate }, later.SupportingObservations);
        Assert.Same(state, later.AwaitingState);
        var reversed = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Conflict>(selector.Select(state, At(92, future, a, b, duplicate)));
        Assert.Equal(later.SupportingObservations, reversed.SupportingObservations);
        Assert.Equal(later.ConflictingPrices, reversed.ConflictingPrices);
        Assert.Throws<NotSupportedException>(() => ((ICollection<decimal>)later.ConflictingPrices).Clear());
    }

    [Fact]
    public void FutureEvidenceDoesNotChangeHistoricalUniqueAndInputOrderDoesNotChangeProvenance()
    {
        var state = Awaiting(false);
        var a = Observation(state, 100m, 88, "review:z");
        var b = Observation(state, 100m, 88, "review:a");
        var future = Observation(state, 200m, 92, "review:future");
        var observations = new[] { a, b, future };
        var original = observations.ToArray();
        var before = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(88, a, b)));
        var after = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(88, observations)));
        var reversed = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(88, future, b, a)));
        Assert.Equal(before.StructuralPrice, after.StructuralPrice);
        Assert.Equal(before.SupportingObservations, after.SupportingObservations);
        Assert.Equal(after.SupportingObservations, reversed.SupportingObservations);
        Assert.Equal(new[] { b, a }, after.SupportingObservations);
        Assert.Equal(original, observations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void IgnoresEachDifferentContextComponentWithoutPartialMatching(int mismatch)
    {
        var state = Awaiting(false);
        // Isolate each identity component using internal construction; normal observation creation remains anchored to a state.
        var differentEpisode = NasdaqPostCompletionEpisode.FromCompleted(new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(
                EquivalentCandidate(false, state.SourceCandidate.CorrectionStartCandle, state.SourceCandidate.TerminalCandle),
                Candle(88, 130, 180, 90, 132, false))));
        var contextConstructor = Assert.Single(typeof(NasdaqPostCompletionCollisionStructuralPriceContext).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance), c => c.GetParameters().Length == 3);
        var key = (NasdaqPostCompletionCollisionStructuralPriceContext)contextConstructor.Invoke([
            mismatch == 0 ? differentEpisode : state.Episode,
            mismatch == 1 ? state.CollisionCandle.OpenTimeUtc.AddHours(4) : state.CollisionCandle.OpenTimeUtc,
            mismatch == 2 ? StructuralCandidateExtremeSide.Upper : state.Collision.CandidateSide]);
        var stateConstructor = Assert.Single(typeof(NasdaqPostCompletionBreakoutAwaitingCompletionState).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var other = (NasdaqPostCompletionBreakoutAwaitingCompletionState)stateConstructor.Invoke([state.LifecycleResult, state.Collision, state.Resolution]);
        typeof(NasdaqPostCompletionBreakoutAwaitingCompletionState).GetField("<EvidenceContext>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(other, key);
        var observation = Observation(other, 100m, 92, "review:unrelated");
        var result = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Missing>(selector.Select(state, At(92, observation)));
        Assert.Same(state.EvidenceContext, result.Context);
        Assert.Empty(result.SupportingObservations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SharedSelectionMatchesOld008MissingUniqueConflictAndVisibility(int scenario)
    {
        var state = Awaiting(false);
        var a = Observation(state, 100m, 88, "review:z");
        var b = Observation(state, scenario == 2 ? 100.0001m : 100m, 88, "review:a");
        var future = Observation(state, 999m, 92, "review:future");
        var inputs = scenario == 0 ? new[] { future } : new[] { b, a, future };
        var oldEpisode = state.Episode.PreviousCompletedEpisode;
        var oldInputs = inputs.Select(o => new NasdaqHumanCollisionStructuralPriceObservation(oldEpisode,
            state.CollisionCandle.OpenTimeUtc, state.Collision.CandidateSide, o.StructuralPrice, o.ObservedAtUtc, o.SourceReference)).ToArray();
        var old = new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(At(88, oldInputs),
            new NasdaqHumanCollisionStructuralPriceEpisode(oldEpisode, state.CollisionCandle.OpenTimeUtc, state.Collision.CandidateSide));
        var selection = selector.Select(state, At(88, inputs));
        Assert.Equal(old.SupportingObservations.Select(o => (o.StructuralPrice, o.ObservedAtUtc, o.SourceReference)),
            selection.SupportingObservations.Select(o => (o.StructuralPrice, o.ObservedAtUtc, o.SourceReference)));
        if (scenario == 0) Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Missing>(selection);
        else if (scenario == 1) Assert.Equal(old.StructuralPrice, Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selection).StructuralPrice);
        else Assert.Equal(old.DistinctStructuralPrices, Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.Conflict>(selection).ConflictingPrices);
    }

    [Fact]
    public void ObservationAndSelectionRejectInvalidInputsAndExposeOnlyImmutableEvidenceFacts()
    {
        var state = Awaiting(false);
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, At(88)));
        Assert.Throws<ArgumentNullException>(() => selector.Select(state, null!));
        Assert.Throws<ArgumentException>(() => selector.Select(state, At(80)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(null!, 100, Start.AddHours(88), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(state, 100, Start.AddHours(83), "review"));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(state, 100, Start.AddHours(88).ToOffset(TimeSpan.FromHours(1)), "review"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(state, 100, Start.AddHours(88), null!));
        Assert.Throws<ArgumentException>(() => Observation(state, 100, 88, " review"));
        Assert.Throws<ArgumentException>(() => Observation(state, 100, 88, ""));
        var obs = Observation(state, 100, 84, "review");
        Assert.Equal(state.CollisionCandle.CloseTimeUtc, obs.ObservedAtUtc);
        Assert.Equal(new[] { "Context", "ObservedAtUtc", "ProviderId", "SourceReference", "StrategyId", "StrategyVersion", "StructuralPrice", "Symbol", "Timeframe" }, obs.GetType().GetProperties().Select(p => p.Name).Order());
        Assert.All(obs.GetType().GetProperties(), p => Assert.Null(p.SetMethod));
        var union = typeof(NasdaqHumanPostCompletionCollisionStructuralPriceSelection);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "Conflict", "Missing", "UniqueEvidenceReady" }, union.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(branch.GetProperties(), p => p.Name.Contains("Geometry") || p.Name.Contains("Completion") || p.Name == "MarketCursor" || p.Name == "ValidatedTurn");
        }
    }

    [Fact]
    public void ObservationUsesActualClosedCollisionAndSelectorNeedsNoCollisionFrameLookup()
    {
        var source = Candidate(false);
        var collisionCandle = new Candle(Provider, Symbol, H4, Start.AddHours(80), Start.AddHours(82), 140, 180, 80, 132, null);
        var state = new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, collisionCandle));
        var observation = Observation(state, 100m, 82, "review:close");
        var selection = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(state, At(84, observation)));
        Assert.Same(collisionCandle, selection.AwaitingState.MarketCursor);
        Assert.Same(observation, Assert.Single(selection.SupportingObservations));
        var otherSeries = new Candle(new("other"), Symbol, H4, Start.AddHours(84), Start.AddHours(88), 120, 180, 90, 123, null);
        Assert.Throws<ArgumentException>(() => selector.Select(state, Context([otherSeries])));
    }

    private static NasdaqPostCompletionBreakoutAwaitingCompletionState Awaiting(bool bearish) =>
        new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(Candidate(bearish), Candle(80, 140, 180, 80, 132, bearish)));
    private static NasdaqHumanPostCompletionCollisionStructuralPriceObservation Observation(
        NasdaqPostCompletionBreakoutAwaitingCompletionState state, decimal price, int hour, string source) => new(state, price, Start.AddHours(hour), source);
    private static StrategyReplayContext At(int hour, params IStrategyReplayInputObservation[] observations) =>
        Context([Candle(hour - 4, 120, 180, 90, 123, false)], observations);

    private static NasdaqPostCompletionCandidateState Candidate(bool bearish)
    {
        var correction = new NasdaqPostCompletionActiveCorrectionInitializer().Initialize(Ready(bearish, false, false));
        var terminal = Candle(32, 120, 180, 90, 125, bearish);
        var observation = Assert.IsType<NasdaqPostCompletionActiveCorrectionBoundaryObservationResult.InsideStructuralRange>(
            new NasdaqPostCompletionActiveCorrectionBoundaryObservationCalculator().Evaluate(correction, terminal));
        var provisional = new NasdaqPostCompletionCorrectionTurnCalculator().Evaluate(observation);
        return Assert.IsType<NasdaqPostCompletionCorrectionTurnReductionResult.Candidate>(new NasdaqPostCompletionCorrectionTurnReducer().Reduce(provisional)).State;
    }

    private static NasdaqPostInvalidationCandidateState EquivalentCandidate(bool bearish, Candle start, Candle terminal)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        impulse = new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(24, 125, 190, 100, 131)).ResultingState;
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
    }
    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 130, 180, 100, 120, bearish);
        var started = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = new NasdaqPostCompletionExtremeMembershipPendingInitializer().Initialize(started);
        var a = source.ConfirmingCandle;
        var b = ties ? Candle(24, 130, 140, 60, 131, bearish) : Candle(24, 125, 190, 100, 120, bearish);
        var selected = overlap ? new[] { a.OpenTimeUtc, b.OpenTimeUtc, turn.OpenTimeUtc } : new[] { a.OpenTimeUtc, b.OpenTimeUtc };
        var observations = new[]
        {
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected, Start.AddHours(40), "review:first"),
            new NasdaqHumanPostCompletionActiveExtremeObservation(pending.MembershipEvent, selected.Reverse().ToArray(), Start.AddHours(48), "review:second"),
        };
        var context = Context([a, b, turn, Candle(44, 135, 180, 100, 145, bearish)], observations);
        var evidence = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady>(
            new NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer().Reduce(pending, context));
        var members = Assert.IsType<NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved>(
            new NasdaqPostCompletionExtremeMembershipPendingResolutionReducer().Reduce(evidence, context));
        return new NasdaqPostCompletionExtremeGeometryPreparer().Prepare(members);
    }
    private static NasdaqPostCompletionActiveImpulseState State(bool bearish)
    {
        Candle C(int hour, decimal open, decimal high, decimal low, decimal close) => Candle(hour, open, high, low, close, bearish);
        var origin = C(0, 100, 110, 85, 90);
        var prior = C(4, 105, 115, 100, 110);
        var invalidating = C(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], observation);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4,
            bearish ? 130 : 70, bearish ? 95 : 105, bearish ? 125 : 75, [prior],
            bearish ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, C(12, 130, 145, 95, 115)));
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, C(16, 80, 100, 60, 90)).Candidate!;
        var completed = new NasdaqH4ReconstructionSnapshot.Completed.DirectCandidate(
            new NasdaqDirectCandidateBreakoutCompletionCalculator().Evaluate(candidate, C(20, 130, 140, 60, 131)));
        return new NasdaqPostCompletionActiveImpulseInitializer().Initialize(completed);
    }

    private static StrategyReplayContext Context(Candle[] candles, params IStrategyReplayInputObservation[] observations)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, candles)]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame!, observations);
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close, bool bearish) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4),
            bearish ? 200 - open : open, bearish ? 200 - low : high, bearish ? 200 - high : low,
            bearish ? 200 - close : close, null);
}
