using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyReplay;

public sealed class StrategyReplayTests
{
    private static readonly StrategyId Strategy = new("synthetic");
    private static readonly StrategyVersion Version = new("v1");
    private static readonly RuleId Rule = new("R-1");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    public void DecisionRejectsInvalidReason(string? reason) => Assert.ThrowsAny<ArgumentException>(() => new ReplayRuleEvaluationDecision(RuleEvaluationResult.Passed, reason!, null));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    public void DecisionRejectsInvalidEvidence(string evidence) => Assert.Throws<ArgumentException>(() => new ReplayRuleEvaluationDecision(RuleEvaluationResult.Passed, "reason", evidence));

    [Fact]
    public void DecisionPreservesOnlyOwnedOutput()
    {
        var value = new ReplayRuleEvaluationDecision(RuleEvaluationResult.Waiting, "reason", "evidence");
        Assert.Equal(RuleEvaluationResult.Waiting, value.Result); Assert.Equal("reason", value.Reason); Assert.Equal("evidence", value.EvidenceReference);
        Assert.Null(new ReplayRuleEvaluationDecision(RuleEvaluationResult.Passed, "reason", null).EvidenceReference);
    }

    [Fact]
    public void ExactEvaluatorUsesDefinitionMetadataAndExactFrame()
    {
        var frame = Frame(2, 4); var fake = new Fake(Strategy, Version, Rule, new(RuleEvaluationResult.Passed, "observed", "ev"));
        var observation = new EvaluateStrategyReplayFrameUseCase([fake]).Execute(Definition(), frame);
        var evaluation = Assert.Single(observation.Evaluations);
        Assert.Same(frame, fake.Frame); Assert.Equal(2, fake.AvailableCount); Assert.Equal(Rule, evaluation.RuleId);
        Assert.Equal(10, evaluation.Sequence); Assert.True(evaluation.IsRequired); Assert.Equal(RuleDefinitionStatus.Candidate, evaluation.DefinitionStatus);
        Assert.Equal(frame.AsOfUtc, evaluation.EvaluatedAtUtc); Assert.Equal(2, observation.Step); Assert.Same(frame.CurrentCandle, observation.CurrentCandle);
    }

    [Fact]
    public void MissingOtherStrategyAndVersionEvaluatorsAreIgnored()
    {
        var others = new[] { new Fake(new("other"), Version, Rule), new Fake(Strategy, new("v2"), Rule) };
        var result = new EvaluateStrategyReplayFrameUseCase(others).Execute(Definition(), Frame());
        Assert.Equal(0, result.EvaluationCount); Assert.All(others, x => Assert.Equal(0, x.Invocations));
    }

    [Fact]
    public void DuplicateAndUnknownRegistrationsAreRejected()
    {
        var fake = new Fake(Strategy, Version, Rule);
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyReplayFrameUseCase([fake, fake]));
        var unknown = new Fake(Strategy, Version, new("UNKNOWN"));
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayFrameUseCase([unknown]).Execute(Definition(), Frame()));
        Assert.Equal(0, unknown.Invocations);
    }

    [Fact]
    public void EvaluatorsRunInDefinitionSequenceAndMissingRequiredIsAllowed()
    {
        var order = new List<string>(); var r2 = new RuleId("R-2");
        var definition = Definition(new StrategyRuleDefinition(r2, "two", "stage", 20, true, RuleDefinitionStatus.Unresolved, "d", "s"));
        var e2 = new Fake(Strategy, Version, r2, callback: () => order.Add("20"));
        var e1 = new Fake(Strategy, Version, Rule, callback: () => order.Add("10"));
        var result = new EvaluateStrategyReplayFrameUseCase([e2, e1]).Execute(definition, Frame());
        Assert.Equal(["10", "20"], order); Assert.Equal([10, 20], result.Evaluations.Select(x => x.Sequence));
        Assert.Equal(0, new EvaluateStrategyReplayFrameUseCase([]).Execute(definition, Frame()).EvaluationCount);
    }

    [Fact]
    public void NullDecisionAndEvaluatorExceptionPropagate()
    {
        Assert.Throws<InvalidOperationException>(() => new EvaluateStrategyReplayFrameUseCase([new Fake(Strategy, Version, Rule, returnNull: true)]).Execute(Definition(), Frame()));
        var expected = new InvalidDataException("failure");
        Assert.Same(expected, Assert.Throws<InvalidDataException>(() => new EvaluateStrategyReplayFrameUseCase([new Fake(Strategy, Version, Rule, exception: expected)]).Execute(Definition(), Frame())));
    }

    [Fact]
    public void ObservationValidatesAndDefensivelyCopiesEvaluations()
    {
        var frame = Frame(); var list = new List<RuleEvaluation>();
        var observation = new StrategyReplayFrameObservation(Strategy, Version, frame.Step, frame.AsOfUtc, frame.CurrentCandle, list); list.Add(Evaluation(frame, Rule, 1));
        Assert.Equal(0, observation.EvaluationCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategyReplayFrameObservation(Strategy, Version, 0, frame.AsOfUtc, frame.CurrentCandle, []));
        Assert.Throws<ArgumentException>(() => new StrategyReplayFrameObservation(Strategy, Version, 1, frame.AsOfUtc.AddMinutes(1), frame.CurrentCandle, []));
        Assert.Throws<ArgumentException>(() => new StrategyReplayFrameObservation(Strategy, Version, 1, frame.AsOfUtc, frame.CurrentCandle, [Evaluation(frame, Rule, 2), Evaluation(frame, new("R-2"), 1)]));
    }

    private static RuleEvaluation Evaluation(ReplayFrame frame, RuleId id, int sequence) => new(id, RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Passed, sequence, true, "r", frame.AsOfUtc, null);
    private static StrategyDefinition Definition(params StrategyRuleDefinition[] extra) => new(Strategy, Version, "Synthetic", "test", [new(Rule, "one", "stage", 10, true, RuleDefinitionStatus.Candidate, "d", "s"), .. extra]);
    private static ReplayFrame Frame(int step = 1, int total = 1)
    {
        var provider = new MarketDataProviderId("fixture"); var symbol = new MarketSymbol("DEMO"); var timeframe = new Timeframe(5, TimeframeUnit.Minute); var start = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var candles = Enumerable.Range(0, total).Select(i => new Candle(provider, symbol, timeframe, start.AddMinutes(i * 5), start.AddMinutes((i + 1) * 5), 100, 101, 99, 100, null)).ToArray();
        var cursor = new CandleReplayCursor(new(provider, symbol, timeframe, candles)); ReplayFrame? frame = null; for (var i = 0; i < step; i++) cursor.TryAdvance(out frame); return frame!;
    }
    private sealed class Fake(StrategyId strategy, StrategyVersion version, RuleId rule, ReplayRuleEvaluationDecision? decision = default, Action? callback = null, Exception? exception = null, bool returnNull = false) : ISingleTimeframeReplayRuleEvaluator
    {
        private readonly ReplayRuleEvaluationDecision? decision = decision is null && exception is null ? new(RuleEvaluationResult.Passed, "ok", null) : decision;
        public StrategyId StrategyId { get; } = strategy; public StrategyVersion StrategyVersion { get; } = version; public RuleId RuleId { get; } = rule;
        public ReplayFrame? Frame { get; private set; }
        public int AvailableCount { get; private set; }
        public int Invocations { get; private set; }
        public ReplayRuleEvaluationDecision Evaluate(ReplayFrame frame) { Invocations++; Frame = frame; AvailableCount = frame.AvailableCandles.Count; callback?.Invoke(); if (exception is not null) throw exception; return returnNull ? null! : decision!; }
    }
}
