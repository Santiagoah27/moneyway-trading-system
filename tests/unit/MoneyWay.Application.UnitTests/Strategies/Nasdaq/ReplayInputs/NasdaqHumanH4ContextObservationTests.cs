using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanH4ContextObservationTests
{
    [Fact]
    public void PreservesExactFactIdentityAvailabilityAndSource()
    {
        var fact = H4ContextFixture.Fact();
        var session = H4ContextFixture.Session();
        var observation = new NasdaqHumanH4ContextObservation(session, fact, H4ContextFixture.At(13), "mentor:file#reviewer:event");
        Assert.Same(session, observation.Session);
        Assert.Same(fact, observation.Fact);
        Assert.Equal(session.StrategyId, observation.StrategyId);
        Assert.Equal(session.StrategyVersion, observation.StrategyVersion);
        Assert.Equal(session.ProviderId, observation.ProviderId);
        Assert.Equal(session.Symbol, observation.Symbol);
        Assert.Equal(new Timeframe(4, TimeframeUnit.Hour), observation.Timeframe);
        Assert.Equal(H4ContextFixture.At(12), observation.EffectiveAtUtc);
        Assert.Equal(H4ContextFixture.At(13), observation.ObservedAtUtc);
        Assert.Equal("mentor:file#reviewer:event", observation.SourceReference);
    }

    [Fact]
    public void RejectsNullIdentityFactAndSource()
    {
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4ContextObservation(null!, H4ContextFixture.Fact(), H4ContextFixture.At(13), "source"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4ContextObservation(H4ContextFixture.Session(), null!, H4ContextFixture.At(13), "source"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4ContextObservation(H4ContextFixture.Session(), H4ContextFixture.Fact(), H4ContextFixture.At(13), null!));
        Assert.Throws<ArgumentNullException>(() => new NasdaqDemoSessionIdentity(null!, new("v"), new("p"), new("s"), new(2026, 10, 3)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqDemoSessionIdentity(new("s"), null!, new("p"), new("s"), new(2026, 10, 3)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqDemoSessionIdentity(new("s"), new("v"), null!, new("s"), new(2026, 10, 3)));
        Assert.Throws<ArgumentNullException>(() => new NasdaqDemoSessionIdentity(new("s"), new("v"), new("p"), null!, new(2026, 10, 3)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" source")]
    [InlineData("source ")]
    public void RejectsInvalidProvenance(string source) => Assert.Throws<ArgumentException>(() =>
        new NasdaqHumanH4ContextObservation(H4ContextFixture.Session(), H4ContextFixture.Fact(), H4ContextFixture.At(13), source));

    [Fact]
    public void RejectsNonNasdaqAndImpossibleOrNonUtcAvailability()
    {
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4ContextObservation(
            H4ContextFixture.Session(strategy: new("moneyway-forex")), H4ContextFixture.Fact(), H4ContextFixture.At(13), "source"));
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Observation(observed: 11));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4ContextObservation(
            H4ContextFixture.Session(), H4ContextFixture.Fact(), H4ContextFixture.At(13).ToOffset(TimeSpan.FromHours(-5)), "source"));
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Fact(effective: H4ContextFixture.At(12).ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Fact(contextOpen: H4ContextFixture.At(8).ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void RejectsInvalidEnumsAndAnchorCollections()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => H4ContextFixture.Fact(direction: (NasdaqHumanH4PermittedDirection)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => H4ContextFixture.Fact(kind: (NasdaqHumanH4ContextKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NasdaqHumanH4StructuralAnchor((NasdaqHumanH4StructuralRole)99, 100, [H4ContextFixture.At(0)]));
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Fact(anchors: []));
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Fact(anchors: [null!]));
        var anchor = H4ContextFixture.Anchor();
        Assert.Throws<ArgumentException>(() => H4ContextFixture.Fact(anchors: [anchor, anchor]));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4ContextFact(NasdaqHumanH4PermittedDirection.Buy,
            NasdaqHumanH4ContextKind.Breakout, H4ContextFixture.At(8), H4ContextFixture.At(12), null!));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 100, []));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 100, null!));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 100,
            [H4ContextFixture.At(0), H4ContextFixture.At(0)]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 100,
            [H4ContextFixture.At(0).ToOffset(TimeSpan.FromHours(-5))]));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 100,
            [H4ContextFixture.At(0)], H4ContextFixture.At(8).ToOffset(TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void SnapshotsAndCanonicalizesMembersAndAnchorsWithoutMutatingCaller()
    {
        var members = new[] { H4ContextFixture.At(4), H4ContextFixture.At(0) };
        var anchor = new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherHigh, 101, members, H4ContextFixture.At(8));
        var other = new NasdaqHumanH4StructuralAnchor(NasdaqHumanH4StructuralRole.HigherLow, 99, [H4ContextFixture.At(4)]);
        var anchors = new[] { other, anchor };
        var fact = H4ContextFixture.Fact(anchors: anchors);
        Assert.Equal(H4ContextFixture.At(4), members[0]);
        Assert.Same(other, anchors[0]);
        members[0] = H4ContextFixture.At(20);
        anchors[0] = anchor;
        Assert.Equal([H4ContextFixture.At(0), H4ContextFixture.At(4)], anchor.MemberOpenTimesUtc);
        Assert.Equal(2, fact.Anchors.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<DateTimeOffset>)anchor.MemberOpenTimesUtc).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanH4StructuralAnchor>)fact.Anchors).Clear());
        var equivalent = H4ContextFixture.Fact(anchors: [anchor, other]);
        Assert.Equal(fact, equivalent);
        Assert.Equal(fact.GetHashCode(), equivalent.GetHashCode());
        Assert.Equal(0, fact.CompareTo(equivalent));
    }

    [Fact]
    public void RecordIdentityAndObservationEqualityAreExact()
    {
        Assert.Equal(H4ContextFixture.Session(), H4ContextFixture.Session());
        Assert.NotEqual(H4ContextFixture.Session(), H4ContextFixture.Session(day: new(2026, 10, 4)));
        Assert.Equal(H4ContextFixture.Observation(), H4ContextFixture.Observation());
        Assert.NotEqual(H4ContextFixture.Observation(), H4ContextFixture.Observation(source: "different"));
        Assert.NotEqual(H4ContextFixture.Observation(), H4ContextFixture.Observation(observed: 14));
        Assert.Equal(MoneyWayNasdaqStrategyDefinition.Instance.StrategyId, H4ContextFixture.Session().StrategyId);
    }
}
