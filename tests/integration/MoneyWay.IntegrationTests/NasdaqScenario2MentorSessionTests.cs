using System.Text;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

/// <summary>Synthetic evidence only. No mentor prices, execution or historical availability is asserted.</summary>
public sealed class NasdaqScenario2MentorSessionTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "moneyway-synthetic-wf2-" + Guid.NewGuid());
    private static DateTimeOffset At(int hour, int minute = 0) => LiquidityFixture.At(hour).AddMinutes(minute);
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly string[] StandardRules = ["NQ-M5-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003"];

    private static CandleSeries[] Series() => Enumerable.Range(-2, 20).Select(h => LiquidityFixture.Candle(h))
        .Concat(new[] { 0, 4, 8, 12 }.Select(h => LiquidityFixture.Candle(h, hours: 4)))
        .Concat(new[] { At(13, 5), At(13, 15), At(13, 30), At(13, 31), At(13, 32), At(13, 33), At(14), At(15), At(16), At(17) }
            .Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M1, t.AddMinutes(-1), t, 100, 110, 90, 100, null)))
        .Concat(new[] { At(13, 5), At(13, 30), At(14), At(15), At(16), At(17) }
            .Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, M5, t.AddMinutes(-5), t, 100, 110, 90, 100, null)))
        .GroupBy(c => c.Timeframe).Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc))).ToArray();

    private static StrategyReplayContext Upstream(DateTimeOffset? at = null, IEnumerable<IStrategyReplayInputObservation>? inputs = null)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(Series());
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == (at ?? At(13, 5))) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs ?? []);
        throw new InvalidOperationException("Synthetic upstream missing.");
    }

    private static NasdaqHumanWickfillScenario2ContextObservation ContextEvidence(bool unavailable = false, int dayOffset = 0)
    {
        var original = LiquidityFixture.Session(At(13));
        var session = new NasdaqDemoSessionIdentity(original.StrategyId, original.StrategyVersion, original.ProviderId, original.Symbol, original.TradingDay.AddDays(dayOffset));
        DateTimeOffset T(int h) => At(h).AddDays(dayOffset);
        var h4 = new NasdaqHumanH4ContextObservation(session, new(NasdaqHumanH4PermittedDirection.Sell,
            NasdaqHumanH4ContextKind.Wickfill, T(8), T(12), [],
            [new(NasdaqHumanH4StructuralRole.LowerHigh, 100, 120, NasdaqHumanH4ZoneBoundaryPrecision.Approximate, [T(8)])]),
            T(13).AddMinutes(5), "synthetic:h4");
        var sources = new[] { 0, 4, 8 }.Select(h => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol,
            NasdaqHumanH4ContextObservation.H4, T(h), T(h + 4), 100, 110, 90, 100, null)).ToList();
        if (unavailable) sources.Add(LiquidityFixture.Candle(-4, hours: 4));
        return new(h4, h4.Fact.ZoneAnchors.Single(), sources, T(13).AddMinutes(10), T(13).AddMinutes(10), "synthetic:context");
    }

    private static StrategyReplayContextObservation? Owner(StrategyReplayContext context) => context.PriorObservations
        .SingleOrDefault(o => o.AsOfUtc == At(13, 31) && o.RuleFacts.Any(f => f.Fact is NasdaqLiquidityTakeRuleFact));

    private static NasdaqMentorSessionEvidenceRecord Branch(string id = "branch", NasdaqStrategyPath path = NasdaqStrategyPath.WickfillScenario2,
        StrategyReplayContextObservation? foreign = null, DateTimeOffset? observed = null) => new(id, observed ?? At(13, 31), (c, _) =>
        (foreign ?? Owner(c)) is { } owner ? new NasdaqHumanBranchApplicabilityObservation(owner, path, At(13, 10), observed ?? At(13, 15), "synthetic:" + id) : null);

    private static NasdaqMentorSessionEvidenceRecord Turn(string id = "turn", DateTimeOffset? observed = null,
        StrategyReplayContextObservation? foreign = null, IReadOnlyList<IStrategyReplayInputObservation>? inputs = null) => new(id, observed ?? At(13, 32), (c, _) =>
    {
        if (inputs is not null) c = Upstream(c.AsOfUtc, inputs).WithPriorObservations(c.PriorObservations);
        var owner = foreign ?? Owner(c);
        var selected = new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(c, LiquidityFixture.Session(At(13)));
        if (owner is null || selected is not NasdaqHumanWickfillScenario2ContextSelection.Unique unique) return null;
        var sweep = NasdaqWickfillScenario2OpeningSweepContext.From(c, unique, owner);
        return new NasdaqHumanWickfillScenario2PostSweepReversalObservation(sweep, "synthetic:" + id, At(13, 32), observed ?? At(13, 32), "synthetic:" + id);
    });

    private NasdaqMentorSessionReplayInput Input(string mode = "valid", IEnumerable<NasdaqMentorSessionEvidenceRecord>? extra = null, string eventId = "synthetic:take")
    {
        Directory.CreateDirectory(directory);
        var files = new Dictionary<Timeframe, string>();
        foreach (var series in Series())
        {
            var path = Path.Combine(directory, $"{series.Timeframe.Amount}-{series.Timeframe.Unit}.csv");
            if (!File.Exists(path)) File.WriteAllText(path, "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume\n" +
                string.Join("\n", series.Candles.Select(c => FormattableString.Invariant($"{c.ProviderId.Value},{c.Symbol.Value},{c.Timeframe.Amount},{c.Timeframe.Unit},{c.OpenTimeUtc:O},{c.CloseTimeUtc:O},{c.Open},{c.High},{c.Low},{c.Close},{c.Volume}"))), new UTF8Encoding(false));
            files.Add(series.Timeframe, path);
        }
        var upstream = Upstream(); var context = ContextEvidence(mode == "unavailable-context");
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
            NasdaqLiquidityTakeReference.SessionEndpoint.LondonHigh, upstream.AsOfUtc, "synthetic:endpoint");
        var take = new NasdaqHumanLiquidityTakeObservation(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider,
            LiquidityFixture.Symbol, eventId, reference.ReferencePrice + 1, At(13, 31), "synthetic:take"), upstream.AsOfUtc, At(13, 31), "synthetic:take");
        var inputs = new List<IStrategyReplayInputObservation> { context.H4Context, take,
            new NasdaqHumanRelevantLiquidityTakeObservation(take, take.ObservedAtUtc, "synthetic:relevant"),
            new NasdaqPreparationCompletionObservation(take.StrategyId, take.StrategyVersion, take.ProviderId, take.Symbol,
                take.Session.TradingDay, At(13, 15), "synthetic:prep") };
        if (mode != "missing-context") inputs.Add(context);
        var records = inputs.Select((o, i) => NasdaqMentorSessionEvidenceRecord.FromObservation("base:" + i, o)).ToList();
        if (mode != "missing-branch") records.Add(Branch(path: mode == "standard" ? NasdaqStrategyPath.Standard : NasdaqStrategyPath.WickfillScenario2));
        if (mode == "conflict-branch") records.Add(Branch("other-branch", NasdaqStrategyPath.Standard));
        if (mode != "missing-turn") records.Add(Turn(observed: mode == "future-turn" ? At(17) : null, inputs: inputs));
        if (mode == "duplicate-turn") records.Add(new("duplicate-turn", At(13, 32), Turn(inputs: inputs).Bind));
        if (mode == "conflict-turn") records.Add(Turn("other-turn", inputs: inputs));
        if (mode == "cancel")
        {
            var oppositeReference = new NasdaqLiquidityTakeReference.SessionLevel(upstream, new NasdaqSessionLiquidityCalculator().Calculate(upstream),
                NasdaqLiquidityTakeReference.SessionEndpoint.LondonLow, upstream.AsOfUtc, "synthetic:opposite");
            var opposite = new NasdaqHumanLiquidityTakeObservation(oppositeReference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider,
                LiquidityFixture.Symbol, "synthetic:terminal", oppositeReference.ReferencePrice - 1, At(15), "synthetic:terminal"), upstream.AsOfUtc, At(15), "synthetic:terminal");
            records.Add(NasdaqMentorSessionEvidenceRecord.FromObservation("terminal", opposite));
            records.Add(NasdaqMentorSessionEvidenceRecord.FromObservation("terminal-relevant", new NasdaqHumanRelevantLiquidityTakeObservation(opposite, At(15), "synthetic:terminal", take)));
        }
        if (extra is not null) records.AddRange(extra);
        return new("synthetic-scenario2-preentry-only", take.Session, files, records);
    }

    private static NasdaqMentorSessionReplayReport Run(NasdaqMentorSessionReplayInput input) => new RunLocalNasdaqMentorSessionUseCase().Execute(input);
    private static RuleEvaluationResult Result(NasdaqMentorSessionFrame f, string id) => f.Strategy.Evaluations.Single(e => e.RuleId.Value == id).Result;
    private static void NoTrade(NasdaqMentorSessionReplayReport report)
    {
        Assert.Empty(report.InputDiagnostics);
        Assert.All(report.Frames, f =>
        {
            Assert.Null(f.Snapshot); Assert.Null(f.FactualEvaluation); Assert.Null(f.DocumentedExit); Assert.Null(f.ContactResolution);
            Assert.DoesNotContain(f.Strategy.RuleFacts, r => StandardRules.Contains(r.RuleId.Value) || r.RuleId.Value is "NQ-SL-001" or "NQ-RISK-001" or "NQ-TP-001");
        });
        Assert.All(report.Canonical!.Frames, f => Assert.NotEqual(StrategyVerdict.Ready, f.Verdict));
    }

    [Fact]
    public async Task SyntheticScenario2TraversesCanonicalHarnessLifecycleAndPersistedReaderWithoutATrade()
    {
        var report = Run(Input()); NoTrade(report);
        Assert.DoesNotContain(report.BindingDiagnostics, d => d.Code != "Bound" && d.Code != "PrerequisiteUnavailable");
        var reversalOwner = report.Frames.First(f => f.Strategy.RuleFacts.Any(r => r.RuleId.Value == "NQ-WF2-001"));
        var eligibilityOwner = report.Frames.First(f => f.Strategy.RuleFacts.Any(r => r.RuleId.Value == "NQ-WF2-002"));
        var reversal = (NasdaqHumanWickfillScenario2ReversalRuleFact)reversalOwner.Strategy.RuleFacts.Single(r => r.RuleId.Value == "NQ-WF2-001").Fact;
        var eligibility = (NasdaqWickfillScenario2PreEntryEligibilityRuleFact)eligibilityOwner.Strategy.RuleFacts.Single(r => r.RuleId.Value == "NQ-WF2-002").Fact;
        Assert.Same(reversal, eligibility.Reversal);
        Assert.True(reversalOwner.Strategy.AsOfUtc < eligibilityOwner.Strategy.AsOfUtc);
        Assert.Equal(At(13, 32), eligibility.EligibilityEffectiveAtUtc);
        Assert.Same(report.Frames.Single(f => f.Strategy.AsOfUtc == At(13, 31)).Strategy, reversal.Applicability.Fact.InitialTakeObservation);
        var completed = report.Frames.First(f => f.Strategy.LifecycleProgression?.ActiveInstance?.Evidence.Evidence
            .OfType<NasdaqScenario2LifecycleCompletionEvidence>().Any(e => e.Eligibility is not null) == true);
        Assert.True(eligibilityOwner.Strategy.AsOfUtc < completed.Strategy.AsOfUtc);
        Assert.All(StandardRules, id => Assert.Equal(RuleEvaluationResult.NotApplicable, Result(completed, id)));
        Assert.Equal(RuleEvaluationResult.Waiting, Result(completed, "NQ-SL-001"));
        Assert.NotNull(report.Frames.Last().Strategy.LifecycleProgression!.ActiveInstance);
        var path = Path.Combine(directory, "report.json");
        using (var stream = File.Create(path)) NasdaqMentorSessionReportJson.Write(stream, report);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var persistedFrames = document.RootElement.GetProperty("Frames").EnumerateArray();
        var persistedCompletion = persistedFrames.Single(f => f.GetProperty("Step").GetInt32() == completed.Strategy.Step);
        Assert.Equal(At(13, 31), persistedCompletion.GetProperty("LifecycleProgression").GetProperty("Instances").GetProperty("$values").EnumerateArray().Single().GetProperty("StartedAtUtc").GetDateTimeOffset());
        var persistedFacts = persistedCompletion.GetProperty("RuleFacts").GetProperty("$values").EnumerateArray();
        Assert.Contains(persistedFacts, f => Resolve(f.GetProperty("RuleId"), persistedCompletion.GetProperty("RuleFacts")).GetProperty("Value").GetString() == "NQ-WF2-002");
        using var reader = new ReadNasdaqHistoricalReport();
        var summary = await reader.ExecuteAsync(path, null, frameStep: completed.Strategy.Step);
        Assert.Equal("Unavailable", summary.SelectedFrame!.SnapshotState);
        Assert.Equal("Passed", summary.SelectedFrame.Rules.Single(r => r.RuleId == "NQ-WF2-002").Result);
        Assert.All(StandardRules, id => Assert.Equal("NotApplicable", summary.SelectedFrame.Rules.Single(r => r.RuleId == id).Result));
        Assert.NotEqual("Ready", summary.SelectedFrame.Verdict);
    }

    private static JsonElement Resolve(JsonElement value, JsonElement scope)
    {
        if (!value.TryGetProperty("$ref", out var reference)) return value;
        return Descendants(scope).Single(e => e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty("$id", out var id) && id.GetString() == reference.GetString());
    }

    private static IEnumerable<JsonElement> Descendants(JsonElement value)
    {
        yield return value;
        var children = value.ValueKind == JsonValueKind.Object ? value.EnumerateObject().Select(p => p.Value)
            : value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().AsEnumerable() : [];
        foreach (var child in children)
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    [Theory]
    [InlineData("missing-branch", "NQ-WF2-001", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("conflict-branch", "NQ-WF2-001", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("missing-context", "NQ-WF2-001", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("missing-turn", "NQ-WF2-001", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("conflict-turn", "NQ-WF2-001", RuleEvaluationResult.HumanValidationRequired)]
    [InlineData("unavailable-context", "NQ-WF2-001", RuleEvaluationResult.DataUnavailable)]
    public void MissingConflictingAndUnavailableEvidenceCannotComplete(string mode, string rule, RuleEvaluationResult expected)
    {
        var report = Run(Input(mode)); NoTrade(report);
        Assert.Equal(expected, Result(report.Frames.Single(f => f.Strategy.AsOfUtc == At(14)), rule));
        Assert.DoesNotContain(report.Frames.SelectMany(f => f.Strategy.RuleFacts), f => f.RuleId.Value == "NQ-WF2-002");
        Assert.Null(report.Frames.Last().Strategy.LifecycleProgression!.ActiveInstance);
    }

    [Fact]
    public void RepeatedRunsAreEquivalentAndFutureReviewsCannotRewriteEarlierFrames()
    {
        var input = Input();
        Assert.Equal(NasdaqMentorSessionReportJson.Serialize(Run(input)), NasdaqMentorSessionReportJson.Serialize(Run(input)));
        var absent = Run(Input("missing-turn")); var future = Run(Input("future-turn")); NoTrade(future);
        foreach (var frame in absent.Frames.Where(f => f.Strategy.AsOfUtc < At(17)))
        {
            var other = future.Frames.Single(f => f.Strategy.AsOfUtc == frame.Strategy.AsOfUtc);
            Assert.Equal(frame.Strategy.Evaluations.Select(e => (e.RuleId, e.Result, e.Reason, e.EvidenceReference)),
                other.Strategy.Evaluations.Select(e => (e.RuleId, e.Result, e.Reason, e.EvidenceReference)));
        }
        Assert.DoesNotContain(future.Frames.SelectMany(f => f.Strategy.RuleFacts), f => f.RuleId.Value == "NQ-WF2-002");
    }

    [Fact]
    public void CompatibleDuplicateReviewsRetainSupportAndCancellationStillDominates()
    {
        var duplicate = Run(Input("duplicate-turn")); NoTrade(duplicate);
        var fact = (NasdaqHumanWickfillScenario2ReversalRuleFact)duplicate.Frames.First(f => f.Strategy.RuleFacts.Any(r => r.RuleId.Value == "NQ-WF2-001"))
            .Strategy.RuleFacts.Single(r => r.RuleId.Value == "NQ-WF2-001").Fact;
        Assert.Equal(2, fact.Reversal.SupportingObservations.Count);
        var cancelled = Run(Input("cancel")); NoTrade(cancelled);
        Assert.Null(cancelled.Frames.Single(f => f.Strategy.AsOfUtc == At(15)).Strategy.LifecycleProgression!.ActiveInstance);
        Assert.Null(cancelled.Frames.Last().Strategy.LifecycleProgression!.ActiveInstance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignCanonicalOwnerWithMatchingStepAndOrdinalIsRejected(bool differentTake)
    {
        var foreign = Run(Input(eventId: differentTake ? "synthetic:other-take" : "synthetic:take")).Frames.Single(f => f.Strategy.AsOfUtc == At(13, 31)).Strategy;
        var records = Input("missing-branch").Evidence.Where(r => r.RecordId != "turn").Append(Branch(foreign: foreign));
        var input = Input(); var report = Run(new(input.SessionId, input.Session, input.CsvFiles, records)); NoTrade(report);
        var actual = report.Frames.Single(f => f.Strategy.AsOfUtc == foreign.AsOfUtc).Strategy;
        Assert.Equal(foreign.Step, actual.Step);
        Assert.Equal(foreign.LifecycleProgression!.ActiveInstance!.InstanceId, actual.LifecycleProgression!.ActiveInstance!.InstanceId);
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "branch" && d.Code == "ForeignCanonicalAncestry");
        Assert.DoesNotContain(report.Frames.SelectMany(f => f.Inputs), o => o is NasdaqHumanBranchApplicabilityObservation);
    }

    [Fact]
    public void ForeignReversalCannotBindEvenWhenItsSetupCoordinatesMatch()
    {
        var previous = Run(Input());
        var foreign = previous.Frames.SelectMany(f => f.Inputs).OfType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>().First();
        var report = Run(Input("missing-turn", [NasdaqMentorSessionEvidenceRecord.FromObservation("foreign-turn", foreign)]));
        NoTrade(report);
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "foreign-turn" && d.Code == "ForeignCanonicalAncestry");
        Assert.DoesNotContain(report.Frames.SelectMany(f => f.Strategy.RuleFacts), r => r.RuleId.Value == "NQ-WF2-002");
    }

    [Fact]
    public void ForeignSessionContextIsRejectedWithoutSubstitutingForMissingContext()
    {
        var input = Input("missing-context", [NasdaqMentorSessionEvidenceRecord.FromObservation("foreign-context", ContextEvidence(dayOffset: -1))]);
        var report = Run(input); NoTrade(report);
        Assert.Contains(report.BindingDiagnostics, d => d.RecordId == "foreign-context" && d.Code == "EvidenceSessionMismatch");
    }

    [Fact]
    public void ExplicitStandardDoesNotConsumeScenario2ContextOrCreateScenario2Facts()
    {
        var report = Run(Input("standard"));
        var frame = report.Frames.Single(f => f.Strategy.AsOfUtc == At(14));
        Assert.Equal(RuleEvaluationResult.NotApplicable, Result(frame, "NQ-WF2-001"));
        Assert.Equal(RuleEvaluationResult.NotApplicable, Result(frame, "NQ-WF2-002"));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, Result(frame, "NQ-M5-001"));
        Assert.DoesNotContain(report.Frames.SelectMany(f => f.Strategy.RuleFacts), f => f.RuleId.Value is "NQ-WF2-001" or "NQ-WF2-002");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
