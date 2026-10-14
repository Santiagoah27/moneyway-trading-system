using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanWickfillScenario2ContextTests
{
    private readonly NasdaqHumanWickfillScenario2ContextObservationSelector selector = new();
    private static DateTimeOffset At(int hour) => H4ContextFixture.At(hour);
    private static readonly Timeframe H4Timeframe = NasdaqHumanH4ContextObservation.H4;
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);

    [Fact]
    public void ConstructionRetainsImmutableTypedAncestryAndHumanEvidenceWithoutGeometryInference()
    {
        var h4 = H4();
        var sources = Sources().ToList();
        var observation = Observation(h4: h4, sources: sources);
        sources.Clear();
        Assert.Same(h4, observation.H4Context);
        Assert.Same(h4.Fact.ZoneAnchors.Single(), observation.ApplicableZone);
        Assert.Equal(h4.Session, observation.Session);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, observation.Direction);
        Assert.Equal(At(13), observation.EffectiveAtUtc);
        Assert.Equal(At(13), observation.ObservedAtUtc);
        Assert.Equal("review:context", observation.SourceReference);
        Assert.Equal(3, observation.ReviewedSourceCandles.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)observation.ReviewedSourceCandles).Clear());
        Assert.All(typeof(NasdaqHumanWickfillScenario2ContextObservation).GetProperties(), p => Assert.Null(p.SetMethod));
        // Zone is far outside synthetic OHLC: membership and completed movement are asserted by a human, not detected.
        Assert.True(observation.ApplicableZone.LowerBound > observation.ReviewedSourceCandles.Max(c => c.High));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" padded")]
    [InlineData("padded ")]
    public void InvalidProvenanceIsRejected(string source) => Assert.Throws<ArgumentException>(() => Observation(source: source));

    [Fact]
    public void RequiredArgumentsAndContextAncestryAreValidated()
    {
        var h4 = H4();
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanWickfillScenario2ContextObservation(null!, Zone(), Sources(), At(13), At(13), "source"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanWickfillScenario2ContextObservation(h4, null!, Sources(), At(13), At(13), "source"));
        Assert.Throws<ArgumentNullException>(() => new NasdaqHumanWickfillScenario2ContextObservation(h4, Zone(), null!, At(13), At(13), "source"));
        Assert.Throws<ArgumentNullException>(() => Observation(source: null!));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanWickfillScenario2ContextObservation(h4, Zone(upper: 501), Sources(), At(13), At(13), "source"));
        Assert.Throws<ArgumentException>(() => H4(session: H4ContextFixture.Session(strategy: new("moneyway-forex"))));
    }

    [Theory]
    [InlineData(NasdaqHumanH4PermittedDirection.Buy, NasdaqHumanH4ContextKind.Wickfill)]
    [InlineData(NasdaqHumanH4PermittedDirection.Unresolved, NasdaqHumanH4ContextKind.Wickfill)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Fakeout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Breakout)]
    [InlineData(NasdaqHumanH4PermittedDirection.Sell, NasdaqHumanH4ContextKind.Unresolved)]
    public void OnlyExplicitSellWickfillIsRepresentedWithoutConvertingOtherContextLabels(
        NasdaqHumanH4PermittedDirection direction, NasdaqHumanH4ContextKind kind) =>
        Assert.Throws<ArgumentException>(() => Observation(h4: H4(direction: direction, kind: kind)));

    [Theory]
    [InlineData("before-h4")]
    [InlineData("before-sources")]
    [InlineData("observed-before-effective")]
    [InlineData("observed-before-h4")]
    [InlineData("effective-offset")]
    [InlineData("observed-offset")]
    [InlineData("opening")]
    [InlineData("after-opening")]
    [InlineData("wrong-day")]
    public void InvalidCausalOrPreOpenTimesAreRejected(string issue)
    {
        var effective = issue switch
        {
            "before-h4" => At(11),
            "before-sources" => At(12).AddMinutes(30),
            "effective-offset" => At(13).ToOffset(TimeSpan.FromHours(-5)),
            "opening" => At(13).AddMinutes(30),
            "after-opening" => At(14),
            "wrong-day" => At(37),
            _ => At(13),
        };
        var observed = issue switch
        {
            "observed-before-effective" => At(12),
            "observed-offset" => At(13).ToOffset(TimeSpan.FromHours(-5)),
            "observed-before-h4" => At(13),
            _ => effective.ToUniversalTime(),
        };
        Assert.Throws<ArgumentException>(() => Observation(effective: effective, observed: observed,
            h4: H4(observed: issue == "observed-before-h4" ? At(14) : At(12)),
            sources: issue == "before-sources" ? [.. Sources(), Minute(At(13))] : Sources()));
    }

    [Fact]
    public void PreOpenBoundaryIsStrictButAvailabilityMayBeLaterWithoutBackdating()
    {
        var effective = At(13).AddMinutes(30).AddTicks(-1);
        var observation = Observation(effective: effective, observed: At(14));
        Assert.Equal(effective, observation.EffectiveAtUtc);
        Assert.Equal(At(14), observation.ObservedAtUtc);
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(13).AddMinutes(30), [observation])));
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(Context(At(14), [observation])));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null-member")]
    [InlineData("duplicate")]
    [InlineData("provider")]
    [InlineData("symbol")]
    [InlineData("missing-context")]
    [InlineData("missing-zone-member")]
    [InlineData("missing-validation")]
    [InlineData("future-validation")]
    public void SourcesRequireExactSessionAndCompleteClosedH4Ancestry(string issue)
    {
        var sources = Sources();
        var h4 = H4();
        sources = issue switch
        {
            "empty" => [],
            "null-member" => [null!],
            "duplicate" => [.. sources, sources[0]],
            "provider" => [Candle(8, provider: new("foreign")), .. sources.Skip(1)],
            "symbol" => [Candle(8, symbol: new("foreign")), .. sources.Skip(1)],
            "missing-context" => sources.Where(c => c.OpenTimeUtc != At(8)).ToArray(),
            "missing-zone-member" => sources.Where(c => c.OpenTimeUtc != At(4)).ToArray(),
            "missing-validation" => sources.Where(c => c.OpenTimeUtc != At(0)).ToArray(),
            _ => [.. sources, Candle(12)],
        };
        if (issue == "future-validation") h4 = H4(zone: Zone(validation: At(12)));
        Assert.Throws<ArgumentException>(() => Observation(h4: h4, sources: sources));
    }

    [Fact]
    public void MissingUniqueAndUnavailableSourcesAreDistinctAndReadOnly()
    {
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(13), [])));
        var observation = Observation();
        var unique = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(Context(At(13), [observation])));
        Assert.Same(observation, unique.Fact);
        Assert.Empty(unique.UnavailableSourceObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2ContextObservation>)unique.SupportingObservations).Clear());
        var missing = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(13), [observation], sources: [])));
        Assert.Same(observation, Assert.Single(missing.UnavailableSourceObservations));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2ContextObservation>)missing.UnavailableSourceObservations).Clear());
    }

    [Fact]
    public void CompatibleDuplicatesPreserveAllNestedProvenanceAndAvailabilityWithoutLatestWins()
    {
        var first = Observation(source: "z");
        var second = Observation(h4: H4(source: "different:h4", observed: At(14)), observed: At(14), source: "a", sources: Sources().Reverse());
        var result = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(Context(At(14), [second, first, first])));
        Assert.Equal([first, first, second], result.SupportingObservations);
        Assert.Equal("different:h4", result.SupportingObservations[2].H4Context.SourceReference);
        Assert.Equal(At(14), result.SupportingObservations[2].H4Context.ObservedAtUtc);
    }

    [Theory]
    [InlineData("zone")]
    [InlineData("effective")]
    [InlineData("sources")]
    [InlineData("context")]
    public void IncompatibleAssertionsConflictWithoutWinnerOrMajority(string difference)
    {
        var first = Observation(source: "same");
        var other = difference switch
        {
            "zone" => Observation(h4: H4(zone: Zone(upper: 501)), source: "same"),
            "effective" => Observation(effective: At(13).AddMinutes(1), observed: At(14), source: "same"),
            "sources" => Observation(sources: [.. Sources(), Minute(At(12).AddMinutes(5))], source: "same"),
            _ => Observation(h4: H4(contextOpen: At(4)), source: "same"),
        };
        var seriesSources = Sources().Append(Minute(At(12).AddMinutes(5))).ToArray();
        var forward = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Conflict>(Select(Context(At(14), [first, other, first], seriesSources)));
        var reverse = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Conflict>(Select(Context(At(14), [other, first, first], seriesSources)));
        Assert.Equal(3, forward.Alternatives.Count);
        Assert.Equal(forward.Alternatives, reverse.Alternatives);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanWickfillScenario2ContextObservation>)forward.Alternatives).Clear());
    }

    [Fact]
    public void FutureConflictingEvidenceDoesNotRewriteEarlierFrameAndSelectorIsStateless()
    {
        var first = Observation();
        var later = Observation(h4: H4(zone: Zone(upper: 501)), observed: At(15));
        var earlierContext = Context(At(13), [first, later]);
        var earlier = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(earlierContext));
        Assert.Same(first, earlier.Fact);
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Conflict>(Select(Context(At(15), [later, first])));
        Assert.Equal(earlier.SupportingObservations, Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(earlierContext)).SupportingObservations);
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(14), [later])));
    }

    [Theory]
    [InlineData("ohlc")]
    [InlineData("volume")]
    [InlineData("close-time")]
    public void SameSourceIdentityWithDifferentExactValuesIsNotAccepted(string difference)
    {
        var modified = new Candle(H4ContextFixture.Provider, H4ContextFixture.Symbol, H4Timeframe, At(8),
            difference == "close-time" ? At(12).AddMinutes(-1) : At(12), 100, difference == "ohlc" ? 102 : 101,
            99, 100, difference == "volume" ? 1 : null);
        var actual = Sources().Select(c => c.OpenTimeUtc == At(8) ? modified : c).ToArray();
        var result = Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(13), [Observation()], actual)));
        Assert.Empty(result.UnavailableSourceObservations);
    }

    [Fact]
    public void WrongSessionAndForeignEnvelopeDoNotParticipate()
    {
        var observation = Observation();
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(29), [observation])));
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(Select(Context(At(28), [observation])));
        var wrong = H4ContextFixture.Session(day: new(2026, 10, 4));
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(selector.Select(Context(At(13), [observation]), wrong));
        var wrongDay = Observation(h4: H4(session: wrong), effective: At(37), observed: At(37), sources: [Candle(0), Candle(4), Candle(8)]);
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(Select(Context(At(13), [wrongDay])));
        foreach (var session in new[] { H4ContextFixture.Session(provider: new("other")), H4ContextFixture.Session(symbol: new("other")), H4ContextFixture.Session(version: new("other")) })
        {
            var foreignSources = Sources().Select(c => new Candle(session.ProviderId, session.Symbol, c.Timeframe,
                c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, c.Volume));
            var foreign = Observation(h4: H4(session: session), sources: foreignSources);
            Assert.Throws<ArgumentException>(() => Context(At(13), [foreign]));
        }
    }

    [Fact]
    public void PrimitiveAndSelectionDoNotProduceRuleFactsEligibilityVerdictOrChangeStandardEvaluations()
    {
        var input = Observation();
        Assert.False((object)input is IReplayRuleFact);
        var resolution = Select(Context(At(14), [input]));
        Assert.False((object)resolution is IReplayRuleFact);
        var evaluator = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var definition = H4ContextFixture.Definition;
        var baseline = evaluator.Execute(definition, Context(At(14), []));
        var withInput = evaluator.Execute(definition, Context(At(14), [input]));
        Assert.Equal(baseline.Evaluations, withInput.Evaluations);
        Assert.Equal(baseline.RuleFacts, withInput.RuleFacts);
        Assert.DoesNotContain(withInput.RuleFacts, f => f.RuleId.Value == "NQ-M1-003");
        var outcome = new EvaluateStrategyReplayContextOutcomeUseCase();
        Assert.Equal(outcome.Execute(definition, baseline).Verdict, outcome.Execute(definition, withInput).Verdict);
        Assert.DoesNotContain(withInput.Evaluations, e => (e.RuleId.Value.StartsWith("NQ-M1") || e.RuleId.Value.StartsWith("NQ-M5"))
            && e.Result == RuleEvaluationResult.NotApplicable);
    }

    [Fact]
    public void NullSelectorInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, H4ContextFixture.Session()));
        Assert.Throws<ArgumentNullException>(() => selector.Select(Context(At(13), []), null!));
    }

    private NasdaqHumanWickfillScenario2ContextSelection Select(StrategyReplayContext context) => selector.Select(context, H4ContextFixture.Session());

    private static NasdaqHumanH4StructuralZoneAnchor Zone(decimal upper = 500, DateTimeOffset? validation = null) =>
        new(NasdaqHumanH4StructuralRole.LowerHigh, 400, upper, NasdaqHumanH4ZoneBoundaryPrecision.Approximate, [At(4)], validation ?? At(0));

    private static NasdaqHumanH4ContextObservation H4(NasdaqHumanH4StructuralZoneAnchor? zone = null,
        NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Sell,
        NasdaqHumanH4ContextKind kind = NasdaqHumanH4ContextKind.Wickfill, NasdaqDemoSessionIdentity? session = null,
        DateTimeOffset? observed = null, string source = "review:h4", DateTimeOffset? contextOpen = null) =>
        new(session ?? H4ContextFixture.Session(), new(direction, kind, contextOpen ?? At(8), At(12), [], [zone ?? Zone()]), observed ?? At(13), source);

    private static NasdaqHumanWickfillScenario2ContextObservation Observation(NasdaqHumanH4ContextObservation? h4 = null,
        IEnumerable<Candle>? sources = null, DateTimeOffset? effective = null, DateTimeOffset? observed = null, string source = "review:context")
    {
        var context = h4 ?? H4();
        return new(context, context.Fact.ZoneAnchors.Single(), sources ?? Sources(), effective ?? At(13), observed ?? At(13), source);
    }

    private static Candle Candle(int hour, MarketDataProviderId? provider = null, MarketSymbol? symbol = null) =>
        new(provider ?? H4ContextFixture.Provider, symbol ?? H4ContextFixture.Symbol, H4Timeframe, At(hour), At(hour + 4), 100, 101, 99, 100, null);
    private static Candle Minute(DateTimeOffset close) => new(H4ContextFixture.Provider, H4ContextFixture.Symbol, M1, close.AddMinutes(-1), close, 100, 101, 99, 100, null);
    private static Candle[] Sources() => [Candle(0), Candle(4), Candle(8)];

    private static StrategyReplayContext Context(DateTimeOffset asOf, IStrategyReplayInputObservation[] observations, Candle[]? sources = null)
    {
        var candles = (sources ?? Sources()).Append(Minute(asOf)).ToArray();
        var series = candles.GroupBy(c => c.Timeframe).Select(group => new CandleSeries(H4ContextFixture.Provider,
            H4ContextFixture.Symbol, group.Key, group.OrderBy(c => c.OpenTimeUtc))).ToArray();
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == asOf) return new CreateStrategyReplayContextUseCase().Execute(H4ContextFixture.Definition, frame, observations);
        throw new InvalidOperationException("Fixture frame missing.");
    }
}
