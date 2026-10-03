using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionCandidateLifecycleCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionCandidateLifecycleCalculator calculator = new();

    public static TheoryData<bool, decimal, decimal, decimal, NasdaqPostInvalidationCandidateTransitionKind, int> Cases
    {
        get
        {
            var cases = new TheoryData<bool, decimal, decimal, decimal, NasdaqPostInvalidationCandidateTransitionKind, int>();
            foreach (var bearish in new[] { false, true })
            {
                cases.Add(bearish, 120, 90, 123, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, 0);
                cases.Add(bearish, 125, 80, 123, NasdaqPostInvalidationCandidateTransitionKind.RebuildPending, 0);
                cases.Add(bearish, 120, 80, 123, NasdaqPostInvalidationCandidateTransitionKind.RebuiltTracking, 0);
                cases.Add(bearish, 130, 90, 132, NasdaqPostInvalidationCandidateTransitionKind.DirectCompleted, 0);
                cases.Add(bearish, 130, 80, 132, NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout, 7);
                cases.Add(bearish, 140, 80, 132, NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout, 8);
                cases.Add(bearish, 132, 80, 132, NasdaqPostInvalidationCandidateTransitionKind.CollisionBreakout, 8);
                cases.Add(bearish, 120, 90, 131, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, 0);
                cases.Add(bearish, 150, 90, 123, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, 0);
                cases.Add(bearish, 123, 90, 123, NasdaqPostInvalidationCandidateTransitionKind.CandidateContinues, 0);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesCanonicalCandidateDispatcherAcrossAllDecisionsAndPreservesPostCompletionSource(
        bool bearish, decimal open, decimal low, decimal close, NasdaqPostInvalidationCandidateTransitionKind kind, int collision)
    {
        var source = Candidate(bearish);
        var legacy = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        Assert.Equal(source.CandidateGeometry, legacy.CandidateGeometry);
        Assert.Equal(source.ActivePair.ActiveExtremeGeometry, legacy.FrozenImpulseTerminal);
        Assert.Equal(source.CorrectionTurnCandles, legacy.CorrectionTurnCandles);
        var incoming = Candle(80, open, 180, low, close, bearish);
        var result = calculator.Evaluate(source, incoming);
        var decision = result.Decision;
        var expected = new NasdaqPostInvalidationCandidateTransitionCalculator().Evaluate(legacy, incoming);
        Assert.Equal(kind, expected.Kind);
        Assert.Equal(expected.Kind, decision.Kind);
        Assert.Same(source.CandidateGeometry, decision.CandidateGeometry);
        Assert.Same(source.ActivePair.ActiveExtremeGeometry, decision.FrozenTerminal);
        Assert.Equal(bearish ? 69m : 131m, decision.Breakout.ReferenceLevel);
        Assert.NotEqual(source.ActivePair.ProtectedTurn.StructuralPrice, decision.Breakout.ReferenceLevel);
        Assert.NotEqual(source.CandidateGeometry.StructuralPrice, decision.Breakout.ReferenceLevel);
        Assert.Equal(new CandleBodyDirectionCalculator().Evaluate(incoming), decision.BodyDirection);
        Assert.Equal(new StructuralCandidateExtremeCalculator().Evaluate(source.CandidateGeometry.ProtectionAnchor,
            bearish ? incoming.High : incoming.Low, source.CandidateSide), decision.Migration);
        switch (expected)
        {
            case NasdaqPostInvalidationCandidateTransitionResult.CandidateContinues continued:
                Assert.IsType<NasdaqCandidateLifecycleDecision.CandidateContinues>(decision);
                Assert.Equal(continued.State.CandidateGeometry, decision.CandidateGeometry);
                Assert.Same(incoming, continued.State.LastProcessedCandle);
                Assert.False(decision.Migration.WasReplaced);
                Assert.False(decision.Breakout.IsConfirmed);
                break;
            case NasdaqPostInvalidationCandidateTransitionResult.DirectCompleted completed:
                var direct = Assert.IsType<NasdaqCandidateLifecycleDecision.DirectCompleted>(decision);
                Assert.Equal(completed.Result.ValidatedCandidate, direct.Validation);
                Assert.Same(decision.CandidateGeometry, direct.Validation.CandidateGeometry);
                Assert.Same(decision.Breakout, direct.Validation.BreakObservation);
                break;
            case NasdaqPostInvalidationCandidateTransitionResult.RebuildPending pending:
                Assert.Equal(pending.State.KnownProtectionAnchor, Assert.IsType<NasdaqCandidateLifecycleDecision.RebuildPending>(decision).EffectiveProtectionAnchor);
                break;
            case NasdaqPostInvalidationCandidateTransitionResult.RebuiltTracking tracking:
                var rebuilt = Assert.IsType<NasdaqCandidateLifecycleDecision.RebuiltTracking>(decision);
                Assert.Equal(tracking.State.KnownProtectionAnchor, rebuilt.EffectiveProtectionAnchor);
                Assert.Same(incoming, rebuilt.FirstTurnCandle);
                break;
            case NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout breakout:
                var collided = Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(decision);
                Assert.Equal(breakout.State.CollisionKind, collided.CollisionKind);
                Assert.Equal(collision == 7 ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                    : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, collided.CollisionKind);
                Assert.Equal(breakout.State.EffectiveProtectionAnchor, collided.EffectiveProtectionAnchor);
                Assert.True(decision.Migration.WasReplaced);
                Assert.True(decision.Breakout.IsConfirmed);
                break;
        }
        Assert.Same(source, result.SourceState);
        Assert.Same(incoming, result.MarketCursor);
        Assert.Same(incoming, decision.Breakout.Candle);
        Assert.Same(source.MarketCursor, decision.PreviousCursor);
        Assert.Same(source.ActivePair, result.ActivePair);
        Assert.Same(source.Episode, result.Episode);
        Assert.Same(source.Provisional, result.SourceState.Provisional);
        Assert.Same(source.SourceCorrection.GeometryReady, result.SourceState.SourceCorrection.GeometryReady);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        Assert.Single(source.CorrectionTurnCandles);
        var repeated = calculator.Evaluate(source, incoming);
        Assert.Equal(decision.GetType(), repeated.Decision.GetType());
        Assert.Equal(decision.Migration, repeated.Decision.Migration);
        Assert.Equal(decision.Breakout, repeated.Decision.Breakout);
        Assert.Equal(decision.BodyDirection, repeated.Decision.BodyDirection);
        Assert.Equal(bearish ? 200 - open : open, incoming.Open);
        Assert.Equal(bearish ? 200 - close : close, incoming.Close);
    }

    [Theory]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(34)]
    public void RejectsEarlierSameAndOverlappingCandidateCandles(int hour) =>
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(false), Candle(hour, 120, 180, 90, 123, false)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RejectsOtherMarketSeries(int mismatch)
    {
        var incoming = new Candle(mismatch == 0 ? new("other") : Provider, mismatch == 1 ? new("other") : Symbol,
            mismatch == 2 ? new(1, TimeframeUnit.Hour) : H4, Start.AddHours(48), Start.AddHours(52), 120, 180, 90, 123, null);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(Candidate(false), incoming));
    }

    [Fact]
    public void PureTypedApiDoesNotExposePostCompletionNextStatesOrRequireEvidence()
    {
        var state = Candidate(false);
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null!, state.MarketCursor));
        Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(state, null!));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(state, state.MarketCursor));
        var method = Assert.Single(typeof(NasdaqPostCompletionCandidateLifecycleCalculator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(new[] { typeof(NasdaqPostCompletionCandidateState), typeof(Candle) }, method.GetParameters().Select(p => p.ParameterType));
        var type = typeof(NasdaqPostCompletionCandidateLifecycleResult);
        Assert.Empty(type.GetConstructors());
        Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.Equal(new[] { "ActivePair", "Decision", "Episode", "MarketCursor", "SourceState" }, type.GetProperties().Select(p => p.Name).Order());
        var union = typeof(NasdaqCandidateLifecycleDecision);
        Assert.True(Assert.Single(union.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)).IsPrivate);
        Assert.Equal(new[] { "CandidateContinues", "CollisionBreakout", "DirectCompleted", "RebuildPending", "RebuiltTracking" }, union.GetNestedTypes().Select(t => t.Name).Order());
        foreach (var branch in union.GetNestedTypes())
        {
            Assert.True(branch.IsSealed);
            Assert.Empty(branch.GetConstructors());
            Assert.All(branch.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(branch.GetProperties(), p => p.Name == "State" || p.Name == "SourceState" || p.Name == "ResultingState");
        }
    }

    [Fact]
    public void ExtractedCoreRejectsMissingAndInconsistentCandidateFacts()
    {
        var source = Candidate(false);
        var core = new NasdaqCandidateLifecycleDecisionCalculator();
        var geometry = source.CandidateGeometry;
        var terminal = source.ActivePair.ActiveExtremeGeometry;
        var candle = Candle(48, 120, 180, 90, 123, false);
        var side = source.CandidateSide;
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(side, null!, terminal, source.MarketCursor, candle));
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(side, geometry, null!, source.MarketCursor, candle));
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(side, geometry, terminal, null!, candle));
        Assert.Throws<ArgumentNullException>(() => core.Evaluate(side, geometry, terminal, source.MarketCursor, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => core.Evaluate((StructuralCandidateExtremeSide)999, geometry, terminal, source.MarketCursor, candle));
        Assert.Throws<ArgumentException>(() => core.Evaluate(StructuralCandidateExtremeSide.Upper, geometry, terminal, source.MarketCursor, candle));
        Assert.Throws<ArgumentException>(() => core.Evaluate(side, geometry, geometry, source.MarketCursor, candle));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectionalCollisionCarriesDefinitiveMigratedVertexAndOldCompletionReusesIt(bool bearish)
    {
        var source = Candidate(bearish);
        var incoming = Candle(80, 130, 180, 80, 132, bearish);
        var result = calculator.Evaluate(source, incoming);
        var collision = Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(result.Decision);
        var definitive = Assert.IsType<NasdaqCollisionCandidateResolution.Directional>(collision.CandidateResolution);
        var validation = definitive.Validation;
        Assert.True(validation.IsValidated);
        Assert.Equal(source.CandidateSide, validation.CandidateSide);
        Assert.Equal(bearish ? 70m : 130m, validation.StructuralPrice);
        Assert.Equal(bearish ? 120m : 80m, validation.ProtectionAnchor);
        Assert.NotEqual(source.CandidateGeometry, validation.CandidateGeometry);
        Assert.Same(source.CandidateGeometry, collision.CandidateGeometry);
        Assert.Same(collision.Breakout, validation.BreakObservation);
        Assert.Same(incoming, definitive.Member);
        Assert.Same(incoming, result.MarketCursor);
        Assert.Same(source.ActivePair.ActiveExtremeGeometry, collision.FrozenTerminal);
        Assert.Same(source.MarketCursor, collision.PreviousCursor);
        Assert.Equal(bearish ? CandleBodyDirection.Bearish : CandleBodyDirection.Bullish, collision.BodyDirection);
        Assert.Same(source.SourceCorrection.GeometryReady, result.SourceState.SourceCorrection.GeometryReady);

        var legacy = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        var transition = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.CollisionBreakout>(
            new NasdaqPostInvalidationCandidateTransitionCalculator().Evaluate(legacy, incoming));
        var oldDecision = transition.State.CandidateDecision!;
        var oldDefinitive = Assert.IsType<NasdaqCollisionCandidateResolution.Directional>(oldDecision.CandidateResolution);
        var completionCalculator = new NasdaqDirectionalMigrationBreakoutCompletionCalculator();
        var completion = completionCalculator.Evaluate(transition.State);
        Assert.Same(oldDefinitive.Validation, completion.ValidatedCandidate);
        Assert.Same(oldDefinitive.Validation.CandidateGeometry, completion.CandidateGeometry);
        Assert.Equal(validation, completion.ValidatedCandidate);
        Assert.Same(oldDecision.Breakout, completion.ValidatedCandidate.BreakObservation);
        Assert.Same(legacy, Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(transition.State.Origin).State);
        Assert.Same(incoming, completion.LastProcessedCandle);
        Assert.Same(completion.ValidatedCandidate, completionCalculator.Evaluate(transition.State).ValidatedCandidate);
        Assert.Same(source.TerminalCandle, source.MarketCursor);
        Assert.Single(source.CorrectionTurnCandles);
        var repeated = Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(calculator.Evaluate(source, incoming).Decision);
        Assert.Equal(validation, Assert.IsType<NasdaqCollisionCandidateResolution.Directional>(repeated.CandidateResolution).Validation);
        var primitive = new NasdaqDirectionalMigrationBreakoutCandidateCalculator();
        Assert.Throws<ArgumentNullException>(() => primitive.Evaluate(null!, collision.Breakout, collision.BodyDirection));
        Assert.Throws<ArgumentNullException>(() => primitive.Evaluate(collision.Migration, null!, collision.BodyDirection));
        Assert.Throws<ArgumentException>(() => primitive.Evaluate(collision.Migration, collision.Breakout, CandleBodyDirection.Neutral));
        var noMigration = new StructuralCandidateExtremeCalculator().Evaluate(validation.ProtectionAnchor,
            validation.ProtectionAnchor, source.CandidateSide);
        Assert.Throws<ArgumentException>(() => primitive.Evaluate(noMigration, collision.Breakout, collision.BodyDirection));
    }

    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void OppositeAndExactDojiCollisionsCarryOnlyUnresolvedPriceOwnership(bool bearish, decimal open)
    {
        var source = Candidate(bearish);
        var incoming = Candle(80, open, 180, 80, 132, bearish);
        var collision = Assert.IsType<NasdaqCandidateLifecycleDecision.CollisionBreakout>(calculator.Evaluate(source, incoming).Decision);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, collision.CollisionKind);
        Assert.IsType<NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired>(collision.CandidateResolution);
        Assert.Empty(collision.CandidateResolution.GetType().GetProperties());
        var legacy = EquivalentCandidate(bearish, source.CorrectionStartCandle, source.TerminalCandle);
        var breakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator().Evaluate(legacy, incoming);
        Assert.IsType<NasdaqCollisionCandidateResolution.HumanStructuralPriceRequired>(breakout.CandidateDecision!.CandidateResolution);
        Assert.Throws<ArgumentException>(() => new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(breakout));
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
