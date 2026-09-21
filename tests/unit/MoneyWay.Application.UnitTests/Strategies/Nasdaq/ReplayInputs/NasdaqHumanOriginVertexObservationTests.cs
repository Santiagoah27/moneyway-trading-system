using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanOriginVertexObservationTests
{
    private static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NASDAQ");
    private static readonly DateTimeOffset Start = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HumanReviewedMembershipPreservesAdrPayloadAndUsesH4Only()
    {
        var invalidation = Start.AddHours(12);
        var members = new[] { Start.AddHours(8), Start.AddHours(4) };
        var observation = Observation(invalidation, members, invalidation.AddHours(5), "review:vertex-1");

        Assert.IsAssignableFrom<IStrategyReplayInputObservation>(observation);
        Assert.Equal(Definition.StrategyId, observation.StrategyId);
        Assert.Equal(Definition.Version, observation.StrategyVersion);
        Assert.Equal(Provider, observation.ProviderId);
        Assert.Equal(Symbol, observation.Symbol);
        Assert.Equal(new Timeframe(4, TimeframeUnit.Hour), observation.Timeframe);
        Assert.Equal(invalidation, observation.InvalidatingCandleOpenTimeUtc);
        Assert.Equal([Start.AddHours(4), Start.AddHours(8)], observation.SelectedMemberOpenTimesUtc);
        Assert.Equal(invalidation.AddHours(5), observation.ObservedAtUtc);
        Assert.Equal("review:vertex-1", observation.SourceReference);
    }

    [Fact]
    public void MembershipIsAnImmutableDistinctSetAndCannotContainInvalidationOrFutureIdentity()
    {
        var invalidation = Start.AddHours(12);
        var input = new List<DateTimeOffset> { Start.AddHours(4), Start.AddHours(8) };
        var observation = Observation(invalidation, input);
        input.Clear();

        Assert.Equal([Start.AddHours(4), Start.AddHours(8)], observation.SelectedMemberOpenTimesUtc);
        Assert.Throws<NotSupportedException>(() => ((ICollection<DateTimeOffset>)observation.SelectedMemberOpenTimesUtc)
            .Add(Start.AddHours(10)));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, []));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [Start.AddHours(4), Start.AddHours(4)]));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [invalidation]));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [invalidation.AddHours(4)]));
    }

    [Fact]
    public void ObservationRejectsNonNasdaqAndNonUtcPayloadsAndHasDeterministicValueEquality()
    {
        var invalidation = Start.AddHours(12);
        var first = Observation(invalidation, [Start.AddHours(4), Start.AddHours(8)]);
        var equivalent = Observation(invalidation, [Start.AddHours(8), Start.AddHours(4)]);
        var different = Observation(invalidation, [Start.AddHours(4)]);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.Throws<ArgumentException>(() => new NasdaqHumanOriginVertexObservation(
            new("moneyway-forex"), Definition.Version, Provider, Symbol, invalidation,
            [Start.AddHours(4)], invalidation.AddHours(1), "source"));
        Assert.Throws<ArgumentException>(() => Observation(invalidation.ToOffset(TimeSpan.FromHours(-5)), [Start.AddHours(4)]));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [Start.AddHours(4).ToOffset(TimeSpan.FromHours(-5))]));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [Start.AddHours(4)], invalidation.AddHours(1).ToOffset(TimeSpan.FromHours(-5))));
        Assert.Throws<ArgumentException>(() => Observation(invalidation, [Start.AddHours(4)], invalidation.AddHours(1), " "));
    }

    [Fact]
    public void ObservationCarriesMembershipOnlyWithoutGeometrySessionOrVerdict()
    {
        var observation = Observation(Start.AddHours(12), [Start.AddHours(4)]);
        var properties = typeof(NasdaqHumanOriginVertexObservation).GetProperties();

        Assert.Null(typeof(NasdaqHumanOriginVertexObservation).GetProperty("StructuralPrice"));
        Assert.Null(typeof(NasdaqHumanOriginVertexObservation).GetProperty("ProtectionAnchor"));
        Assert.Null(typeof(NasdaqHumanOriginVertexObservation).GetProperty("TradingDay"));
        Assert.Null(typeof(NasdaqHumanOriginVertexObservation).GetProperty("Result"));
        Assert.Null(typeof(NasdaqHumanOriginVertexObservation).GetProperty("Verdict"));
        Assert.All(properties, property => Assert.False(property.CanWrite));
        Assert.DoesNotContain(properties, property => property.Name.Contains("Tolerance", StringComparison.Ordinal));
        Assert.Single(observation.SelectedMemberOpenTimesUtc);
    }

    private static NasdaqHumanOriginVertexObservation Observation(
        DateTimeOffset invalidation,
        IEnumerable<DateTimeOffset> members,
        DateTimeOffset? observedAt = null,
        string source = "review:vertex") =>
        new(Definition.StrategyId, Definition.Version, Provider, Symbol, invalidation, members,
            observedAt ?? invalidation.AddHours(1), source);
}
