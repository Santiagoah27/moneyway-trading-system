using System.Reflection;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class NasdaqTimeCutoffEvidenceTests
{
    private static readonly DateTimeOffset Cutoff = M5Fixture.At(16);
    private const string CutoffReason = "The canonical TIME-owned entry-acquisition cutoff has been reached.";

    [Fact]
    public void RepeatedCanonicalValuesHaveConstantEvidenceSizeAndKeepOwners()
    {
        var small = Context(1, Cutoff);
        var large = Context(1000, Cutoff);
        var original = large.PriorObservations.SelectMany(o => o.RuleFacts).ToArray();
        var expected = JsonSerializer.Serialize(new[] { original.Last().Fact as NasdaqTradingWindowFact });

        foreach (var evaluator in Evaluators())
        {
            var decision = evaluator.Evaluate(large);
            Assert.Equal(RuleEvaluationResult.Failed, decision.Result);
            Assert.Equal(CutoffReason, decision.Reason);
            Assert.Equal(expected, decision.EvidenceReference);
            Assert.Equal(evaluator.Evaluate(small).EvidenceReference, decision.EvidenceReference);
            Assert.Single(Windows(decision));
        }
        var retained = large.PriorObservations.SelectMany(o => o.RuleFacts).ToArray();
        Assert.Equal(2000, retained.Length);
        for (var i = 0; i < original.Length; i++) Assert.Same(original[i], retained[i]);
        Assert.All(large.PriorObservations, o => Assert.Equal(new[] { "NQ-TIME-001", "NQ-TIME-002" }, o.RuleFacts.Select(f => f.RuleId.Value)));
    }

    [Fact]
    public void NonconsecutiveDuplicatesKeepDistinctValuesInFirstOccurrenceOrder()
    {
        var bogota = Window(Cutoff, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));
        var utc = Window(Cutoff, TimeZoneInfo.Utc);
        var third = Window(Cutoff, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        // Exercise the existing fact constructor's boundary contract, not alternative runtime
        // TIME configuration. Production TIME owners remain fixed to America/Bogota.
        NasdaqTradingWindowFact[] values = [utc, bogota, utc, third, bogota, third];
        var context = Context(values.Length, Cutoff, i => values[i]);
        var expected = JsonSerializer.Serialize(new[] { utc, bogota, third });

        foreach (var evaluator in Evaluators())
        {
            var decision = evaluator.Evaluate(context);
            Assert.Equal(expected, decision.EvidenceReference);
            Assert.Equal(3, Windows(decision).Length);
            Assert.Equal(expected, evaluator.Evaluate(context).EvidenceReference);
        }
        Assert.Equal(JsonSerializer.Serialize(new[] { utc, bogota }), Evaluators()[0].Evaluate(Context(2, Cutoff, i => values[i])).EvidenceReference);
    }

    [Fact]
    public void SingleWindowHasExactlyTheOriginalSerializedContents()
    {
        var context = Context(1, Cutoff);
        var window = Assert.IsType<NasdaqTradingWindowFact>(context.PriorObservations.Single().RuleFacts.First().Fact);
        foreach (var evaluator in Evaluators())
            Assert.Equal(JsonSerializer.Serialize(new[] { window }), evaluator.Evaluate(context).EvidenceReference);
    }

    [Fact]
    public void EmptyHistoryStillWaitsForItsCanonicalPrerequisite()
    {
        foreach (var evaluator in Evaluators())
        {
            var decision = evaluator.Evaluate(Context(0, Cutoff));
            Assert.Equal(RuleEvaluationResult.Waiting, decision.Result);
            Assert.Equal("The exact canonical prerequisite has not established eligible progression.", decision.Reason);
            Assert.Null(decision.EvidenceReference);
        }
    }

    [Fact]
    public void MultipleDaysRetainOnlyCurrentSessionWindows()
    {
        var context = Context(1800, Cutoff);
        Assert.True(context.PriorObservations.SelectMany(o => o.RuleFacts).Select(f => (NasdaqTradingWindowFact)f.Fact).Select(w => w.DayStartUtc).Distinct().Count() > 1);
        var expected = JsonSerializer.Serialize(new[] { context.PriorObservations.Last().RuleFacts.Last().Fact as NasdaqTradingWindowFact });
        foreach (var evaluator in Evaluators()) Assert.Equal(expected, evaluator.Evaluate(context).EvidenceReference);
    }

    [Theory]
    [InlineData(-1, RuleEvaluationResult.Waiting, RuleEvaluationResult.Passed)]
    [InlineData(0, RuleEvaluationResult.Failed, RuleEvaluationResult.Failed)]
    [InlineData(1, RuleEvaluationResult.Failed, RuleEvaluationResult.Failed)]
    public void ExclusiveCutoffAndTimeOwnerDecisionsRemainUnchanged(int minutes, RuleEvaluationResult fvgResult, RuleEvaluationResult timeResult)
    {
        var context = Context(10, Cutoff.AddMinutes(minutes));
        var time = new MoneyWayNasdaqTradingWindowEndEvaluator().Evaluate(context);
        Assert.Equal(timeResult, time.Result);
        Assert.Equal(RuleEvaluationResult.Passed, new MoneyWayNasdaqTradingWindowStartEvaluator().Evaluate(context).Result);
        foreach (var evaluator in Evaluators())
        {
            var decision = evaluator.Evaluate(context);
            Assert.Equal(fvgResult, decision.Result);
            if (minutes >= 0) Assert.Equal(CutoffReason, decision.Reason);
            else Assert.Null(decision.EvidenceReference);
        }
    }

    [Fact]
    public void FutureHistoryCannotEnterOrRewriteAnEarlierProjection()
    {
        var earlier = Context(5, Cutoff);
        var evaluator = Evaluators()[0];
        var evidence = evaluator.Evaluate(earlier).EvidenceReference;
        var later = Context(5, Cutoff.AddDays(1));
        evaluator.Evaluate(later);
        Assert.Throws<ArgumentException>(() => earlier.WithPriorObservations(earlier.PriorObservations.Concat(later.PriorObservations)));
        Assert.Equal(evidence, evaluator.Evaluate(earlier).EvidenceReference);
        Assert.Equal(evidence, evaluator.Evaluate(Context(5, Cutoff)).EvidenceReference);
    }

    private static JsonElement[] Windows(ReplayRuleEvaluationDecision decision) => JsonSerializer.Deserialize<JsonElement[]>(decision.EvidenceReference!)!;

    private static IReplayRuleEvaluator[] Evaluators() => [new MoneyWayNasdaqHumanM5FvgEvaluator(), new MoneyWayNasdaqHumanM5FvgQualityEvaluator()];

    private static StrategyReplayContext Context(int count, DateTimeOffset at, Func<int, NasdaqTradingWindowFact>? window = null)
    {
        var definition = LiquidityFixture.Definition;
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        var candles = Enumerable.Range(0, count + 1).Select(i =>
        {
            var close = at.AddMinutes(i - count);
            return new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, minute, close.AddMinutes(-1), close, 100, 110, 90, 100, null);
        });
        var cursor = new MultiTimeframeCandleReplayCursor([new(LiquidityFixture.Provider, LiquidityFixture.Symbol, minute, candles)]);
        var history = new List<StrategyReplayContextObservation>();
        var owners = new EvaluateStrategyReplayContextUseCase([new MoneyWayNasdaqTradingWindowStartEvaluator(), new MoneyWayNasdaqTradingWindowEndEvaluator()]);
        while (cursor.TryAdvance(out var frame))
        {
            var context = new CreateStrategyReplayContextUseCase().Execute(definition, frame!);
            if (context.AsOfUtc == at) return context.WithPriorObservations(history);
            var observation = owners.Execute(definition, context);
            if (window is not null)
            {
                var value = window(history.Count);
                var constructor = typeof(StrategyReplayRuleFact).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(c => c.GetParameters().Length == 2);
                observation = new(observation.StrategyId, observation.StrategyVersion, observation.ProviderId, observation.Symbol,
                    observation.Step, observation.AsOfUtc, observation.Evaluations,
                    ruleFacts: observation.RuleFacts.Select(f => (StrategyReplayRuleFact)constructor.Invoke([f.RuleId, value])));
            }
            history.Add(observation);
        }
        throw new InvalidOperationException("Fixture missing final frame.");
    }

    private static NasdaqTradingWindowFact Window(DateTimeOffset at, TimeZoneInfo zone) =>
        (NasdaqTradingWindowFact)typeof(NasdaqTradingWindowFact).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(c => c.GetParameters().Length == 2).Invoke([at, zone]);
}
