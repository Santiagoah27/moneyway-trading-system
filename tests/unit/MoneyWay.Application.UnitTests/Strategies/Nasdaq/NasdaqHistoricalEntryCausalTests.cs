using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq;

// Documentary records and canonical projections below are explicitly synthetic; no authentic fills are asserted.
public sealed class NasdaqHistoricalEntryCausalTests
{
    private static DateTimeOffset At(int minute) => LiquidityFixture.At(13).AddMinutes(minute);
    public static NasdaqHistoricalEntryEvidence Evidence() => new(LiquidityFixture.Session(At(32)),
        "WickfillScenario2", "Sell", "synthetic:entry-review", "synthetic:source", "synthetic:human-reviewer",
        "HumanReview", "ReportedExecution", "synthetic:platform", LiquidityFixture.Provider.Value, LiquidityFixture.Symbol.Value,
        "synthetic:13:32Z", "SourceReportedSecond", At(32), At(32), "synthetic:explicit-UTC-source", At(32),
        null, null, null, null);
    public static NasdaqHistoricalEntryFrame Frame(int minute, string wf2 = "Waiting", string? verdict = "HumanValidationRequired") =>
        new(minute, At(minute), verdict, true,
        [new("NQ-WF2-002", "Confirmed", wf2, 100, true, "synthetic:frozen-eligibility", At(minute), null),
         new("NQ-M5-001", "Confirmed", "NotApplicable", 110, true, "synthetic:separate-Standard-path", At(minute), null),
         new("NQ-SL-001", "HumanValidationRequired", wf2 == "Passed" ? "Passed" : "Waiting", 200, true, "synthetic:SL", At(minute), null)]);
    private static NasdaqHistoricalEntryAssessment? Select(int asOf, NasdaqHistoricalEntryEvidence[] evidence, params NasdaqHistoricalEntryFrame[] frames) =>
        new NasdaqHistoricalEntryCausalSelector().Select(LiquidityFixture.Session(At(32)), At(asOf), evidence, frames);

    [Fact]
    public void HumanExecutionIsPreservedWithoutPriceOrDealAndCannotAuthorizeAnEntry()
    {
        var evidence = Evidence(); var result = Select(32, [evidence], Frame(32))!;
        Assert.Equal("Unique", result.SelectionStatus); var item = Assert.Single(result.Observations);
        Assert.Same(evidence, item.Evidence); Assert.Null(item.Evidence.SourceEntryPrice); Assert.Null(item.Evidence.ExecutionReference);
        Assert.Contains("Wf2NotPassedAtOriginalEntry", item.Issues); Assert.Equal("NativeExecutionUnverified", item.NativeExecutionStatus);
        Assert.False(result.NativeExecutionIndependentlyVerified); Assert.Equal("NotGrantedByDiagnostic", result.EntryAuthorization);
        Assert.Equal("inconclusive", result.FailureClassification);
    }

    [Fact]
    public void LaterPassedFramesCannotBackdateTheRecordedEntryOrOriginalRequiredRules()
    {
        var before = Select(32, [Evidence()], Frame(32))!;
        var later = Select(36, [Evidence()], Frame(32), Frame(35, "Passed", "Ready"), Frame(36, "Passed", "Ready"))!;
        Assert.Equal(JsonSerializer.Serialize(before.Observations), JsonSerializer.Serialize(later.Observations));
        Assert.Equal(At(32), later.Observations.Single().CanonicalAtEntry!.AsOfUtc);
        Assert.Equal("Waiting", later.Observations.Single().CanonicalAtEntry!.Rules[0].Result);
        Assert.Equal(At(32), later.Observations.Single().Evidence.EarliestEventUtc);
    }

    [Fact]
    public void UnknownAvailabilityIsRetrospectiveAndKnownFutureEvidenceCannotLeak()
    {
        var retrospective = Evidence() with { HistoricalAvailableAtUtc = null, ReviewCreatedOn = new(2026, 11, 5) };
        var note = Select(36, [retrospective], Frame(32))!;
        Assert.Equal("RetrospectiveOnly", note.SelectionStatus);
        Assert.Equal("RetrospectiveOnly", note.Observations.Single().Availability);
        Assert.Contains("HistoricalAvailabilityUnknown_NotAdmitted", note.Observations.Single().Issues);
        var future = Evidence() with { HistoricalAvailableAtUtc = At(37), SourceEntryPrice = 999 };
        Assert.Null(Select(36, [future], Frame(32)));
        var expected = JsonSerializer.Serialize(Select(36, [Evidence()], Frame(32)));
        Assert.Equal(expected, JsonSerializer.Serialize(Select(36, [Evidence(), future], Frame(32), Frame(37, "Passed", "Ready"))));
        Assert.Contains("EvidenceAvailableOnlyAfterEntry_NoBackdating", Select(37, [future], Frame(32))!.Observations.Single().Issues);
        Assert.Null(Select(31, [retrospective], Frame(32)));
    }

    [Fact]
    public void ApproximateTimeIsAnIntervalAndDoesNotBecomeAnExactFillOrInventABoundary()
    {
        var approximate = Evidence() with { TimePrecision = "Approximate", EarliestEventUtc = At(31), LatestEventUtc = At(33) };
        var item = Select(36, [approximate], Frame(32), Frame(35, "Passed", "Ready"))!.Observations.Single();
        Assert.Null(item.CanonicalAtEntry); Assert.Contains("ApproximateEventTime_NoExactFill", item.Issues);
        Assert.Equal(At(31), item.Evidence.EarliestEventUtc); Assert.Equal(At(33), item.Evidence.LatestEventUtc);
        var absent = Evidence() with { EarliestEventUtc = At(32).AddSeconds(10), LatestEventUtc = At(32).AddSeconds(10), HistoricalAvailableAtUtc = At(33) };
        Assert.Null(Select(36, [absent], Frame(32))!.Observations.Single().CanonicalAtEntry);
    }

    [Fact]
    public void NativeDocumentWithExplicitExactFieldsRemainsSeparateFromStrategyAuthorization()
    {
        var native = Evidence() with
        {
            Origin = "NativeExecutionRecord",
            TimePrecision = "ExactNativeRecord",
            ExecutionReference = "synthetic:deal-123",
            SourceEntryPrice = 100.12345678901234567890123456m,
            SourcePriceUnit = "synthetic:quoted-price"
        };
        var result = Select(32, [native], Frame(32, "Passed", "Ready"))!;
        var item = result.Observations.Single(); Assert.Equal("NativeRecordDocumented", item.NativeExecutionStatus);
        Assert.Equal("Ready", item.CanonicalAtEntry!.Verdict); Assert.DoesNotContain("Wf2NotPassedAtOriginalEntry", item.Issues);
        Assert.Equal("100.12345678901234567890123456", item.Evidence.SourceEntryPriceText);
        Assert.False(result.NativeExecutionIndependentlyVerified); Assert.Equal("NotGrantedByDiagnostic", result.EntryAuthorization);
    }

    [Theory]
    [InlineData("scenario")]
    [InlineData("direction")]
    public void OtherStrategyPathOrDirectionCannotBeSelectedAsWf2Sell(string wrong)
    {
        var item = Evidence() with { Scenario = wrong == "scenario" ? "Standard" : "WickfillScenario2", Direction = wrong == "direction" ? "Buy" : "Sell" };
        Assert.Null(Select(36, [item], Frame(32)));
    }

    [Fact]
    public void InstrumentConflictAndRetrospectivePriceNeverBecomeNativeUs100Evidence()
    {
        var foreign = Evidence() with { SourceSymbol = "USTEC", SourceProviderId = null, HistoricalAvailableAtUtc = null, SourceEntryPrice = 29604m };
        var item = Select(36, [foreign], Frame(32))!.Observations.Single();
        Assert.Contains("OriginalInstrumentMismatch_NoMapping", item.Issues); Assert.Equal("NativeExecutionUnverified", item.NativeExecutionStatus);
        var conflict = foreign with { ReviewId = "synthetic:other", SourceSymbol = "US100", HistoricalAvailableAtUtc = At(32) };
        var local = Evidence() with { ReviewId = "synthetic:third" };
        Assert.Equal("Conflict", Select(36, [conflict, local], Frame(32))!.SelectionStatus);
        Assert.Equal("Unique", Select(36, [foreign, local], Frame(32))!.SelectionStatus); // Unknown availability cannot change causal selection.
    }

    [Theory]
    [InlineData("source")]
    [InlineData("human")]
    [InlineData("utc")]
    [InlineData("mapping")]
    [InlineData("range")]
    public void InvalidProvenanceAndImplicitNativeConversionAreRejected(string invalid)
    {
        var e = Evidence() with
        {
            SourceReference = invalid == "source" ? "" : "synthetic:source",
            HumanValidationIdentity = invalid == "human" ? "" : "synthetic:human",
            EarliestEventUtc = invalid == "utc" ? At(32).ToOffset(TimeSpan.FromHours(-5)) : At(32),
            LatestEventUtc = invalid == "range" ? At(31) : At(32),
            TimeMappingReference = invalid == "mapping" ? null : "synthetic:mapping"
        };
        Assert.Throws<ArgumentException>(() => Select(36, [e], Frame(32)));
    }

    [Fact]
    public void UnconvertedNativeClockAndOrderPlacementRemainUnresolvedObservations()
    {
        var e = Evidence() with
        {
            OriginalTime = "native:16:32",
            EarliestEventUtc = null,
            LatestEventUtc = null,
            HistoricalAvailableAtUtc = null,
            TimeMappingReference = null,
            TimePrecision = "Unknown",
            EventKind = "OrderPlacement"
        };
        var item = Select(36, [e], Frame(32))!.Observations.Single();
        Assert.Contains("EventUtcUnknown_NoImplicitConversion", item.Issues); Assert.Contains("OrderPlacementIsNotExecution", item.Issues);
        Assert.Null(item.CanonicalAtEntry);
    }
}
