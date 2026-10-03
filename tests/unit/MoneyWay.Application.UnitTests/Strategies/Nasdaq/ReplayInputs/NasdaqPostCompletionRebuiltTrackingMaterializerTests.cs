using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionRebuiltTrackingMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionRebuiltTrackingMaterializer materializer = new();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PreservesFirstTurnEffectiveMigrationAndFullLineageWithoutConsumingAnotherCandle(bool bearish, bool dualRole)
    {
        var candidate = Candidate(bearish);
        var migration = Candle(80, 125, 180, 80, 123, bearish);
        var pending = new NasdaqPostCompletionRebuildPendingMaterializer().Materialize(
            new NasdaqPostCompletionCandidateLifecycleCalculator().Evaluate(candidate, migration));
        var firstTurn = Candle(89, 120, 180, dualRole ? 79 : 80, 123, bearish);
        var started = Assert.IsType<NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted>(
            new NasdaqPostCompletionRebuildPendingTransitionCalculator().Evaluate(pending, firstTurn));
        var correctionMembers = candidate.CorrectionTurnCandles.ToArray();
        var legacyCandidate = EquivalentCandidate(bearish, candidate.CorrectionStartCandle, candidate.TerminalCandle);
        var legacyPending = Assert.IsType<NasdaqPostInvalidationCandidateTransitionResult.RebuildPending>(
            new NasdaqPostInvalidationCandidateTransitionCalculator().Evaluate(legacyCandidate, migration)).State;
        var legacy = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(legacyPending, firstTurn).Tracking!;

        var state = materializer.Materialize(started);
        var repeated = materializer.Materialize(started);

        Assert.Same(started, state.SourceTransition);
        Assert.Same(pending, state.SourcePending);
        Assert.Same(candidate, state.SourceCandidate);
        Assert.Same(candidate.SourceCorrection, state.SourceCandidate.SourceCorrection);
        Assert.Same(candidate.Provisional, state.SourceCandidate.Provisional);
        Assert.Same(candidate.SourceCorrection.GeometryReady, state.SourceCandidate.SourceCorrection.GeometryReady);
        Assert.Same(candidate.Episode, state.Episode);
        Assert.Same(candidate.ActivePair, state.ActivePair);
        Assert.Same(candidate.ActivePair.ProtectedTurn, state.ActivePair.ProtectedTurn);
        Assert.Same(pending.FrozenBreakoutTerminal, state.FrozenBreakoutTerminal);
        Assert.Same(state.ActivePair.ActiveExtremeGeometry, state.FrozenBreakoutTerminal);
        Assert.Equal(pending.CandidateSide, state.CandidateSide);
        Assert.Same(firstTurn, state.FirstTurnCandle);
        Assert.Same(firstTurn, state.MarketCursor);
        Assert.Same(started.MarketCursor, state.MarketCursor);
        Assert.Same(dualRole ? firstTurn : migration, state.MigrationCandle);
        Assert.Same(dualRole ? started.Migration : pending.Migration, state.Migration);
        Assert.Equal(started.EffectiveProtectionAnchor, state.KnownProtectionAnchor);
        Assert.Equal(state.Migration.ResultingExtreme, state.KnownProtectionAnchor);
        Assert.Equal(bearish ? dualRole ? 121m : 120m : dualRole ? 79m : 80m, state.KnownProtectionAnchor);
        Assert.NotEqual(candidate.CandidateGeometry.ProtectionAnchor, state.KnownProtectionAnchor);
        Assert.Same(migration, state.SourcePending.MigrationCandle); // Prior migration remains recoverable in dual role.
        Assert.Same(pending.Migration, state.SourcePending.Migration);
        Assert.Same(started.Migration, state.SourceTransition.Migration);
        Assert.Equal(dualRole, started.Migration.WasReplaced);
        Assert.False(started.Breakout.IsConfirmed);

        Assert.Equal(legacy.CandidateSide, state.CandidateSide);
        Assert.Same(legacy.FirstTurnCandle, state.FirstTurnCandle);
        Assert.Same(legacy.MigrationCandle, state.MigrationCandle);
        Assert.Same(legacy.LastProcessedCandle, state.MarketCursor);
        Assert.Equal(legacy.KnownProtectionAnchor, state.KnownProtectionAnchor);
        Assert.Equal(legacy.FrozenImpulseTerminal, state.FrozenBreakoutTerminal);

        Assert.NotSame(state, repeated); // Existing state reference-equality conventions.
        Assert.Same(started, repeated.SourceTransition);
        Assert.Same(state.Episode, repeated.Episode);
        Assert.Same(state.ActivePair, repeated.ActivePair);
        Assert.Same(state.FirstTurnCandle, repeated.FirstTurnCandle);
        Assert.Same(state.Migration, repeated.Migration);
        Assert.Equal(state.KnownProtectionAnchor, repeated.KnownProtectionAnchor);
        Assert.Same(state.MarketCursor, repeated.MarketCursor);
        Assert.Same(migration, pending.MarketCursor);
        Assert.Same(candidate.TerminalCandle, candidate.MarketCursor);
        Assert.Equal(correctionMembers, candidate.CorrectionTurnCandles);
        Assert.DoesNotContain(firstTurn, candidate.CorrectionTurnCandles);
    }

    [Fact]
    public void ApiAcceptsOnlyTrackingStartedAndExposesNoDefinitiveGeometryOrExtraCandleInput()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize(null!));
        var type = typeof(NasdaqPostCompletionRebuiltTrackingMaterializer);
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionRebuildPendingTransitionResult.TrackingStarted),
            Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionRebuiltTrackingState), method.ReturnType);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        var state = typeof(NasdaqPostCompletionRebuiltTrackingState);
        Assert.Empty(state.GetConstructors());
        Assert.All(state.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "ActivePair", "CandidateSide", "Episode", "FirstTurnCandle", "FrozenBreakoutTerminal", "KnownProtectionAnchor",
            "MarketCursor", "Migration", "MigrationCandle", "SourceCandidate", "SourcePending", "SourceTransition" },
            state.GetProperties().Select(p => p.Name).Order());
        Assert.DoesNotContain(state.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => !method.IsSpecialName);
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
