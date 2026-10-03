using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionHumanStructuralPriceCompletionMaterializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionHumanStructuralPriceCompletionMaterializer materializer = new();

    [Theory]
    [InlineData(false, 140)]
    [InlineData(true, 140)]
    [InlineData(false, 132)]
    [InlineData(true, 132)]
    public void MaterializesResolved008WithoutLosingHumanLineageOrChangingConsumedMarketFacts(bool bearish, decimal open)
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
        var pair = candidate.ActivePair;
        var observations = selection.SupportingObservations.ToArray();
        var correctionMembers = candidate.CorrectionTurnCandles.ToArray();

        var completion = materializer.Materialize(resolved);

        var source = Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved>(completion.Source);
        Assert.Same(resolved, source.ResolvedCandidate);
        Assert.Same(awaiting, source.ResolvedCandidate.AwaitingState);
        Assert.Same(selection, source.ResolvedCandidate.Selection);
        Assert.Same(selection.SupportingObservations, source.ResolvedCandidate.Selection.SupportingObservations);
        Assert.Same(first, source.ResolvedCandidate.Selection.SupportingObservations[0]);
        Assert.Same(second, source.ResolvedCandidate.Selection.SupportingObservations[1]);
        Assert.Equal(new[] { "review:first", "review:second" }, source.ResolvedCandidate.Selection.SupportingObservations.Select(o => o.SourceReference));
        Assert.Equal(new[] { Start.AddHours(88), Start.AddHours(92) }, source.ResolvedCandidate.Selection.SupportingObservations.Select(o => o.ObservedAtUtc));
        Assert.Same(awaiting.EvidenceContext, source.ResolvedCandidate.Selection.Context);
        Assert.Same(awaiting.Migration, source.ResolvedCandidate.AwaitingState.Migration);
        Assert.Same(lifecycle, completion.LifecycleResult);
        Assert.Same(resolved.ValidatedCandidate, completion.ValidatedTurn);
        Assert.Same(resolved.CandidateGeometry, completion.ValidatedTurn.CandidateGeometry);
        Assert.True(completion.ValidatedTurn.IsValidated);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, completion.ValidatedTurn.CandidateSide);
        Assert.Equal(price, completion.ValidatedTurn.StructuralPrice);
        Assert.NotEqual(candidate.CandidateGeometry.StructuralPrice, price);
        Assert.NotEqual(incoming.Open, price);
        Assert.NotEqual(incoming.Close, price);
        Assert.Equal(awaiting.EffectiveProtectionAnchor, completion.ValidatedTurn.ProtectionAnchor);
        Assert.Equal(bearish ? 120m : 80m, completion.ValidatedTurn.ProtectionAnchor);
        Assert.NotEqual(candidate.CandidateGeometry.ProtectionAnchor, completion.ValidatedTurn.ProtectionAnchor);
        Assert.Same(pair.ProtectedTurn, completion.PreviousProtectedTurn);
        Assert.NotSame(completion.PreviousProtectedTurn, completion.ValidatedTurn);
        Assert.NotEqual(completion.PreviousProtectedTurn.StructuralPrice, completion.ValidatedTurn.StructuralPrice);
        Assert.Same(pair.ActiveExtremeGeometry, completion.FrozenBreakoutTerminal);
        Assert.Same(awaiting.FrozenBreakoutTerminal, completion.FrozenBreakoutTerminal);
        Assert.Same(incoming, completion.ConfirmingCandle);
        Assert.Same(incoming, completion.MarketCursor);
        Assert.Same(resolved.MarketCursor, completion.MarketCursor);
        Assert.Equal(open == 132 ? CandleBodyDirection.Neutral : bearish ? CandleBodyDirection.Bullish : CandleBodyDirection.Bearish, completion.BodyDirection);
        Assert.Equal(awaiting.BodyDirection, completion.BodyDirection);
        Assert.Same(candidate.Episode, completion.Episode);
        Assert.Same(resolved.Episode, completion.Episode);
        Assert.Same(candidate, completion.SourceCandidate);
        Assert.Same(pair, candidate.ActivePair);
        Assert.Same(pair, resolved.AwaitingState.ActivePair);
        Assert.Same(candidate.TerminalCandle, candidate.MarketCursor);
        Assert.Equal(correctionMembers, candidate.CorrectionTurnCandles);
        Assert.Equal(observations, selection.SupportingObservations);
        Assert.DoesNotContain(incoming, candidate.CorrectionTurnCandles);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NasdaqHumanPostCompletionCollisionStructuralPriceObservation>)selection.SupportingObservations).Clear());

        var repeated = materializer.Materialize(resolved);
        // Completion retains reference equality; repeated composition preserves the exact canonical facts.
        Assert.NotSame(completion, repeated);
        Assert.Same(resolved, Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved>(repeated.Source).ResolvedCandidate);
        Assert.Same(completion.ValidatedTurn, repeated.ValidatedTurn);
        Assert.Same(completion.PreviousProtectedTurn, repeated.PreviousProtectedTurn);
        Assert.Same(completion.FrozenBreakoutTerminal, repeated.FrozenBreakoutTerminal);
        Assert.Same(completion.MarketCursor, repeated.MarketCursor);
        Assert.Same(completion.Episode, repeated.Episode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct007AndResolved008ConvergeOnTheSameCommonCompletionContract(bool bearish)
    {
        var candidate = Candidate(bearish);
        var calculator = new NasdaqPostCompletionCandidateLifecycleCalculator();
        var direct = new NasdaqPostCompletionDirectCandidateCompletionMaterializer().Materialize(
            calculator.Evaluate(candidate, Candle(80, 130, 180, 90, 132, bearish)));
        var directional = new NasdaqPostCompletionDirectionalCollisionCompletionMaterializer().Materialize(
            calculator.Evaluate(candidate, Candle(80, 130, 180, 80, 132, bearish)));
        var awaiting = new NasdaqPostCompletionHumanStructuralPriceAwaitingCompletionMaterializer().Materialize(
            calculator.Evaluate(candidate, Candle(80, 140, 180, 80, 132, bearish)));
        var observation = new NasdaqHumanPostCompletionCollisionStructuralPriceObservation(awaiting, 100m, Start.AddHours(92), "review");
        var selection = Assert.IsType<NasdaqHumanPostCompletionCollisionStructuralPriceSelection.UniqueEvidenceReady>(
            new NasdaqHumanPostCompletionCollisionStructuralPriceObservationSelector().Select(awaiting, At(92, observation)));
        var human = materializer.Materialize(new NasdaqPostCompletionHumanStructuralPriceCandidateResolver().Resolve(awaiting, selection));
        Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.Direct>(direct.Source);
        Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.DirectionalCollision>(directional.Source);
        Assert.IsType<NasdaqPostCompletionStructuralCompletionSource.HumanStructuralPriceResolved>(human.Source);
        foreach (var completion in new[] { direct, directional, human })
        {
            Assert.IsType<NasdaqPostCompletionStructuralCompletion>(completion);
            Assert.True(completion.ValidatedTurn.IsValidated);
            Assert.Same(candidate.ActivePair.ProtectedTurn, completion.PreviousProtectedTurn);
            Assert.Same(candidate.ActivePair.ActiveExtremeGeometry, completion.FrozenBreakoutTerminal);
            Assert.Same(completion.ConfirmingCandle, completion.MarketCursor);
            Assert.Same(candidate.Episode, completion.Episode);
        }
    }

    [Fact]
    public void StatelessTypedApiAcceptsOnlyResolvedCandidatesAndExposesNoNextStructure()
    {
        Assert.Throws<ArgumentNullException>(() => materializer.Materialize(null!));
        var type = typeof(NasdaqPostCompletionHumanStructuralPriceCompletionMaterializer);
        var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("Materialize", method.Name);
        Assert.Equal(typeof(NasdaqPostCompletionResolvedHumanStructuralPriceCandidate), Assert.Single(method.GetParameters()).ParameterType);
        Assert.Equal(typeof(NasdaqPostCompletionStructuralCompletion), method.ReturnType);
        Assert.Empty(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        Assert.Empty(Assert.Single(type.GetConstructors()).GetParameters());
        Assert.Equal(new[] { "BodyDirection", "ConfirmingCandle", "Episode", "FrozenBreakoutTerminal", "LifecycleResult", "MarketCursor", "PreviousProtectedTurn", "Source", "SourceCandidate", "ValidatedTurn" },
            typeof(NasdaqPostCompletionStructuralCompletion).GetProperties().Select(p => p.Name).Order());
        Assert.All(typeof(NasdaqPostCompletionStructuralCompletion).GetProperties(), p => Assert.Null(p.SetMethod));
    }

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
