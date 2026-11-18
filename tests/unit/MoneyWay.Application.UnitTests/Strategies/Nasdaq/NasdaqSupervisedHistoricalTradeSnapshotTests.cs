using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq;

// Synthetic projection inputs only. Canonical end-to-end evaluator coverage lives in integration tests.
public sealed class NasdaqSupervisedHistoricalTradeSnapshotTests
{
    private static DateTimeOffset At(int minute) => LiquidityFixture.At(13).AddMinutes(minute);
    private static readonly NasdaqDemoSessionIdentity Session = LiquidityFixture.Session(At(32));
    private static readonly NasdaqSupervisedHistoricalTradeSnapshotAssembler Assembler = new();
    private static NasdaqHistoricalTradeLifecycleSummary Empty() => NasdaqHistoricalTradeLifecycleSummary.FromRules([]);
    private static NasdaqHistoricalEntryAssessment Entry(bool ready = false, bool native = false, bool retrospective = false, bool approximate = false)
    {
        var e = NasdaqHistoricalEntryCausalTests.Evidence() with
        {
            Origin = native ? "NativeExecutionRecord" : "HumanReview",
            ExecutionReference = native ? "synthetic:deal" : null,
            SourceEntryPrice = native ? 110 : null,
            SourcePriceUnit = native ? "NativeInstrumentPrice" : null,
            HistoricalAvailableAtUtc = retrospective ? null : At(32),
            TimePrecision = approximate ? "Approximate" : native ? "ExactNativeRecord" : "SourceReportedSecond",
            LatestEventUtc = approximate ? At(33) : At(32)
        };
        return new NasdaqHistoricalEntryCausalSelector().Select(Session, At(36), [e], [NasdaqHistoricalEntryCausalTests.Frame(32, ready ? "Passed" : "Waiting", ready ? "Ready" : "HumanValidationRequired")])!;
    }
    private static NasdaqHistoricalTradeLifecycleSummary Parameters(bool riskPassed = true)
    {
        NasdaqHistoricalTradeSource Source(string name) => NasdaqHistoricalTradeLifecycleSummary.Source(name, At(31), "synthetic:" + name);
        var binding = new NasdaqHistoricalScenario2Binding(Session, 31, At(31));
        return Empty() with
        {
            Path = "WickfillScenario2",
            HistoricalEntry = Entry(ready: true, native: true),
            Stages = Empty().Stages.Select(s => s.Stage is "StopLoss" or "Risk" or "TakeProfit"
                ? s with { RuleResult = s.Stage == "Risk" && !riskPassed ? "Failed" : "Passed", Reason = "synthetic:canonical-evaluation" } : s).ToArray(),
            Scenario2StopLoss = new("synthetic:reference", 115, 116, At(31), At(31), Source("SL"), Source("protective")) { Binding = binding },
            Scenario2RiskExposure = new(new(10000, "USD", "synthetic:account-basis", riskPassed ? 100 : 101, 110, "synthetic:costs", "synthetic:calculation"),
                116, riskPassed ? .01m : .0101m, false, .01m, riskPassed, At(31), At(31), Source("risk"))
            { Binding = binding },
            Scenario2TakeProfit = new(Session.ProviderId.Value, Session.Symbol.Value, "Sell", "SessionLevel", "AsiaLow", new string('a', 64),
                90, 90, 116, At(31), At(31), Source("TP"), Source("selected-low"))
            { Binding = binding }
        };
    }
    private static NasdaqSupervisedHistoricalTradeSnapshot Snapshot(NasdaqHistoricalTradeLifecycleSummary value) => Assembler.Assemble(Session, 36, At(36), "HumanValidationRequired", value);

    [Fact]
    public void EmptySnapshotAndPreentryOnlyKeepIndependentMissingComponents()
    {
        var empty = Snapshot(Empty()); Assert.False(empty.HistoricalEntryObserved); Assert.False(empty.DocumentedParameterSetAvailable);
        Assert.Equal(8, empty.Components.Count); Assert.Null(empty.StopLoss); Assert.Null(empty.Risk); Assert.Null(empty.TakeProfit);
        Assert.Contains("HistoricalEntryMissing", empty.MissingEvidence); Assert.False(empty.CompleteSupervisedTrade);
        var preentry = Snapshot(Empty() with { HistoricalEntry = Entry(ready: true) });
        Assert.True(preentry.HistoricalEntryObserved); Assert.True(preentry.CanonicalEligibilityRecordedAtEntry);
        Assert.False(preentry.StopLossSupported); Assert.False(preentry.NativeExecutionDocumented); Assert.Null(preentry.Exit); Assert.Null(preentry.EconomicOutcome);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void HumanEntryApproximationAndRetrospectionNeverGrantAuthorization(bool approximate, bool retrospective)
    {
        var view = Snapshot(Empty() with { HistoricalEntry = Entry(retrospective: retrospective, approximate: approximate) });
        Assert.True(view.HistoricalEntryObserved); Assert.False(view.CanonicalEligibilityRecordedAtEntry);
        Assert.Equal("NotGrantedBySnapshot", view.EntryAuthorization); Assert.False(view.NativeExecutionIndependentlyVerified);
        Assert.False(view.CompleteSupervisedTrade); Assert.Equal("inconclusive", view.FailureClassification);
        Assert.Null(view.DocumentaryEntry!.Observations.Single().Evidence.SourceEntryPrice);
    }

    [Fact]
    public void DocumentaryParametersAreAvailableWithoutClaimingVerifiedExecutionExitOrOutcome()
    {
        var source = Parameters(); var view = Snapshot(source);
        Assert.Same(source.HistoricalEntry, view.DocumentaryEntry); Assert.Same(source.Scenario2StopLoss, view.StopLoss);
        Assert.Same(source.Scenario2RiskExposure, view.Risk); Assert.Same(source.Scenario2TakeProfit, view.TakeProfit);
        Assert.True(view.CanonicalBindingsConsistent); Assert.True(view.StopLossSupported); Assert.True(view.RiskExposureSupported); Assert.True(view.TakeProfitSupported);
        Assert.True(view.NativeExecutionDocumented); Assert.True(view.DocumentedParameterSetAvailable);
        Assert.Equal("native_identity_matches", view.InstrumentCorrespondence); Assert.False(view.CompleteSupervisedTrade);
        Assert.False(view.ExitSupported); Assert.False(view.EconomicOutcomeSupported); Assert.Null(view.Exit); Assert.Null(view.EconomicOutcome);
        Assert.Null(view.Risk!.Exposure.Quantity); Assert.Null(view.Risk.Exposure.QuantityUnit);
    }

    [Fact]
    public void DocumentedFailedRiskRemainsFailedRatherThanMissingOrApproved()
    {
        var view = Snapshot(Parameters(false)); Assert.True(view.RiskExposureSupported);
        Assert.Equal("Failed", view.Components.Single(c => c.Stage == "Risk").RuleResult);
        Assert.False(view.Risk!.IsWithinLimit); Assert.Equal(101, view.Risk.Exposure.ReportedMaximumLoss);
        Assert.Equal(10000, view.Risk.Exposure.AccountBasisAmount); Assert.False(view.CompleteSupervisedTrade);
    }

    [Theory]
    [InlineData("SL")]
    [InlineData("Risk")]
    [InlineData("TP")]
    public void MissingCanonicalConstituentRemainsExplicit(string missing)
    {
        var p = Parameters(); p = p with
        {
            Scenario2StopLoss = missing == "SL" ? null : p.Scenario2StopLoss,
            Scenario2RiskExposure = missing == "Risk" ? null : p.Scenario2RiskExposure,
            Scenario2TakeProfit = missing == "TP" ? null : p.Scenario2TakeProfit
        };
        var v = Snapshot(p); Assert.False(v.CanonicalBindingsConsistent); Assert.False(v.DocumentedParameterSetAvailable);
        Assert.Equal(missing != "SL", v.StopLossSupported); Assert.Equal(missing != "Risk", v.RiskExposureSupported); Assert.Equal(missing != "TP", v.TakeProfitSupported);
    }

    [Theory]
    [InlineData("instrument")]
    [InlineData("start")]
    [InlineData("stop-price")]
    [InlineData("missing-binding")]
    public void NativeBindingsAndConflictingPricesCannotFormACompatibleParameterSet(string mismatch)
    {
        var p = Parameters(); var risk = p.Scenario2RiskExposure!;
        p = p with
        {
            Scenario2RiskExposure = risk with
            {
                StopPrice = mismatch == "stop-price" ? 117 : 116,
                Binding = mismatch == "missing-binding" ? null : risk.Binding! with
                {
                    Session = mismatch == "instrument" ? new(Session.StrategyId, Session.StrategyVersion, Session.ProviderId, new("USTEC"), Session.TradingDay) : Session,
                    StartStep = mismatch == "start" ? 30 : 31
                }
            }
        };
        var v = Snapshot(p); Assert.False(v.CanonicalBindingsConsistent); Assert.Equal("unresolved", v.InstrumentCorrespondence);
        Assert.False(v.DocumentedParameterSetAvailable); Assert.False(v.CompleteSupervisedTrade);
    }

    [Fact]
    public void MixedUstecEntryNeverBecomesUs100FillAndLaterEligibilityCannotReplaceTheOriginalFrame()
    {
        var p = Parameters(); var evidence = NasdaqHistoricalEntryCausalTests.Evidence() with { SourceSymbol = "USTEC", SourceProviderId = null, HistoricalAvailableAtUtc = null, SourceEntryPrice = 29604m };
        var audit = new NasdaqHistoricalEntryCausalSelector().Select(Session, At(36), [evidence],
            [NasdaqHistoricalEntryCausalTests.Frame(32), NasdaqHistoricalEntryCausalTests.Frame(35, "Passed", "Ready")]);
        var v = Snapshot(p with { HistoricalEntry = audit });
        Assert.True(v.HistoricalEntryObserved); Assert.False(v.CanonicalEligibilityRecordedAtEntry); Assert.False(v.NativeExecutionDocumented);
        Assert.Equal("unresolved", v.InstrumentCorrespondence); Assert.Equal(29604, v.DocumentaryEntry!.Observations[0].Evidence.SourceEntryPrice);
        Assert.Equal(At(32), v.DocumentaryEntry.Observations[0].CanonicalAtEntry!.AsOfUtc); Assert.False(v.CompleteSupervisedTrade);
    }

    [Fact]
    public void EarlierNativeExecutionCannotBorrowParametersFromALaterCanonicalSetup()
    {
        var p = Parameters(); var laterStart = p.Scenario2StopLoss!.Binding! with { StartStep = 35, StartAsOfUtc = At(35) };
        var view = Snapshot(p with
        {
            Scenario2StopLoss = p.Scenario2StopLoss with { Binding = laterStart },
            Scenario2RiskExposure = p.Scenario2RiskExposure! with { Binding = laterStart },
            Scenario2TakeProfit = p.Scenario2TakeProfit! with { Binding = laterStart }
        });
        Assert.True(view.NativeExecutionDocumented); Assert.True(view.CanonicalBindingsConsistent);
        Assert.False(view.DocumentedParameterSetAvailable); Assert.Contains("EntrySetupAssociationUnresolved", view.MissingEvidence);
        Assert.Equal(At(32), view.DocumentaryEntry!.Observations.Single().CanonicalAtEntry!.AsOfUtc);
    }

    [Fact]
    public void FutureConstituentsAreRejectedAndRepeatedAssemblyDoesNotMutateEarlierSnapshots()
    {
        var p = Parameters(); var before = Snapshot(p); var expected = JsonSerializer.Serialize(before);
        Assert.Equal(expected, JsonSerializer.Serialize(Snapshot(p)));
        var future = p with { Scenario2StopLoss = p.Scenario2StopLoss! with { ObservedAtUtc = At(37) } };
        Assert.Throws<ArgumentException>(() => Snapshot(future));
        var later = Assembler.Assemble(Session, 37, At(37), "Ready", future with { HistoricalEntry = null });
        Assert.Equal(At(37), later.AsOfUtc); Assert.Equal(expected, JsonSerializer.Serialize(before));
        Assert.Null(Assembler.Project(Session, 36, At(36), "Ready", [], p with { Path = "Standard" }).SupervisedSnapshot);
    }
}
