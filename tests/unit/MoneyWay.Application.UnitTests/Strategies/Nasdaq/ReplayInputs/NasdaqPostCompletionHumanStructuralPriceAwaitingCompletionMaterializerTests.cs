using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer materializer = new();

    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void FreezesCanonicalOppositeAndDojiCollisionWithoutDefinitiveCandidatePrice(bool bearish, decimal open)
    {
        var source = Candidate(bearish);
        var incoming = Candle(80, open, 180, 80, 132, bearish);
        var lifecycle = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, incoming);
        var collision = Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(lifecycle.Decision);
        var resolution = Assert.IsType<NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired>(collision.CandidateResolution);
        var awaiting = materializer.Materialize(lifecycle);
        Assert.Same(lifecycle, awaiting.LifecycleResult);
        Assert.Same(collision, awaiting.Collision);
        Assert.Same(resolution, awaiting.Resolution);
        Assert.Same(source, awaiting.SourceCandidate);
        Assert.Same(source.CandidateGeometry, awaiting.Collision.CandidateGeometry);
        Assert.Same(source.ActivePair, awaiting.ActivePair);
        Assert.Same(source.ActivePair.ProtectedTurn, awaiting.PreviousProtectedTurn);
        Assert.Same(source.ActivePair.ActiveExtremeGeometry, awaiting.FrozenBreakoutTerminal);
        Assert.Equal(awaiting.FrozenBreakoutTerminal.StructuralPrice, awaiting.Breakout.ReferenceLevel);
        Assert.Same(collision.Migration, awaiting.Migration);
        Assert.True(awaiting.Migration.WasReplaced);
        Assert.True(awaiting.Breakout.IsConfirmed);
        Assert.Same(collision.Breakout, awaiting.Breakout);
        Assert.Equal(bearish ? 120m : 80m, awaiting.EffectiveProtectionAnchor);
        Assert.NotEqual(source.CandidateGeometry.ProtectionAnchor, awaiting.EffectiveProtectionAnchor);
        Assert.Equal(collision.EffectiveProtectionAnchor, awaiting.EffectiveProtectionAnchor);
        Assert.Same(source.Episode, awaiting.Episode);
        Assert.Same(incoming, awaiting.CollisionCandle);
        Assert.Same(incoming, awaiting.MarketCursor);
        Assert.Same(lifecycle.MarketCursor, awaiting.MarketCursor);
        Assert.Equal(collision.BodyDirection, awaiting.BodyDirection);
        Assert.Equal(open == 132 ? CandleBodyDirection.Neutral : bearish ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish, awaiting.BodyDirection);
        Assert.True(source.MarketCursor.CloseTimeUtc <= incoming.OpenTimeUtc);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        Assert.Same(source.Provisional, awaiting.SourceCandidate.Provisional);
        Assert.Same(source.CorrectionTurnCandles, awaiting.SourceCandidate.CorrectionTurnCandles);
        Assert.Same(source.SourceCorrection.GeometryReady, awaiting.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Same(source.SourceCorrection.Completion, awaiting.SourceCandidate.SourceCorrection.Completion);
        Assert.DoesNotContain(incoming, source.CorrectionTurnCandles);
        Assert.Empty(awaiting.Resolution.GetType().GetProperties());
        var repeated = materializer.Materialize(lifecycle);
        Assert.Same(lifecycle, repeated.LifecycleResult);
        Assert.Same(resolution, repeated.Resolution);
        Assert.Same(awaiting.ActivePair, repeated.ActivePair);
        Assert.Same(awaiting.MarketCursor, repeated.MarketCursor);
        Assert.Equal(awaiting.EvidenceContext, repeated.EvidenceContext);
        Assert.Equal(awaiting.EvidenceContext.GetHashCode(), repeated.EvidenceContext.GetHashCode());
        Assert.Same(source.Episode, awaiting.EvidenceContext.Episode);
        Assert.Equal(incoming.OpenTimeUtc, awaiting.EvidenceContext.CollisionCandleOpenTimeUtc);
        Assert.Equal(source.CandidateSide, awaiting.EvidenceContext.CandidateSide);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextIdentifiesOriginalCollisionAndRetainsCandidateContinuation(bool bearish)
    {
        var original = Candidate(bearish);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var continuation = calculator.Evaluate(original, Candle(48, 120, 180, 90, 123, bearish));
        var source = new NasdaqPostCompletionCandidateContinuationReducer().Reduce(continuation);
        var awaiting = materializer.Materialize(calculator.Evaluate(source, Candle(80, 140, 180, 80, 132, bearish)));
        Assert.Same(continuation, awaiting.SourceCandidate.Continuation);
        Assert.Same(original, awaiting.SourceCandidate.Continuation!.SourceState);
        Assert.Same(original.Episode, awaiting.Episode);
        Assert.Same(continuation.MarketCursor, source.MarketCursor);
        var same = materializer.Materialize(calculator.Evaluate(Candidate(bearish), Candle(80, 140, 180, 80, 132, bearish)));
        Assert.Equal(awaiting.EvidenceContext, same.EvidenceContext);
        var later = materializer.Materialize(calculator.Evaluate(source, Candle(84, 140, 180, 80, 132, bearish)));
        Assert.NotEqual(awaiting.EvidenceContext, later.EvidenceContext);
        var mirror = materializer.Materialize(calculator.Evaluate(Candidate(!bearish), Candle(80, 140, 180, 80, 132, !bearish)));
        Assert.NotEqual(awaiting.EvidenceContext, mirror.EvidenceContext);
        Assert.Same(awaiting.CollisionCandle, awaiting.MarketCursor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsCanonical007(bool bearish)
    {
        var lifecycle = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(Candidate(bearish), Candle(80, 130, 180, 80, 132, bearish));
        Assert.IsType<NasdaqCollisionCandidateResolution.Directional>(Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(lifecycle.Decision).CandidateResolution);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(lifecycle));
    }

    [Theory]
    [InlineData(120, 90, 123)]
    [InlineData(130, 90, 132)]
    [InlineData(125, 80, 123)]
    [InlineData(120, 80, 123)]
    public void RejectsContinuationDirectAndRebuildBranches(decimal open, decimal low, decimal close)
    {
        var lifecycle = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(Candidate(false), Candle(80, open, 180, low, close, false));
        Assert.IsNotType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(lifecycle.Decision);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(lifecycle));
    }

    [Fact]
    public void PureApiHasNoResolvedPriceGeometryCompletionEvidenceSelectionOrNextStates()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize(null!));
        var source = Candidate(false);
        var lifecycle = new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(source, Candle(80, 140, 180, 80, 132, false));
        var constructor = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionCandidateLifecycleResult)constructor.Invoke([Candidate(false), lifecycle.Decision]);
        Assert.Throws<ArgumentException>(() => materializer.Materialize(detached));
        var type = typeof(NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer);
        Assert.Empty(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionCandidateLifecycleResult), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionBreakoutAwaitingCompletionState), method.ReturnType);
        var stateType = method.ReturnType;
        Assert.Empty(stateType.GetConstructors());
        Assert.All(stateType.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "ActivePair", "BodyDirection", "Breakout", "Collision", "CollisionCandle", "EffectiveProtectionAnchor", "Episode", "EvidenceContext", "FrozenBreakoutTerminal", "LifecycleResult", "MarketCursor", "Migration", "PreviousProtectedTurn", "Resolution", "SourceCandidate" }, stateType.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), m => !m.IsSpecialName);
        var contextType = typeof(NasdaqPostCompletionCollisionStructuralPriceContext);
        Assert.Empty(contextType.GetConstructors());
        Assert.All(contextType.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "CandidateSide", "CollisionCandleOpenTimeUtc", "Episode" }, contextType.GetProperties().Select(p => p.Name).Order());
    }

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
