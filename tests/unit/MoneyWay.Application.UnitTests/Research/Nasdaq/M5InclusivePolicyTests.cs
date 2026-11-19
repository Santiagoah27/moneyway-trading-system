using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using static MoneyWay.Application.UnitTests.Research.Nasdaq.M5LabFixtures;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M5InclusivePolicyTests
{
    private readonly CompareM5SwingPoliciesUseCase runner = new();

    [Theory]
    [InlineData(false, 80)]
    [InlineData(false, 92)]
    [InlineData(false, 90)]
    [InlineData(true, 80)]
    [InlineData(true, 92)]
    [InlineData(true, 90)]
    public void OnlyClosedConfirmationExtremeChangesWithExactTiesAndDirectionalSymmetry(bool descending, decimal confirmationLow)
    {
        var input = Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 109, 90, 105), Bar(2, 105, 109, 90, 107), Bar(3, 107, 112, confirmationLow, 111)], descending);
        var report = runner.Execute(input); var expected = Math.Min(90, confirmationLow); if (descending) expected = 200 - expected;
        Assert.Equal(4, report.Differences.Count);
        foreach (var (old, inclusive) in report.ExclusiveV1.Results.Zip(report.InclusiveV2.Results))
        {
            Assert.Equal(descending ? 110 : 90, old.ProposedReferencePrice);
            Assert.Equal(expected, inclusive.ProposedReferencePrice);
            Assert.Equal(old.PullbackStart, inclusive.PullbackStart); Assert.Equal(old.ContinuationCandle, inclusive.ContinuationCandle);
            Assert.Equal(At(4), inclusive.ConfirmationAtUtc); Assert.Equal(old.ConfirmationAtUtc, inclusive.ConfirmationAtUtc);
            Assert.Equal("2.0.0", inclusive.Algorithm.AlgorithmVersion); Assert.Equal(old.Algorithm.AlgorithmId.Replace("-V1", "-V2"), inclusive.Algorithm.AlgorithmId);
            Assert.Equal(old.Algorithm.StartCondition, inclusive.Algorithm.StartCondition);
            Assert.Equal("EXP-M5-BOUNDED-CONTINUATION-INCLUSIVE-V2", inclusive.SharedPolicyId);
            Assert.Equal(old.AggregationCandles.Append(inclusive.ContinuationCandle!), inclusive.AggregationCandles);
            var expectedOpens = confirmationLow < 90 ? new[] { At(3) } : confirmationLow == 90 ? [At(1), At(2), At(3)] : [At(1), At(2)];
            Assert.Equal(expectedOpens, inclusive.ReferenceCandles.Select(c => c.OpenTimeUtc));
            Assert.Equal(new[] { At(1), At(2) }, old.ReferenceCandles.Select(c => c.OpenTimeUtc));
        }
        Assert.All(report.Differences, p =>
        {
            Assert.True(p.ComparableConfirmedReferences); Assert.Equal(confirmationLow <= 90, p.ReferenceChanged); Assert.Equal(confirmationLow < 90, p.PriceChanged);
            Assert.Equal(descending ? expected - 110 : expected - 90, p.SignedPriceDifference);
            Assert.Equal(Math.Abs(descending ? expected - 110 : expected - 90), p.AbsolutePriceDifference);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ConfirmationCannotInitializeAPullbackOrSkipIntoALaterCycle(bool descending, bool ambiguous)
    {
        var candle = ambiguous ? Bar(1, 115, 116, 94, 111) : Bar(1, 108, 112, 96, 111);
        var report = runner.Execute(Input([Bar(0, 100, 110, 95, 108), candle, Bar(2, 111, 112, 80, 105), Bar(3, 105, 114, 85, 113)], descending));
        var status = ambiguous ? M5ExperimentalStatus.AbstainedAmbiguousStartAndContinuation : M5ExperimentalStatus.AbstainedNoPullbackBeforeContinuation;
        Assert.All(report.ExclusiveV1.Results.Concat(report.InclusiveV2.Results), r =>
        {
            Assert.Equal(status, r.Status); Assert.Null(r.ProposedReferencePrice); Assert.Null(r.ConfirmationAtUtc); Assert.Empty(r.AggregationCandles);
        });
        Assert.All(report.Differences, p => { Assert.False(p.ComparableConfirmedReferences); Assert.Null(p.ReferenceChanged); Assert.Null(p.SignedPriceDifference); });
    }

    [Theory]
    [InlineData("110", false)]
    [InlineData("109.5", false)]
    [InlineData("110.00000000000000000000000001", true)]
    public void StrictCloseEqualityWickAndUnfinishedBarsRetainExistingSafety(string close, bool confirmed)
    {
        var price = decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture);
        var input = Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 109, 90, 105), Bar(2, 105, 109, 90, 107), Bar(3, 107, 112, 80, price)]);
        var before = runner.Execute(input with { CutoffUtc = At(4).AddTicks(-1) });
        Assert.All(before.InclusiveV2.Results, r => { Assert.False(r.IsExperimentalReferenceConfirmed); Assert.Equal(90, r.ProposedReferencePrice); Assert.Null(r.ConfirmationAtUtc); Assert.DoesNotContain(r.AggregationCandles, c => c.OpenTimeUtc == At(3)); });
        var complete = runner.Execute(input);
        Assert.All(complete.InclusiveV2.Results, r => { Assert.Equal(confirmed, r.IsExperimentalReferenceConfirmed); Assert.Equal(80, r.ProposedReferencePrice); Assert.Equal(confirmed ? At(4) : null, r.ConfirmationAtUtc); });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDecimalChangesAreExactWithoutTolerance(bool descending)
    {
        const decimal low = 89.99999999999999999999999999m;
        var report = runner.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 109, 90, 105), Bar(2, 105, 112, low, 111)], descending));
        Assert.All(report.InclusiveV2.Results, r => Assert.Equal(descending ? 200 - low : low, r.ProposedReferencePrice));
        Assert.All(report.Differences, p => { Assert.True(p.PriceChanged); Assert.Equal(0.00000000000000000000000001m, p.AbsolutePriceDifference); });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("future")]
    [InlineData("before-close")]
    [InlineData("late")]
    [InlineData("instrument")]
    [InlineData("provider")]
    [InlineData("absent-anchor")]
    [InlineData("timeframe")]
    public void BothPoliciesRetainTheSameAnchorAndIdentityFailures(string kind)
    {
        var input = Disagreement();
        input = kind switch
        {
            "missing" => input with { Initialization = null },
            "future" => input with { Initialization = input.Initialization! with { AvailableAtUtc = At(9) } },
            "before-close" => input with { Initialization = input.Initialization! with { AvailableAtUtc = At(0) } },
            "late" => input with { Initialization = input.Initialization! with { AvailableAtUtc = At(3) } },
            "instrument" => input with { Initialization = input.Initialization! with { Symbol = new("USTEC") } },
            "provider" => input with { Initialization = input.Initialization! with { ProviderId = new("unrelated") } },
            "absent-anchor" => input with { Initialization = input.Initialization! with { AnchorOpenTimeUtc = At(-1) } },
            _ => input with { Initialization = input.Initialization! with { Timeframe = new(1, TimeframeUnit.Hour) } }
        };
        var report = runner.Execute(input);
        Assert.All(report.ExclusiveV1.Results.Zip(report.InclusiveV2.Results), pair => { Assert.Equal(pair.First.Status, pair.Second.Status); Assert.Equal(pair.First.Reason, pair.Second.Reason); Assert.False(pair.Second.IsExperimentalReferenceConfirmed); Assert.True(pair.Second.Abstained); });
    }

    [Fact]
    public void IncorrectSeriesTimeframeAndNonUtcCutoffAreNotReinterpreted()
    {
        var input = Disagreement(); var hour = new Timeframe(1, TimeframeUnit.Hour);
        var series = new CandleSeries(Provider, Symbol, hour, [new(Provider, Symbol, hour, At(0), At(12), 100, 110, 95, 108, null)]);
        Assert.All(runner.Execute(input with { Series = series }).InclusiveV2.Results, r => Assert.Equal(M5ExperimentalStatus.RejectedInput, r.Status));
        Assert.Throws<ArgumentException>(() => runner.Execute(input with { CutoffUtc = input.CutoffUtc.ToOffset(TimeSpan.FromHours(-5)) }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalSyntheticResultsArePreservedAndV2SelectsTheActualConfirmationExtreme(bool descending)
    {
        var report = runner.Execute(Disagreement(descending));
        Assert.Equal(descending ? new decimal?[] { 110, 110, 107, 110 } : [90, 90, 93, 90], report.ExclusiveV1.Results.Select(r => r.ProposedReferencePrice));
        Assert.All(report.InclusiveV2.Results, r => { Assert.Equal(descending ? 120 : 80, r.ProposedReferencePrice); Assert.Equal(At(5), Assert.Single(r.ReferenceCandles).OpenTimeUtc); Assert.Equal(At(6), r.ConfirmationAtUtc); });
        Assert.Equal(report.ExclusiveV1.Results.Select(r => r.PullbackStart!.OpenTimeUtc), report.InclusiveV2.Results.Select(r => r.PullbackStart!.OpenTimeUtc));
    }

    [Fact]
    public void EveryPrefixRepeatedCalculationAndFutureExtensionAreIdenticalWithoutHumanInputs()
    {
        var input = Disagreement(); var json = JsonSerializer.Serialize(runner.Execute(input)); Assert.Equal(json, JsonSerializer.Serialize(runner.Execute(input)));
        var future = input with { Series = new(Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, input.Series.Candles.Append(Bar(6, 111, 120, 1, 118))) };
        Assert.Equal(json, JsonSerializer.Serialize(runner.Execute(future)));
        foreach (var cutoff in new[] { At(0), At(1), At(2), At(3), At(4), At(5), At(6).AddTicks(-1), At(6) })
        {
            var full = future with { CutoffUtc = cutoff };
            var prefix = full with { Series = new(Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, future.Series.Candles.Where(c => c.CloseTimeUtc <= cutoff)) };
            Assert.Equal(JsonSerializer.Serialize(runner.Execute(prefix)), JsonSerializer.Serialize(runner.Execute(full)));
        }
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(M5PairedPolicyReport)));
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(typeof(M5PairedPolicyReport)));
        Assert.False(typeof(IReplayRuleEvaluator).IsAssignableFrom(typeof(CompareM5SwingPoliciesUseCase)));
        Assert.All(runner.Execute(input).InclusiveV2.Results, r => { Assert.False(r.CanonicalAdmissionPermitted); Assert.False(r.OperationalSignal); Assert.False(r.MentorRuleConfirmed); Assert.Equal("NativeInstrumentPrice", r.Input.PriceUnit); });
    }
}
