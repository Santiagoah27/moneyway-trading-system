using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionStructuralCompletionSourceTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void DestinationContractRetainsExactResolved008ObjectAndAllHumanLineage(bool bearish, decimal open)
    {
        var candidate = Candidate(bearish);
        var incoming = Candle(80, open, 180, 80, 132, bearish);
        var lifecycle = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, incoming);
        var awaiting = new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(lifecycle);
        var price = bearish ? 95.1234m : 104.8766m;
        var first = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, price, Start.AddHours(88), "review:first");
        var second = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, price, Start.AddHours(92), "review:second");
        var selection = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(
            new NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector().Select(awaiting, At(92, second, first)));
        var resolved = new NasdaqPostCompletionHumanStructuralPriceCandidateResolver().Resolve(awaiting, selection);
        // Contract-only construction; there is intentionally no production 008 completion materializer.
        var provenance = ConstructSource<NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved>(resolved);
        Assert.Same(resolved, provenance.ResolvedCandidate);
        Assert.Same(awaiting, provenance.ResolvedCandidate.AwaitingState);
        Assert.Same(selection, provenance.ResolvedCandidate.Selection);
        Assert.Same(selection.SupportingObservations, provenance.ResolvedCandidate.Selection.SupportingObservations);
        Assert.Same(first, provenance.ResolvedCandidate.Selection.SupportingObservations[0]);
        Assert.Same(second, provenance.ResolvedCandidate.Selection.SupportingObservations[1]);
        Assert.Equal(new[] { "review:first", "review:second" }, provenance.ResolvedCandidate.Selection.SupportingObservations.Select(o => o.SourceReference));
        Assert.Equal(new[] { Start.AddHours(88), Start.AddHours(92) }, provenance.ResolvedCandidate.Selection.SupportingObservations.Select(o => o.ObservedAtUtc));
        Assert.Same(awaiting.EvidenceContext, provenance.ResolvedCandidate.Selection.Context);
        Assert.Same(resolved.ValidatedCandidate, provenance.ValidatedTurn);
        Assert.Equal(price, provenance.ValidatedTurn.StructuralPrice);
        Assert.Same(resolved.CandidateGeometry, provenance.ValidatedTurn.CandidateGeometry);
        var completion = ConstructCompletion(provenance);
        Assert.Same(provenance, completion.Source);
        Assert.Same(lifecycle, completion.LifecycleResult);
        Assert.Same(resolved.ValidatedCandidate, completion.ValidatedTurn);
        Assert.Same(candidate.Episode, completion.Episode);
        Assert.Same(candidate, completion.SourceCandidate);
        Assert.Same(candidate.ActivePair.ProtectedTurn, completion.PreviousProtectedTurn);
        Assert.Same(candidate.ActivePair.ActiveExtremeGeometry, completion.FrozenBreakoutTerminal);
        Assert.Same(incoming, completion.ConfirmingCandle);
        Assert.Same(resolved.MarketCursor, completion.MarketCursor);
        Assert.Equal(awaiting.BodyDirection, completion.BodyDirection);
        Assert.Same(candidate.SourceCorrection.GeometryReady, completion.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>)selection.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingDirectAnd007SourcesKeepCommonFactsAndDistinctTypedCanonicalPaths(bool bearish)
    {
        var candidate = Candidate(bearish);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var directResult = calculator.Evaluate(candidate, Candle(80, 130, 180, 90, 132, bearish));
        var collisionResult = calculator.Evaluate(candidate, Candle(80, 130, 180, 80, 132, bearish));
        var direct = new NasdaqPostCompletionDirectCandidateCompletionMaterializer().Materialize(directResult);
        var directional = new NasdaqPostCompletionDirectionalCollisionCompletionMaterializer().Materialize(collisionResult);
        var directSource = Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.Direct>(direct.Source);
        var directionalSource = Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.DirectionalCollision>(directional.Source);
        Assert.Same(directResult, directSource.LifecycleResult);
        Assert.Same(directResult.Decision, directSource.Decision);
        Assert.Same(Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(collisionResult.Decision).CandidateResolution, directionalSource.Resolution);
        foreach (var completion in new[] { direct, directional })
        {
            Assert.Same(completion.Source.ValidatedTurn, completion.ValidatedTurn);
            Assert.Same(candidate.ActivePair.ProtectedTurn, completion.PreviousProtectedTurn);
            Assert.Same(candidate.ActivePair.ActiveExtremeGeometry, completion.FrozenBreakoutTerminal);
            Assert.Same(completion.LifecycleResult.MarketCursor, completion.MarketCursor);
            Assert.Same(completion.MarketCursor, completion.ConfirmingCandle);
            Assert.Same(candidate.Episode, completion.Episode);
            Assert.Equal(completion.LifecycleResult.Decision.BodyDirection, completion.BodyDirection);
        }
        Assert.NotEqual(direct.Source.GetType(), directional.Source.GetType());
        Assert.Same(directSource.ValidatedTurn, ConstructSource<NasdaqPostCompletionStructuralCompletionSource.Direct>(directResult).ValidatedTurn);
        Assert.NotSame(direct, ConstructCompletion(direct.Source));
        Assert.Null(typeof(NasdaqPostCompletionStructuralCompletion).GetMethod(nameof(Equals), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void ClosedImmutableBranchesRejectWrongSourceKindsAndDetachedStructuralFacts()
    {
        var candidate = Candidate(false);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var direct = calculator.Evaluate(candidate, Candle(80, 130, 180, 90, 132, false));
        var directional = calculator.Evaluate(candidate, Candle(80, 130, 180, 80, 132, false));
        var unresolved = calculator.Evaluate(candidate, Candle(80, 140, 180, 80, 132, false));
        foreach (var wrong in new[] { directional, unresolved })
            Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => ConstructSource<NasdaqPostCompletionStructuralCompletionSource.Direct>(wrong)).InnerException);
        foreach (var wrong in new[] { direct, unresolved })
            Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => ConstructSource<NasdaqPostCompletionStructuralCompletionSource.DirectionalCollision>(wrong)).InnerException);
        var lifecycleConstructor = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionCandidateLifecycleResult)lifecycleConstructor.Invoke([Candidate(false), direct.Decision]);
        var detachedSource = ConstructSource<NasdaqPostCompletionStructuralCompletionSource.Direct>(detached);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => ConstructCompletion(detachedSource)).InnerException);
        var union = typeof(NasdaqPostCompletionStructuralCompletionSource);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "Direct", "DirectionalCollision", "HumanStructuralPriceResolved" }, union.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
            var ctor = Assert.Single(branch.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Equal(branch == typeof(NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved)
                ? typeof(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate) : typeof(NasdaqPostCompletionCandidateLifecycleResult), Assert.Single(ctor.GetParameters()).ParameterType);
        }
    }

    [Fact]
    public void HumanResolvedSourceRejectsContradictoryHumanLineage()
    {
        var candidate = Candidate(false);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var materializer = new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer();
        var awaiting = materializer.Materialize(calculator.Evaluate(candidate, Candle(80, 140, 180, 80, 132, false)));
        var other = materializer.Materialize(calculator.Evaluate(candidate, Candle(84, 140, 180, 80, 132, false)));
        var selector = new NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector();
        var selection = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(awaiting,
            At(92, new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, 100m, Start.AddHours(92), "review"))));
        var otherSelection = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(selector.Select(other,
            At(92, new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(other, 100m, Start.AddHours(92), "review:other"))));
        var resolved = new NasdaqPostCompletionHumanStructuralPriceCandidateResolver().Resolve(awaiting, selection);
        var constructor = Assert.Single(typeof(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var inconsistent = (NasdaqPostCompletionResolvedHumanStructuralPriceCandidate)constructor.Invoke([awaiting, otherSelection, resolved.ValidatedCandidate]);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => ConstructSource<NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved>(inconsistent)).InnerException);
    }

    private static T ConstructSource<T>(object source) where T : NasdaqPostCompletionStructuralCompletionSource =>
        (T)Assert.Single(typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).Invoke([source]);
    private static NasdaqPostCompletionStructuralCompletion ConstructCompletion(NasdaqPostCompletionStructuralCompletionSource source) =>
        (NasdaqPostCompletionStructuralCompletion)Assert.Single(typeof(NasdaqPostCompletionStructuralCompletion).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).Invoke([source]);
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
