using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using static MoneyWay.Application.UnitTests.Research.Nasdaq.M5LabFixtures;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class M5SwingExperimentTests
{
    private readonly CompareM5SwingHypothesesUseCase lab = new();
    [Fact]
    public void HumanReportEscapesUntrustedMarkupAndKeepsMissingLatencyExplicit()
    {
        var input = Input([Bar(0, 100, 110, 95, 108)]) with { SeriesId = "![external](https://example.invalid/image)", Provenance = "<script>untrusted</script>" };
        var markdown = M5SwingComparisonMarkdown.Render(lab.Execute(input));
        Assert.DoesNotContain("![external]", markdown); Assert.DoesNotContain("<script>", markdown);
        Assert.Contains("&lt;script&gt;", markdown); Assert.Contains("—", markdown);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FourHypothesesDisagreeWithoutChoosingAWinnerAndExcludeTheBreakCandle(bool descending)
    {
        var report = lab.Execute(Disagreement(descending));
        Assert.Equal(new[] { 1, 2, 4, 1 }.Select(At), report.Results.Select(r => r.PullbackStart!.OpenTimeUtc));
        Assert.Equal(descending ? new decimal?[] { 110, 110, 107, 110 } : [90, 90, 93, 90], report.Results.Select(r => r.ProposedReferencePrice));
        Assert.All(report.Results, r => { Assert.True(r.IsExperimentalReferenceConfirmed); Assert.Equal(At(6), r.ConfirmationAtUtc); Assert.DoesNotContain(r.AggregationCandles, c => c.OpenTimeUtc == At(5)); Assert.False(r.OperationalSignal); Assert.False(r.CanonicalAdmissionPermitted); });
        Assert.False(report.AllAlgorithmsAgree); Assert.Equal(6, report.Comparisons.Count);
        Assert.True(report.Comparisons.Single(p => p.LeftAlgorithm.Contains("-A-") && p.RightAlgorithm.Contains("-D-")).FullAgreement);
        Assert.False(report.Comparisons.Single(p => p.LeftAlgorithm.Contains("-A-") && p.RightAlgorithm.Contains("-C-")).ConfirmedReferenceAgreement);
        var ab = report.Comparisons.Single(p => p.LeftAlgorithm.Contains("-A-") && p.RightAlgorithm.Contains("-B-"));
        Assert.True(ab.ConfirmedReferenceAgreement); Assert.Equal(TimeSpan.Zero, ab.ConfirmationLatencyDifference); Assert.Equal(TimeSpan.FromMinutes(5), ab.PullbackLatencyDifference);
    }
    [Theory]
    [InlineData(true, false, true, false, false, true)]
    [InlineData(false, true, false, true, false, true)]
    [InlineData(true, true, true, true, true, true)]
    [InlineData(false, false, false, false, false, false)]
    public void IsolatedStartPredicatesAreIndependentOfColorStructureAndEachOther(bool color, bool structure, bool a, bool b, bool c, bool d)
    {
        var first = Bar(1, color ? 108 : 100, 109, structure ? 94 : 96, color ? 107 : 108);
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), first]));
        Assert.Equal(new[] { a, b, c, d }, r.Results.Select(x => x.PullbackStart is not null));
    }
    [Fact]
    public void DojiHasNoColorSignalButCanHaveAStrictPriceStructureSignal()
    {
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 107, 109, 94, 107)]));
        Assert.Equal(new[] { false, true, false, true }, r.Results.Select(x => x.PullbackStart is not null));
    }
    [Fact]
    public void EqualExtremesAreNotViolationsAndTiedReferenceIdentitiesAreAllRetained()
    {
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 109, 90, 105), Bar(2, 105, 109, 90, 107), Bar(3, 107, 112, 50, 111)]));
        Assert.All(r.Results, x => { Assert.Equal(90, x.ProposedReferencePrice); Assert.Equal(new[] { At(1), At(2) }, x.ReferenceCandles.Select(c => c.OpenTimeUtc)); });
        var equal = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 110, 95, 108)])); Assert.All(equal.Results, x => Assert.Null(x.PullbackStart));
    }
    [Theory]
    [InlineData("110", false)]
    [InlineData("110.00000000000000000000000001", true)]
    public void ConfirmationUsesFixedActualAnchorHighNotPreviousCloseOrLaterWicks(string close, bool confirms)
    {
        var value = decimal.Parse(close, System.Globalization.CultureInfo.InvariantCulture);
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 115, 90, 105), Bar(2, 105, 116, 80, value)]));
        Assert.All(r.Results, x => { Assert.Equal(confirms, x.IsExperimentalReferenceConfirmed); Assert.Equal(confirms ? 90 : 80, x.ProposedReferencePrice); });
        // A close above previous Close but below the anchor is not continuation.
        var below = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 109, 90, 103), Bar(2, 103, 112, 89, 108)]));
        Assert.All(below.Results, x => { Assert.Equal(M5ExperimentalStatus.AwaitingContinuation, x.Status); Assert.Null(x.ConfirmationAtUtc); });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameCandleStartAndContinuationAbstainWithoutInferringIntrabarOrder(bool descending)
    {
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 115, 116, 94, 111)], descending));
        Assert.All(r.Results, x => { Assert.Equal(M5ExperimentalStatus.AbstainedAmbiguousStartAndContinuation, x.Status); Assert.Null(x.ProposedReferencePrice); Assert.Null(x.ConfirmationAtUtc); });
    }
    [Fact]
    public void FirstContinuationWithoutPullbackEndsTheEpisodeAndNeverStartsAnotherCycle()
    {
        var r = lab.Execute(Input([Bar(0, 100, 110, 95, 108), Bar(1, 108, 112, 96, 111), Bar(2, 111, 112, 80, 105), Bar(3, 105, 114, 85, 113)]));
        Assert.All(r.Results, x => { Assert.Equal(M5ExperimentalStatus.AbstainedNoPullbackBeforeContinuation, x.Status); Assert.Null(x.PullbackStart); });
    }
    [Fact]
    public void NoContinuationOnlyProducesProvisionalReferences()
    {
        var input = Disagreement(); var shortInput = input with { CutoffUtc = At(5) };
        var r = lab.Execute(shortInput); Assert.All(r.Results, x => { Assert.Equal(M5ExperimentalStatus.AwaitingContinuation, x.Status); Assert.Null(x.ConfirmationLatencyFromAnchor); });
        var none = lab.Execute(Input([Bar(0, 100, 110, 95, 108)])); Assert.All(none.Results, x => Assert.Equal(M5ExperimentalStatus.AwaitingPullbackStart, x.Status));
    }
    [Fact]
    public void MissingUnavailableAndLateInitializationNeverBackdateAuthorization()
    {
        var input = Disagreement();
        Assert.All(lab.Execute(input with { Initialization = null }).Results, x => Assert.Equal(M5ExperimentalStatus.AbstainedMissingInitialization, x.Status));
        var future = input with { Initialization = input.Initialization! with { AvailableAtUtc = At(8) } };
        Assert.All(lab.Execute(future).Results, x => Assert.Equal(M5ExperimentalStatus.AbstainedInitializationUnavailable, x.Status));
        var late = input with { Initialization = input.Initialization! with { AvailableAtUtc = At(3) } };
        Assert.All(lab.Execute(late).Results, x => Assert.Equal(M5ExperimentalStatus.AbstainedLateInitialization, x.Status));
        var missing = input with { Initialization = input.Initialization! with { AnchorOpenTimeUtc = At(-5) } };
        Assert.All(lab.Execute(missing).Results, x => Assert.Equal(M5ExperimentalStatus.AbstainedInitializationUnavailable, x.Status));
    }
    [Theory]
    [InlineData("instrument")]
    [InlineData("provider")]
    [InlineData("timeframe")]
    [InlineData("availability")]
    public void InvalidInitialReferencesAreRejectedWithExplicitReasons(string issue)
    {
        var input = Disagreement(); var init = input.Initialization!;
        init = issue switch { "instrument" => init with { Symbol = new("USTEC") }, "provider" => init with { ProviderId = new("other") }, "timeframe" => init with { Timeframe = new(1, TimeframeUnit.Hour) }, _ => init with { AvailableAtUtc = At(0) } };
        Assert.All(lab.Execute(input with { Initialization = init }).Results, x => { Assert.Equal(M5ExperimentalStatus.RejectedInput, x.Status); Assert.False(string.IsNullOrWhiteSpace(x.Reason)); });
    }
    [Fact]
    public void NonM5SeriesAndNonUtcCutoffsCannotBeTreatedAsEligibleResearchInput()
    {
        var input = Disagreement(); var hour = new Timeframe(1, TimeframeUnit.Hour);
        var wrong = new CandleSeries(Provider, Symbol, hour, [new(Provider, Symbol, hour, At(0), At(12), 100, 110, 95, 108, null)]);
        Assert.All(lab.Execute(input with { Series = wrong }).Results, x => Assert.Equal(M5ExperimentalStatus.RejectedInput, x.Status));
        Assert.Throws<ArgumentException>(() => lab.Execute(input with { CutoffUtc = At(6).ToOffset(TimeSpan.FromHours(-5)) }));
    }
    [Fact]
    public void RepeatedRunsAndFutureExtensionsPreserveEveryEarlierCutoffAndExcludeUnfinishedCandles()
    {
        var input = Disagreement(); var full = lab.Execute(input); Assert.Equal(JsonSerializer.Serialize(full), JsonSerializer.Serialize(lab.Execute(input)));
        foreach (var end in new[] { At(1), At(2), At(3), At(5), At(6).AddSeconds(-1) })
        {
            var prefix = input with { CutoffUtc = end, Series = new(Provider, Symbol, CompareM5SwingHypothesesUseCase.M5, input.Series.Candles.Where(c => c.CloseTimeUtc <= end)) };
            var a = lab.Execute(prefix); var b = lab.Execute(input with { CutoffUtc = end }); Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
            Assert.All(b.EvidenceCandles, c => Assert.True(c.CloseTimeUtc <= end)); Assert.All(b.Results, r => Assert.False(r.IsExperimentalReferenceConfirmed));
        }
        Assert.False(typeof(IStrategyReplayInputObservation).IsAssignableFrom(typeof(M5SwingHypothesisResult)));
        Assert.False(typeof(IReplayRuleFact).IsAssignableFrom(typeof(M5SwingHypothesisResult)));
        Assert.False(typeof(IReplayRuleEvaluator).IsAssignableFrom(typeof(CompareM5SwingHypothesesUseCase)));
    }
}
