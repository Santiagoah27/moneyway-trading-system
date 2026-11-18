using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq;

// All native records in this fixture are synthetic documentary assertions, never authenticated broker executions.
public sealed class NasdaqHistoricalExitCausalTests
{
    private static DateTimeOffset At(int minute) => LiquidityFixture.At(13).AddMinutes(minute);
    private static readonly NasdaqDemoSessionIdentity Session = LiquidityFixture.Session(At(32));
    private static NasdaqHistoricalEntryEvidence Opening() => NasdaqHistoricalEntryCausalTests.Evidence() with
    { Origin = "NativeExecutionRecord", EventKind = "ReportedExecution", ExecutionReference = "synthetic:opening-deal", PositionReference = "synthetic:position", SourceEntryPrice = 110, SourcePriceUnit = "NativePrice", TimePrecision = "ExactNativeRecord", HistoricalAvailableAtUtc = At(32) };
    private static NasdaqHistoricalExitEvidence Exit() => new(Session, "synthetic:exit", "synthetic:retained-close-record", "synthetic:reviewer", "NativeExecutionRecord", "ReportedClosure", Opening().SourcePlatform)
    {
        SourceProviderId = Session.ProviderId.Value,
        SourceSymbol = Session.Symbol.Value,
        OriginalTime = "synthetic:13:37Z",
        OriginalClockDomain = "synthetic:UTC",
        TimePrecision = "ExactNativeRecord",
        EarliestEventUtc = At(37),
        LatestEventUtc = At(37),
        TimeMappingReference = "synthetic:source-qualified-UTC",
        HistoricalAvailableAtUtc = At(37),
        ExitPrice = 100,
        PriceUnit = "NativePrice",
        ExecutionReference = "synthetic:closing-deal",
        PositionReference = "synthetic:position",
        LinkedEntryReviewId = Opening().ReviewId,
        LinkedEntryExecutionReference = Opening().ExecutionReference,
        AssociationSourceReference = "synthetic:position-history",
        DocumentedCloseMechanism = "synthetic:manual-close",
        CloseMechanismSourceReference = "synthetic:retained-close-record"
    };
    private static NasdaqHistoricalExitAssessment? Select(NasdaqHistoricalExitEvidence e, NasdaqHistoricalEntryEvidence? opening = null, int at = 38)
    {
        var entry = new NasdaqHistoricalEntryCausalSelector().Select(Session, At(at), [opening ?? Opening()], [NasdaqHistoricalEntryCausalTests.Frame(32)]);
        return new NasdaqHistoricalExitCausalSelector().Select(Session, At(at), [e], entry);
    }
    [Fact]
    public void CompleteSyntheticNativeProvenanceDocumentsIdentityWithoutAuthenticatingExecutionsOrOutcome()
    {
        var view = Select(Exit())!; var e = Assert.Single(view.Observations);
        Assert.True(e.NativeIdentityConsistent); Assert.Equal("NativeIdentityConsistent_NotIndependentlyVerified", e.EntryAssociation);
        Assert.False(view.NativeExitIndependentlyVerified); Assert.False(view.EntryExitRelationshipIndependentlyVerified);
        Assert.False(view.CompleteSupervisedTrade); Assert.False(view.EconomicOutcomeSupported); Assert.True(view.HumanValidationRequired);
        Assert.Equal("100", e.Evidence.ExitPriceText); Assert.Null(e.Evidence.Quantity); Assert.Equal(At(37), e.Evidence.EffectiveAtUtc);
        Assert.Equal("synthetic:manual-close", e.Evidence.DocumentedCloseMechanism);
    }
    [Fact]
    public void RetrospectiveApproximateCloseDoesNotInventBoundsFillMechanismQuantityOrRealizedOneR()
    {
        var close = Exit() with
        {
            Origin = "HumanReview",
            TimePrecision = "Approximate",
            NominalEventUtc = At(37),
            EarliestEventUtc = null,
            LatestEventUtc = null,
            HistoricalAvailableAtUtc = null,
            ExitPrice = null,
            PriceUnit = null,
            ExecutionReference = null,
            PositionReference = null,
            LinkedEntryExecutionReference = null,
            DocumentedCloseMechanism = null,
            CloseMechanismSourceReference = null,
            ReportedStopProtection = "synthetic:reviewer reports approximately 1:1 protection",
            StopProtectionSourceReference = "synthetic:separate-management-review"
        };
        var v = Select(close)!; var o = Assert.Single(v.Observations);
        Assert.Equal("RetrospectiveOnly", o.Availability); Assert.Equal("DocumentaryCandidate_OrderingRequiresValidation", o.EntryAssociation);
        Assert.Null(o.Evidence.EffectiveAtUtc); Assert.Null(o.Evidence.Quantity); Assert.Null(o.Evidence.ExitPrice);
        Assert.Contains("ReportedProtectionIsNotExecutionOrRealizedReturn", o.Issues); Assert.Contains("CloseMechanismUnknown", o.Issues);
        Assert.False(o.NativeIdentityConsistent); Assert.False(v.CompleteSupervisedTrade);
    }
    [Theory]
    [InlineData("source")]
    [InlineData("price-unit")]
    [InlineData("quantity-unit")]
    [InlineData("mapping")]
    [InlineData("association")]
    [InlineData("mechanism")]
    [InlineData("protection")]
    public void MissingProvenanceAndInventedDefaultsAreRejected(string field)
    {
        var e = Exit(); e = field switch
        {
            "source" => e with { SourceReference = "" },
            "price-unit" => e with { PriceUnit = null },
            "quantity-unit" => e with { Quantity = 1 },
            "mapping" => e with { TimeMappingReference = null },
            "association" => e with { AssociationSourceReference = null },
            "mechanism" => e with { CloseMechanismSourceReference = null },
            _ => e with { ReportedStopProtection = "synthetic:1:1", StopProtectionSourceReference = null }
        };
        Assert.Throws<ArgumentException>(() => Select(e));
    }
    [Theory]
    [InlineData("symbol")]
    [InlineData("provider")]
    [InlineData("position")]
    [InlineData("opening-reference")]
    [InlineData("missing-reference")]
    [InlineData("missing-entry")]
    [InlineData("before")]
    [InlineData("equal")]
    public void ConflictingOrInsufficientIdentityAndOrderingNeverVerifyTheTrade(string field)
    {
        var e = Exit(); e = field switch
        {
            "symbol" => e with { SourceSymbol = "USTEC" },
            "provider" => e with { SourceProviderId = "synthetic:other" },
            "position" => e with { PositionReference = "synthetic:other-position" },
            "opening-reference" => e with { LinkedEntryExecutionReference = "synthetic:other-deal" },
            "missing-reference" => e with { ExecutionReference = null },
            "missing-entry" => e with { LinkedEntryReviewId = "synthetic:missing-review" },
            "before" => e with { EarliestEventUtc = At(31), LatestEventUtc = At(31) },
            _ => e with { EarliestEventUtc = At(32), LatestEventUtc = At(32) }
        };
        var v = Select(e)!; Assert.False(v.Observations.Single().NativeIdentityConsistent); Assert.False(v.EntryExitRelationshipIndependentlyVerified);
        if (field is "symbol" or "provider" or "opening-reference" or "missing-entry" or "before") Assert.Equal("Unresolved", v.Observations.Single().EntryAssociation);
    }
    [Fact]
    public void ExplicitUstecDocumentaryAssociationDoesNotMergeWithUs100AndProtectionIsNotClosure()
    {
        var opening = Opening() with { SourceSymbol = "USTEC", Origin = "HumanReview" };
        var e = Exit() with { SourceSymbol = "USTEC", Origin = "HumanReview" };
        Assert.Equal("DocumentaryAssociation", Select(e, opening)!.Observations.Single().EntryAssociation);
        Assert.Equal("Unresolved", Select(e with { SourceSymbol = "US100" }, opening)!.Observations.Single().EntryAssociation);
        var protection = Select(e with { EventKind = "ReportedStopProtection" }, opening)!.Observations.Single();
        Assert.Equal("StopProtectionOnly", protection.ObservationStatus); Assert.Equal("Unresolved", protection.EntryAssociation);
    }
    [Fact]
    public void CompetingHumanAndNativeClaimsRetainConflictWithoutChoosingAWinner()
    {
        var e = Exit(); var other = e with { ReviewId = "synthetic:other", Origin = "HumanReview", ExitPrice = 101 };
        var entry = new NasdaqHistoricalEntryCausalSelector().Select(Session, At(38), [Opening()], []);
        var v = new NasdaqHistoricalExitCausalSelector().Select(Session, At(38), [other, e], entry)!;
        Assert.All(v.Observations, o => { Assert.Equal("Conflict", o.ObservationStatus); Assert.False(o.NativeIdentityConsistent); });
    }
    [Fact]
    public void FutureAvailabilityAndFutureEventsCannotChangeEarlierSnapshotsOrApproveOriginalEntry()
    {
        var e = Exit(); Assert.Null(Select(e, at: 36));
        Assert.Null(Select(e with { HistoricalAvailableAtUtc = At(39) }));
        Assert.Null(Select(e with { HistoricalAvailableAtUtc = null, TimePrecision = "Approximate", NominalEventUtc = At(39), EarliestEventUtc = null, LatestEventUtc = null }));
        var earlier = Select(e)!; var frozen = JsonSerializer.Serialize(earlier);
        _ = Select(e with { ReviewCreatedOn = new(2026, 11, 17) }, at: 39);
        Assert.Equal(frozen, JsonSerializer.Serialize(earlier)); Assert.Equal(frozen, JsonSerializer.Serialize(Select(e)));
        var entry = new NasdaqHistoricalEntryCausalSelector().Select(Session, At(38), [Opening()], [NasdaqHistoricalEntryCausalTests.Frame(32)]);
        var lifecycle = NasdaqHistoricalTradeLifecycleSummary.FromRules([]) with { Path = "WickfillScenario2", HistoricalEntry = entry, HistoricalExit = earlier };
        var snapshot = new NasdaqSupervisedHistoricalTradeSnapshotAssembler().Assemble(Session, 38, At(38), "HumanValidationRequired", lifecycle);
        Assert.True(snapshot.HistoricalExitObserved); Assert.False(snapshot.CanonicalEligibilityRecordedAtEntry);
        Assert.False(snapshot.ExitSupported); Assert.False(snapshot.CompleteSupervisedTrade); Assert.Null(snapshot.EconomicOutcome);
        Assert.Equal("NotGrantedBySnapshot", snapshot.EntryAuthorization);
    }
}
