using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

public sealed class NasdaqHumanStructuralLiquidityTests
{
    private readonly NasdaqHumanStructuralLiquidityObservationSelector selector = new();

    [Fact]
    public void NoEvidenceOrUnrelatedPreparationInputIsMissing()
    {
        var preparation = new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId,
            LiquidityFixture.Definition.Version, LiquidityFixture.Provider, LiquidityFixture.Symbol,
            new(2026, 10, 3), LiquidityFixture.At(13), "preparation");
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([])));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([], [preparation])));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void OrdinaryTurnsUseExactCanonicalBodyCoordinateNotWicks(bool upper, int hours)
    {
        var role = upper ? NasdaqHumanH4StructuralRole.LowerHigh : NasdaqHumanH4StructuralRole.HigherLow;
        var a = LiquidityFixture.Candle(0, 100, 150, 50, 110, hours);
        var b = LiquidityFixture.Candle(hours, 105, 140, 40, 95, hours);
        var coordinate = new StructuralTurnBodyCoordinateCalculator().Evaluate([a, b], upper
            ? StructuralTurnBodyCoordinateSide.Upper : StructuralTurnBodyCoordinateSide.Lower).StructuralPrice;
        var reference = NasdaqStructuralLiquidityReference.OrdinaryTurn(role, [b, a], "review:turn", suppliedPrice: coordinate);
        var observation = LiquidityFixture.Observation([reference]);
        var unique = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, b], [observation])));
        Assert.Equal(upper ? 110m : 95m, reference.StructuralPrice);
        Assert.Equal(coordinate, Assert.Single(unique.References).StructuralPrice);
        Assert.Equal(upper ? NasdaqStructuralLiquiditySide.High : NasdaqStructuralLiquiditySide.Low, reference.Side);
        Assert.Equal(NasdaqStructuralLiquidityPriceOwnership.FormulaBacked, reference.PriceOwnership);
        Assert.Equal([a, b], reference.Members);
        Assert.Same(observation, Assert.Single(unique.SupportingObservations));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.OrdinaryTurn(role, [a, b], "wrong", suppliedPrice: coordinate + 0.0000001m));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void ExpansionOriginRequiresActualDirectionalConfirmationAndUsesOpen(bool upper, int hours)
    {
        var candle = LiquidityFixture.Candle(0, 100, 150, 50, upper ? 70 : 130, hours);
        var direction = upper ? StructuralBreakDirection.Lower : StructuralBreakDirection.Upper;
        var validation = new StructuralBodyCloseBreakCalculator().Evaluate(candle, upper ? 80 : 120, direction);
        var role = upper ? NasdaqHumanH4StructuralRole.LowerHigh : NasdaqHumanH4StructuralRole.HigherLow;
        var reference = NasdaqStructuralLiquidityReference.ExpansionOrigin(role, validation, "review:expansion", 100);
        Assert.Equal(100, reference.StructuralPrice);
        Assert.Same(validation, reference.Validation);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([candle], [LiquidityFixture.Observation([reference])])));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.ExpansionOrigin(role, validation, "wrong", 100.0001m));
        var unconfirmed = new StructuralBodyCloseBreakCalculator().Evaluate(candle, upper ? 60 : 140, direction);
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.ExpansionOrigin(role, unconfirmed, "wrong"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Verified007RetainsExactCompletionAndUsesOpenWithDistinctWick(bool upper)
    {
        var (breakout, source) = LiquidityFixture.Breakout(upper, human: false);
        var completed = new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(breakout);
        var reference = NasdaqStructuralLiquidityReference.DirectionalCollision(completed, "review:007", 100);
        var observation = LiquidityFixture.Observation([reference], effective: 32, observed: 33);
        var unique = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context(source, [observation], 33)));
        Assert.Same(completed, reference.DirectionalCompletion);
        Assert.Equal(NasdaqStructuralLiquidityModel.DirectionalCollision007, reference.Model);
        Assert.Equal(100, Assert.Single(unique.References).StructuralPrice);
        Assert.NotEqual(completed.CandidateGeometry.ProtectionAnchor, reference.StructuralPrice);
        Assert.Same(breakout.ValidatingCandle, Assert.Single(reference.Members));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.DirectionalCollision(completed, "wrong", 100.0001m));
        var incompleteSource = source.Where(candle => candle.OpenTimeUtc != breakout.InvalidatingCandle.OpenTimeUtc).ToArray();
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(incompleteSource, [observation], 33)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Completed008RetainsHumanPriceWithoutInventingBodyMembers(bool upper)
    {
        var (completed, source) = LiquidityFixture.HumanCompletion(upper);
        var reference = NasdaqStructuralLiquidityReference.HumanCollision(completed, "review:008");
        var observation = LiquidityFixture.Observation([reference], effective: 40, observed: 41);
        var result = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context(source, [observation], 41)));
        Assert.Equal(111.2345m, Assert.Single(result.References).StructuralPrice);
        Assert.Equal(NasdaqStructuralLiquidityPriceOwnership.HumanDocumented, reference.PriceOwnership);
        Assert.Same(completed, reference.HumanCollisionCompletion);
        Assert.Empty(reference.Members);
        Assert.Contains(completed.Breakout.ValidatingCandle, reference.SourceCandles);
        Assert.NotEqual(completed.CandidateGeometry.ProtectionAnchor, reference.StructuralPrice);
        Assert.Equal(2, reference.HumanCollisionCompletion!.HumanPriceSelection.SupportingObservations.Count);
        Assert.DoesNotContain(reference.StructuralPrice, new[] { completed.Breakout.ValidatingCandle.Open,
            completed.Breakout.ValidatingCandle.Close, completed.Breakout.ValidatingCandle.High, completed.Breakout.ValidatingCandle.Low });
        var premature = LiquidityFixture.Observation([reference], effective: 32, observed: 41);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(source, [premature], 41)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostCompletedHHLLRequiresResolvedFinalClusterAndRetainsFullMembership(bool bearish)
    {
        var (geometry, source) = LiquidityFixture.Extreme(bearish);
        var reference = NasdaqStructuralLiquidityReference.PostCompletionExtreme(geometry, "review:cluster");
        var observation = LiquidityFixture.Observation([reference], effective: 40, observed: 41);
        var result = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context(source, [observation], 41)));
        Assert.Same(geometry, reference.ActiveExtremeGeometry);
        Assert.Equal(geometry.Geometry.StructuralPrice, Assert.Single(result.References).StructuralPrice);
        Assert.Equal(bearish ? NasdaqHumanH4StructuralRole.LowerLow : NasdaqHumanH4StructuralRole.HigherHigh, reference.Role);
        Assert.Equal(geometry.MemberResolution.SelectedMembers, reference.Members);
        Assert.Equal(2, reference.Members.Count);
        Assert.Equal(2, reference.ActiveExtremeGeometry!.MemberResolution.Selection.SupportingObservations.Count);
        Assert.NotEqual(geometry.Geometry.ProtectionAnchor, reference.StructuralPrice);
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.PostCompletionExtreme(geometry, "wrong", reference.StructuralPrice + 0.0001m));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(source.Where(c => c != reference.Members[0]).ToArray(), [observation], 41)));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 4)]
    [InlineData(true, 4)]
    public void HumanEstablishedHHLLHasNoInventedOhlcCoordinateRelation(bool low, int hours)
    {
        var member = LiquidityFixture.Candle(0, hours: hours);
        var reference = NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(low
            ? NasdaqHumanH4StructuralRole.LowerLow : NasdaqHumanH4StructuralRole.HigherHigh, [member], 21500.123m, "mentor:exact point");
        var result = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([member], [LiquidityFixture.Observation([reference])])));
        Assert.Equal(21500.123m, Assert.Single(result.References).StructuralPrice);
        Assert.Equal(NasdaqStructuralLiquidityPriceOwnership.HumanDocumented, reference.PriceOwnership);
        Assert.Equal(low ? NasdaqStructuralLiquiditySide.Low : NasdaqStructuralLiquiditySide.High, reference.Side);
        Assert.Equal(hours, reference.Timeframe.Amount);
    }

    [Fact]
    public void UnsupportedClassesAndTimeframesCannotCreateEvidence()
    {
        Assert.Equal(6, Enum.GetValues<NasdaqStructuralLiquidityModel>().Length);
        var one = LiquidityFixture.Candle(0);
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherHigh, [one], "wrong"));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherLow, [one], 100, "wrong"));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme((NasdaqHumanH4StructuralRole)99, [one], 100, "wrong"));
        var minute = new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, new(5, TimeframeUnit.Minute),
            LiquidityFixture.At(0), LiquidityFixture.At(0).AddMinutes(5), 100, 110, 90, 100, null);
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherHigh, [minute], 100, "wrong"));
        Assert.Null(typeof(NasdaqStructuralLiquidityReference).GetConstructor(Type.EmptyTypes));
        Assert.Empty(typeof(NasdaqStructuralLiquidityReference).GetConstructors()); // No free-form model constructor.
        Assert.Throws<ArgumentNullException>(() => NasdaqStructuralLiquidityReference.HumanCollision(null!, "provisional"));
        Assert.Throws<ArgumentNullException>(() => NasdaqStructuralLiquidityReference.PostCompletionExtreme(null!, "single random candle"));
    }

    [Fact]
    public void InvalidSetsSourcesProvenanceAndTimestampsAreRejected()
    {
        var member = LiquidityFixture.Candle(0);
        var reference = LiquidityFixture.Human(member);
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference, reference]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([null!]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], source: " "));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], source: " untrimmed"));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], effective: 14, observed: 13));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(13), [reference],
            LiquidityFixture.At(12).ToOffset(TimeSpan.FromHours(1)), LiquidityFixture.At(13), "wrong"));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherHigh, [], 100, "naked"));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherHigh, [member, member], 100, "duplicate members"));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherHigh, [member], 100, ""));
        Assert.Throws<ArgumentNullException>(() => selector.Select(null!, LiquidityFixture.Session(13)));
        Assert.Throws<ArgumentNullException>(() => selector.Select(Context([]), null!));
    }

    [Fact]
    public void ReorderedSetsAndMembersPreserveAllCompatibleSupportAndProvenance()
    {
        var a = LiquidityFixture.Candle(0);
        var b = LiquidityFixture.Candle(1);
        var c = LiquidityFixture.Candle(4, hours: 4);
        var firstReference = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [a, b], "source:a");
        var secondReference = LiquidityFixture.Human(c);
        var references = new[] { firstReference, secondReference };
        var first = LiquidityFixture.Observation(references, source: "review:z");
        var same = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [b, a], "source:b");
        Assert.Equal(firstReference, same);
        Assert.Equal(firstReference.GetHashCode(), same.GetHashCode());
        var second = LiquidityFixture.Observation([secondReference, same], observed: 14, source: "review:a");
        references[0] = secondReference; // Input array cannot mutate the stored set.
        var unique = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, b, c], [second, first], 14)));
        var reordered = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, b, c], [first, second], 14)));
        Assert.Equal(2, unique.References.Count);
        Assert.Equal([first, second], unique.SupportingObservations);
        Assert.Equal(unique.SupportingObservations, reordered.SupportingObservations);
        Assert.Contains(firstReference, first.References);
        Assert.Contains(same, second.References);
        Assert.Contains("source:a", first.References.Select(item => item.SourceReference));
        Assert.Contains("source:b", second.References.Select(item => item.SourceReference));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqStructuralLiquidityReference>)first.References).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Candle>)firstReference.Members).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanStructuralLiquidityObservation>)unique.SupportingObservations).Clear());
    }

    [Theory]
    [InlineData("price")]
    [InlineData("class")]
    [InlineData("members")]
    [InlineData("timeframe")]
    [InlineData("effective")]
    [InlineData("model")]
    [InlineData("set")]
    [InlineData("validation")]
    public void ExactSemanticDifferencesConflictAndNoMajoritySupersedes(string difference)
    {
        var a = LiquidityFixture.Candle(0);
        var b = LiquidityFixture.Candle(1);
        var h4 = LiquidityFixture.Candle(0, hours: 4);
        var validating = LiquidityFixture.Candle(2, close: 109);
        var reference = LiquidityFixture.Human(a, price: 110);
        var otherReference = difference switch
        {
            "price" => LiquidityFixture.Human(a, price: 110.0001m),
            "class" => LiquidityFixture.Human(a, low: true, price: 110),
            "members" => LiquidityFixture.Human(b, price: 110),
            "timeframe" => LiquidityFixture.Human(h4, price: 110),
            "model" => NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.LowerHigh, [a], "ordinary"),
            "validation" => NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(NasdaqHumanH4StructuralRole.HigherHigh,
                [a], 110, "with validation", new StructuralBodyCloseBreakCalculator().Evaluate(validating, 108, StructuralBreakDirection.Upper)),
            _ => reference,
        };
        var first = LiquidityFixture.Observation([reference], source: "a");
        var second = LiquidityFixture.Observation(difference == "set" ? [reference, LiquidityFixture.Human(b, price: 111)] : [otherReference],
            effective: difference == "effective" ? 13 : 12, observed: 14, source: "b");
        var duplicate = LiquidityFixture.Observation([reference], observed: 15, source: "c");
        var result = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Conflict>(Select(Context([a, b, h4, validating], [first, second, duplicate], 15)));
        Assert.Equal(3, result.SupportingObservations.Count);
        Assert.Contains(first, result.SupportingObservations);
        Assert.Contains(second, result.SupportingObservations);
        Assert.Null(result.GetType().GetProperty("References"));
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqHumanStructuralLiquidityObservation>)result.SupportingObservations).Clear());
        var reverse = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Conflict>(Select(Context([a, b, h4, validating], [duplicate, second, first], 15)));
        Assert.Equal(result.SupportingObservations, reverse.SupportingObservations);
    }

    [Fact]
    public void DifferentMembersRemainDistinctEvenWhenPricesCoincide()
    {
        var a = LiquidityFixture.Candle(0);
        var b = LiquidityFixture.Candle(1);
        var first = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [a], "a");
        var second = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [b], "b");
        Assert.Equal(first.StructuralPrice, second.StructuralPrice);
        Assert.NotEqual(first, second);
        var unique = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, b], [LiquidityFixture.Observation([first, second])])));
        Assert.Equal(2, unique.References.Count);
    }

    [Fact]
    public void FutureAvailabilityIsInvisibleLateEvidenceDoesNotRewriteEarlierFrames()
    {
        var a = LiquidityFixture.Candle(0);
        var observation = LiquidityFixture.Observation([LiquidityFixture.Human(a)], observed: 15);
        var earlier = Context([a], [observation], 14);
        Assert.Empty(earlier.InputObservations);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(earlier));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [observation], 15)));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(earlier));
    }

    [Fact]
    public void OpenFutureMissingAndFalselyEarlySourceFactsRemainUnusable()
    {
        var future = LiquidityFixture.Candle(12, hours: 4);
        var human = LiquidityFixture.Observation([LiquidityFixture.Human(future)]);
        var formula = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [future], "future")]);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([future], [human, formula])));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([future], [human, formula], 16)));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([], [human])));
        var causal = LiquidityFixture.Observation([LiquidityFixture.Human(future)], effective: 16, observed: 16);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([future], [causal], 16)));
    }

    [Fact]
    public void FormulaUsesResolvedSourceIdentityAndRejectsAlteredOhlc()
    {
        var a = LiquidityFixture.Candle(0);
        var altered = LiquidityFixture.Candle(0, open: 101, close: 101);
        var observation = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [a], "source")]);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([altered], [observation])));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [observation])));
    }

    [Fact]
    public void WrongSessionAndExpectedIdentityAreIgnoredWhileForeignInputsAreRejected()
    {
        var a = LiquidityFixture.Candle(0);
        var reference = LiquidityFixture.Human(a);
        var observation = LiquidityFixture.Observation([reference]);
        var context = Context([a], [observation]);
        var session = observation.Session;
        var wrongs = new[]
        {
            new NasdaqDemoSessionIdentity(session.StrategyId, session.StrategyVersion, session.ProviderId, session.Symbol, session.TradingDay.AddDays(1)),
            new NasdaqDemoSessionIdentity(new("forex"), session.StrategyVersion, session.ProviderId, session.Symbol, session.TradingDay),
            new NasdaqDemoSessionIdentity(session.StrategyId, new("other"), session.ProviderId, session.Symbol, session.TradingDay),
            new NasdaqDemoSessionIdentity(session.StrategyId, session.StrategyVersion, new("other"), session.Symbol, session.TradingDay),
            new NasdaqDemoSessionIdentity(session.StrategyId, session.StrategyVersion, session.ProviderId, new("other"), session.TradingDay),
        };
        Assert.All(wrongs, identity => Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(selector.Select(context, identity)));
        var wrongDay = LiquidityFixture.Observation([reference], session: wrongs[0]);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([a], [wrongDay])));
        var otherVersion = LiquidityFixture.Observation([reference], session: wrongs[2]);
        Assert.Throws<ArgumentException>(() => Context([a], [otherVersion]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], session: wrongs[3]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], session: wrongs[4]));
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], session: wrongs[1]));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(selector.Select(Context([a], [observation], 28), session)); // Bogota Oct 3.
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(selector.Select(Context([a], [observation], 29), session)); // Bogota Oct 4.
    }

    [Fact]
    public void LaterTakeTrajectoryAndUnavailableOtherTimeframeDoNotAffectSelection()
    {
        var a = LiquidityFixture.Candle(0);
        var observation = LiquidityFixture.Observation([LiquidityFixture.Human(a, price: 110)]);
        var taken = LiquidityFixture.Candle(12, high: 200, close: 120);
        var notTaken = LiquidityFixture.Candle(12, high: 105);
        var first = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, taken], [observation], 13)));
        var second = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a, notTaken], [observation], 13)));
        Assert.Equal(first.References, second.References);
        Assert.Equal(first.SupportingObservations, second.SupportingObservations);
        Assert.DoesNotContain(typeof(NasdaqHumanStructuralLiquidityObservation).GetProperties(), p => p.Name is "RuleId" or "Passed" or "TakeResult");
    }

    [Fact]
    public void OrdinaryTurnCannotImportConfirmationOrPostValidationMembers()
    {
        var before = LiquidityFixture.Candle(0);
        var confirming = LiquidityFixture.Candle(1, close: 109);
        var validation = new StructuralBodyCloseBreakCalculator().Evaluate(confirming, 108, StructuralBreakDirection.Upper);
        var valid = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [before], "review", validation);
        Assert.Same(validation, valid.Validation);
        Assert.Contains(confirming, valid.SourceCandles);
        Assert.DoesNotContain(confirming, valid.Members);
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow,
            [before, confirming], "wrong", validation));
        Assert.Throws<ArgumentException>(() => NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow,
            [LiquidityFixture.Candle(2)], "wrong", validation));
    }

    [Fact]
    public void ValidationSourceMustBeObservableByEffectiveTime()
    {
        var before = LiquidityFixture.Candle(0, hours: 4);
        var future = LiquidityFixture.Candle(12, close: 109, hours: 4);
        var validation = new StructuralBodyCloseBreakCalculator().Evaluate(future, 108, StructuralBreakDirection.Upper);
        var reference = NasdaqStructuralLiquidityReference.OrdinaryTurn(NasdaqHumanH4StructuralRole.HigherLow, [before], "review", validation);
        var observation = LiquidityFixture.Observation([reference]);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([before, future], [observation])));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context([before, future], [observation], 16)));
    }

    [Fact]
    public void Later008ConflictQuarantinesRetainedCompletionWithoutRewritingEarlierFrame()
    {
        var (completion, source) = LiquidityFixture.HumanCompletion(false);
        var observation = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.HumanCollision(completion, "selected")], effective: 40, observed: 41);
        var breakout = completion.Breakout;
        var conflict = new NasdaqHumanCollisionStructuralPriceObservation(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc,
            breakout.CandidateSide, 111.2346m, LiquidityFixture.At(42), "conflicting later review");
        var earlier = Context(source, [observation, conflict], 41);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(earlier));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(source, [observation, conflict], 42)));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(earlier));
    }

    [Fact]
    public void LaterClusterConflictCannotReuseThePreviouslyResolvedMembership()
    {
        var (geometry, source) = LiquidityFixture.Extreme(false);
        var observation = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.PostCompletionExtreme(geometry, "selected")], effective: 40, observed: 41);
        var conflictingMembers = new NasdaqHumanPostCompletionActiveExtremeObservation(geometry.MemberResolution.MembershipEvent,
            [LiquidityFixture.At(24)], LiquidityFixture.At(42), "different member set");
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context(source, [observation, conflictingMembers], 41)));
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(source, [observation, conflictingMembers], 42)));
    }

    [Fact]
    public void FinalClusterMustRemainContiguousInTheActualClosedHistory()
    {
        var (geometry, source) = LiquidityFixture.Extreme(false, omitMiddleInResolution: true);
        var observation = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.PostCompletionExtreme(geometry, "selected")], effective: 40, observed: 41);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(source, [observation], 41)));
    }

    [Fact]
    public void H4ModelCannotAttachToAnotherStrategyVersionOrOneHourSeries()
    {
        var (breakout, _) = LiquidityFixture.Breakout(false, false);
        var reference = NasdaqStructuralLiquidityReference.DirectionalCollision(new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(breakout), "selected");
        var session = LiquidityFixture.Session(33);
        var otherVersion = new NasdaqDemoSessionIdentity(session.StrategyId, new("other version"), session.ProviderId, session.Symbol, session.TradingDay);
        Assert.Throws<ArgumentException>(() => LiquidityFixture.Observation([reference], effective: 32, observed: 33, session: otherVersion));
        var wrongSource = reference.SourceCandles.Select(c => new Candle(c.ProviderId, c.Symbol, new(1, TimeframeUnit.Hour),
            c.OpenTimeUtc, c.OpenTimeUtc.AddHours(1), c.Open, c.High, c.Low, c.Close, c.Volume)).ToArray();
        var observation = LiquidityFixture.Observation([reference], effective: 32, observed: 33);
        Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Missing>(Select(Context(wrongSource, [observation], 33)));
    }

    [Fact]
    public void EqualDecimalCoordinatesAndSameTimeSupportOrderAreStable()
    {
        var a = LiquidityFixture.Candle(0);
        var first = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(
            NasdaqHumanH4StructuralRole.HigherHigh, [a], 110.0m, "z")]);
        var second = LiquidityFixture.Observation([NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(
            NasdaqHumanH4StructuralRole.HigherHigh, [a], 110.000m, "a")]);
        Assert.Equal(first.References, second.References);
        var forward = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [first, second])));
        var reverse = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [second, first])));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Equal([second, first], forward.SupportingObservations);
    }

    [Fact]
    public void IdenticalAuditRecordsAreValueEqualButEveryOriginalRecordIsRetained()
    {
        var a = LiquidityFixture.Candle(0);
        var first = LiquidityFixture.Observation([LiquidityFixture.Human(a)]);
        var second = LiquidityFixture.Observation([LiquidityFixture.Human(a)]);
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        var forward = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [first, second])));
        var reverse = Assert.IsType<NasdaqHumanStructuralLiquiditySelection.Unique>(Select(Context([a], [second, first])));
        Assert.Equal(forward.SupportingObservations, reverse.SupportingObservations);
        Assert.Same(first, forward.SupportingObservations[0]);
        Assert.Same(second, forward.SupportingObservations[1]);
    }

    [Fact]
    public void StructuralEvidenceCannotOverrideAsiaLondonExtremaOrMissingSessionData()
    {
        // Full Asia/London source coverage: local Oct 2 17:00 through Oct 3 06:00.
        var source = Enumerable.Range(-2, 14).Select(hour => LiquidityFixture.Candle(hour)).ToArray();
        var observation = LiquidityFixture.Observation([LiquidityFixture.Human(source[0], price: 21500)]);
        var calculator = new NasdaqSessionLiquidityCalculator();
        var baseline = calculator.Calculate(Context(source));
        var withStructuralInput = calculator.Calculate(Context(source, [observation]));
        Assert.True(baseline.IsAvailable);
        Assert.Equal(baseline.Levels, withStructuralInput.Levels);
        var evaluator = new MoneyWayNasdaqSessionLiquidityEvaluator(calculator);
        var withoutEvidence = evaluator.Evaluate(Context(source));
        var withEvidence = evaluator.Evaluate(Context(source, [observation]));
        Assert.Equal((withoutEvidence.Result, withoutEvidence.Reason, withoutEvidence.EvidenceReference),
            (withEvidence.Result, withEvidence.Reason, withEvidence.EvidenceReference));
        var missingSessionSource = source.Skip(1).ToArray();
        Assert.Equal(RuleEvaluationResult.DataUnavailable, evaluator.Evaluate(Context(missingSessionSource, [observation])).Result);
    }

    private NasdaqHumanStructuralLiquiditySelection Select(StrategyReplayContext context) => selector.Select(context, LiquidityFixture.Session(context.AsOfUtc));
    private static StrategyReplayContext Context(Candle[] source, IStrategyReplayInputObservation[]? observations = null, int hour = 13) =>
        LiquidityFixture.Context(source, observations ?? [], hour);
}
