using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Research.Nasdaq;

public sealed class NasdaqEndToEndResearchUseCaseTests
{
    private static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    private static readonly MarketSymbol Symbol = new("US100");
    private static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly DateOnly Day = new(2026, 6, 23);
    private static readonly DateTimeOffset Preparation = new(2026, 6, 23, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExactPreparationIncludesAllSimultaneousClosesAndH1BecomesAvailableAtItsBoundary()
    {
        var before = Run(Preparation.AddTicks(-1));
        Assert.Null(before.Preparation.ReplayCutoff);
        Assert.All(before.Preparation.Profiles, p => Assert.Equal("PreparationCutoffNotReached", p.ReasonCode));
        var earlyLondon = Assert.Single(before.SessionLiquidity, s => s.Session == "London");
        Assert.Equal("SessionNotCompleted", earlyLondon.CoverageStatus);
        Assert.Null(earlyLondon.AvailableAtUtc);

        var exact = Run(Preparation);
        Assert.Equal(Preparation, exact.ReplayCutoff.CanonicalFrameUtc);
        Assert.Equal(4, exact.ReplayCutoff.UpdatedTimeframes.Count);
        var london = Assert.Single(exact.SessionLiquidity, s => s.Session == "London");
        Assert.Equal(Preparation, london.AvailableAtUtc);
        Assert.Equal("Complete", london.CoverageStatus);
        Assert.All(exact.Preparation.Profiles, p =>
        {
            Assert.False(p.ContextAvailable);
            Assert.False(p.IndependentlyValidated);
            Assert.Equal(H4StructureClassification.InsufficientEvidence, p.Evidence!.MarketStructure.Classification);
        });
        var between = Run(Preparation.AddTicks(1));
        Assert.Equal(Preparation, between.ReplayCutoff.CanonicalFrameUtc);
        Assert.Equal(Preparation.AddTicks(1), between.ReplayCutoff.RequestedUtc);
        Assert.Empty(between.ReplayCutoff.UpdatedTimeframes);
        Assert.Equal(exact.Preparation.ReplayCutoff!.CanonicalStep, between.Preparation.ReplayCutoff!.CanonicalStep);
    }

    [Theory]
    [InlineData(29, false, false)]
    [InlineData(30, true, true)]
    [InlineData(180, true, false)]
    [InlineData(209, true, false)]
    [InlineData(210, false, false)]
    public void CandidatesKeepTheirOwnHalfOpenWindowWithoutRuntimeMutation(int minute, bool candidate, bool published)
    {
        var result = Run(Preparation.AddMinutes(minute));
        Assert.Equal(candidate, result.Window.Candidate!.WithinNewSetupAndEntryTimeWindow);
        Assert.Equal(published, result.Window.PublishedRuntime!.WithinNewSetupAndEntryTimeWindow);
        Assert.False(result.Window.TerminatesSession);
        Assert.False(result.Window.ForcesPositionClose);
        Assert.False(result.OperationalSignal);
        Assert.False(result.CanonicalAdmissionPermitted);
        Assert.Empty(result.StructuralTakeProfit!.Evaluations);
    }

    [Fact]
    public void MissingH1KeepsExplicitCoverageAndProducesNoFabricatedEpisodes()
    {
        var sources = Fixture();
        sources[1] = Series(H1, []);
        var result = Run(Preparation.AddHours(1), sources);
        Assert.All(result.SessionLiquidity, s => Assert.Equal("IncompleteCoverage", s.CoverageStatus));
        Assert.Equal(NasdaqResearchChainOutcome.DataUnavailable, result.Outcome);
        Assert.Empty(result.Chains);
        Assert.Equal(0, result.Summary.TotalResearchConfigurationAssociations);
    }

    [Fact]
    public void ProviderAndTimeframeIdentityCannotBeSubstituted()
    {
        var sources = Fixture();
        sources[3] = new(new("foreign"), Symbol, M1, []);
        Assert.Throws<ArgumentException>(() => Run(Preparation, sources));
        sources[3] = Series(M5, []);
        Assert.Throws<ArgumentException>(() => Run(Preparation, sources));
    }

    private static NasdaqEndToEndResearchResult Run(DateTimeOffset cutoff, CandleSeries[]? sources = null) =>
        new EvaluateNasdaqEndToEndResearchUseCase().Execute(new("fixture", Day, cutoff, sources ?? Fixture(),
            Preparation.AddHours(-4), "fixture"));

    private static CandleSeries[] Fixture() =>
        [
            Series(H4, [Candle(H4, Preparation.AddHours(-4), Preparation)]),
            Series(H1, Enumerable.Range(0, 15).Select(i =>
                Candle(H1, Preparation.AddHours(i - 15), Preparation.AddHours(i - 14)))),
            Series(M5, [Candle(M5, Preparation.AddMinutes(-5), Preparation)]),
            Series(M1, [Candle(M1, Preparation.AddMinutes(-1), Preparation)])
        ];

    private static CandleSeries Series(Timeframe timeframe, IEnumerable<Candle> candles) => new(Provider, Symbol, timeframe, candles);
    private static Candle Candle(Timeframe timeframe, DateTimeOffset open, DateTimeOffset close) =>
        new(Provider, Symbol, timeframe, open, close, 100, 110, 90, 100, null);
}
