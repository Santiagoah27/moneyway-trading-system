using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanH4ContextObservationSelectorTests
{
    private readonly NasdaqHumanH4ContextObservationSelector selector = new();

    [Fact]
    public void MissingForNoH4EvidenceUnrelatedInputOrUnavailableH4()
    {
        var preparation = new NasdaqPreparationCompletionObservation(H4ContextFixture.Definition.StrategyId,
            H4ContextFixture.Definition.Version, H4ContextFixture.Provider, H4ContextFixture.Symbol,
            new(2026, 10, 3), H4ContextFixture.At(13), "preparation");
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13)));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13, [preparation])));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13, [H4ContextFixture.Observation()], h4: false)));
    }

    [Fact]
    public void UniquePreservesTheFactAndOriginalObservation()
    {
        var observation = H4ContextFixture.Observation();
        var result = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(13, [observation])));
        Assert.Same(observation.Fact, result.Fact);
        Assert.Same(observation, Assert.Single(result.SupportingObservations));
        Assert.Equal(H4ContextFixture.Session(), observation.Session);
        Assert.Equal(H4ContextFixture.At(12), result.Fact.EffectiveAtUtc);
    }

    [Fact]
    public void CompatibleDuplicatesRetainAllAvailabilityAndProvenance()
    {
        var first = H4ContextFixture.Observation(source: "review:z");
        var second = H4ContextFixture.Observation(observed: 14, source: "review:a");
        var context = Context(14, [second, first]);
        var result = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(context));
        Assert.Equal([first, second], result.SupportingObservations);
        Assert.Equal(["review:z", "review:a"], result.SupportingObservations.Select(item => item.SourceReference));
        Assert.Equal([H4ContextFixture.At(13), H4ContextFixture.At(14)], result.SupportingObservations.Select(item => item.ObservedAtUtc));
        Assert.All(result.SupportingObservations, item => Assert.Equal(H4ContextFixture.At(12), item.EffectiveAtUtc));
        Assert.Equal([second, first], context.InputObservations);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanH4ContextObservation>)result.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData("direction")]
    [InlineData("kind")]
    [InlineData("price")]
    [InlineData("members")]
    [InlineData("validation")]
    [InlineData("role")]
    [InlineData("effective")]
    [InlineData("context")]
    public void ExactSemanticDifferencesConflictWithoutWinner(string difference)
    {
        var first = H4ContextFixture.Observation();
        var fact = difference switch
        {
            "direction" => H4ContextFixture.Fact(direction: NasdaqHumanH4PermittedDirection.Sell),
            "kind" => H4ContextFixture.Fact(kind: NasdaqHumanH4ContextKind.Fakeout),
            "price" => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(price: 100.999m)]),
            "members" => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(members: [H4ContextFixture.At(4)])]),
            "validation" => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(validation: H4ContextFixture.At(4))]),
            "role" => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(role: NasdaqHumanH4StructuralRole.LowerHigh)]),
            "effective" => H4ContextFixture.Fact(effective: H4ContextFixture.At(13)),
            _ => H4ContextFixture.Fact(contextOpen: H4ContextFixture.At(4)),
        };
        var other = H4ContextFixture.Observation(fact: fact, source: "review:conflict");
        var result = Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(Select(Context(14, [first, other])));
        Assert.Contains(first, result.SupportingObservations);
        Assert.Contains(other, result.SupportingObservations);
        Assert.Equal(2, result.SupportingObservations.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanH4ContextObservation>)result.SupportingObservations).Clear());
    }

    [Fact]
    public void NewerCompatibleMajorityNeverSupersedesConflict()
    {
        var first = H4ContextFixture.Observation(source: "a");
        var conflict = H4ContextFixture.Observation(fact: H4ContextFixture.Fact(direction: NasdaqHumanH4PermittedDirection.Sell), source: "b");
        var later = H4ContextFixture.Observation(observed: 15, source: "c");
        var result = Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(Select(Context(15, [first, conflict, later])));
        Assert.Equal(3, result.SupportingObservations.Count);
    }

    [Fact]
    public void FutureEvidenceCannotChangeEarlierSelectionAndLateEvidenceBecomesVisible()
    {
        var early = H4ContextFixture.Observation();
        var later = H4ContextFixture.Observation(observed: 15, source: "late",
            fact: H4ContextFixture.Fact(direction: NasdaqHumanH4PermittedDirection.Sell));
        var baseline = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(13, [early])));
        var earlierContext = Context(13, [early, later]);
        var earlier = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(earlierContext));
        Assert.Equal(baseline.Fact, earlier.Fact);
        Assert.Equal(baseline.SupportingObservations, earlier.SupportingObservations);
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(14, [later])));
        Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(15, [later])));
        Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(Select(Context(15, [early, later])));
        Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(earlierContext));
    }

    [Theory]
    [InlineData("context")]
    [InlineData("member")]
    [InlineData("validation")]
    public void FutureOrUnresolvableSourceFactsAreNotUsable(string source)
    {
        var missingOpen = H4ContextFixture.At(12); // Candle exists in the input series, but closes only at 16.
        var fact = source switch
        {
            "context" => H4ContextFixture.Fact(contextOpen: missingOpen),
            "member" => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(members: [missingOpen])]),
            _ => H4ContextFixture.Fact(anchors: [H4ContextFixture.Anchor(validation: missingOpen)]),
        };
        var observation = H4ContextFixture.Observation(fact: fact);
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13, [observation])));
        // Later visibility cannot repair a falsely early effective time.
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(16, [observation])));
    }

    [Fact]
    public void SourceClosedAtEffectiveBoundaryIsUsableAndUnknownReferenceIsNot()
    {
        var atClose = H4ContextFixture.Observation(observed: 12);
        Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(12, [atClose])));
        var unknown = H4ContextFixture.Observation(fact: H4ContextFixture.Fact(contextOpen: H4ContextFixture.At(1)));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13, [unknown])));
    }

    [Fact]
    public void DifferentSessionAndAllExpectedIdentityDimensionsAreIgnored()
    {
        var observation = H4ContextFixture.Observation();
        var context = Context(13, [observation]);
        var identities = new[]
        {
            H4ContextFixture.Session(day: new(2026, 10, 4)),
            H4ContextFixture.Session(strategy: new("moneyway-forex")),
            H4ContextFixture.Session(version: new("other")),
            H4ContextFixture.Session(provider: new("other")),
            H4ContextFixture.Session(symbol: new("OTHER")),
        };
        Assert.All(identities, identity => Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(selector.Select(context, identity)));
        var wrongDay = H4ContextFixture.Observation(session: H4ContextFixture.Session(day: new(2026, 10, 4)));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(13, [wrongDay])));
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(selector.Select(Context(13, [wrongDay]), wrongDay.Session));
    }

    [Fact]
    public void CanonicalTransportRejectsForeignInstrumentProviderVersionRatherThanMixingThem()
    {
        Assert.Throws<ArgumentException>(() => Context(13, [H4ContextFixture.Observation(session: H4ContextFixture.Session(provider: new("other")))]));
        Assert.Throws<ArgumentException>(() => Context(13, [H4ContextFixture.Observation(session: H4ContextFixture.Session(symbol: new("OTHER")))]));
        Assert.Throws<ArgumentException>(() => Context(13, [H4ContextFixture.Observation(session: H4ContextFixture.Session(version: new("other")))]));
    }

    [Fact]
    public void SessionDateUsesBogotaNotUtcAndDoesNotCarryIntoAnotherSession()
    {
        var observation = H4ContextFixture.Observation();
        Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(28, [observation]))); // UTC Oct 4, Bogota Oct 3.
        Assert.IsType<NasdaqHumanH4ContextSelection.Missing>(Select(Context(29, [observation]))); // Bogota midnight Oct 4.
    }

    [Fact]
    public void UnresolvedReviewRemainsAnExplicitFactNotPassedOrMissing()
    {
        var fact = H4ContextFixture.Fact(direction: NasdaqHumanH4PermittedDirection.Unresolved, kind: NasdaqHumanH4ContextKind.Unresolved);
        var result = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(Select(Context(13, [H4ContextFixture.Observation(fact: fact)])));
        Assert.Equal(NasdaqHumanH4PermittedDirection.Unresolved, result.Fact.PermittedDirection);
        Assert.Equal(NasdaqHumanH4ContextKind.Unresolved, result.Fact.ContextKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PermutationAndRepeatedCallsPreserveResultAndOriginalObjects(bool conflict)
    {
        var first = H4ContextFixture.Observation(source: "same");
        var second = H4ContextFixture.Observation(source: "same", fact: conflict
            ? H4ContextFixture.Fact(kind: NasdaqHumanH4ContextKind.Wickfill) : H4ContextFixture.Fact());
        var forward = Select(Context(13, [first, second]));
        var reverse = Select(Context(13, [second, first]));
        var repeat = Select(Context(13, [first, second]));
        if (conflict)
        {
            var result = Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(forward);
            Assert.Equal(result.SupportingObservations, Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(reverse).SupportingObservations);
            Assert.Equal(result.SupportingObservations, Assert.IsType<NasdaqHumanH4ContextSelection.Conflict>(repeat).SupportingObservations);
        }
        else
        {
            var result = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(forward);
            var reordered = Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(reverse);
            Assert.Equal(result.Fact, reordered.Fact);
            Assert.Equal(result.SupportingObservations, reordered.SupportingObservations);
            Assert.Equal(result.SupportingObservations, Assert.IsType<NasdaqHumanH4ContextSelection.Unique>(repeat).SupportingObservations);
        }
    }

    [Fact]
    public void NullSelectorInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, H4ContextFixture.Session()));
        Assert.Throws<ArgumentNullException>(() => selector.Select(Context(13), null!));
    }

    private NasdaqHumanH4ContextSelection Select(StrategyReplayContext context) => selector.Select(context, H4ContextFixture.Session());

    private static StrategyReplayContext Context(int hour, IStrategyReplayInputObservation[]? observations = null, bool h4 = true)
    {
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var series = new List<CandleSeries>
        {
            new(H4ContextFixture.Provider, H4ContextFixture.Symbol, minute,
                [new(H4ContextFixture.Provider, H4ContextFixture.Symbol, minute,
                    H4ContextFixture.At(hour).AddMinutes(-1), H4ContextFixture.At(hour), 100, 101, 99, 100, null)]),
        };
        if (h4)
        {
            var timeframe = NasdaqHumanH4ContextObservation.H4;
            series.Add(new(H4ContextFixture.Provider, H4ContextFixture.Symbol, timeframe,
                new[] { 0, 4, 8, 12 }.Select(open => new Candle(H4ContextFixture.Provider, H4ContextFixture.Symbol, timeframe,
                    H4ContextFixture.At(open), H4ContextFixture.At(open + 4), 100, 101, 99, 100, null))));
        }
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc == H4ContextFixture.At(hour))
                return new CreateStrategyReplayContextUseCase().Execute(H4ContextFixture.Definition, frame, observations ?? []);
        }
        throw new InvalidOperationException("Fixture frame missing.");
    }
}

internal static class H4ContextFixture
{
    internal static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    internal static readonly MarketDataProviderId Provider = new("fixture");
    internal static readonly MarketSymbol Symbol = new("NASDAQ");
    internal static DateTimeOffset At(int hour) => new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero).AddHours(hour);

    internal static NasdaqDemoSessionIdentity Session(StrategyId? strategy = null, StrategyVersion? version = null,
        MarketDataProviderId? provider = null, MarketSymbol? symbol = null, DateOnly? day = null) =>
        new(strategy ?? Definition.StrategyId, version ?? Definition.Version, provider ?? Provider, symbol ?? Symbol, day ?? new(2026, 10, 3));

    internal static NasdaqHumanH4StructuralAnchor Anchor(decimal price = 101, DateTimeOffset[]? members = null,
        DateTimeOffset? validation = null, NasdaqHumanH4StructuralRole role = NasdaqHumanH4StructuralRole.HigherHigh) =>
        new(role, price, members ?? [At(0)], validation ?? At(8));

    internal static NasdaqHumanH4ContextFact Fact(NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy,
        NasdaqHumanH4ContextKind kind = NasdaqHumanH4ContextKind.Breakout, DateTimeOffset? contextOpen = null,
        DateTimeOffset? effective = null, NasdaqHumanH4StructuralAnchor[]? anchors = null) =>
        new(direction, kind, contextOpen ?? At(8), effective ?? At(12), anchors ?? [Anchor()]);

    internal static NasdaqHumanH4ContextObservation Observation(int observed = 13, string source = "review:h4",
        NasdaqHumanH4ContextFact? fact = null, NasdaqDemoSessionIdentity? session = null) =>
        new(session ?? Session(), fact ?? Fact(), At(observed), source);
}
