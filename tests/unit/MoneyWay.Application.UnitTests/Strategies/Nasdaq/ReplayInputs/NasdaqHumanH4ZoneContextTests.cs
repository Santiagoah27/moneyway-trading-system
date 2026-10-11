using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanH4ZoneContextTests
{
    private static NasdaqHumanH4StructuralZoneAnchor Zone(decimal lower = 29618.6m, decimal upper = 29633.1m,
        NasdaqHumanH4ZoneBoundaryPrecision precision = NasdaqHumanH4ZoneBoundaryPrecision.Approximate,
        DateTimeOffset? member = null, DateTimeOffset? validation = null) => new(
            NasdaqHumanH4StructuralRole.LowerHigh, lower, upper, precision, [member ?? H4ContextFixture.At(8)], validation);

    private static NasdaqHumanH4ContextObservation Observation(NasdaqHumanH4StructuralZoneAnchor? zone = null, int observed = 13) => new(
        H4ContextFixture.Session(), new(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Fakeout,
            H4ContextFixture.At(8), H4ContextFixture.At(12), [], [zone ?? Zone()]), H4ContextFixture.At(observed),
        "human:video-zone; cross-feed annotation, not native OHLC equivalence");

    [Fact]
    public void ZoneReviewPassesAndEvidencePreservesBoundsPrecisionSourceAndAvailabilityWithoutScalar()
    {
        var observation = Observation();
        var decision = new MoneyWayNasdaqHumanH4ContextEvaluator().Evaluate(Context(13, observation));
        Assert.Equal(RuleEvaluationResult.Passed, decision.Result);
        var fact = Assert.IsType<NasdaqHumanH4ContextRuleFact>(decision.Fact).Selection.Fact;
        Assert.Empty(fact.Anchors);
        Assert.Same(observation.Fact.ZoneAnchors.Single(), fact.ZoneAnchors.Single());
        using var evidence = JsonDocument.Parse(decision.EvidenceReference!);
        var record = evidence.RootElement[0];
        var zone = record.GetProperty("Fact").GetProperty("ZoneAnchors")[0];
        Assert.Equal(29618.6m, zone.GetProperty("LowerBound").GetDecimal());
        Assert.Equal(29633.1m, zone.GetProperty("UpperBound").GetDecimal());
        Assert.Equal((int)NasdaqHumanH4ZoneBoundaryPrecision.Approximate, zone.GetProperty("BoundaryPrecision").GetInt32());
        Assert.Equal(observation.SourceReference, record.GetProperty("SourceReference").GetString());
        Assert.Equal(observation.ObservedAtUtc, record.GetProperty("ObservedAtUtc").GetDateTimeOffset());
        Assert.DoesNotContain("StructuralPrice", decision.EvidenceReference!);
        Assert.DoesNotContain("Midpoint", decision.EvidenceReference!);
        // The referenced source's High is 101: no relation to either human zone bound is inferred.
        Assert.True(Context(13, observation).TryGetFrame(NasdaqHumanH4ContextObservation.H4, out var frame));
        Assert.Equal(101m, frame!.AvailableCandles.Last().High);
    }

    [Fact]
    public void RejectsInvertedBoundsInvalidPrecisionAndInvalidSourcesButPreservesEqualBoundsAsZone()
    {
        Assert.Throws<ArgumentException>(() => Zone(lower: 3, upper: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Zone(precision: (NasdaqHumanH4ZoneBoundaryPrecision)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NasdaqHumanH4StructuralZoneAnchor((NasdaqHumanH4StructuralRole)99, 1, 2,
            NasdaqHumanH4ZoneBoundaryPrecision.Exact, [H4ContextFixture.At(0)]));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4StructuralZoneAnchor(NasdaqHumanH4StructuralRole.LowerHigh, 1, 2,
            NasdaqHumanH4ZoneBoundaryPrecision.Exact, null!));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4StructuralZoneAnchor(NasdaqHumanH4StructuralRole.LowerHigh, 1, 2,
            NasdaqHumanH4ZoneBoundaryPrecision.Exact, []));
        Assert.Throws<ArgumentException>(() => Zone(member: H4ContextFixture.At(8).ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => Zone(validation: H4ContextFixture.At(8).ToOffset(TimeSpan.FromHours(-5))));
        var pointZone = Zone(lower: 2, upper: 2);
        Assert.Equal(2, pointZone.LowerBound); Assert.Equal(2, pointZone.UpperBound);
        Assert.Empty(Observation(pointZone).Fact.Anchors);
    }

    [Fact]
    public void CopiesAndCanonicalizesZonesRejectingDuplicateOrMissingAnchors()
    {
        var members = new[] { H4ContextFixture.At(8), H4ContextFixture.At(4) };
        var zone = new NasdaqHumanH4StructuralZoneAnchor(NasdaqHumanH4StructuralRole.LowerHigh, 1, 2,
            NasdaqHumanH4ZoneBoundaryPrecision.Exact, members);
        members[0] = H4ContextFixture.At(16);
        Assert.Equal([H4ContextFixture.At(4), H4ContextFixture.At(8)], zone.MemberOpenTimesUtc);
        Assert.Throws<NotSupportedException>(() => ((IList<DateTimeOffset>)zone.MemberOpenTimesUtc).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanH4StructuralZoneAnchor>)Observation().Fact.ZoneAnchors).Clear());
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4ContextFact(NasdaqHumanH4PermittedDirection.Sell,
            NasdaqHumanH4ContextKind.Fakeout, H4ContextFixture.At(8), H4ContextFixture.At(12), [], [zone, zone]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4ContextFact(NasdaqHumanH4PermittedDirection.Sell,
            NasdaqHumanH4ContextKind.Fakeout, H4ContextFixture.At(8), H4ContextFixture.At(12), [], []));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4ContextFact(NasdaqHumanH4PermittedDirection.Sell,
            NasdaqHumanH4ContextKind.Fakeout, H4ContextFixture.At(8), H4ContextFixture.At(12), [], [null!]));
    }

    [Theory]
    [InlineData("lower")]
    [InlineData("upper")]
    [InlineData("precision")]
    [InlineData("member")]
    [InlineData("validation")]
    public void ExactZoneDifferencesConflictWithoutMerging(string change)
    {
        var first = Observation();
        var other = Observation(change switch
        {
            "lower" => Zone(lower: 29618.7m),
            "upper" => Zone(upper: 29633.2m),
            "precision" => Zone(precision: NasdaqHumanH4ZoneBoundaryPrecision.Exact),
            "member" => Zone(member: H4ContextFixture.At(4)),
            _ => Zone(validation: H4ContextFixture.At(4)),
        });
        Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(new NasdaqHumanH4ContextObservationSelector().Select(
            Context(13, first, other), first.Session));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, new MoneyWayNasdaqHumanH4ContextEvaluator().Evaluate(Context(13, first, other)).Result);
    }

    [Fact]
    public void FutureObservationCannotChangeEarlierFramesAndCompatibleSupportRemainsLossless()
    {
        var early = Observation(); var future = Observation(Zone(upper: 29634m), observed: 15);
        var selector = new NasdaqHumanH4ContextObservationSelector();
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(selector.Select(Context(12, early), early.Session));
        var baseline = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(selector.Select(Context(13, early), early.Session));
        var earlier = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(selector.Select(Context(13, early, future), early.Session));
        Assert.Equal(baseline.Fact, earlier.Fact);
        Assert.Equal(baseline.SupportingObservations, earlier.SupportingObservations);
        Assert.Equal(baseline.Fact.GetHashCode(), earlier.Fact.GetHashCode());
        Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(selector.Select(Context(15, early, future), early.Session));
        var compatible = new NasdaqHumanH4ContextObservation(early.Session, Observation().Fact, H4ContextFixture.At(14), "second:review");
        Assert.Equal(2, Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(selector.Select(Context(14, early, compatible), early.Session)).SupportingObservations.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FutureMemberOrValidationCannotBeMadeObservableByLaterEvaluation(bool validation)
    {
        var observation = Observation(validation ? Zone(validation: H4ContextFixture.At(12)) : Zone(member: H4ContextFixture.At(12)));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(new NasdaqHumanH4ContextObservationSelector().Select(Context(16, observation), observation.Session));
    }

    [Fact]
    public void LegacyScalarReviewsRemainExactAndAreNotEquatedWithZones()
    {
        var legacy = H4ContextFixture.Observation();
        Assert.Empty(legacy.Fact.ZoneAnchors);
        Assert.Equal(101, legacy.Fact.Anchors.Single().StructuralPrice);
        Assert.Equal(RuleEvaluationResult.Passed, new MoneyWayNasdaqHumanH4ContextEvaluator().Evaluate(Context(13, legacy)).Result);
        var scalar = new NasdaqHumanH4ContextObservation(legacy.Session,
            new(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Fakeout, H4ContextFixture.At(8), H4ContextFixture.At(12),
                [new(NasdaqHumanH4StructuralRole.LowerHigh, 101, [H4ContextFixture.At(8)])]), H4ContextFixture.At(13), "scalar:review");
        var zone = Observation(Zone(lower: 101, upper: 101));
        Assert.NotEqual(scalar.Fact, zone.Fact);
        Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(new NasdaqHumanH4ContextObservationSelector().Select(Context(13, scalar, zone), scalar.Session));
    }

    private static StrategyReplayContext Context(int hour, params IStrategyReplayInputObservation[] observations)
    {
        var h4 = NasdaqHumanH4ContextObservation.H4; var minute = new Timeframe(1, TimeframeUnit.Minute);
        var series = new CandleSeries[]
        {
            new(H4ContextFixture.Provider, H4ContextFixture.Symbol, h4, new[] { 0, 4, 8, 12 }.Select(open =>
                new Candle(H4ContextFixture.Provider, H4ContextFixture.Symbol, h4, H4ContextFixture.At(open), H4ContextFixture.At(open + 4), 100, 101, 99, 100, null))),
            new(H4ContextFixture.Provider, H4ContextFixture.Symbol, minute, [new(H4ContextFixture.Provider, H4ContextFixture.Symbol,
                minute, H4ContextFixture.At(hour).AddMinutes(-1), H4ContextFixture.At(hour), 100, 101, 99, 100, null)]),
        };
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == H4ContextFixture.At(hour)) return new CreateStrategyReplayContextUseCase().Execute(H4ContextFixture.Definition, frame, observations);
        throw new InvalidOperationException("Fixture frame missing.");
    }
}
