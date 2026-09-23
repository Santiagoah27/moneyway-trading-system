using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqH4BreakoutAwaitingCompletionReducerTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqH4BreakoutAwaitingCompletionReducer reducer = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Routes007FromBothOriginsToDirectionalCompletion(bool rebuildOrigin)
    {
        var input = Snapshot007(rebuildOrigin);

        var result = reducer.Reduce(input, Context(28));

        var completed = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.Directional007>(result);
        Assert.Same(input.State, completed.Result.Result.Breakout);
        Assert.Same(input.State.ValidatingCandle, completed.Snapshot.MarketCursor);
        Assert.Equal(rebuildOrigin, completed.Result.Result.Breakout.Origin is NasdaqPostInvalidationBreakoutOrigin.Rebuild);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Routes008MissingConflictAndCompletedWithoutReplacingFocusedProvenance(bool rebuildOrigin)
    {
        var input = Snapshot008(rebuildOrigin);
        var missing = reducer.Reduce(input, Context(28));
        var first = PriceObservation(input.State, 111m, 28, "review:first");
        var second = PriceObservation(input.State, 112m, 28, "review:second");
        var conflict = reducer.Reduce(input, Context(28, first, second));
        var unique = reducer.Reduce(input, Context(28, first));

        var missing008 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.HumanStructuralPrice008>(missing);
        var conflict008 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict.HumanStructuralPrice008>(conflict);
        var completed008 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.HumanStructuralPrice008>(unique);
        Assert.Same(input, missing008.Snapshot);
        Assert.Same(input, conflict008.Snapshot);
        Assert.Equal([first, second], conflict008.Reduction.Selection.SupportingObservations);
        Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.HumanStructuralPrice>(completed008.Snapshot);
        Assert.Same(first, Assert.Single(completed008.Reduction.Selection.SupportingObservations));
        Assert.Same(input.State.ValidatingCandle, completed008.Snapshot.MarketCursor);
    }

    [Fact]
    public void Preserves008EvidenceCausalityAcrossReplayFrames()
    {
        var input = Snapshot008();
        var observation = PriceObservation(input.State, 111m, 32, "review:later");

        var early = reducer.Reduce(input, Context(28, observation));
        var later = reducer.Reduce(input, Context(32, observation));

        Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.HumanStructuralPrice008>(early);
        Assert.Same(input, early.Snapshot);
        Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.HumanStructuralPrice008>(later);
        Assert.Same(input.State.ValidatingCandle, later.Snapshot.MarketCursor);
    }

    [Fact]
    public void Routes009MissingConflictAndCompletedWithoutDuplicatingMembershipResolution()
    {
        var fixture = Snapshot009();
        var missing = reducer.Reduce(fixture.Snapshot, ContextWithH4(fixture.Candles, 32));
        var first = MemberObservation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc], 32, "review:first");
        var second = MemberObservation(fixture.Snapshot.State, [fixture.Migration.OpenTimeUtc, fixture.Turn.OpenTimeUtc], 32, "review:second");
        var conflict = reducer.Reduce(fixture.Snapshot, ContextWithH4(fixture.Candles, 32, first, second));
        var unique = reducer.Reduce(fixture.Snapshot, ContextWithH4(fixture.Candles, 32, first));

        var missing009 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Missing.OrdinaryRebuilt009>(missing);
        var conflict009 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Conflict.OrdinaryRebuilt009>(conflict);
        var completed009 = Assert.IsType<NasdaqH4BreakoutAwaitingCompletionReductionResult.Completed.OrdinaryRebuilt009>(unique);
        Assert.Same(fixture.Snapshot, missing009.Snapshot);
        Assert.Same(fixture.Snapshot, conflict009.Snapshot);
        Assert.Equal([first, second], conflict009.Reduction.Selection.SupportingObservations);
        Assert.IsType<NasdaqH4ReconstructionSnapshot.Completed.Ordinary>(completed009.Snapshot);
        Assert.Equal([fixture.Migration], completed009.Reduction.CompletionResult!.MemberResolution.SelectedMembers);
        Assert.Same(fixture.Snapshot.State.ValidatingCandle, completed009.Snapshot.MarketCursor);
    }

    [Fact]
    public void RejectsSnapshotsOutsideBreakoutAwaitingCompletion()
    {
        var candidate = Candidate();
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, Candle(20, 100, 120, 50, 90));
        var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 80, 120, 60, 100)).Tracking!;
        var completed = reducer.Reduce(Snapshot007(false), Context(28)).Snapshot;

        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.Candidate(candidate), Context(28)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuildPending(pending), Context(28)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(new NasdaqH4ReconstructionSnapshot.RebuiltTracking(tracking), Context(28)));
        Assert.Throws<ArgumentException>(() => reducer.Reduce(completed, Context(28)));
    }

    private static NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion Snapshot007(bool rebuildOrigin)
    {
        var candidate = Candidate();
        if (!rebuildOrigin)
            return new(new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
                .Evaluate(candidate, Candle(20, 100, 140, 50, 131)));
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, Candle(20, 100, 140, 50, 90));
        return new(new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 100, 140, 40, 131)).Breakout!);
    }

    private static NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion Snapshot008(bool rebuildOrigin = false)
    {
        var candidate = Candidate();
        if (!rebuildOrigin)
            return new(new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
                .Evaluate(candidate, Candle(20, 140, 150, 50, 131)));
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 50, 90));
        return new(new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, 140, 145, 49, 131)).Breakout!);
    }

    private static Fixture009 Snapshot009()
    {
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(Candidate(), Candle(20, 100, 120, 50, 90));
        var turn = Candle(24, 80, 120, 60, 100);
        var tracking = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, turn).Tracking!;
        var breakout = new NasdaqPostInvalidationRebuiltCandidateTrackingTransitionCalculator()
            .Evaluate(tracking, Candle(28, 100, 140, 55, 131)).Breakout!;
        return new(new(breakout), pending.MigrationCandle, turn, [pending.MigrationCandle, turn, breakout.ValidatingCandle]);
    }

    private static NasdaqHumanCollisionStructuralPriceObservation PriceObservation(NasdaqPostInvalidationCandidateRebuildBreakoutState breakout, decimal price, int hour, string source) =>
        new(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide, price, Start.AddHours(hour), source);

    private static NasdaqHumanRebuiltCandidateVertexObservation MemberObservation(NasdaqPostInvalidationCandidateRebuildBreakoutState breakout, IEnumerable<DateTimeOffset> members, int hour, string source)
    {
        var rebuild = (NasdaqPostInvalidationBreakoutOrigin.Rebuild)breakout.Origin;
        return new(breakout.Episode.StrategyId, breakout.Episode.StrategyVersion, breakout.Episode.ProviderId, breakout.Episode.Symbol,
            breakout.Episode.InvalidatingCandleOpenTimeUtc, rebuild.PriorMigrationCandle.OpenTimeUtc, breakout.CandidateSide, members, Start.AddHours(hour), source);
    }

    private static StrategyReplayContext Context(int hour, params IStrategyReplayInputObservation[] observations) =>
        ContextWithH4([], hour, observations);

    private static StrategyReplayContext ContextWithH4(IReadOnlyList<Candle> h4Candles, int hour, params IStrategyReplayInputObservation[] observations)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var close = Start.AddHours(hour);
        var trigger = new Candle(Provider, Symbol, minute, close.AddMinutes(-1), close, 100, 101, 99, 100, null);
        var cursor = new MultiTimeframeCandleReplayCursor([
            new CandleSeries(Provider, Symbol, H4, h4Candles),
            new CandleSeries(Provider, Symbol, minute, [trigger])]);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == close)
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        }
        throw new InvalidOperationException("Test replay frame was not found.");
    }

    private static NasdaqPostInvalidationCandidateState Candidate()
    {
        var origin = Candle(0, 100, 110, 85, 90); var prior = Candle(4, 105, 115, 100, 110); var invalidating = Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version, Provider, Symbol, invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [origin, prior, invalidating])]); MultiTimeframeReplayFrame? frame = null; while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, H4, 70, 105, 75, [prior], StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, Candle(12, 130, 145, 95, 115)));
        return new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
    }

    private static Candle Candle(int hour, decimal open, decimal high, decimal low, decimal close) =>
        new(Provider, Symbol, H4, Start.AddHours(hour), Start.AddHours(hour + 4), open, high, low, close, null);

    private sealed record Fixture009(NasdaqH4ReconstructionSnapshot.BreakoutAwaitingCompletion Snapshot, Candle Migration, Candle Turn, IReadOnlyList<Candle> Candles);
}
