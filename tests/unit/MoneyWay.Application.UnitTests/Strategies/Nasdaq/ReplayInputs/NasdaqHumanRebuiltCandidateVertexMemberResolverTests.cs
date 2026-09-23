using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanRebuiltCandidateVertexMemberResolverTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqHumanRebuiltCandidateVertexMemberResolver resolver = new();
    private readonly NasdaqHumanRebuiltCandidateVertexObservationSelector selector = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void ResolvesExactlyHumanSelectedMembersAndVerifiesKnownProtectionAnchor(StructuralCandidateExtremeSide side)
    {
        var (pending, migration) = Pending(side);
        var adjacent = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 120, 60, 110)
            : Candle(24, 100, 130, 80, 90);
        var unselected = side == StructuralCandidateExtremeSide.Lower
            ? Candle(28, 100, 120, 1, 110)
            : Candle(28, 100, 999, 80, 90);
        var observation = Observation(pending, [adjacent.OpenTimeUtc, migration.OpenTimeUtc], adjacent.CloseTimeUtc);
        var context = Context([migration, adjacent, unselected], adjacent.CloseTimeUtc, observation);
        var selection = Select(context, pending);

        var result = resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), selection, context);

        Assert.Same(migration, result.MigrationCandle);
        Assert.Equal([migration, adjacent], result.SelectedMembers);
        Assert.DoesNotContain(unselected, result.SelectedMembers);
        Assert.Equal(pending.KnownProtectionAnchor, result.KnownProtectionAnchor);
        Assert.Equal(pending.InvalidatingCandle.OpenTimeUtc, result.Episode.InvalidatingCandleOpenTimeUtc);
        Assert.Equal(migration.OpenTimeUtc, result.Episode.MigrationCandleOpenTimeUtc);
        Assert.Equal(pending.CandidateSide, result.Episode.CandidateSide);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Candle>)result.SelectedMembers).Add(unselected));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower)]
    [InlineData(StructuralCandidateExtremeSide.Upper)]
    public void AnchorMismatchOrMissingSelectedCandleFailsWithoutSubstitution(StructuralCandidateExtremeSide side)
    {
        var (pending, migration) = Pending(side);
        var mismatching = side == StructuralCandidateExtremeSide.Lower
            ? Candle(24, 100, 120, 40, 110)
            : Candle(24, 100, 150, 80, 90);
        var mismatchObservation = Observation(pending, [migration.OpenTimeUtc, mismatching.OpenTimeUtc], mismatching.CloseTimeUtc);
        var mismatchContext = Context([migration, mismatching], mismatching.CloseTimeUtc, mismatchObservation);

        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), Select(mismatchContext, pending), mismatchContext));

        var missingTime = Start.AddHours(24);
        var missingObservation = Observation(pending, [migration.OpenTimeUtc, missingTime], Start.AddHours(28));
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var trigger = new Candle(Provider, Symbol, minute, Start.AddHours(28), Start.AddHours(29), 100, 101, 99, 100, null);
        var missingContext = Context([migration], trigger.CloseTimeUtc, missingObservation, new CandleSeries(Provider, Symbol, minute, [trigger]));

        Assert.Throws<InvalidOperationException>(() => resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), Select(missingContext, pending), missingContext));
    }

    [Fact]
    public void MissingConflictAndDifferentRebuildEpisodeCannotResolve()
    {
        var (pending, migration) = Pending(StructuralCandidateExtremeSide.Lower);
        var adjacent = Candle(24, 100, 120, 60, 110);
        var first = Observation(pending, [migration.OpenTimeUtc], adjacent.CloseTimeUtc);
        var conflicting = Observation(pending, [migration.OpenTimeUtc, adjacent.OpenTimeUtc], adjacent.CloseTimeUtc, "review:conflict");
        var context = Context([migration, adjacent], adjacent.CloseTimeUtc, [first, conflicting]);
        var missing = selector.Select(context, Episode(pending, migration.OpenTimeUtc.AddHours(4)));
        var conflict = Select(context, pending);
        var otherObservation = new NasdaqHumanRebuiltCandidateVertexObservation(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc.AddHours(4), migration.OpenTimeUtc,
            pending.CandidateSide, [migration.OpenTimeUtc], adjacent.CloseTimeUtc, "review:other-episode");
        var otherContext = Context([migration, adjacent], adjacent.CloseTimeUtc, otherObservation);
        var otherEpisode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc.AddHours(4), migration.OpenTimeUtc, pending.CandidateSide);
        var otherEpisodeSelection = selector.Select(otherContext, otherEpisode);

        Assert.Throws<ArgumentException>(() => resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), missing, context));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), conflict, context));
        Assert.Throws<ArgumentException>(() => resolver.Evaluate(NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending), otherEpisodeSelection, context));
    }

    [Fact]
    public void FutureDataDoesNotChangeEarlierResolutionAndRepeatedResolutionIsDeterministic()
    {
        var (pending, migration) = Pending(StructuralCandidateExtremeSide.Lower);
        var observation = Observation(pending, [migration.OpenTimeUtc], migration.CloseTimeUtc);
        var future = Candle(24, 100, 120, 1, 110);
        var earlier = Context([migration], migration.CloseTimeUtc, observation);
        var extended = Context([migration, future], migration.CloseTimeUtc, observation);

        var resolutionContext = NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending);
        var first = resolver.Evaluate(resolutionContext, Select(earlier, pending), earlier);
        var second = resolver.Evaluate(resolutionContext, Select(extended, pending), extended);
        var repeated = resolver.Evaluate(resolutionContext, Select(earlier, pending), earlier);

        Assert.Equal([migration], first.SelectedMembers);
        Assert.Equal(first.SelectedMembers, second.SelectedMembers);
        Assert.Equal(first.SelectedMembers, repeated.SelectedMembers);
        Assert.DoesNotContain(future, second.SelectedMembers);
    }

    [Fact]
    public void FrozenOrdinaryRebuildBreakoutUsesTheSameResolutionContextWithoutReconstructingPendingState()
    {
        var (pending, migration) = Pending(StructuralCandidateExtremeSide.Lower);
        var validating = Candle(24, 100, 140, 60, 131);
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, validating).Breakout!;
        var fromPending = NasdaqHumanRebuiltCandidateVertexResolutionContext.From(pending);
        var fromBreakout = NasdaqHumanRebuiltCandidateVertexResolutionContext.From(breakout);
        var rebuildOrigin = Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Rebuild>(breakout.Origin);
        var observation = Observation(pending, [migration.OpenTimeUtc], validating.CloseTimeUtc);
        var context = Context([migration, validating], validating.CloseTimeUtc, observation);
        var selection = selector.Select(context, fromBreakout.Episode);

        var result = resolver.Evaluate(fromBreakout, selection, context);

        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.CollisionKind);
        Assert.False(breakout.HasStrictMigration);
        Assert.Equal(fromPending.Episode, fromBreakout.Episode);
        Assert.Equal(fromPending.CandidateSide, fromBreakout.CandidateSide);
        Assert.Same(pending.MigrationCandle, fromPending.MigrationCandle);
        Assert.Same(rebuildOrigin.PriorMigrationCandle, fromBreakout.MigrationCandle);
        Assert.Equal(fromPending.KnownProtectionAnchor, fromBreakout.KnownProtectionAnchor);
        Assert.Equal(breakout.PreviousProtectionAnchor, fromBreakout.KnownProtectionAnchor);
        Assert.NotSame(validating, fromBreakout.MigrationCandle);
        Assert.Same(migration, result.MigrationCandle);
        Assert.Equal([migration], result.SelectedMembers);
        Assert.Same(validating, breakout.ValidatingCandle);
    }

    [Fact]
    public void CandidateOriginBreakoutCannotCreateRebuiltMembershipResolutionContext()
    {
        var correction = InitialCorrection(StructuralCandidateExtremeSide.Lower);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, Candle(16, 80, 100, 60, 90)).Candidate!;
        var candidateOriginBreakout = new NasdaqPostInvalidationCandidateCollisionBreakoutTransitionCalculator()
            .Evaluate(candidate, Candle(20, 100, 140, 50, 131));

        Assert.IsType<NasdaqPostInvalidationBreakoutOrigin.Candidate>(candidateOriginBreakout.Origin);
        Assert.Throws<ArgumentException>(() => NasdaqHumanRebuiltCandidateVertexResolutionContext.From(candidateOriginBreakout));
    }

    private NasdaqHumanRebuiltCandidateVertexObservationSelection Select(
        StrategyReplayContext context, NasdaqPostInvalidationCandidateRebuildPendingState pending) =>
        selector.Select(context, Episode(pending, pending.MigrationCandle.OpenTimeUtc));

    private static NasdaqHumanRebuiltCandidateVertexEpisode Episode(
        NasdaqPostInvalidationCandidateRebuildPendingState pending, DateTimeOffset migrationTime) => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc, migrationTime, pending.CandidateSide);

    private static NasdaqHumanRebuiltCandidateVertexObservation Observation(
        NasdaqPostInvalidationCandidateRebuildPendingState pending,
        IEnumerable<DateTimeOffset> members,
        DateTimeOffset observedAtUtc,
        string source = "review:rebuilt") => new(
            MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, MoneyWayNasdaqStrategyDefinition.Instance.Version,
            Provider, Symbol, pending.InvalidatingCandle.OpenTimeUtc, pending.MigrationCandle.OpenTimeUtc,
            pending.CandidateSide, members, observedAtUtc, source);

    private static StrategyReplayContext Context(IReadOnlyList<Candle> h4Candles, DateTimeOffset asOfUtc,
        NasdaqHumanRebuiltCandidateVertexObservation observation, CandleSeries? additionalSeries = null) =>
        Context(h4Candles, asOfUtc, [observation], additionalSeries);

    private static StrategyReplayContext Context(IReadOnlyList<Candle> h4Candles, DateTimeOffset asOfUtc,
        IReadOnlyList<NasdaqHumanRebuiltCandidateVertexObservation> observations, CandleSeries? additionalSeries = null)
    {
        var series = new List<CandleSeries> { new(Provider, Symbol, H4, h4Candles) };
        if (additionalSeries is not null) series.Add(additionalSeries);
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == asOfUtc)
                return new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, observations);
        }
        throw new InvalidOperationException("Test replay frame was not found.");
    }

    private static (NasdaqPostInvalidationCandidateRebuildPendingState Pending, Candle Migration) Pending(
        StructuralCandidateExtremeSide side)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, 90)
            : Candle(20, 100, 140, 80, 100);
        return (new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, migration), migration);
    }

    private static NasdaqPostInvalidationCorrectionState InitialCorrection(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish ? Candle(0, 100, 110, 95, 110) : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish ? Candle(4, 95, 100, 90, 96) : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish ? Candle(8, 100, 110, 80, 90) : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var originObservation = new NasdaqHumanOriginVertexObservation(definition.StrategyId, definition.Version,
            Provider, Symbol, invalidating.OpenTimeUtc, [originCandle.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor([new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [originObservation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(originObservation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context, H4, bullish ? 130 : 70, bullish ? 95 : 105, bullish ? 140 : 75, [oldCandidate], oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish ? Candle(12, 70, 105, 65, 95) : Candle(12, 130, 145, 95, 115);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
    }

    private static Candle Candle(int openHour, decimal open = 100, decimal high = 101, decimal low = 99, decimal close = 100) =>
        new(Provider, Symbol, H4, Start.AddHours(openHour), Start.AddHours(openHour + 4), open, high, low, close, null);
}
