using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Backtesting.Diagnostics;

public sealed class MultiTimeframeStrategyDiagnosticsModelTests
{
    private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe FiveMinutes = new(5, TimeframeUnit.Minute);

    [Theory]
    [InlineData(StrategyVerdict.Ready, null, null)]
    [InlineData(StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 20)]
    [InlineData(StrategyVerdict.NoTrade, RuleEvaluationResult.Failed, 20)]
    [InlineData(StrategyVerdict.HumanValidationRequired, RuleEvaluationResult.HumanValidationRequired, 20)]
    [InlineData(StrategyVerdict.DataUnavailable, RuleEvaluationResult.DataUnavailable, 20)]
    [InlineData(StrategyVerdict.DataUnavailable, null, null)]
    public void CompleteFramesPreserveGlobalOutcomeAndTimeframeMetadata(
        StrategyVerdict verdict,
        RuleEvaluationResult? blockingResult,
        int? blockingSequence)
    {
        var blocker = blockingResult is null ? null : new RuleId("B");
        var frame = new MultiTimeframeStrategyFrameDiagnostic(
            1, At, verdict, true, "Synthetic.", blocker, blockingSequence, blockingResult, [], [Minute], [Minute, FiveMinutes]);

        Assert.Equal(1, frame.Step);
        Assert.Equal(At, frame.AsOfUtc);
        Assert.Equal(verdict, frame.Verdict);
        Assert.True(frame.HasCompleteRequiredCoverage);
        Assert.Equal("Synthetic.", frame.Reason);
        Assert.Equal(blocker, frame.BlockingRuleId);
        Assert.Equal(blockingSequence, frame.BlockingSequence);
        Assert.Equal(blockingResult, frame.BlockingResult);
        Assert.Empty(frame.MissingRequiredRuleIds);
        Assert.Equal([Minute], frame.UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes], frame.AvailableTimeframes);
    }

    [Fact]
    public void IncompleteDataUnavailableFrameIsValidAndCollectionsAreDefensivelyCopied()
    {
        var missing = new List<RuleId> { new("A") };
        var updated = new List<Timeframe> { Minute };
        var available = new List<Timeframe> { Minute, FiveMinutes };
        var frame = new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.DataUnavailable, false, "Missing.", null, null, null, missing, updated, available);

        missing.Clear();
        updated.Clear();
        available.Clear();
        Assert.Equal([new RuleId("A")], frame.MissingRequiredRuleIds);
        Assert.Equal([Minute], frame.UpdatedTimeframes);
        Assert.Equal([Minute, FiveMinutes], frame.AvailableTimeframes);
    }

    [Theory]
    [InlineData("step")]
    [InlineData("timezone")]
    [InlineData("reason-empty")]
    [InlineData("reason-untrimmed")]
    [InlineData("partial-blocker")]
    [InlineData("blocking-sequence")]
    [InlineData("incomplete-verdict")]
    [InlineData("incomplete-blocker")]
    [InlineData("incomplete-empty")]
    [InlineData("complete-missing")]
    [InlineData("duplicate-missing")]
    [InlineData("duplicate-updated")]
    [InlineData("duplicate-available")]
    [InlineData("updated-unavailable")]
    public void FrameRejectsInvalidInvariants(string scenario)
    {
        Assert.ThrowsAny<ArgumentException>(() => scenario switch
        {
            "step" => Complete(step: 0),
            "timezone" => Complete(asOf: At.ToOffset(TimeSpan.FromHours(1))),
            "reason-empty" => Complete(reason: " "),
            "reason-untrimmed" => Complete(reason: " Ready."),
            "partial-blocker" => Complete(blocker: new("A")),
            "blocking-sequence" => Complete(blocker: new("A"), sequence: 0, result: RuleEvaluationResult.Waiting),
            "incomplete-verdict" => Incomplete(verdict: StrategyVerdict.Wait),
            "incomplete-blocker" => Incomplete(blocker: new("A"), sequence: 10, result: RuleEvaluationResult.DataUnavailable),
            "incomplete-empty" => Incomplete(missing: []),
            "complete-missing" => Complete(missing: [new("A")]),
            "duplicate-missing" => Incomplete(missing: [new("A"), new("A")]),
            "duplicate-updated" => Complete(updated: [Minute, Minute]),
            "duplicate-available" => Complete(available: [Minute, Minute]),
            "updated-unavailable" => Complete(updated: [FiveMinutes], available: [Minute]),
            _ => throw new InvalidOperationException(),
        });
    }

    [Fact]
    public void FrameRejectsNullCollections()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, null!, [Minute], [Minute]));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, [], null!, [Minute]));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, [], [Minute], null!));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.DataUnavailable, false, "Missing.", null, null, null, new RuleId[] { null! }, [Minute], [Minute]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, [], new Timeframe[] { null! }, [Minute]));
        Assert.Throws<ArgumentException>(() => new MultiTimeframeStrategyFrameDiagnostic(
            1, At, StrategyVerdict.Ready, true, "Ready.", null, null, null, [], [Minute], new Timeframe[] { null! }));
    }

    [Fact]
    public void AggregateCountModelsValidateAndPreserveEveryValue()
    {
        var blocker = new MultiTimeframeStrategyBlockingRuleCount(new("A"), 10, StrategyVerdict.NoTrade, RuleEvaluationResult.Failed, 3);
        Assert.Equal(new RuleId("A"), blocker.RuleId);
        Assert.Equal(10, blocker.Sequence);
        Assert.Equal(StrategyVerdict.NoTrade, blocker.Verdict);
        Assert.Equal(RuleEvaluationResult.Failed, blocker.BlockingResult);
        Assert.Equal(3, blocker.Count);
        var missing = new MultiTimeframeMissingRequiredRuleCount(new("B"), 20, 2);
        Assert.Equal(new RuleId("B"), missing.RuleId);
        Assert.Equal(20, missing.Sequence);
        Assert.Equal(2, missing.Count);
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeStrategyBlockingRuleCount(null!, 1, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeStrategyBlockingRuleCount(new("A"), 0, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeStrategyBlockingRuleCount(new("A"), 1, StrategyVerdict.Wait, RuleEvaluationResult.Waiting, 0));
        Assert.Throws<ArgumentNullException>(() => new MultiTimeframeMissingRequiredRuleCount(null!, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeMissingRequiredRuleCount(new("A"), 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MultiTimeframeMissingRequiredRuleCount(new("A"), 1, 0));
    }

    private static MultiTimeframeStrategyFrameDiagnostic Complete(
        int step = 1,
        DateTimeOffset? asOf = null,
        string reason = "Ready.",
        RuleId? blocker = null,
        int? sequence = null,
        RuleEvaluationResult? result = null,
        IEnumerable<RuleId>? missing = null,
        IEnumerable<Timeframe>? updated = null,
        IEnumerable<Timeframe>? available = null) => new(
            step, asOf ?? At, StrategyVerdict.Ready, true, reason, blocker, sequence, result,
            missing ?? [], updated ?? [Minute], available ?? [Minute]);

    private static MultiTimeframeStrategyFrameDiagnostic Incomplete(
        StrategyVerdict verdict = StrategyVerdict.DataUnavailable,
        RuleId? blocker = null,
        int? sequence = null,
        RuleEvaluationResult? result = null,
        IEnumerable<RuleId>? missing = null) => new(
            1, At, verdict, false, "Missing.", blocker, sequence, result, missing ?? [new("A")], [Minute], [Minute]);
}
