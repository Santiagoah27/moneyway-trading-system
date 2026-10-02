using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqPostCompletionExtremeMembershipPendingInitializerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqPostCompletionExtremeMembershipPendingInitializer initializer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesExactEpisodeTurnEventProtectedGeometryAndCompletionWithoutReprocessing(bool bearish)
    {
        var source = State(bearish);
        var sourceCursor = source.MarketCursor;
        var turn = Candle(28, 145, 180, 100, 135, bearish);
        var transition = Assert.IsType<NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted>(
            new NasdaqPostCompletionActiveImpulseTransitionCalculator().Evaluate(source, turn));
        var pending = initializer.Initialize(transition);
        Assert.Same(transition.MembershipEvent, pending.MembershipEvent);
        Assert.Equal(transition.MembershipEvent, pending.MembershipEvent);
        Assert.Same(source.Episode, pending.Episode);
        Assert.Same(source.Completion, pending.Completion);
        Assert.Same(source.Completion.Episode, pending.Episode.PreviousCompletedEpisode);
        Assert.Equal(bearish ? StructuralTurnBodyCoordinateSide.Lower : StructuralTurnBodyCoordinateSide.Upper, pending.ActiveExtremeSide);
        Assert.Equal(bearish ? StructuralCandidateExtremeSide.Upper : StructuralCandidateExtremeSide.Lower, pending.ValidatedProtectedTurn.CandidateSide);
        Assert.Same(source.ValidatedProtectedTurn, pending.ValidatedProtectedTurn);
        Assert.Same(source.ProtectedTurnGeometry, pending.ProtectedTurnGeometry);
        Assert.Same(turn, pending.CorrectionStartCandle);
        Assert.Same(turn, pending.MarketCursor);
        Assert.Same(transition.MarketCursor, pending.MarketCursor);
        Assert.Equal(Start.AddHours(32), pending.MarketCursor.CloseTimeUtc);
        Assert.Same(sourceCursor, source.MarketCursor);
        Assert.Same(source, transition.SourceState);
        Assert.Same(turn, transition.CorrectionStartCandle);
        var second = initializer.Initialize(transition);
        Assert.Same(pending.MembershipEvent, second.MembershipEvent);
        Assert.Same(pending.Episode, second.Episode);
        Assert.Same(pending.ProtectedTurnGeometry, second.ProtectedTurnGeometry);
        Assert.Same(pending.MarketCursor, second.MarketCursor);
        Assert.Equal(bearish ? 55m : 145m, turn.Open);
        Assert.Equal(bearish ? 65m : 135m, turn.Close);
    }

    [Fact]
    public void RejectsNullAndRequiresOnlyTypedCorrectionStartedInput()
    {
        Assert.Throws<ArgumentNullException>(() => initializer.Initialize(null!));
        var method = Assert.Single(typeof(NasdaqPostCompletionExtremeMembershipPendingInitializer).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal(typeof(NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted), Assert.Single(method.GetParameters()).ParameterType);
    }

    [Fact]
    public void UniversalImmutableStateRequiresOnlyEventAndHasNoEvidenceOrFinalExtremePayload()
    {
        var type = typeof(NasdaqPostCompletionExtremeMembershipPendingState);
        Assert.Empty(type.GetConstructors());
        var constructor = Assert.Single(type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.Equal(typeof(NasdaqPostCompletionActiveExtremeMembershipEvent), Assert.Single(constructor.GetParameters()).ParameterType);
        Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.Equal(new[] { "ActiveExtremeSide", "Completion", "CorrectionStartCandle", "Episode", "MarketCursor", "MembershipEvent", "ProtectedTurnGeometry", "ValidatedProtectedTurn" },
            type.GetProperties().Select(property => property.Name).Order());
        Assert.DoesNotContain(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == typeof(NasdaqPostCompletionActiveImpulseState)
                || field.FieldType == typeof(NasdaqPostCompletionActiveImpulseTransitionResult.CorrectionStarted));
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
