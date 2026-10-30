using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    private static DateTimeOffset S9Time(int minute, int second = 0) => new(2026, 9, 8, 13, minute, second, TimeSpan.Zero);

    // Opt-in source tests: compile the September adapter through the documented property and select these tests explicitly.
    // These validate real source contexts; they do not execute a complete mentor-session strategy replay or write a report.
    [LocalMentorSessionFact]
    public void ValidateReplay2S9ContextSourceMembersAvailabilityAndDeterminism()
    {
        var first = ReadS9Contexts();
        var repeated = ReadS9Contexts();
        Assert.Equal(first.Select(f => JsonSerializer.Serialize(f.Observation.Evaluations)),
            repeated.Select(f => JsonSerializer.Serialize(f.Observation.Evaluations)));
        var before = first.Single(f => f.Context.AsOfUtc == S9Time(27));
        var after = first.Single(f => f.Context.AsOfUtc == S9Time(28));
        Assert.DoesNotContain(first, f => f.Context.AsOfUtc == S9Time(27, 42)); // No synthetic second-resolution boundary.
        Assert.All(first.Where(f => f.Context.AsOfUtc < S9Time(27, 42)), f =>
            Assert.DoesNotContain(f.Context.InputObservations, o => o is NasdaqHumanH4ContextObservation or NasdaqHumanWickfillScenario2ContextObservation));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, H4Result(before.Observation));
        var context = Assert.Single(after.Context.InputObservations.OfType<NasdaqHumanWickfillScenario2ContextObservation>());
        var h4 = Assert.Single(after.Context.InputObservations.OfType<NasdaqHumanH4ContextObservation>());
        Assert.Same(h4, context.H4Context);
        Assert.Equal(NasdaqHumanH4PermittedDirection.Sell, h4.Fact.PermittedDirection);
        Assert.Equal(NasdaqHumanH4ContextKind.Wickfill, h4.Fact.ContextKind);
        Assert.Equal(S9Time(0), h4.EffectiveAtUtc);
        Assert.Equal(S9Time(27, 42), h4.ObservedAtUtc);
        Assert.Equal(h4.EffectiveAtUtc, context.EffectiveAtUtc);
        Assert.Equal(h4.ObservedAtUtc, context.ObservedAtUtc);
        Assert.Empty(h4.Fact.Anchors);
        Assert.Same(Assert.Single(h4.Fact.ZoneAnchors), context.ApplicableZone);
        Assert.Equal(29618.6m, context.ApplicableZone.LowerBound);
        Assert.Equal(29633.1m, context.ApplicableZone.UpperBound);
        Assert.Equal(NasdaqHumanH4ZoneBoundaryPrecision.Approximate, context.ApplicableZone.BoundaryPrecision);
        var member = Assert.Single(context.ReviewedSourceCandles);
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero), member.OpenTimeUtc);
        Assert.Equal(S9Time(0), member.CloseTimeUtc);
        Assert.Equal(new[] { member.OpenTimeUtc }, context.ApplicableZone.MemberOpenTimesUtc);
        Assert.True(after.Context.TryGetFrame(NasdaqHumanH4ContextObservation.H4, out var h4Frame));
        Assert.Same(h4Frame!.AvailableCandles.Single(c => c.OpenTimeUtc == member.OpenTimeUtc), member);
        Assert.Equal(29618.7m, member.High); Assert.Equal(29448.9m, member.Low); // Native MT5, not screenshot OHLC.
        Assert.Equal(member.ProviderId, context.ProviderId); Assert.Equal(member.Symbol, context.Symbol);
        Assert.All(new[] { "S1", "S8", "S9", "49ff660c-601f-49ce-bf92-643afba282e3" }, source => Assert.Contains(source, context.SourceReference));
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Unique>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(after.Context, context.Session));
        Assert.Equal(RuleEvaluationResult.Passed, H4Result(after.Observation));
        // Supplying later human records to the earlier context cannot change its bounded inputs or outcomes.
        var injected = CreateContext(before.Frame, before.Context.InputObservations.Concat([h4, context]));
        Assert.DoesNotContain(injected.InputObservations, o => o is NasdaqHumanH4ContextObservation or NasdaqHumanWickfillScenario2ContextObservation);
        Assert.Equal(JsonSerializer.Serialize(before.Observation.Evaluations), JsonSerializer.Serialize(Evaluate(injected).Evaluations));
        output.WriteLine("Real S9 source: 13:27Z absent; first existing eligible frame 13:28Z; Effective=13:00Z, Observed=13:27:42Z. No full session replay executed.");
    }

    [LocalMentorSessionFact]
    public void ValidateReplay2S9ContextDoesNotConvertFakeoutOrInventMissingStrategyFacts()
    {
        var frames = ReadS9Contexts();
        var after = frames.Single(f => f.Context.AsOfUtc == S9Time(28));
        var context = Assert.Single(after.Context.InputObservations.OfType<NasdaqHumanWickfillScenario2ContextObservation>());
        var h4 = context.H4Context;
        // Deliberately contradictory test-only assertions, never added to the historical adapter or persisted as evidence.
        var fakeout = new NasdaqHumanH4ContextObservation(h4.Session,
            new(h4.Fact.PermittedDirection, NasdaqHumanH4ContextKind.Fakeout, h4.Fact.ContextCandleOpenTimeUtc,
                h4.EffectiveAtUtc, [], h4.Fact.ZoneAnchors), h4.ObservedAtUtc, "negative-test:legacy-label-is-not-a-converter");
        Assert.Throws<ArgumentException>(() => new NasdaqHumanWickfillScenario2ContextObservation(fakeout, context.ApplicableZone,
            context.ReviewedSourceCandles, context.EffectiveAtUtc, context.ObservedAtUtc, "negative-test:invalid-WF2-context"));
        var conflict = CreateContext(after.Frame, after.Context.InputObservations.Append(fakeout));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, H4Result(Evaluate(conflict)));
        var missing = CreateContext(after.Frame, []);
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Missing>(new NasdaqHumanWickfillScenario2ContextObservationSelector().Select(missing, context.Session));
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, H4Result(Evaluate(missing)));
        var other = new NasdaqHumanWickfillScenario2ContextObservation(h4, context.ApplicableZone, context.ReviewedSourceCandles,
            context.EffectiveAtUtc.AddTicks(1), context.ObservedAtUtc, "negative-test:conflicting-effective-time");
        Assert.IsType<NasdaqHumanWickfillScenario2ContextSelection.Conflict>(new NasdaqHumanWickfillScenario2ContextObservationSelector()
            .Select(CreateContext(after.Frame, after.Context.InputObservations.Append(other)), context.Session));
        Assert.All(frames, f =>
        {
            Assert.DoesNotContain(f.Context.InputObservations, o => o is NasdaqHumanBranchApplicabilityObservation
                or NasdaqHumanWickfillScenario2PostSweepReversalObservation or NasdaqHumanStructuralStopLossObservation
                or NasdaqHumanTakeProfitObservation or NasdaqRiskExposureObservation);
            Assert.DoesNotContain(f.Observation.RuleFacts, r => r.RuleId.Value is "NQ-WF2-001" or "NQ-WF2-002"
                or "NQ-M5-001" or "NQ-M1-001" or "NQ-M1-002" or "NQ-M1-003" or "NQ-SL-001" or "NQ-TP-001" or "NQ-RISK-001");
            Assert.NotEqual(StrategyVerdict.Ready, new EvaluateStrategyReplayContextOutcomeUseCase()
                .Execute(MoneyWayNasdaqStrategyDefinition.Instance, f.Observation).Verdict);
        });
        // Existing independent native take evidence is retained: closed M1, not first intraminute crossing.
        var take = Assert.Single(frames.Single(f => f.Context.AsOfUtc == S9Time(31)).Context.InputObservations.OfType<NasdaqHumanLiquidityTakeObservation>());
        Assert.Equal(S9Time(31), take.EffectiveAtUtc);
        Assert.Equal(S9Time(31), take.ObservedAtUtc);
        Assert.Equal("native-M1-close-2026-09-08T13:31Z", Assert.IsType<NasdaqHumanLiquidityTakeEvent.Documented>(take.Event).EventId);
    }

    private static RuleEvaluationResult H4Result(StrategyReplayContextObservation observation) => observation.Evaluations.Single(e => e.RuleId.Value == "NQ-H4-001").Result;
    private static StrategyReplayContext CreateContext(MultiTimeframeReplayFrame frame, IEnumerable<IStrategyReplayInputObservation> inputs) =>
        new CreateStrategyReplayContextUseCase().Execute(MoneyWayNasdaqStrategyDefinition.Instance, frame, inputs);
    private static StrategyReplayContextObservation Evaluate(StrategyReplayContext context) =>
        new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll()).Execute(MoneyWayNasdaqStrategyDefinition.Instance, context);
    private static IReadOnlyList<(MultiTimeframeReplayFrame Frame, StrategyReplayContext Context, StrategyReplayContextObservation Observation)> ReadS9Contexts()
    {
        NasdaqMentorSessionReplayInput? input = null;
        ProvideValidatedInput(ref input);
        Assert.NotNull(input);
        Assert.Equal(new DateOnly(2026, 9, 8), input.Session.TradingDay);
        Assert.Equal("oanda-global-markets-mt5-demo", input.Session.ProviderId.Value);
        Assert.Equal("US100", input.Session.Symbol.Value);
        Assert.Single(input.Evidence, e => e.RecordId == "wf2-pre-open-context");
        var series = input.CsvFiles.Select(kv => new CsvMarketDataImporter().ImportFile(kv.Value)).ToArray();
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        var result = new List<(MultiTimeframeReplayFrame, StrategyReplayContext, StrategyReplayContextObservation)>();
        while (cursor.TryAdvance(out var frame))
        {
            if (frame!.AsOfUtc > S9Time(31)) break;
            if (frame.AsOfUtc < S9Time(0)) continue;
            var empty = CreateContext(frame, []);
            var observations = input.Evidence.Where(r => r.ObservedAtUtc <= frame.AsOfUtc).Select(r => r.Bind(empty, null))
                .Where(o => o is not null).Cast<IStrategyReplayInputObservation>().ToArray();
            Assert.All(observations, o => Assert.True(o.ObservedAtUtc <= frame.AsOfUtc));
            var context = CreateContext(frame, observations);
            result.Add((frame, context, Evaluate(context)));
        }
        Assert.Equal(32, result.Count); // Every existing native close from 13:00Z through 13:31Z, inclusive.
        return result;
    }
}
