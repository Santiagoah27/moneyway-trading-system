using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting.Diagnostics;

public sealed class StrategyDiagnosticsModelTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FramePreservesCompleteBlockingMetadataAndDefensivelyCopiesMissingRules()
    {
        var frame = new StrategyFrameDiagnostic(1, At, StrategyVerdict.Wait, true, "Waiting.", new("B"), 20, RuleEvaluationResult.Waiting, []);
        Assert.Equal(1, frame.Step); Assert.Equal(At, frame.AsOfUtc); Assert.Equal(StrategyVerdict.Wait, frame.Verdict);
        Assert.Equal(new RuleId("B"), frame.BlockingRuleId); Assert.Equal(20, frame.BlockingSequence); Assert.Equal(RuleEvaluationResult.Waiting, frame.BlockingResult);
        var source = new List<RuleId> { new("A") }; var incomplete = new StrategyFrameDiagnostic(2, At, StrategyVerdict.DataUnavailable, false, "Missing.", null, null, null, source); source.Clear(); Assert.Single(incomplete.MissingRequiredRuleIds);
    }

    [Fact]
    public void CompleteDataUnavailableWithoutBlockerIsValid()
    {
        var frame = new StrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, true, "Unavailable.", null, null, null, []);
        Assert.Null(frame.BlockingRuleId);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("timezone")]
    [InlineData("reason")]
    [InlineData("partial")]
    [InlineData("sequence")]
    [InlineData("incomplete-verdict")]
    [InlineData("incomplete-blocker")]
    [InlineData("incomplete-empty")]
    [InlineData("complete-missing")]
    [InlineData("duplicate")]
    public void FrameRejectsInvalidInvariants(string scenario)
    {
        Assert.ThrowsAny<ArgumentException>(() => scenario switch
        {
            "step" => new StrategyFrameDiagnostic(0, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, []),
            "timezone" => new StrategyFrameDiagnostic(1, At.ToOffset(TimeSpan.FromHours(1)), StrategyVerdict.Ready, true, "Ready.", null, null, null, []),
            "reason" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.Ready, true, " ", null, null, null, []),
            "partial" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.Wait, true, "Wait.", new("A"), null, RuleEvaluationResult.Waiting, []),
            "sequence" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.Wait, true, "Wait.", new("A"), 0, RuleEvaluationResult.Waiting, []),
            "incomplete-verdict" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.Wait, false, "Missing.", null, null, null, [new("A")]),
            "incomplete-blocker" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false, "Missing.", new("A"), 1, RuleEvaluationResult.DataUnavailable, [new("B")]),
            "incomplete-empty" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false, "Missing.", null, null, null, []),
            "complete-missing" => new StrategyFrameDiagnostic(1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, [new("A")]),
            _ => new StrategyFrameDiagnostic(1, At, StrategyVerdict.DataUnavailable, false, "Missing.", null, null, null, [new("A"), new("A")]),
        });
    }

    [Fact]
    public void AggregateCountModelsValidateAndPreserveValues()
    {
        var blocker = new StrategyBlockingRuleCount(new("A"), 10, StrategyVerdict.NoTrade, RuleEvaluationResult.Failed, 3);
        Assert.Equal(10, blocker.Sequence); Assert.Equal(3, blocker.Count); Assert.Equal(RuleEvaluationResult.Failed, blocker.BlockingResult);
        var missing = new MissingRequiredRuleCount(new("B"), 20, 2); Assert.Equal(20, missing.Sequence); Assert.Equal(2, missing.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategyBlockingRuleCount(new("A"), 0, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StrategyBlockingRuleCount(new("A"), 1, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MissingRequiredRuleCount(new("A"), 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MissingRequiredRuleCount(new("A"), 1, 0));
    }
}
