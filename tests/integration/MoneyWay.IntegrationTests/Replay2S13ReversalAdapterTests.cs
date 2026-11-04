using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    [LocalMentorSessionFact]
    public void ValidateReplay2S13RealReversalAndEligibilityConsumeExactPriorAncestry()
    {
        // Two independently created adapters/runs; the longer tail must not alter their common prefix.
        var first = RunS11Prefix(35);
        var longer = RunS11Prefix(36);
        var frames = first.Link.Frames;
        var laterFrames = longer.Link.Frames.Where(f => f.Strategy.AsOfUtc <= S9Time(35)).ToArray();
        Assert.Equal(S13Fingerprint(frames), S13Fingerprint(laterFrames));
        Assert.All(first.Link.Diagnostics, d => Assert.Contains(d.Code, new[] { "Bound", "PrerequisiteUnavailable" }));
        Assert.All(longer.Link.Diagnostics, d => Assert.Contains(d.Code, new[] { "Bound", "PrerequisiteUnavailable" }));
        var bound = Assert.Single(first.Link.Diagnostics, d => d.RecordId == "wf2-S13-reversal" && d.Code == "Bound");
        Assert.Equal(S9Time(34), bound.AsOfUtc);
        var reversalOwner = frames.First(f => f.Strategy.RuleFacts.Any(r => r.RuleId.Value == "NQ-WF2-001"));
        var eligibilityOwner = frames.First(f => f.Strategy.RuleFacts.Any(r => r.RuleId.Value == "NQ-WF2-002"));
        Assert.Equal(S9Time(34), reversalOwner.Strategy.AsOfUtc);
        Assert.Equal(S9Time(35), eligibilityOwner.Strategy.AsOfUtc);
        Assert.Equal(RuleEvaluationResult.Passed, S13Result(reversalOwner, "NQ-WF2-001"));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(reversalOwner, "NQ-WF2-002"));
        Assert.Equal(RuleEvaluationResult.Passed, S13Result(eligibilityOwner, "NQ-WF2-002"));
        var reversal = Assert.Single(reversalOwner.Inputs.OfType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>());
        var record = first.Input.Evidence.Single(r => r.RecordId == "wf2-S13-reversal");
        Assert.Equal("human-recognition:8fddfee6-00d6-4675-bc79-6e7fcd4eafc8:2026-09-08T13:33:10Z", reversal.EventId);
        Assert.Equal(S9Time(33, 10), reversal.EffectiveAtUtc);
        Assert.Equal(S9Time(33, 10), reversal.ObservedAtUtc);
        Assert.Equal(reversal.ObservedAtUtc, record.ObservedAtUtc);
        Assert.Empty(reversal.SupportingCandles); // No final OHLC from the forming M30 source.
        Assert.All(new[] { "S13", "section 2.12", "f6231e1d353609080faf8399879540d09b4d3dd6",
            "8fddfee6-00d6-4675-bc79-6e7fcd4eafc8", "d9a90be436fe59b345a8b170c967c3c69d55c87e04ba44841d3eb1080844860a",
            "documented recognition/availability", "not first tick", "USTEC", "US100", "external/not locally verified", "S12" },
            text => Assert.Contains(text, reversal.SourceReference));
        var start = frames.Single(f => f.Strategy.AsOfUtc == S9Time(31)).Strategy;
        var app = Assert.Single(reversalOwner.Inputs.OfType<NasdaqHumanBranchApplicabilityObservation>());
        var sweep = reversal.OpeningSweep;
        Assert.Same(start, sweep.TakeObservation);
        Assert.Same(app.InitialTakeObservation, start);
        Assert.Same(app.InitialTake, sweep.Take);
        Assert.Same(sweep.Take.Take, sweep.Take.InitiatingTake);
        Assert.Equal("native-M1-close-2026-09-08T13:31Z", Assert.IsType<NasdaqHumanLiquidityTakeEvent.Documented>(sweep.Take.Take.Event).EventId);
        Assert.Equal(first.Input.Session, reversal.Session);
        Assert.Equal(NasdaqStrategyPath.WickfillScenario2, app.Path);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, reversal.Direction);
        Assert.Equal(start.AsOfUtc, sweep.EffectiveAtUtc);
        Assert.Equal(start.AsOfUtc, sweep.ObservedAtUtc);
        Assert.Contains(frames.Where(f => f.Strategy.AsOfUtc < start.AsOfUtc).SelectMany(f => f.Strategy.RuleFacts),
            r => ReferenceEquals(r.Fact, sweep.CanonicalH4Context));
        Assert.Equal(start.LifecycleProgression!.ActiveInstance!.InstanceId, reversalOwner.Strategy.LifecycleProgression!.ActiveInstance!.InstanceId);
        var fact = Assert.IsType<NasdaqHumanWickfillScenario2ReversalRuleFact>(reversalOwner.Strategy.RuleFacts.Single(r => r.RuleId.Value == "NQ-WF2-001").Fact);
        Assert.Same(reversal, fact.Reversal.Fact);
        Assert.Same(app, fact.Applicability.Fact);
        var eligibility = Assert.IsType<NasdaqWickfillScenario2PreEntryEligibilityRuleFact>(eligibilityOwner.Strategy.RuleFacts.Single(r => r.RuleId.Value == "NQ-WF2-002").Fact);
        Assert.Same(fact, eligibility.Reversal);
        Assert.Equal(reversal.EffectiveAtUtc, eligibility.EligibilityEffectiveAtUtc);
        Assert.True(reversalOwner.Strategy.AsOfUtc < eligibilityOwner.Strategy.AsOfUtc);
        Assert.All(frames.Where(f => f.Strategy.AsOfUtc <= S9Time(33)), f =>
        {
            Assert.DoesNotContain(f.Inputs, o => o is NasdaqHumanWickfillScenario2PostSweepReversalObservation);
            Assert.DoesNotContain(f.Strategy.RuleFacts, r => r.RuleId.Value is "NQ-WF2-001" or "NQ-WF2-002");
        });
        Assert.DoesNotContain(frames, f => f.Strategy.AsOfUtc == S9Time(33, 10));

        // Exercise the actual external callback with bounded real frames and each run's own history.
        StrategyReplayContext ContextAt(DateTimeOffset time, IEnumerable<StrategyReplayContextObservation>? history = null)
        {
            var cursor = new MultiTimeframeCandleReplayCursor(first.Series);
            while (cursor.TryAdvance(out var frame))
                if (frame!.AsOfUtc == time)
                    return CreateContext(frame, []).WithPriorObservations(history ?? frames.Where(f => f.Strategy.AsOfUtc < time).Select(f => f.Strategy));
            throw new InvalidOperationException("Real source frame missing.");
        }
        Assert.Null(record.Bind(ContextAt(S9Time(33)), null));
        Assert.Null(record.Bind(ContextAt(S9Time(34), []), null));
        Assert.Null(record.Bind(ContextAt(S9Time(34), longer.Link.Frames.Where(f => f.Strategy.AsOfUtc < S9Time(34)).Select(f => f.Strategy)), null));
        var repeated = Assert.IsType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>(record.Bind(ContextAt(S9Time(34)), null));
        Assert.Same(start, repeated.OpeningSweep.TakeObservation);
        Assert.Equal(reversal.EventId, repeated.EventId);

        Assert.All(frames.Concat(longer.Link.Frames), f =>
        {
            Assert.Null(f.Snapshot); Assert.Null(f.FactualEvaluation); Assert.Null(f.DocumentedExit); Assert.Null(f.ContactResolution);
            Assert.DoesNotContain(f.Strategy.RuleFacts, r => r.RuleId.Value is "NQ-M5-001" or "NQ-M1-001" or "NQ-M1-002"
                or "NQ-M1-003" or "NQ-SL-001" or "NQ-TP-001" or "NQ-RISK-001");
            Assert.NotEqual(StrategyVerdict.Ready, new EvaluateStrategyReplayContextOutcomeUseCase()
                .Execute(MoneyWayNasdaqStrategyDefinition.Instance, f.Strategy).Verdict);
        });
        Assert.All(new[] { "NQ-M5-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003" }, id =>
            Assert.Equal(RuleEvaluationResult.NotApplicable, S13Result(eligibilityOwner, id)));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(eligibilityOwner, "NQ-SL-001"));
        output.WriteLine($"REAL S13: Start step={start.Step}, owner={start.LifecycleProgression.ActiveInstance.InstanceId}, " +
            $"take={sweep.Take.Take.EffectiveAtUtc:O}, recognized={reversal.EffectiveAtUtc:O}, bound={bound.AsOfUtc:O}, " +
            $"WF2-001 Passed={reversalOwner.Strategy.AsOfUtc:O}, WF2-002 Passed={eligibilityOwner.Strategy.AsOfUtc:O}. " +
            $"Independent real prefixes: {frames.Count} / {longer.Link.Frames.Count} frames; common prefix identical. No report or trade generated.");
    }

    private static RuleEvaluationResult S13Result(NasdaqMentorSessionFrame frame, string id) =>
        frame.Strategy.Evaluations.Single(e => e.RuleId.Value == id).Result;

    private static string S13Fingerprint(IEnumerable<NasdaqMentorSessionFrame> frames) => JsonSerializer.Serialize(frames.Select(f => new
    {
        f.Strategy.Step, f.Strategy.AsOfUtc,
        Evaluations = f.Strategy.Evaluations.Select(e => new { e.RuleId, e.Result, e.Reason, e.EvidenceReference }),
        Inputs = f.Inputs.OfType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>()
            .Select(o => new { o.EventId, o.EffectiveAtUtc, o.ObservedAtUtc, o.SourceReference })
    }));
}
