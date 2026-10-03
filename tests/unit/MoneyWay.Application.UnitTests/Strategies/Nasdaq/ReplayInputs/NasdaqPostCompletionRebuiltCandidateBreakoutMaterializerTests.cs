using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuiltCandidateBreakoutMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuiltCandidateBreakoutMaterializer materializer = new();

    public static TheoryData<bool, bool, bool, int> Cases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, int>();
            foreach (var bearish in new[] { false, true })
                foreach (var tracking in new[] { false, true })
                    foreach (var dualRole in tracking ? new[] { false, true } : new[] { false })
                        foreach (var collision in new[] { 0, 1, 2, 3 })
                            cases.Add(bearish, tracking, dualRole, collision);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void PreservesEitherExactBreakoutSourceAndAllCommonFactsWithoutAnotherMarketStep(
        bool bearish, bool tracking, bool dualRole, int collision)
    {
        var pending = Pending(Candidate(bearish));
        var firstTurn = Candle(89, 120, 180, dualRole ? 79 : 80, 123, bearish);
        var currentLow = dualRole ? 79 : 80;
        var candle = Candle(97, collision == 2 ? 140 : collision == 3 ? 132 : 130, 180,
            collision == 0 ? currentLow : currentLow - 1, 132, bearish);
        var members = pending.SourceCandidate.CorrectionTurnCandles.ToArray();
        NasdaqPostCompletionRebuiltCandidateBreakout common;
        NasdaqPostCompletionRebuiltCandidateBreakout repeated;
        if (tracking)
        {
            var started = Assert.IsType<NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted>(
                new NasdaqPostCompletionRebuildPendingTransitionCalculator().Evaluate(pending, firstTurn));
            var state = new NasdaqPostCompletionRebuiltTrackingMaterializer().Materialize(started);
            var source = Assert.IsType<NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected>(
                new NasdaqPostCompletionRebuiltTrackingTransitionCalculator().Evaluate(state, candle));
            common = materializer.Materialize(source);
            repeated = materializer.Materialize(source);
            var origin = Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.RebuiltTracking>(common.Source);
            Assert.Same(source, origin.BreakoutResult);
            Assert.Same(state, origin.BreakoutResult.SourceState);
            Assert.Same(started, origin.BreakoutResult.SourceState.SourceTransition);
            Assert.Same(pending, origin.BreakoutResult.SourceState.SourcePending);
            Assert.Same(firstTurn, origin.FirstTurnCandle);
            Assert.Same(state.MigrationCandle, common.PriorMigrationCandle);
            Assert.Same(state.Migration, origin.BreakoutResult.SourceState.Migration);
            Assert.Same(source.Migration, common.Migration);
            Assert.Same(source.Breakout, common.Breakout);
            Assert.Equal(source.BodyDirection, common.BodyDirection);
            Assert.Equal(source.PreviousProtectionAnchor, common.PreviousProtectionAnchor);
            Assert.Equal(source.EffectiveProtectionAnchor, common.EffectiveProtectionAnchor);
            Assert.Same(source.EffectiveMigrationCandle, common.EffectiveMigrationCandle);
            Assert.Equal(source.CollisionKind, common.CollisionKind);
            Assert.Same(source.MarketCursor, common.MarketCursor);
            Assert.Same(firstTurn, state.MarketCursor);
            Assert.Same(source, Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.RebuiltTracking>(repeated.Source).BreakoutResult);
        }
        else
        {
            var source = Assert.IsType<NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected>(
                new NasdaqPostCompletionRebuildPendingTransitionCalculator().Evaluate(pending, candle));
            common = materializer.Materialize(source);
            repeated = materializer.Materialize(source);
            var origin = Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.Pending>(common.Source);
            Assert.Same(source, origin.BreakoutResult);
            Assert.Same(pending, origin.BreakoutResult.SourceState);
            Assert.Null(origin.GetType().GetProperty("FirstTurnCandle"));
            Assert.Same(pending.MigrationCandle, common.PriorMigrationCandle);
            Assert.Same(pending.Migration, origin.BreakoutResult.SourceState.Migration);
            Assert.Same(source.Migration, common.Migration);
            Assert.Same(source.Breakout, common.Breakout);
            Assert.Equal(source.BodyDirection, common.BodyDirection);
            Assert.Equal(source.PreviousProtectionAnchor, common.PreviousProtectionAnchor);
            Assert.Equal(source.EffectiveProtectionAnchor, common.EffectiveProtectionAnchor);
            Assert.Same(source.EffectiveMigrationCandle, common.EffectiveMigrationCandle);
            Assert.Equal(source.CollisionKind, common.CollisionKind);
            Assert.Same(source.MarketCursor, common.MarketCursor);
            Assert.Same(source, Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.Pending>(repeated.Source).BreakoutResult);
        }
        Assert.Same(pending.SourceCandidate.Episode, common.Episode);
        Assert.Same(pending.ActivePair, common.ActivePair);
        Assert.Same(pending.ActivePair.ProtectedTurn, common.ActivePair.ProtectedTurn);
        Assert.Same(pending.FrozenBreakoutTerminal, common.FrozenBreakoutTerminal);
        Assert.Equal(pending.CandidateSide, common.CandidateSide);
        Assert.Same(candle, common.BreakoutCandle);
        Assert.Same(candle, common.MarketCursor);
        Assert.True(common.Breakout.IsConfirmed);
        Assert.Equal(common.FrozenBreakoutTerminal.StructuralPrice, common.Breakout.ReferenceLevel);
        Assert.Equal(collision != 0, common.Migration.WasReplaced);
        Assert.Same(collision == 0 ? common.PriorMigrationCandle : candle, common.EffectiveMigrationCandle);
        Assert.Equal(collision == 0 ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None
            : collision == 1 ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
            : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired, common.CollisionKind);
        Assert.NotSame(common, repeated);
        Assert.Same(common.Migration, repeated.Migration);
        Assert.Same(common.Breakout, repeated.Breakout);
        Assert.Same(common.ActivePair, repeated.ActivePair);
        Assert.Same(common.Episode, repeated.Episode);
        Assert.Same(common.MarketCursor, repeated.MarketCursor);
        Assert.Equal(common.EffectiveProtectionAnchor, repeated.EffectiveProtectionAnchor);
        Assert.Same(pending.MigrationCandle, pending.MarketCursor);
        Assert.Equal(members, pending.SourceCandidate.CorrectionTurnCandles);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdenticalCommonFactsDoNotEraseDifferentCausalSources(bool bearish)
    {
        var pending = Pending(Candidate(bearish));
        var candle = Candle(97, 130, 180, 80, 132, bearish);
        var direct = materializer.Materialize(Assert.IsType<NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected>(
            new NasdaqPostCompletionRebuildPendingTransitionCalculator().Evaluate(pending, candle)));
        var started = Assert.IsType<NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted>(
            new NasdaqPostCompletionRebuildPendingTransitionCalculator().Evaluate(pending, Candle(89, 120, 180, 80, 123, bearish)));
        var state = new NasdaqPostCompletionRebuiltTrackingMaterializer().Materialize(started);
        var tracked = materializer.Materialize(Assert.IsType<NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected>(
            new NasdaqPostCompletionRebuiltTrackingTransitionCalculator().Evaluate(state, candle)));
        Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.Pending>(direct.Source);
        Assert.IsType<NasdaqPostCompletionRebuildBreakoutSource.RebuiltTracking>(tracked.Source);
        Assert.Same(direct.Episode, tracked.Episode);
        Assert.Same(direct.ActivePair, tracked.ActivePair);
        Assert.Equal(direct.CandidateSide, tracked.CandidateSide);
        Assert.Same(direct.FrozenBreakoutTerminal, tracked.FrozenBreakoutTerminal);
        Assert.Same(direct.BreakoutCandle, tracked.BreakoutCandle);
        Assert.Same(direct.MarketCursor, tracked.MarketCursor);
        Assert.Same(direct.PriorMigrationCandle, tracked.PriorMigrationCandle);
        Assert.Equal(direct.EffectiveProtectionAnchor, tracked.EffectiveProtectionAnchor);
    }

    [Fact]
    public void ApiIsClosedImmutableTypedAndHasNoOtherInputsOrDownstreamProducts()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize((NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected)null!));
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize((NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected)null!));
        var type = typeof(NasdaqPostCompletionRebuiltCandidateBreakoutMaterializer);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(2, methods.Length);
        Assert.Equal(new[] { typeof(NasdaqPostCompletionRebuildPendingTransitionResult.BreakoutDetected),
            typeof(NasdaqPostCompletionRebuiltTrackingTransitionResult.BreakoutDetected) },
            methods.Select(m => Assert.Single(m.GetParameters()).ParameterType).OrderBy(t => t.FullName));
        Assert.All(methods, m => Assert.Equal(typeof(NasdaqPostCompletionRebuiltCandidateBreakout), m.ReturnType));
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        var source = typeof(NasdaqPostCompletionRebuildBreakoutSource);
        Assert.Empty(source.GetConstructors());
        Assert.Equal(new[] { "Pending", "RebuiltTracking" }, source.GetNestedTypes().Select(t => t.Name).Order());
        var common = typeof(NasdaqPostCompletionRebuiltCandidateBreakout);
        Assert.Null(common.GetProperty("FirstTurnCandle"));
        foreach (var value in source.GetNestedTypes().Append(source).Append(common))
        {
            Assert.Empty(value.GetConstructors());
            Assert.All(value.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(value.GetProperties(), p => p.Name is "CandidateGeometry" or "SelectedMemberOpenTimesUtc"
                or "MembersResolved" or "UniqueEvidenceReady" or "Completion" or "SuccessorEpisode");
        }
    }

    private static NasdaqPostCompletionRebuildPendingState Pending(NasdaqPostCompletionCandidateState candidate) =>
        new NasdaqPostCompletionRebuildPendingMaterializer().Materialize(new NasdaqPostCompletionCandidateLifecycleCalculator()
            .Evaluate(candidate, Candle(80, 125, 180, 80, 123, candidate.CandidateSide == StructuralCandidateExtremeSide.Upper)));

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
