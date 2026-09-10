using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Observability;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay.Observability;

public sealed class AssessReplayTemporalOrderingObservabilityUseCaseTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly MarketDataProviderId Provider = new("fixture");
    private static readonly MarketSymbol Symbol = new("DEMO");
    private static readonly RuleId Rule = new("R-1");
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DifferentObservationWindowsAreSufficientAndDeterministic()
    {
        var definition = Definition();
        var context = Context(definition, Series(1, 2), frameIndex: 1);
        var first = Evidence("condition-a", Start, Start.AddMinutes(1));
        var second = Evidence("condition-b", Start.AddMinutes(2), Start.AddMinutes(2));
        var useCase = new AssessReplayTemporalOrderingObservabilityUseCase();

        var left = useCase.Execute(definition, context, Rule, first, second);
        var right = useCase.Execute(definition, context, Rule, first, second);

        Assert.Equal(left, right);
        Assert.Equal(ReplayMarketDataObservabilityStatus.Sufficient, left.Status);
        Assert.Equal(AssessReplayTemporalOrderingObservabilityUseCase.SufficientReason, left.Reason);
        AssertIdentity(left, context);
    }

    [Fact]
    public void SameObservationWindowsRemainResolutionInsufficientWithoutGuessingOrder()
    {
        var definition = Definition();
        var context = Context(definition, Series(1));
        var interval = Evidence("condition-a", Start, Start.AddMinutes(1));
        var sameInterval = Evidence("condition-b", Start, Start.AddMinutes(1));

        var result = new AssessReplayTemporalOrderingObservabilityUseCase()
            .Execute(definition, context, Rule, interval, sameInterval);

        Assert.Equal(ReplayMarketDataObservabilityStatus.ResolutionInsufficient, result.Status);
        Assert.Equal(AssessReplayTemporalOrderingObservabilityUseCase.ResolutionInsufficientReason, result.Reason);
        Assert.DoesNotContain("first", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("second", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FutureSeriesDoesNotChangeAssessmentAtSameReplayBoundaryAndFutureEvidenceIsRejected()
    {
        var definition = Definition();
        var shortContext = Context(definition, Series(1));
        var extendedContext = Context(definition, Series(1, 2, 3));
        var first = Evidence("condition-a", Start, Start.AddMinutes(1));
        var second = Evidence("condition-b", Start, Start.AddMinutes(1));
        var useCase = new AssessReplayTemporalOrderingObservabilityUseCase();

        var shortResult = useCase.Execute(definition, shortContext, Rule, first, second);
        var extendedResult = useCase.Execute(definition, extendedContext, Rule, first, second);

        Assert.Equal(shortResult, extendedResult);
        var future = Evidence("future", Start.AddMinutes(2), Start.AddMinutes(2));
        Assert.Throws<ArgumentException>(() => useCase.Execute(definition, shortContext, Rule, first, future));
    }

    [Fact]
    public void EvidenceAndAssessmentRejectInvalidIdentityAndTemporalBounds()
    {
        Assert.Throws<ArgumentException>(() => Evidence(" ", Start, Start));
        Assert.Throws<ArgumentException>(() => Evidence("condition", Start.AddMinutes(1), Start));
        Assert.Throws<ArgumentException>(() => Evidence("condition", Start.ToOffset(TimeSpan.FromHours(-5)), Start));

        var definition = Definition();
        var context = Context(definition, Series(1));
        var useCase = new AssessReplayTemporalOrderingObservabilityUseCase();
        var first = Evidence("condition-a", Start, Start);
        var second = Evidence("condition-b", Start.AddMinutes(1), Start.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(
            new(new("other"), Version, "Other", "test", definition.Rules),
            context,
            Rule,
            first,
            second));
        Assert.Throws<InvalidOperationException>(() => useCase.Execute(definition, context, new("UNKNOWN"), first, second));
    }

    private static void AssertIdentity(ReplayMarketDataObservabilityAssessment assessment, StrategyReplayContext context)
    {
        Assert.Equal(context.StrategyId, assessment.StrategyId);
        Assert.Equal(context.StrategyVersion, assessment.StrategyVersion);
        Assert.Equal(context.ProviderId, assessment.ProviderId);
        Assert.Equal(context.Symbol, assessment.Symbol);
        Assert.Equal(Rule, assessment.RuleId);
        Assert.Equal(context.Step, assessment.Step);
        Assert.Equal(context.AsOfUtc, assessment.AsOfUtc);
    }

    private static StrategyDefinition Definition() => new(
        Strategy,
        Version,
        "Synthetic",
        "test",
        [new(Rule, "Rule", "stage", 10, true, RuleDefinitionStatus.Confirmed, "description", "source")]);

    private static StrategyReplayContext Context(
        StrategyDefinition definition,
        CandleSeries series,
        int frameIndex = 0)
    {
        var cursor = new MultiTimeframeCandleReplayCursor([series]);
        MultiTimeframeReplayFrame? frame = null;
        for (var index = 0; index <= frameIndex; index++)
        {
            Assert.True(cursor.TryAdvance(out frame));
        }

        return new CreateStrategyReplayContextUseCase().Execute(definition, frame!);
    }

    private static ReplayTemporalEvidenceWindow Evidence(string id, DateTimeOffset earliest, DateTimeOffset latest) => new(id, earliest, latest);

    private static CandleSeries Series(params int[] closes) => new(
        Provider,
        Symbol,
        Minute,
        closes.Select(close => new Candle(
            Provider,
            Symbol,
            Minute,
            Start.AddMinutes(close - 1),
            Start.AddMinutes(close),
            100,
            101,
            99,
            100,
            null)));
}
