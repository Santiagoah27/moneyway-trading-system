using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayLifecycle;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    [LocalMentorSessionFact]
    public void ValidateReplay2S11RealCanonicalBindingRetainsApprovedBoundsAndOwnRunStart()
    {
        var first = RunS11Prefix();
        var repeated = RunS11Prefix();
        Assert.Equal(S11Fingerprint(first.Link), S11Fingerprint(repeated.Link));
        var binding = Assert.Single(first.Link.Diagnostics, d => d.RecordId == "wf2-applicability" && d.Code == "Bound");
        Assert.Equal(S9Time(32), binding.AsOfUtc);
        Assert.All(first.Link.Diagnostics, d => Assert.Contains(d.Code, new[] { "Bound", "PrerequisiteUnavailable" }));
        var start = first.Link.Frames.Single(f => f.Strategy.AsOfUtc == S9Time(31)).Strategy;
        var firstBound = first.Link.Frames.Single(f => f.Strategy.AsOfUtc == binding.AsOfUtc);
        var branch = Assert.Single(firstBound.Inputs.OfType<NasdaqHumanBranchApplicabilityObservation>());
        Assert.Same(start, branch.InitialTakeObservation);
        var take = Assert.IsType<NasdaqLiquidityTakeRuleFact>(Assert.Single(start.RuleFacts, r => r.RuleId.Value == "NQ-LIQ-003").Fact);
        Assert.Same(take, branch.InitialTake);
        Assert.Same(take.Take, take.InitiatingTake);
        Assert.Equal("native-M1-close-2026-09-08T13:31Z", Assert.IsType<NasdaqHumanLiquidityTakeEvent.Documented>(take.Take.Event).EventId);
        Assert.Equal(S9Time(31), take.Take.EffectiveAtUtc);
        Assert.Equal(S9Time(31), take.Take.ObservedAtUtc);
        Assert.Equal(RuleEvaluationResult.Passed, start.Evaluations.Single(e => e.RuleId.Value == "NQ-LIQ-003").Result);
        Assert.True(start.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-LIQ-003").EstablishesProgression);
        Assert.Equal(S9Time(31), start.LifecycleProgression!.ActiveInstance!.StartedAtUtc);
        Assert.Contains(start.LifecycleProgression.TransitionHistory, t => t.Kind == StrategyReplayLifecycleTransitionKind.Start
            && t.InstanceId == start.LifecycleProgression.ActiveInstance.InstanceId && t.AsOfUtc == start.AsOfUtc);
        Assert.Equal(start.LifecycleProgression.ActiveInstance.InstanceId, firstBound.Strategy.LifecycleProgression!.ActiveInstance!.InstanceId);
        Assert.Equal(first.Input.Session, branch.Session);
        Assert.Equal(NasdaqStrategyPath.WickfillScenario2, branch.Path);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, branch.Direction);
        Assert.Equal(S9Time(30, 37), branch.ReviewEffectiveAtUtc);
        Assert.Equal(S9Time(30, 42), branch.ReviewObservedAtUtc);
        Assert.Equal(start.AsOfUtc, branch.EffectiveAtUtc);
        Assert.Equal(start.AsOfUtc, branch.ObservedAtUtc);
        Assert.Equal(branch.ObservedAtUtc, first.Input.Evidence.Single(r => r.RecordId == "wf2-applicability").ObservedAtUtc);
        Assert.All(new[] { "S10", "S11", "1e2a10a8-642f-4457-86f7-0f16365753e2", "conservative latest-known bounds", "not first decision/observation" },
            text => Assert.Contains(text, branch.SourceReference));
        Assert.All(first.Link.Frames.Where(f => f.Strategy.AsOfUtc <= start.AsOfUtc),
            f => Assert.DoesNotContain(f.Inputs, o => o is NasdaqHumanBranchApplicabilityObservation));

        // Original CSV frames and original history; no fabricated same-frame Start or transport.
        StrategyReplayContext ContextAt(DateTimeOffset time, IEnumerable<IStrategyReplayInputObservation> inputs,
            IEnumerable<StrategyReplayContextObservation>? history = null)
        {
            var cursor = new MultiTimeframeCandleReplayCursor(first.Series);
            while (cursor.TryAdvance(out var frame))
                if (frame!.AsOfUtc == time)
                    return CreateContext(frame, inputs).WithPriorObservations(history ?? first.Link.Frames
                        .Where(f => f.Strategy.AsOfUtc < time).Select(f => f.Strategy));
            throw new InvalidOperationException("Real source frame missing.");
        }
        var selector = new NasdaqHumanBranchApplicabilityObservationSelector();
        var before = ContextAt(S9Time(30), [branch]);
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(before, start));
        var current = ContextAt(S9Time(32), firstBound.Inputs);
        Assert.Same(branch, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(current, start)).Fact);
        var future = new NasdaqHumanBranchApplicabilityObservation(start, NasdaqStrategyPath.Standard, S9Time(30, 37), S9Time(40), "negative-test:future-review");
        var futureInjected = ContextAt(S9Time(32), firstBound.Inputs.Append(future));
        Assert.Same(branch, Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Unique>(selector.Select(futureInjected, start)).Fact);
        Assert.Equal(JsonSerializer.Serialize(Evaluate(current).Evaluations), JsonSerializer.Serialize(Evaluate(futureInjected).Evaluations));
        var record = first.Input.Evidence.Single(r => r.RecordId == "wf2-applicability");
        Assert.Null(record.Bind(ContextAt(S9Time(31), []), null));
        // Matching ordinal/time from another real run cannot replace the adapter's own native take.
        Assert.Null(record.Bind(ContextAt(S9Time(32), [], repeated.Link.Frames.Where(f => f.Strategy.AsOfUtc < S9Time(32)).Select(f => f.Strategy)), null));
        Assert.Throws<ArgumentException>(() => new NasdaqHumanBranchApplicabilityObservation(firstBound.Strategy,
            NasdaqStrategyPath.WickfillScenario2, S9Time(30, 37), S9Time(30, 42), "negative-test:not-the-Start"));
        Assert.IsType<NasdaqHumanBranchApplicabilitySelection.Missing>(selector.Select(current,
            repeated.Link.Frames.Single(f => f.Strategy.AsOfUtc == start.AsOfUtc).Strategy));

        Assert.All(first.Link.Frames, f =>
        {
            Assert.Null(f.Snapshot); Assert.Null(f.FactualEvaluation); Assert.Null(f.DocumentedExit); Assert.Null(f.ContactResolution);
            Assert.DoesNotContain(f.Strategy.RuleFacts, r => r.RuleId.Value is "NQ-WF2-001" or "NQ-WF2-002"
                or "NQ-M5-001" or "NQ-M1-001" or "NQ-M1-002" or "NQ-M1-003" or "NQ-SL-001" or "NQ-TP-001" or "NQ-RISK-001");
            Assert.NotEqual(StrategyVerdict.Ready, new EvaluateStrategyReplayContextOutcomeUseCase()
                .Execute(MoneyWayNasdaqStrategyDefinition.Instance, f.Strategy).Verdict);
        });
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, firstBound.Strategy.Evaluations.Single(e => e.RuleId.Value == "NQ-WF2-001").Result);
        Assert.All(new[] { "NQ-M5-001", "NQ-M1-001", "NQ-M1-002", "NQ-M1-003" },
            id => Assert.Equal(RuleEvaluationResult.NotApplicable, firstBound.Strategy.Evaluations.Single(e => e.RuleId.Value == id).Result));
        output.WriteLine($"Real canonical source prefix: {first.Link.Frames.Count} frames; native initial take/Start {start.AsOfUtc:O}; derived effective/observed {branch.ObservedAtUtc:O}; first binding {binding.AsOfUtc:O}. Two independent runs agree. No complete session/trade replay or report write.");
    }

    private static string S11Fingerprint(NasdaqMentorSessionReplay link) => JsonSerializer.Serialize(new
    {
        link.Diagnostics,
        Frames = link.Frames.Select(f => new { f.Strategy.Step, f.Strategy.AsOfUtc,
            Evaluations = f.Strategy.Evaluations.Select(e => new { e.RuleId, e.Result, e.Reason, e.EvidenceReference }),
            Branches = f.Inputs.OfType<NasdaqHumanBranchApplicabilityObservation>().Select(o => new
                { o.ReviewEffectiveAtUtc, o.ReviewObservedAtUtc, o.EffectiveAtUtc, o.ObservedAtUtc, o.Path, o.SourceReference }) })
    });

    private static (NasdaqMentorSessionReplayInput Input, NasdaqMentorSessionReplay Link, CandleSeries[] Series,
        MultiTimeframeStrategyBacktestDiagnosticsReport Canonical) RunS11Prefix(int endMinute = 33)
    {
        NasdaqMentorSessionReplayInput? input = null;
        ProvideValidatedInput(ref input);
        Assert.NotNull(input);
        Assert.Equal(new DateOnly(2026, 9, 8), input.Session.TradingDay);
        Assert.Equal("oanda-global-markets-mt5-demo", input.Session.ProviderId.Value);
        Assert.Equal("US100", input.Session.Symbol.Value);
        // Keep every original historical member through the requested boundary. No CSV is rewritten, no source history
        // is replaced, and no future candles are needed for this first-binding regression.
        var series = input.CsvFiles.Values.Select(path => new CsvMarketDataImporter().ImportFile(path))
            .Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Where(c => c.CloseTimeUtc <= S9Time(endMinute)))).ToArray();
        var definitions = new StrategyDefinitionCatalog().GetAll();
        var progression = new AdvanceStrategyReplayProgressionUseCase();
        var runner = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, MoneyWayReplayWorkflowDefinitions.GetAll()), progression,
            new(definitions, MoneyWayReplayLifecyclePolicies.GetAll()), new(progression));
        var link = new NasdaqMentorSessionReplay(input);
        var canonical = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(runner, new()), new())
            .ExecuteMentorSession(MoneyWayNasdaqStrategyDefinition.Instance, series, link);
        return (input, link, series, canonical);
    }
}
