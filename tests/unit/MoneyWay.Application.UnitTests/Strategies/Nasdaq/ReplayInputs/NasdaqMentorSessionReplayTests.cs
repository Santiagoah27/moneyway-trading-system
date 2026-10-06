using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

/// <summary>Synthetic plumbing only; reuses accepted Feature 28 source geometry and assertions.</summary>
public sealed class NasdaqMentorSessionReplayTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve };
    private static NasdaqMentorSessionReplayReport Run(DateTimeOffset? end = null,
        NasdaqMentorSessionEvidenceRecord[]? records = null)
    {
        var input = new NasdaqMentorSessionReplayInput("synthetic-test-only", LiquidityFixture.Session(M5Fixture.At(13)),
            new Dictionary<Timeframe, string>(), records ?? SyntheticNasdaqMentorSessionFixture.Records());
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(new()));
        var session = new NasdaqMentorSessionReplay(input);
        var series = M5Fixture.Series().Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.CloseTimeUtc <= (end ?? M5Fixture.At(15, 10))))).ToArray();
        var canonical = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(run, new()), new())
            .ExecuteMentorSession(LiquidityFixture.Definition, series, session);
        return new(input, [], [], canonical, session.Frames, session.Diagnostics);
    }

    [Fact]
    public void SingleCanonicalRunLinksAllHistoricalArtifactsToItsOwnFacts()
    {
        var report = Run();
        var final = report.Frames.Last();
        var snapshot = Assert.IsType<NasdaqHistoricalTradeSnapshotResult.Available>(final.Snapshot).Snapshot;
        var factual = Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(final.FactualEvaluation);
        Assert.Same(snapshot, factual.Snapshot);
        Assert.Same(snapshot, final.DocumentedExit!.Snapshot);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Unique>(final.DocumentedExit);
        Assert.NotEmpty(final.ContactResolution!.Contacts);
        Assert.All(final.ContactResolution.Contacts, c => Assert.Same(snapshot, c.Snapshot));
        var emitted = report.Frames.SelectMany(f => f.Strategy.RuleFacts).Select(f => f.Fact).ToArray();
        Assert.Contains(emitted, f => ReferenceEquals(f, snapshot.PreEntryEligibility));
        Assert.Contains(emitted, f => ReferenceEquals(f, snapshot.StopLoss));
        Assert.Contains(emitted, f => ReferenceEquals(f, snapshot.TakeProfit));
        Assert.Contains(emitted, f => ReferenceEquals(f, snapshot.Risk));
        Assert.Equal(snapshot.PreEntryEligibility.Realignment.Selection.Fact.RealignmentEffectiveAtUtc,
            snapshot.PreEntryEligibility.EligibilityEffectiveAtUtc);
        Assert.Same(report.Canonical!.OutcomeRun.StrategyRun.StrategyObservations.Last(), final.Strategy);
        Assert.Equal(report.Canonical.Frames.Count, report.Frames.Count);
        Assert.Equal(4, report.Canonical.OutcomeRun.StrategyRun.ConfiguredTimeframes.Count);
        Assert.All(report.Frames, f =>
        {
            Assert.All(f.Inputs, o => Assert.True(o.ObservedAtUtc <= f.Strategy.AsOfUtc));
            Assert.Equal(f.Strategy.Evaluations.OrderBy(e => e.Sequence), f.Strategy.Evaluations);
            Assert.All(f.Strategy.Evaluations, e => Assert.Equal(f.Strategy.AsOfUtc, e.EvaluatedAtUtc));
        });
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "realignment" && d.Code == "PrerequisiteUnavailable");
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "realignment" && d.Code == "Bound");
        Assert.All(report.Frames.Where(f => f.Snapshot is NasdaqHistoricalTradeSnapshotResult.Available),
            f => Assert.Same(snapshot, ((NasdaqHistoricalTradeSnapshotResult.Available)f.Snapshot!).Snapshot));
    }

    [Fact]
    public void FutureExitNeverChangesEarlierStrategyOrFactualArtifacts()
    {
        var records = SyntheticNasdaqMentorSessionFixture.Records(M5Fixture.At(15, 10));
        var shortRun = Run(M5Fixture.At(15, 5), records);
        var original = JsonSerializer.Serialize(shortRun.Frames, JsonOptions);
        var later = Run(records: records);
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(shortRun.Frames.Last().DocumentedExit);
        Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(shortRun.Frames.Last().FactualEvaluation);
        Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Available>(later.Frames.Last().FactualEvaluation);
        Assert.Equal(original, JsonSerializer.Serialize(shortRun.Frames, JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(shortRun.Frames, JsonOptions), JsonSerializer.Serialize(later.Frames.Take(shortRun.Frames.Count), JsonOptions));
        Assert.Equal(shortRun.Canonical!.Frames.Select(f => f.Verdict), later.Canonical!.Frames.Take(shortRun.Frames.Count).Select(f => f.Verdict));
        Assert.DoesNotContain(shortRun.BindingDiagnostics, d => d.RecordId == "exit");
    }

    [Fact]
    public void AnotherRunFactIsRejectedWithoutRebinding()
    {
        var foreign = ((NasdaqHistoricalTradeSnapshotResult.Available)Run().Frames.Last().Snapshot!).Snapshot.PreEntryEligibility;
        var report = Run(records: SyntheticNasdaqMentorSessionFixture.Records(foreignEligibility: foreign));
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "stop" && d.Code == "ForeignCanonicalAncestry");
        Assert.DoesNotContain(report.Frames.SelectMany(f => f.Inputs).OfType<NasdaqHumanStructuralStopLossObservation>(), o => ReferenceEquals(o.PreEntryEligibility, foreign));
        Assert.Contains(report.Frames, f => f.Snapshot is NasdaqHistoricalTradeSnapshotResult.Unavailable);
        Assert.DoesNotContain(report.Frames, f => f.Snapshot is NasdaqHistoricalTradeSnapshotResult.Available);
    }

    [Fact]
    public void NoExitDoesNotChangeCanonicalStrategyVerdict()
    {
        var with = Run(); var without = Run(records: SyntheticNasdaqMentorSessionFixture.Records(includeExit: false));
        Assert.Equal(with.Canonical!.Frames.Select(f => f.Verdict), without.Canonical!.Frames.Select(f => f.Verdict));
        Assert.IsType<NasdaqHistoricalFactualTradeEvaluation.Unavailable>(without.Frames.Last().FactualEvaluation);
    }

    [Fact]
    public void InputAndOutputMembershipAreImmutableAndNoEconomicOrExecutionSurfaceExists()
    {
        var records = SyntheticNasdaqMentorSessionFixture.Records(); var files = new Dictionary<Timeframe, string>();
        var input = new NasdaqMentorSessionReplayInput("test", LiquidityFixture.Session(M5Fixture.At(13)), files, records);
        files.Add(M5Fixture.Minute, "later"); records[0] = null!;
        Assert.Empty(input.CsvFiles); Assert.NotNull(input.Evidence[0]);
        var report = Run();
        Assert.Throws<NotSupportedException>(() => ((IList<NasdaqMentorSessionFrame>)report.Frames).Clear());
        Assert.All(new[] { typeof(NasdaqMentorSessionReplayReport), typeof(NasdaqMentorSessionFrame) }, type =>
        {
            Assert.All(type.GetProperties(), p => Assert.Null(p.SetMethod));
            Assert.DoesNotContain(type.GetProperties(), p => p.Name is "Win" or "Loss" or "PnL" or "Profit" or "Order" or "RMultiple" or "Verdict");
        });
    }

    [Fact]
    public void FutureBinderIsNotInvokedBeforeAuthenticAvailability()
    {
        var calls = 0;
        var record = new NasdaqMentorSessionEvidenceRecord("future", M5Fixture.At(15, 10), (_, _) => { calls++; return null; });
        Run(M5Fixture.At(15, 5), SyntheticNasdaqMentorSessionFixture.Records().Append(record).ToArray());
        Assert.Equal(0, calls);
        Run(records: SyntheticNasdaqMentorSessionFixture.Records().Append(record).ToArray());
        Assert.True(calls > 0);
    }

    [Fact]
    public void WrongSnapshotAndMismatchedAvailabilityAreInputDiagnosticsNotRuleFailures()
    {
        var foreign = ((NasdaqHistoricalTradeSnapshotResult.Available)Run().Frames.Last().Snapshot!).Snapshot;
        var records = SyntheticNasdaqMentorSessionFixture.Records(includeExit: false).Concat([
            new NasdaqMentorSessionEvidenceRecord("foreign-exit", M5Fixture.At(15, 5), (_, _) =>
                new NasdaqHistoricalDocumentedExitObservation(new(foreign, "foreign:exit", 105,
                    M5Fixture.At(14, 20).AddSeconds(30), "foreign:record"), M5Fixture.At(15, 5), "foreign:review")),
            new NasdaqMentorSessionEvidenceRecord("invalid-availability", M5Fixture.At(14), (_, _) =>
                M5Fixture.H4(NasdaqHumanH4PermittedDirection.Buy)),
            new NasdaqMentorSessionEvidenceRecord("wrong-day", M5Fixture.At(14), (_, _) =>
                new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version,
                    LiquidityFixture.Provider, LiquidityFixture.Symbol, LiquidityFixture.Session(M5Fixture.At(13)).TradingDay.AddDays(-1),
                    M5Fixture.At(14), "wrong-day:review")),
            new NasdaqMentorSessionEvidenceRecord("invalid-constructor", M5Fixture.At(14), (_, _) =>
                throw new ArgumentException("Invalid reviewed payload")),
        ]).ToArray();
        var report = Run(records: records);
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "foreign-exit" && d.Code == "ForeignCanonicalAncestry");
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "invalid-availability" && d.Code == "EvidenceIdentityOrAvailabilityMismatch");
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "invalid-constructor" && d.Code == "InvalidTypedEvidence");
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "wrong-day" && d.Code == "EvidenceSessionMismatch");
        Assert.IsType<NasdaqHistoricalDocumentedExitSelection.Missing>(report.Frames.Last().DocumentedExit);
        Assert.All(report.Frames.SelectMany(f => f.Strategy.Evaluations), e => Assert.DoesNotContain("ForeignCanonicalAncestry", e.Reason));
    }

    [Fact]
    public void InvalidInputContractRejectsDuplicateRecordsAndNonUtcAvailability()
    {
        var record = SyntheticNasdaqMentorSessionFixture.Records()[0];
        Assert.Throws<ArgumentException>(() => new NasdaqMentorSessionReplayInput("test", LiquidityFixture.Session(M5Fixture.At(13)),
            new Dictionary<Timeframe, string>(), [record, record]));
        Assert.Throws<ArgumentException>(() => new NasdaqMentorSessionEvidenceRecord("bad", M5Fixture.At(13).ToOffset(TimeSpan.FromHours(1)), (_, _) => null));
    }
}
