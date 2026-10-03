using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionHumanStructuralPriceCandidateResolverTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionHumanStructuralPriceCandidateResolver resolver = new();

    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void ResolvesExactHumanPriceAndMigratedAnchorWithOld008EquivalentCandidateFacts(bool bearish, decimal open)
    {
        var source = Candidate(bearish);
        var collisionCandle = Candle(80, open, 180, 80, 132, bearish);
        var awaiting = new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, collisionCandle));
        var price = bearish ? 95.1234m : 104.8766m;
        var first = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, price, Start.AddHours(88), "review:first");
        var second = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, price, Start.AddHours(92), "review:second");
        var selection = Unique(awaiting, first, second);
        var result = resolver.Resolve(awaiting, selection);
        Assert.Same(awaiting, result.AwaitingState);
        Assert.Same(selection, result.Selection);
        Assert.Same(first, result.Selection.SupportingObservations[0]);
        Assert.Same(second, result.Selection.SupportingObservations[1]);
        Assert.Equal(price, result.CandidateGeometry.StructuralPrice);
        Assert.Equal(selection.StructuralPrice, result.CandidateGeometry.StructuralPrice);
        Assert.NotEqual(source.CandidateGeometry.StructuralPrice, price);
        Assert.NotEqual(collisionCandle.Open, price);
        Assert.NotEqual(collisionCandle.Close, price);
        Assert.Equal(awaiting.EffectiveProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.Equal(bearish ? 120m : 80m, result.CandidateGeometry.ProtectionAnchor);
        Assert.NotEqual(source.CandidateGeometry.ProtectionAnchor, result.CandidateGeometry.ProtectionAnchor);
        Assert.NotEqual(source.CandidateGeometry, result.CandidateGeometry);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower, result.CandidateGeometry.Side);
        Assert.Equal(source.CandidateSide, result.CandidateSide);
        Assert.True(result.ValidatedCandidate.IsValidated);
        Assert.Same(result.CandidateGeometry, result.ValidatedCandidate.CandidateGeometry);
        Assert.Same(awaiting.Breakout, result.ValidatedCandidate.BreakObservation);
        Assert.Same(collisionCandle, result.MarketCursor);
        Assert.Same(awaiting.Episode, result.Episode);
        Assert.Same(source.ActivePair, result.AwaitingState.ActivePair);
        Assert.Same(source.ActivePair.ProtectedTurn, result.AwaitingState.PreviousProtectedTurn);
        Assert.Same(source.ActivePair.ActiveExtremeGeometry, result.AwaitingState.FrozenBreakoutTerminal);
        Assert.Same(awaiting.Collision.Migration, result.AwaitingState.Migration);
        Assert.Same(awaiting.Resolution, result.AwaitingState.Resolution);
        Assert.Same(source.CandidateGeometry, result.AwaitingState.SourceCandidate.CandidateGeometry);
        Assert.Same(source.SourceCorrection.GeometryReady, result.AwaitingState.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Same(source.CorrectionTurnCandles, result.AwaitingState.SourceCandidate.CorrectionTurnCandles);
        Assert.Equal(open == 132 ? CandleBodyDirection.Neutral : bearish ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish, result.AwaitingState.BodyDirection);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        Assert.DoesNotContain(collisionCandle, source.CorrectionTurnCandles);
        var repeated = resolver.Resolve(awaiting, selection);
        Assert.Equal(result.CandidateGeometry, repeated.CandidateGeometry);
        Assert.Equal(result.ValidatedCandidate, repeated.ValidatedCandidate);
        Assert.Same(selection, repeated.Selection);
        Assert.Same(awaiting, repeated.AwaitingState);

        var oldCandidate = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        var oldBreakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(oldCandidate, collisionCandle);
        var oldFirst = new NasdaqHumanCollisionStructuralPriceObservation(oldBreakout.Episode, collisionCandle.OpenTimeUtc,
            oldBreakout.CandidateSide, price, first.ObservedAtUtc, first.SourceReference);
        var oldSecond = new NasdaqHumanCollisionStructuralPriceObservation(oldBreakout.Episode, collisionCandle.OpenTimeUtc,
            oldBreakout.CandidateSide, price, second.ObservedAtUtc, second.SourceReference);
        var oldSelection = new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(At(92, oldSecond, oldFirst),
            new NasdaqHumanCollisionStructuralPriceEpisode(oldBreakout.Episode, collisionCandle.OpenTimeUtc, oldBreakout.CandidateSide));
        var old = new NasdaqHumanStructuralPriceBreakoutCompletionCalculator().Evaluate(oldBreakout, oldSelection);
        Assert.Equal(old.CandidateGeometry, result.CandidateGeometry);
        Assert.Equal(old.ValidatedCandidate, result.ValidatedCandidate);
        Assert.Equal(old.HumanPriceSelection.SupportingObservations.Select(o => (o.StructuralPrice, o.ObservedAtUtc, o.SourceReference)),
            result.Selection.SupportingObservations.Select(o => (o.StructuralPrice, o.ObservedAtUtc, o.SourceReference)));
        Assert.Same(old.LastProcessedCandle, result.MarketCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsUniqueSelectionFromAnotherCollision(bool bearish)
    {
        var awaiting = Awaiting(bearish, 80);
        var other = Awaiting(bearish, 84);
        var selection = Unique(other, new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(other, 100m, Start.AddHours(92), "review:other"));
        Assert.NotEqual(awaiting.EvidenceContext, selection.Context);
        Assert.Throws<ArgumentException>(() => resolver.Resolve(awaiting, selection));
        var valid = Unique(awaiting, new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, 100m, Start.AddHours(92), "review:valid"));
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!, valid));
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(awaiting, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RejectsInconsistentInternalUniqueEvidenceWithoutSelectingAnotherPrice(int inconsistency)
    {
        var awaiting = Awaiting(false, 80);
        var observation = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, 100m, Start.AddHours(92), "review");
        var constructor = Assert.Single(typeof(NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var inconsistent = (NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady)constructor.Invoke([
            awaiting, Start.AddHours(92), inconsistency == 0 ? Array.Empty<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>() : new[] { observation }, 101m]);
        Assert.Throws<ArgumentException>(() => resolver.Resolve(awaiting, inconsistent));
    }

    [Fact]
    public void PureTypedApiAcceptsOnlyUniqueAndDoesNotExposeCompletionNextStructureOrMarketBodyOwner()
    {
        var method = Assert.Single(typeof(NasdaqPostCompletionHumanStructuralPriceCandidateResolver).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { typeof(NasdaqPostCompletionBreakoutAwaitingCompletionState), typeof(NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady) }, method.GetParameters().Select(p => p.ParameterType));
        var resultType = typeof(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate);
        Assert.Equal(resultType, method.ReturnType);
        Assert.Empty(resultType.GetConstructors());
        Assert.All(resultType.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "AwaitingState", "CandidateGeometry", "CandidateSide", "Episode", "MarketCursor", "Selection", "ValidatedCandidate" }, resultType.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(resultType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
        var primitive = new NasdaqHumanStructuralPriceCandidateGeometryCalculator();
        Assert.Throws<ArgumentOutOfRangeException>(() => primitive.Evaluate((StructuralCandidateExtremeSide)99, 80m, 100m));
    }

    private static NasdaqPostCompletionBreakoutAwaitingCompletionState Awaiting(bool bearish, int hour) =>
        new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(Candidate(bearish), Candle(hour, 140, 180, 80, 132, bearish)));
    private static NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady Unique(
        NasdaqPostCompletionBreakoutAwaitingCompletionState state, params NasdaqHumanPostCompletionCollisionStructuralPriceObservation[] observations) =>
        Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(
            new NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector().Select(state, At(92, observations)));
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
