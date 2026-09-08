using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayEvaluators;

public sealed class MoneyWayNasdaqTradingWindowEndEvaluatorTests
{
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("NQ");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);
    private readonly MoneyWayNasdaqTradingWindowEndEvaluator evaluator = new();

    [Fact]
    public void IdentityExactlyMatchesBuiltInRuleDefinition()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var rule = definition.Rules.Single(item => item.RuleId.Value == "NQ-TIME-002");

        Assert.Equal(definition.StrategyId, evaluator.StrategyId);
        Assert.Equal(definition.Version, evaluator.StrategyVersion);
        Assert.Equal(rule.RuleId, evaluator.RuleId);
        Assert.Equal(("Trading-window end", "Schedule", 260, true, RuleDefinitionStatus.Confirmed, "docs/strategies/nasdaq/rule-catalog.md"),
            (rule.Name, rule.Stage, rule.Sequence, rule.IsRequired, rule.DefinitionStatus, rule.SourceReference));
    }

    [Theory]
    [InlineData(2026, 1, 15, 12, 30, 0, RuleEvaluationResult.Passed)]
    [InlineData(2026, 1, 15, 13, 0, 0, RuleEvaluationResult.Passed)]
    [InlineData(2026, 1, 15, 13, 29, 59, RuleEvaluationResult.Passed)]
    [InlineData(2026, 1, 15, 16, 29, 59, RuleEvaluationResult.Passed)]
    [InlineData(2026, 1, 15, 16, 30, 0, RuleEvaluationResult.Passed)]
    [InlineData(2026, 1, 15, 16, 30, 1, RuleEvaluationResult.Failed)]
    [InlineData(2026, 1, 15, 17, 0, 0, RuleEvaluationResult.Failed)]
    [InlineData(2026, 7, 15, 16, 30, 0, RuleEvaluationResult.Passed)]
    [InlineData(2026, 7, 16, 16, 30, 1, RuleEvaluationResult.Failed)]
    public void ConvertsUtcToBogotaAndAppliesInclusiveEndBoundary(
        int year, int month, int day, int hour, int minute, int second, RuleEvaluationResult expected)
    {
        var decision = evaluator.Evaluate(Context(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero)));

        Assert.Equal(expected, decision.Result);
        Assert.Equal(expected == RuleEvaluationResult.Failed
                ? "The replay context is after the 11:30 America/Bogota trading-window end."
                : "The replay context is not after the 11:30 America/Bogota trading-window end.",
            decision.Reason);
        Assert.Null(decision.EvidenceReference);
    }

    [Fact]
    public void DecisionDependsOnlyOnAsOfUtcAndIgnoresMarketDataAndFutureCandles()
    {
        var at = new DateTimeOffset(2026, 3, 10, 16, 30, 1, TimeSpan.Zero);
        var first = Context(at, Minute, 100, 101, provider: new("fixture-a"), symbol: new("NQ-A"));
        var second = Context(at, FiveMinutes, 500, 50, provider: new("fixture-b"), symbol: new("NQ-B"));

        var firstDecision = evaluator.Evaluate(first);
        var secondDecision = evaluator.Evaluate(second);

        Assert.Equal(firstDecision.Result, secondDecision.Result);
        Assert.Equal(firstDecision.Reason, secondDecision.Reason);
        Assert.Equal(firstDecision.EvidenceReference, secondDecision.EvidenceReference);
    }

    [Fact]
    public void DecisionAtTDoesNotDependOnFutureCandles()
    {
        var at = new DateTimeOffset(2026, 4, 20, 16, 30, 0, TimeSpan.Zero);
        var first = Context(at, Minute, 100, 101);
        var second = Context(at, Minute, 100, 999);

        var firstDecision = evaluator.Evaluate(first);
        var secondDecision = evaluator.Evaluate(second);

        Assert.Equal(firstDecision.Result, secondDecision.Result);
        Assert.Equal(firstDecision.Reason, secondDecision.Reason);
        Assert.Equal(firstDecision.EvidenceReference, secondDecision.EvidenceReference);
    }

    [Fact]
    public void EvaluationIsRepeatableAndIndependentOfCallOrder()
    {
        var passed = Context(new DateTimeOffset(2026, 2, 1, 15, 0, 0, TimeSpan.Zero));
        var failed = Context(new DateTimeOffset(2026, 2, 1, 17, 0, 0, TimeSpan.Zero));

        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(failed).Result);
        Assert.Equal(RuleEvaluationResult.Passed, evaluator.Evaluate(passed).Result);
        Assert.Equal(RuleEvaluationResult.Passed, evaluator.Evaluate(passed).Result);
        Assert.Equal(RuleEvaluationResult.Failed, evaluator.Evaluate(failed).Result);
    }

    [Fact]
    public void RejectsNullOrMismatchedStrategyContext()
    {
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate(null!));
        var context = Context(new DateTimeOffset(2026, 1, 15, 16, 30, 0, TimeSpan.Zero), definition: new(
            new("other-strategy"), new("v1"), "Other", "test",
            [new(new("OTHER-001"), "Other", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]));
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(context));
    }

    private static StrategyReplayContext Context(
        DateTimeOffset asOfUtc,
        Timeframe? timeframe = null,
        decimal close = 100,
        decimal futureClose = 200,
        StrategyDefinition? definition = null,
        MarketDataProviderId? provider = null,
        MarketSymbol? symbol = null)
    {
        timeframe ??= Minute;
        definition ??= MoneyWayNasdaqStrategyDefinition.Instance;
        provider ??= Provider;
        symbol ??= Symbol;
        var series = new CandleSeries(provider, symbol, timeframe,
        [
            Candle(provider, symbol, timeframe, asOfUtc.AddMinutes(-1), asOfUtc, close),
            Candle(provider, symbol, timeframe, asOfUtc, asOfUtc.AddMinutes(1), futureClose),
        ]);
        StrategyReplayContext? context = null;
        new RunMultiTimeframeReplayUseCase().Execute([series], frame =>
        {
            if (context is null)
                context = new CreateStrategyReplayContextUseCase().Execute(definition, frame);
        });
        return context!;
    }

    private static Candle Candle(
        MarketDataProviderId provider,
        MarketSymbol symbol,
        Timeframe timeframe,
        DateTimeOffset open,
        DateTimeOffset close,
        decimal value) =>
        new(provider, symbol, timeframe, open, close, value, value + 1, value - 1, value, null);
}
