using System.Reflection;
using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqOrdinaryRebuiltBreakoutCompletionCalculatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly Timeframe H4 = NasdaqHumanOriginVertexObservation.H4;
    private static readonly DateTimeOffset Start = new(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly NasdaqOrdinaryRebuiltBreakoutCompletionCalculator calculator = new();

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 90, 50, StructuralBreakDirection.Upper, 130)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 100, 140, StructuralBreakDirection.Lower, 70)]
    public void MatchingOrdinaryBreakoutProducesExistingValidatedCandidateResult(
        StructuralCandidateExtremeSide side,
        decimal expectedStructuralPrice,
        decimal expectedProtectionAnchor,
        StructuralBreakDirection expectedDirection,
        decimal expectedReference)
    {
        var fixture = Fixture(side);

        var result = calculator.Evaluate(fixture.Breakout, fixture.Resolution, fixture.Geometry);
        var snapshot = new NasdaqH4ReconstructionSnapshot.Completed.Ordinary(result);

        Assert.Equal(NasdaqH4ReconstructionSnapshotKind.Completed, snapshot.Kind);
        Assert.Same(result, snapshot.Result);
        Assert.Same(result.Breakout.Episode, snapshot.Episode);
        Assert.Same(result.LastProcessedCandle, snapshot.MarketCursor);
        Assert.Same(result.ValidatedCandidate, snapshot.ValidatedCandidate);

        Assert.True(result.ValidatedCandidate.IsValidated);
        Assert.Equal(side, result.ValidatedCandidate.CandidateSide);
        Assert.Same(fixture.Geometry, result.ValidatedCandidate.CandidateGeometry);
        Assert.Equal(expectedStructuralPrice, result.ValidatedCandidate.StructuralPrice);
        Assert.Equal(expectedProtectionAnchor, result.ValidatedCandidate.ProtectionAnchor);
        Assert.Equal(expectedDirection, result.ValidatedCandidate.BreakObservation.Direction);
        Assert.Equal(expectedReference, result.ValidatedCandidate.BreakObservation.ReferenceLevel);
        Assert.Same(fixture.Breakout.ValidatingCandle, result.ValidatedCandidate.BreakObservation.Candle);
        Assert.Same(fixture.Breakout.ValidatingCandle, result.LastProcessedCandle);
        Assert.Same(fixture.Resolution, result.MemberResolution);
        Assert.DoesNotContain(fixture.Breakout.ValidatingCandle, result.MemberResolution.SelectedMembers);
        Assert.Null(result.GetType().GetProperty("RuleStatus"));
        Assert.Null(result.GetType().GetProperty("StrategyVerdict"));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 140, 145, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Lower, 131, 140, 50, 131)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 60, 140, 50, 69)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 69, 140, 50, 69)]
    public void OrdinaryBreakoutCompletionIsIndependentOfValidatingBodyDirection(
        StructuralCandidateExtremeSide side,
        decimal open,
        decimal high,
        decimal low,
        decimal close)
    {
        var pending = Pending(side);
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, Candle(24, open, high, low, close)).Breakout!;
        var resolution = Resolution(breakout, [pending.MigrationCandle]);
        var geometry = new NasdaqHumanRebuiltCandidateVertexGeometryCalculator().Evaluate(resolution);

        var result = calculator.Evaluate(breakout, resolution, geometry);

        Assert.False(breakout.HasStrictMigration);
        Assert.Equal(NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.None, breakout.CollisionKind);
        Assert.True(result.ValidatedCandidate.IsValidated);
    }

    [Theory]
    [InlineData("strategy")]
    [InlineData("version")]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("invalidation")]
    [InlineData("migration")]
    [InlineData("side")]
    public void ExactEpisodeMismatchIsRejected(string mismatch)
    {
        var fixture = Fixture(StructuralCandidateExtremeSide.Lower);
        var source = fixture.Resolution.Episode;
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            mismatch == "strategy" ? new StrategyId("other-strategy") : source.StrategyId,
            mismatch == "version" ? new StrategyVersion("other-version") : source.StrategyVersion,
            mismatch == "provider" ? new MarketDataProviderId("other-provider") : source.ProviderId,
            mismatch == "symbol" ? new MarketSymbol("OTHER") : source.Symbol,
            mismatch == "invalidation" ? source.InvalidatingCandleOpenTimeUtc.AddHours(4) : source.InvalidatingCandleOpenTimeUtc,
            mismatch == "migration" ? source.MigrationCandleOpenTimeUtc.AddHours(4) : source.MigrationCandleOpenTimeUtc,
            mismatch == "side" ? StructuralCandidateExtremeSide.Upper : source.CandidateSide);
        var resolution = CreateResolution(
            episode,
            fixture.Resolution.MigrationCandle,
            fixture.Resolution.SelectedMembers,
            fixture.Resolution.KnownProtectionAnchor);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            fixture.Breakout,
            resolution,
            fixture.Geometry));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, true)]
    [InlineData(StructuralCandidateExtremeSide.Lower, false)]
    [InlineData(StructuralCandidateExtremeSide.Upper, true)]
    [InlineData(StructuralCandidateExtremeSide.Upper, false)]
    public void SameCandleMigrationBreakoutCollisionsAreRejected(
        StructuralCandidateExtremeSide side,
        bool directionalBody)
    {
        var fixture = Fixture(side, directionalBody
            ? Collision.Directional
            : Collision.HumanStructuralPriceRequired);

        Assert.True(fixture.Breakout.HasStrictMigration);
        Assert.Equal(
            directionalBody
                ? NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4007DirectionalBody
                : NasdaqPostInvalidationCandidateRebuildBreakoutCollisionKind.NqQH4008HumanStructuralPriceRequired,
            fixture.Breakout.CollisionKind);
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            fixture.Breakout,
            fixture.Resolution,
            fixture.Geometry));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(28)]
    public void MemberAtOrAfterValidatingBreakoutIsRejected(int memberOpenHour)
    {
        var baseline = Fixture(StructuralCandidateExtremeSide.Lower);
        var invalidMember = Candle(memberOpenHour, 100, 120, 60, 110);
        var resolution = Resolution(
            baseline.Breakout,
            [baseline.Resolution.MigrationCandle, invalidMember]);
        var geometry = new NasdaqHumanRebuiltCandidateVertexGeometryCalculator().Evaluate(resolution);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            baseline.Breakout,
            resolution,
            geometry));
    }

    [Theory]
    [InlineData(StructuralCandidateExtremeSide.Lower, 70, 50)]
    [InlineData(StructuralCandidateExtremeSide.Upper, 130, 140)]
    public void DiscontinuousMembersRemainExactAndMaySupplyDifferentGeometryComponents(
        StructuralCandidateExtremeSide side,
        decimal expectedStructuralPrice,
        decimal expectedProtectionAnchor)
    {
        var baseline = Fixture(side);
        var earlier = side == StructuralCandidateExtremeSide.Lower
            ? Candle(12, 70, 135, 65, 75)
            : Candle(12, 130, 135, 95, 115);
        var resolution = Resolution(
            baseline.Breakout,
            [earlier, baseline.Resolution.MigrationCandle]);
        var geometry = new NasdaqHumanRebuiltCandidateVertexGeometryCalculator().Evaluate(resolution);

        var result = calculator.Evaluate(baseline.Breakout, resolution, geometry);

        Assert.Equal([earlier, baseline.Resolution.MigrationCandle], result.MemberResolution.SelectedMembers);
        Assert.Equal(expectedStructuralPrice, result.ValidatedCandidate.StructuralPrice);
        Assert.Equal(expectedProtectionAnchor, result.ValidatedCandidate.ProtectionAnchor);
        Assert.Equal(2, result.MemberResolution.SelectedMembers.Count);
    }

    [Fact]
    public void GeometryNotProducedByTheResolvedMembersIsRejectedExactly()
    {
        var fixture = Fixture(StructuralCandidateExtremeSide.Lower);
        var geometryCalculator = new StructuralTurnGeometryCalculator();
        var wrongBody = geometryCalculator.Evaluate(
            [Candle(12, 91, 120, 50, 95)],
            StructuralTurnBodyCoordinateSide.Lower);
        var wrongAnchor = geometryCalculator.Evaluate(
            [Candle(12, 90, 120, 49, 95)],
            StructuralTurnBodyCoordinateSide.Lower);

        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            fixture.Breakout,
            fixture.Resolution,
            wrongBody));
        Assert.Throws<ArgumentException>(() => calculator.Evaluate(
            fixture.Breakout,
            fixture.Resolution,
            wrongAnchor));
    }

    [Fact]
    public void RepeatedCompletionIsEquivalentAndDoesNotMutateInputs()
    {
        var fixture = Fixture(StructuralCandidateExtremeSide.Lower);
        var originalMembers = fixture.Resolution.SelectedMembers.ToArray();
        var originalCursor = fixture.Breakout.LastProcessedCandle;

        var first = calculator.Evaluate(fixture.Breakout, fixture.Resolution, fixture.Geometry);
        var second = calculator.Evaluate(fixture.Breakout, fixture.Resolution, fixture.Geometry);

        Assert.Equal(first.ValidatedCandidate, second.ValidatedCandidate);
        Assert.Equal(originalMembers, fixture.Resolution.SelectedMembers);
        Assert.Same(originalCursor, fixture.Breakout.LastProcessedCandle);
        Assert.Same(fixture.Breakout.ValidatingCandle, first.LastProcessedCandle);
        Assert.Same(fixture.Breakout.Episode, first.Breakout.Episode);
    }

    private static CompletionFixture Fixture(
        StructuralCandidateExtremeSide side,
        Collision collision = Collision.None)
    {
        var pending = Pending(side);
        var validating = (side, collision) switch
        {
            (StructuralCandidateExtremeSide.Lower, Collision.None) => Candle(24, 100, 140, 50, 131),
            (StructuralCandidateExtremeSide.Lower, Collision.Directional) => Candle(24, 100, 140, 49, 131),
            (StructuralCandidateExtremeSide.Lower, Collision.HumanStructuralPriceRequired) => Candle(24, 140, 145, 49, 131),
            (StructuralCandidateExtremeSide.Upper, Collision.None) => Candle(24, 100, 140, 50, 69),
            (StructuralCandidateExtremeSide.Upper, Collision.Directional) => Candle(24, 100, 141, 50, 69),
            (StructuralCandidateExtremeSide.Upper, Collision.HumanStructuralPriceRequired) => Candle(24, 60, 141, 50, 69),
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator()
            .Evaluate(pending, validating).Breakout!;
        var resolution = Resolution(breakout, [pending.MigrationCandle]);
        var geometry = new NasdaqHumanRebuiltCandidateVertexGeometryCalculator().Evaluate(resolution);
        return new CompletionFixture(breakout, resolution, geometry);
    }

    private static NasdaqHumanRebuiltCandidateVertexMemberResolution Resolution(
        NasdaqPostInvalidationCandidateRebuildBreakoutState breakout,
        IReadOnlyList<Candle> members)
    {
        var origin = (NasdaqPostInvalidationBreakoutOrigin.Rebuild)breakout.Origin;
        var episode = new NasdaqHumanRebuiltCandidateVertexEpisode(
            breakout.Episode.StrategyId,
            breakout.Episode.StrategyVersion,
            breakout.Episode.ProviderId,
            breakout.Episode.Symbol,
            breakout.Episode.InvalidatingCandleOpenTimeUtc,
            origin.PriorMigrationCandle.OpenTimeUtc,
            breakout.CandidateSide);
        var knownProtectionAnchor = breakout.CandidateSide == StructuralCandidateExtremeSide.Lower
            ? origin.PriorMigrationCandle.Low
            : origin.PriorMigrationCandle.High;
        return CreateResolution(
            episode,
            origin.PriorMigrationCandle,
            members,
            knownProtectionAnchor);
    }

    private static NasdaqHumanRebuiltCandidateVertexMemberResolution CreateResolution(
        NasdaqHumanRebuiltCandidateVertexEpisode episode,
        Candle migrationCandle,
        IEnumerable<Candle> members,
        decimal knownProtectionAnchor)
    {
        var constructor = typeof(NasdaqHumanRebuiltCandidateVertexMemberResolution)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (NasdaqHumanRebuiltCandidateVertexMemberResolution)constructor.Invoke(
            [episode, migrationCandle, members, knownProtectionAnchor]);
    }

    private static NasdaqPostInvalidationCandidateRebuildPendingState Pending(StructuralCandidateExtremeSide side)
    {
        var correction = InitialCorrection(side);
        var terminal = side == StructuralCandidateExtremeSide.Lower
            ? Candle(16, 80, 100, 60, 90)
            : Candle(16, 100, 130, 90, 99);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator()
            .Evaluate(correction, terminal).Candidate!;
        var migration = side == StructuralCandidateExtremeSide.Lower
            ? Candle(20, 100, 120, 50, 90)
            : Candle(20, 100, 140, 80, 100);
        return new NasdaqPostInvalidationCandidateRebuildTransitionCalculator()
            .Evaluate(candidate, migration);
    }

    private static NasdaqPostInvalidationCorrectionState InitialCorrection(StructuralCandidateExtremeSide side)
    {
        var oldSide = side == StructuralCandidateExtremeSide.Upper
            ? StructuralCandidateExtremeSide.Lower
            : StructuralCandidateExtremeSide.Upper;
        var bullish = oldSide == StructuralCandidateExtremeSide.Lower;
        var originCandle = bullish
            ? Candle(0, 100, 110, 95, 110)
            : Candle(0, 100, 110, 85, 90);
        var oldCandidate = bullish
            ? Candle(4, 95, 100, 90, 96)
            : Candle(4, 105, 115, 100, 110);
        var invalidating = bullish
            ? Candle(8, 100, 110, 80, 90)
            : Candle(8, 100, 120, 90, 110);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var observation = new NasdaqHumanOriginVertexObservation(
            definition.StrategyId,
            definition.Version,
            Provider,
            Symbol,
            invalidating.OpenTimeUtc,
            [originCandle.OpenTimeUtc],
            invalidating.CloseTimeUtc,
            "review:origin");
        var cursor = new MultiTimeframeCandleReplayCursor(
            [new CandleSeries(Provider, Symbol, H4, [originCandle, oldCandidate, invalidating])]);
        MultiTimeframeReplayFrame? frame = null;
        while (cursor.TryAdvance(out var next)) frame = next;
        var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!, [observation]);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(
            context,
            H4,
            bullish ? 130 : 70,
            bullish ? 95 : 105,
            bullish ? 140 : 75,
            [oldCandidate],
            oldSide);
        var origin = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, origin);
        var start = bullish
            ? Candle(12, 70, 105, 65, 95)
            : Candle(12, 130, 145, 95, 115);
        return new NasdaqPostInvalidationCorrectionInitializer().Initialize(
            new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
    }

    private static Candle Candle(
        int openHour,
        decimal open,
        decimal high,
        decimal low,
        decimal close) =>
        new(
            Provider,
            Symbol,
            H4,
            Start.AddHours(openHour),
            Start.AddHours(openHour + 4),
            open,
            high,
            low,
            close,
            null);

    private sealed record CompletionFixture(
        NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout,
        NasdaqHumanRebuiltCandidateVertexMemberResolution Resolution,
        StructuralTurnGeometryResult Geometry);

    public enum Collision
    {
        None,
        Directional,
        HumanStructuralPriceRequired,
    }
}
