using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class NasdaqM5StrictCloseCandidateTests
{
    [Theory]
    [InlineData(true, 99, 100, 101, 98)]
    [InlineData(true, 100, 100, 101, 99)]
    [InlineData(false, 101, 100, 102, 99)]
    [InlineData(false, 100, 100, 101, 99)]
    public void ExactRequestedWickAndEqualityFixturesNeverConfirm(bool buy, int close, int open, int high, int low)
    {
        var f = new M5CandidateFixture(buy, close, candleOpen: open, high: high, low: low);
        Assert.Equal(RuleEvaluationResult.Waiting, M5CandidateFixture.Result(f.Run(f.Inputs.Append(f.Swing())), 10));
    }
    [Fact]
    public void CompatibleSwingReviewsRetainEverySourceAndMissingM5FrameIsUnavailable()
    {
        var f = new M5CandidateFixture(); var first = f.Swing();
        var second = new NasdaqValidatedM5SwingObservation(f.Take, first.ReferenceId, first.Side, first.Price, first.PriceUnit, first.SourceCandles, first.EffectiveAtUtc,
            M5CandidateFixture.At(7), "synthetic:independent compatible selection review");
        var report = f.Run(f.Inputs.Concat([first, second]));
        var fact = (NasdaqHumanM5TriggerRuleFact)M5CandidateFixture.Frame(report, 10).RuleFacts.Single(r => r.RuleId.Value == "NQ-M5-001").Fact;
        Assert.Equal(2, fact.Selection.Fact.SwingSelections!.Count);
        var take = (NasdaqLiquidityTakeRuleFact)M5CandidateFixture.Frame(report, 5).RuleFacts.Single(r => r.RuleId.Value == "NQ-LIQ-003").Fact;
        var context = f.Context(M5CandidateFixture.At(10), f.Inputs.Append(first), f.Series.Where(s => s.Timeframe != NasdaqHumanM5TriggerEvent.M5).ToArray());
        Assert.Equal(RuleEvaluationResult.DataUnavailable, new NasdaqM5StrictCloseDetector().Evaluate(context, take).UnconfirmedResult);
    }
    [Theory]
    [InlineData(true, "99", false)]
    [InlineData(true, "100", false)]
    [InlineData(true, "100.01", true)]
    [InlineData(true, "105", true)]
    [InlineData(false, "101", false)]
    [InlineData(false, "100", false)]
    [InlineData(false, "99.99", true)]
    [InlineData(false, "95", true)]
    [InlineData(true, "100.00000000000000000000000001", true)]
    [InlineData(false, "99.99999999999999999999999999", true)]
    public void CanonicalStrictCloseUsesExactDecimalWithoutColorOrSizeThreshold(bool buy, string close, bool qualifies)
    {
        var f = new M5CandidateFixture(buy, decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture));
        var report = f.Run(f.Inputs.Append(f.Swing()));
        Assert.Equal(qualifies ? RuleEvaluationResult.Passed : RuleEvaluationResult.Waiting, M5CandidateFixture.Result(report, 10));
        Assert.Equal(0, report.ReadyCount);
        Assert.NotEqual(RuleEvaluationResult.Passed, M5CandidateFixture.Result(report, 10, "NQ-M1-001"));
        if (!qualifies) return;
        var fact = Assert.IsType<NasdaqHumanM5TriggerRuleFact>(M5CandidateFixture.Frame(report, 10).RuleFacts.Single(r => r.RuleId.Value == "NQ-M5-001").Fact);
        Assert.Equal("DeterministicStrictClose", fact.Selection.Fact.Origin); Assert.NotNull(fact.Selection.Fact.SwingSelections);
        Assert.Equal(f.Five(10).CloseTimeUtc, fact.Selection.Fact.EffectiveAtUtc);
        Assert.Contains(M5CandidateFixture.Frame(report, 15).WorkflowProgression!.RuleEligibility, r => r.RuleId.Value == "NQ-M1-001" && r.IsEligible);
    }
    [Fact]
    public void BaselineStillRequiresHumanScWhileCandidateConsumesOnlyTheValidatedSwing()
    {
        var before = new M5CandidateFixture(baseline: true); var after = new M5CandidateFixture();
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(before.Run(before.Inputs.Append(before.Swing())), 10));
        Assert.Equal(RuleEvaluationResult.Passed, M5CandidateFixture.Result(after.Run(after.Inputs.Append(after.Swing())), 10));
        Assert.Null(new StrategyDefinitionCatalog().Find(after.Definition.StrategyId, after.Definition.Version));
        Assert.DoesNotContain(MoneyWayReplayRuleEvaluators.GetAll(), e => e.StrategyVersion == after.Definition.Version);
        var definitions = before.Definition.Rules.Zip(after.Definition.Rules);
        Assert.All(definitions, pair => Assert.Equal((pair.First.RuleId, pair.First.Sequence, pair.First.IsRequired, pair.First.DefinitionStatus), (pair.Second.RuleId, pair.Second.Sequence, pair.Second.IsRequired, pair.Second.DefinitionStatus)));
        Assert.Equal(13, after.Definition.Rules.Count(r => r.IsRequired));
        Assert.All(after.Definition.Rules.Where(r => r.IsRequired), r => Assert.Contains(MoneyWayNasdaqM5StrictCloseCandidate.Evaluators(), e => e.RuleId == r.RuleId));
    }
    [Fact]
    public void MissingUnvalidatedConflictingAndWrongDirectionReferencesNeverPass()
    {
        var f = new M5CandidateFixture();
        foreach (var refs in new NasdaqValidatedM5SwingObservation[][] { [], [f.Swing(validation: NasdaqM5SwingValidation.AwaitingHumanValidation)], [f.Swing(side: NasdaqStructuralLiquiditySide.Low)], [f.Swing(), f.Swing(101)] })
            Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(f.Run(f.Inputs.Concat(refs)), 10));
    }
    [Fact]
    public void MissingPrerequisiteAndUnavailableNamedSwingSourceFailClosed()
    {
        var f = new M5CandidateFixture(); var inputs = f.Inputs.Append(f.Swing()).ToArray();
        Assert.NotEqual(RuleEvaluationResult.Passed, M5CandidateFixture.Result(f.Run(inputs.Where(i => i is not NasdaqPreparationCompletionObservation)), 10));
        var series = f.Series.Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Where(c => !(c.Timeframe == NasdaqHumanM5TriggerEvent.M5 && c.CloseTimeUtc == M5CandidateFixture.At(0))))).ToArray();
        Assert.Equal(RuleEvaluationResult.DataUnavailable, M5CandidateFixture.Result(f.Run(inputs, series), 10));
    }
    [Fact]
    public void LaterReferenceCannotConfirmTheEarlierBreakAndRepeatedPrefixesAreImmutable()
    {
        var f = new M5CandidateFixture(); var inputs = f.Inputs.Append(f.Swing(observed: M5CandidateFixture.At(11))).ToArray();
        var full = f.Run(inputs); Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(full, 10));
        Assert.Equal(RuleEvaluationResult.Waiting, M5CandidateFixture.Result(full, 15)); // The newly closed 14:15 candle equals the level, never rescan 14:10.
        var shortSeries = f.Series.Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Where(c => c.CloseTimeUtc <= M5CandidateFixture.At(10)))).ToArray();
        var prefix = f.Run(inputs, shortSeries); var again = f.Run(inputs);
        Assert.Equal(JsonSerializer.Serialize(M5CandidateFixture.Frame(prefix, 10)), JsonSerializer.Serialize(M5CandidateFixture.Frame(full, 10)));
        Assert.Equal(JsonSerializer.Serialize(full.Frames), JsonSerializer.Serialize(again.Frames));
    }
    [Theory]
    [InlineData(5, RuleEvaluationResult.Passed)]
    [InlineData(10, RuleEvaluationResult.HumanValidationRequired)]
    [InlineData(15, RuleEvaluationResult.Passed)]
    public void EarlierIfvgLaterIfvgAndEqualTimeConflictPreserveFirstConfirmation(int ifvgMinute, RuleEvaluationResult result)
    {
        var f = new M5CandidateFixture(); var report = f.Run(f.Inputs.Concat([f.Swing(), f.Ifvg(ifvgMinute)]));
        Assert.Equal(result, M5CandidateFixture.Result(report, 10));
        if (ifvgMinute == 10) { Assert.Equal(result, M5CandidateFixture.Result(report, 15)); Assert.Equal(result, M5CandidateFixture.Result(report, 30)); return; }
        var fact = (NasdaqHumanM5TriggerRuleFact)M5CandidateFixture.Frame(report, 15).RuleFacts.Single(r => r.RuleId.Value == "NQ-M5-001").Fact;
        Assert.Equal(ifvgMinute == 5 ? NasdaqHumanM5TriggerKind.Ifvg : NasdaqHumanM5TriggerKind.StructuralChange, fact.Selection.Fact.Event.Kind);
    }
    [Fact]
    public void Wf2ExclusionAndUnfinishedM5CannotTriggerStandardSc()
    {
        var f = new M5CandidateFixture(buy: false, close: 99.99m, path: NasdaqStrategyPath.WickfillScenario2);
        Assert.Equal(RuleEvaluationResult.NotApplicable, M5CandidateFixture.Result(f.Run(f.Inputs.Append(f.Swing())), 10));
        var standard = new M5CandidateFixture();
        var atTake = M5CandidateFixture.Frame(standard.Run(), 5);
        var take = (NasdaqLiquidityTakeRuleFact)atTake.RuleFacts.Single(r => r.RuleId.Value == "NQ-LIQ-003").Fact;
        var one = new Timeframe(1, TimeframeUnit.Minute);
        var series = standard.Series.Select(s => s.Timeframe != one ? s : new CandleSeries(s.ProviderId, s.Symbol, one,
            s.Candles.Append(new(s.ProviderId, s.Symbol, one, M5CandidateFixture.At(10), M5CandidateFixture.At(11), 100, 110, 90, 105, null)).OrderBy(c => c.OpenTimeUtc))).ToArray();
        var detection = new NasdaqM5StrictCloseDetector().Evaluate(standard.Context(M5CandidateFixture.At(11), standard.Inputs.Append(standard.Swing()), series), take);
        Assert.Null(detection.Observation);
        Assert.Equal(RuleEvaluationResult.Waiting, detection.UnconfirmedResult);
    }
    [Fact]
    public void CompatibleHumanSameCandleScAndIfvgStillWorkButHumanEqualityCannotOverrideTheCandidate()
    {
        var f = new M5CandidateFixture();
        var same = new NasdaqHumanM5TriggerObservation(f.Take, new NasdaqHumanM5TriggerEvent.StructuralChange(f.Five(5), f.Direction, [f.Five(0)], [f.Five(0)], 99),
            M5CandidateFixture.At(5), "synthetic:legacy same-candle review", NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation);
        Assert.Equal(RuleEvaluationResult.Passed, M5CandidateFixture.Result(f.Run(f.Inputs.Append(same)), 10));
        Assert.Equal(RuleEvaluationResult.Passed, M5CandidateFixture.Result(f.Run(f.Inputs.Append(f.Ifvg(10))), 10));
        var equal = new NasdaqHumanM5TriggerObservation(f.Take, new NasdaqHumanM5TriggerEvent.StructuralChange(f.Five(10), f.Direction, [f.Five(0)], [f.Five(0)], f.Five(10).Close),
            M5CandidateFixture.At(10), "synthetic:nonqualifying legacy claim");
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(f.Run(f.Inputs.Append(equal)), 10));
    }
    [Fact]
    public void RetainedAutomaticEvidenceStaysBoundedAndAReferenceForAnotherTakeCannotBeBorrowed()
    {
        var f = new M5CandidateFixture(); var report = f.Run(f.Inputs.Append(f.Swing()));
        var fact = (NasdaqHumanM5TriggerRuleFact)M5CandidateFixture.Frame(report, 30).RuleFacts.Single(r => r.RuleId.Value == "NQ-M5-001").Fact;
        Assert.Single(fact.Selection.SupportingObservations);
        var other = new NasdaqHumanLiquidityTakeObservation(f.Take.Reference, new NasdaqHumanLiquidityTakeEvent.Documented(f.Take.ProviderId, f.Take.Symbol,
            "synthetic:different decisive take", 89, M5CandidateFixture.At(5), "synthetic:other source", [f.Five(5)]), f.Take.ReferenceEligibleAtUtc, f.Take.ObservedAtUtc, "synthetic:other take");
        var unrelated = new NasdaqValidatedM5SwingObservation(other, "synthetic:other swing", NasdaqStructuralLiquiditySide.High, 100, "NativeInstrumentPrice", [f.Five(0)], M5CandidateFixture.At(0), M5CandidateFixture.At(6), "synthetic:other reference");
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, M5CandidateFixture.Result(f.Run(f.Inputs.Append(unrelated)), 10));
    }
    [Theory]
    [InlineData("unit")]
    [InlineData("timeframe")]
    [InlineData("instrument")]
    [InlineData("time")]
    public void InvalidReferenceContractsAreRejected(string kind)
    {
        var f = new M5CandidateFixture(); var c = f.Five(0);
        if (kind == "timeframe") c = new(c.ProviderId, c.Symbol, new(1, TimeframeUnit.Minute), c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, null);
        if (kind == "instrument") c = new(c.ProviderId, new("USTEC"), c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, c.Open, c.High, c.Low, c.Close, null);
        Assert.Throws<ArgumentException>(() => new NasdaqValidatedM5SwingObservation(f.Take, "synthetic:swing", NasdaqStructuralLiquiditySide.High, 100,
            kind == "unit" ? "OtherInstrumentPoints" : "NativeInstrumentPrice", [c], M5CandidateFixture.At(0), kind == "time" ? M5CandidateFixture.At(-1) : M5CandidateFixture.At(6), "synthetic:source"));
    }
}
