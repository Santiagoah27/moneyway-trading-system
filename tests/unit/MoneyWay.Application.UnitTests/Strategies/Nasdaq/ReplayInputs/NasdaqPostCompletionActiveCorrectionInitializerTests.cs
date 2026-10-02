using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionActiveCorrectionInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionActiveCorrectionInitializer initializer = new();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public void InitializesExactPairAndSeparateCorrectionMembershipWithoutConsumingMarket(
        bool bearish, bool overlap, bool ties)
    {
        var ready = Ready(bearish, overlap, ties);
        var pending = ready.PendingState;
        var extremeMembers = ready.ExtremeGeometry.MemberResolution.SelectedMembers;
        var state = initializer.Initialize(ready);
        Assert.Same(ready, state.GeometryReady);
        Assert.Same(pending.Episode, state.Episode);
        Assert.Same(pending.Completion, state.Completion);
        Assert.Same(pending.Episode.PreviousCompletedEpisode, state.Completion.Episode);
        Assert.Same(ready.ExtremeGeometry, state.ActivePair.ActiveExtreme);
        Assert.Same(ready.ExtremeGeometry.Geometry, state.ActivePair.ActiveExtremeGeometry);
        Assert.Same(pending.ValidatedProtectedTurn, state.ActivePair.ProtectedTurn);
        Assert.Same(pending.ProtectedTurnGeometry, state.ActivePair.ProtectedTurnGeometry);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper,
            state.ActivePair.ActiveExtremeSide);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower,
            state.ActivePair.ProtectedTurnGeometry.Side);
        Assert.Same(pending.CorrectionStartCandle, state.CorrectionStartCandle);
        Assert.Same(ready.MarketCursor, state.MarketCursor);
        Assert.Same(state.CorrectionStartCandle, Assert.Single(state.CorrectionTurnCandles));
        Assert.Same(state.CorrectionStartCandle, state.MarketCursor);
        Assert.Same(extremeMembers, state.ActivePair.ActiveExtreme.MemberResolution.SelectedMembers);
        Assert.Equal(overlap, extremeMembers.Contains(state.CorrectionStartCandle));
        Assert.True(extremeMembers[0].OpenTimeUtc < state.MarketCursor.OpenTimeUtc);
        Assert.NotSame(extremeMembers, state.CorrectionTurnCandles);
        var selection = state.ActivePair.ActiveExtreme.MemberResolution.Selection;
        Assert.Same(ready.MembersResolved.EvidenceReady.Selection, selection);
        Assert.Same(pending.MembershipEvent, state.ActivePair.ActiveExtreme.MemberResolution.MembershipEvent);
        Assert.Equal(new[] { "review:first", "review:second" }, selection.SupportingObservations.Select(o => o.SourceReference));
        Assert.Equal(new[] { Start.AddHours(40), Start.AddHours(48) }, selection.SupportingObservations.Select(o => o.ObservedAtUtc));
        if (ties)
        {
            Assert.Equal(extremeMembers[0].Close, extremeMembers[1].Close);
            Assert.Equal(extremeMembers[0].High, extremeMembers[1].High);
            Assert.Equal(extremeMembers[0].Low, extremeMembers[1].Low);
        }
        var repeated = initializer.Initialize(ready);
        Assert.Same(state.GeometryReady, repeated.GeometryReady);
        Assert.Same(state.Episode, repeated.Episode);
        Assert.Same(state.ActivePair.ActiveExtreme, repeated.ActivePair.ActiveExtreme);
        Assert.Same(state.ActivePair.ProtectedTurn, repeated.ActivePair.ProtectedTurn);
        Assert.Equal(state.CorrectionTurnCandles, repeated.CorrectionTurnCandles);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)state.CorrectionTurnCandles).Add(state.CorrectionStartCandle));
        Assert.Same(pending.MarketCursor, ready.MarketCursor);
    }

    [Fact]
    public void PublicContractsAreImmutableUniversalAndExposeNoLaterLifecycleInputOrOutcome()
    {
        Assert.Throws<ArgumentNullException>(() => initializer.Initialize(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionActiveCorrectionInitializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionExtremeGeometryReady), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionActiveCorrectionState), method.ReturnType);
        foreach (var type in new[] { typeof(NasdaqPostCompletionActiveCorrectionState), typeof(NasdaqPostCompletionStructuralPair) })
        {
            Assert.Empty(type.GetConstructors());
            Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        }
        Assert.Null(initializer.Initialize(Ready(false, false, false)).CorrectionProgression);
        Assert.Equal(new[] { "ActivePair", "Completion", "CorrectionProgression", "CorrectionStartCandle", "CorrectionTurnCandles", "Episode", "GeometryReady", "MarketCursor" },
            typeof(NasdaqPostCompletionActiveCorrectionState).GetProperties().Select(p => p.Name).Order());
        Assert.Equal(new[] { "ActiveExtreme", "ActiveExtremeGeometry", "ActiveExtremeSide", "ProtectedTurn", "ProtectedTurnGeometry" },
            typeof(NasdaqPostCompletionStructuralPair).GetProperties().Select(p => p.Name).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsContradictoryGeometrySidesAndUnorderedReferences(bool bearish)
    {
        var ready = Ready(bearish, false, false);
        var side = ready.PendingState.ActiveExtremeSide;
        var opposite = side == StructuralTurnBodyCoordinateSide.Upper
            ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper;
        var calculator = new StructuralTurnGeometryCalculator();
        var wrongSide = calculator.Evaluate(ready.ExtremeGeometry.MemberResolution.SelectedMembers, opposite);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(WithGeometry(ready, wrongSide)));
        var level = ready.PendingState.ValidatedProtectedTurn.StructuralPrice;
        var equalGeometry = calculator.Evaluate([Candle(24, level, level + 1, level - 1, level, false)], side);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(WithGeometry(ready, equalGeometry)));
        var reversedLevel = level + (bearish ? 1 : -1);
        var reversedGeometry = calculator.Evaluate([Candle(24, reversedLevel, reversedLevel + 1, reversedLevel - 1, reversedLevel, false)], side);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(WithGeometry(ready, reversedGeometry)));
        var wrongProtectedTurn = Ready(!bearish, false, false).PendingState.ValidatedProtectedTurn;
        var constructor = Assert.Single(typeof(NasdaqPostCompletionStructuralPair).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke([ready.ExtremeGeometry, wrongProtectedTurn]));
        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void RejectsGeometryDetachedFromItsExactResolvedProvenance()
    {
        var ready = Ready(false, false, false);
        var another = Ready(false, false, false);
        var constructor = Assert.Single(typeof(NasdaqPostCompletionExtremeGeometryReady).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var detached = (NasdaqPostCompletionExtremeGeometryReady)constructor.Invoke([ready.MembersResolved, another.ExtremeGeometry]);
        Assert.Throws<ArgumentException>(() => initializer.Initialize(detached));
    }

    // Exercise defensive invariants that the normal pure geometry preparer cannot violate.
    private static NasdaqPostCompletionExtremeGeometryReady WithGeometry(
        NasdaqPostCompletionExtremeGeometryReady ready, StructuralTurnGeometryResult geometry)
    {
        var extremeConstructor = Assert.Single(typeof(NasdaqHumanPostCompletionActiveExtremeGeometryResult).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        var extreme = (NasdaqHumanPostCompletionActiveExtremeGeometryResult)extremeConstructor.Invoke([ready.ExtremeGeometry.MemberResolution, geometry]);
        var readyConstructor = Assert.Single(typeof(NasdaqPostCompletionExtremeGeometryReady).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        return (NasdaqPostCompletionExtremeGeometryReady)readyConstructor.Invoke([ready.MembersResolved, extreme]);
    }

    private static NasdaqPostCompletionExtremeGeometryReady Ready(bool bearish, bool overlap, bool ties)
    {
        var source = State(bearish);
        var turn = Candle(28, 145, 180, 100, 135, bearish);
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
