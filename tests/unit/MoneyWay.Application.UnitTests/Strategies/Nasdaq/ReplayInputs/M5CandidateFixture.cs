using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Backtesting.Diagnostics;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

// All market values and availability assertions are synthetic admitted fixtures, not authentic market evidence.
internal sealed class M5CandidateFixture
{
    internal StrategyDefinition Definition { get; }
    internal CandleSeries[] Series { get; }
    internal NasdaqHumanLiquidityTakeObservation Take { get; }
    internal IStrategyReplayInputObservation[] Inputs { get; }
    internal NasdaqHumanH4PermittedDirection Direction { get; }
    internal static DateTimeOffset At(int minute) => M5Fixture.At(14, minute);
    internal M5CandidateFixture(bool buy = true, decimal close = 100.01m, bool baseline = false, NasdaqStrategyPath path = NasdaqStrategyPath.Standard,
        decimal? candleOpen = null, decimal? high = null, decimal? low = null, bool sessionCandidate = false, bool window1130Candidate = false)
    {
        sessionCandidate |= window1130Candidate;
        Definition = window1130Candidate ? MoneyWayNasdaqWindow1130Candidate.Definition
            : sessionCandidate ? MoneyWayNasdaqSessionCandidate.Definition : baseline ? MoneyWayNasdaqStrategyDefinition.Instance : MoneyWayNasdaqM5StrictCloseCandidate.Definition;
        Direction = buy ? NasdaqHumanH4PermittedDirection.Buy : NasdaqHumanH4PermittedDirection.Sell;
        Series = M5Fixture.Series().Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.CloseTimeUtc <= At(30)).Select(c => c.Timeframe == NasdaqHumanM5TriggerEvent.M5 && c.CloseTimeUtc == At(10)
                ? new Candle(c.ProviderId, c.Symbol, c.Timeframe, c.OpenTimeUtc, c.CloseTimeUtc, candleOpen ?? (buy ? 105 : 95), high ?? Math.Max(110, close), low ?? Math.Min(90, close), close, null) : c))).ToArray();
        if (sessionCandidate)
            Series = Series.Select(s => s.Timeframe == new Timeframe(1, TimeframeUnit.Hour)
                ? new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe, s.Candles.Append(LiquidityFixture.Candle(12)).OrderBy(c => c.OpenTimeUtc)) : s).ToArray();
        var context = Context(M5Fixture.At(13, 15));
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            buy ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh, context.AsOfUtc, "synthetic:reference");
        Take = new(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol, "synthetic:take", buy ? 89 : 111,
            At(5), "synthetic:event source", [Five(5)]), context.AsOfUtc, At(5), "synthetic:take review");
        var session = new NasdaqDemoSessionIdentity(Definition.StrategyId, Definition.Version, LiquidityFixture.Provider, LiquidityFixture.Symbol, LiquidityFixture.Session(context.AsOfUtc).TradingDay);
        // Use the established source-backed H4 fixture geometry, with this exact version identity.
        var h4 = M5Fixture.H4(Direction);
        Inputs = [new NasdaqHumanH4ContextObservation(session, h4.Fact, h4.ObservedAtUtc, "synthetic:H4 review"),
            new NasdaqPreparationCompletionObservation(Definition.StrategyId, Definition.Version, LiquidityFixture.Provider, LiquidityFixture.Symbol, session.TradingDay, M5Fixture.At(13, 15), "synthetic:preparation"),
            Take, new NasdaqHumanRelevantLiquidityTakeObservation(Take, Take.ObservedAtUtc, "synthetic:relevance")];
        var owner = Run(Inputs).OutcomeRun.StrategyRun.StrategyObservations.Single(o => o.LifecycleProgression?.ActiveInstance?.StartedAtUtc == o.AsOfUtc);
        Inputs = Inputs.Append(new NasdaqHumanBranchApplicabilityObservation(owner, path, owner.AsOfUtc, owner.AsOfUtc, "synthetic:branch")).ToArray();
    }
    internal Candle Five(int minute) => Series.Single(s => s.Timeframe == NasdaqHumanM5TriggerEvent.M5).Candles.Single(c => c.CloseTimeUtc == At(minute));
    internal NasdaqValidatedM5SwingObservation Swing(decimal price = 100, DateTimeOffset? observed = null, NasdaqM5SwingValidation validation = NasdaqM5SwingValidation.Validated,
        NasdaqStructuralLiquiditySide? side = null) => new(Take, "synthetic:selected-swing", side ?? (Direction == NasdaqHumanH4PermittedDirection.Buy ? NasdaqStructuralLiquiditySide.High : NasdaqStructuralLiquiditySide.Low),
        price, "NativeInstrumentPrice", [Five(0)], At(0), observed ?? At(6), "synthetic:validated swing source", validation);
    internal NasdaqHumanM5TriggerObservation Ifvg(int minute) => new(Take, new NasdaqHumanM5TriggerEvent.Ifvg(Five(minute), Direction, [Five(0)], [Five(0)]), At(minute), "synthetic:IFVG review",
        minute == 5 ? NasdaqHumanM5TakeTriggerOrder.TakeBeforeTriggerConfirmation : NasdaqHumanM5TakeTriggerOrder.Unspecified);
    internal StrategyReplayContext Context(DateTimeOffset time, IEnumerable<IStrategyReplayInputObservation>? inputs = null, CandleSeries[]? series = null)
    {
        var cursor = new MultiTimeframeCandleReplayCursor(series ?? Series);
        while (cursor.TryAdvance(out var frame)) if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(Definition, frame, inputs ?? []);
        throw new InvalidOperationException("Synthetic boundary missing.");
    }
    internal MultiTimeframeStrategyBacktestDiagnosticsReport Run(IEnumerable<IStrategyReplayInputObservation>? inputs = null, CandleSeries[]? series = null)
    {
        if (Definition.Version == MoneyWayNasdaqWindow1130Candidate.Version)
            return MoneyWayNasdaqWindow1130Candidate.CreateBacktest().Execute(Definition, series ?? Series, inputs ?? Inputs);
        if (Definition.Version == MoneyWayNasdaqSessionCandidate.Version) return MoneyWayNasdaqSessionCandidate.CreateBacktest().Execute(Definition, series ?? Series, inputs ?? Inputs);
        var candidate = Definition.Version == MoneyWayNasdaqM5StrictCloseCandidate.Version;
        if (candidate) return MoneyWayNasdaqM5StrictCloseCandidate.CreateBacktest().Execute(Definition, series ?? Series, inputs ?? Inputs);
        var definitions = new[] { Definition };
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(candidate ? MoneyWayNasdaqM5StrictCloseCandidate.Evaluators() : MoneyWayReplayRuleEvaluators.GetAll()),
            new(definitions, candidate ? [MoneyWayNasdaqM5StrictCloseCandidate.Workflow()] : MoneyWayReplayWorkflowDefinitions.GetAll()), new(),
            new(definitions, candidate ? [MoneyWayNasdaqM5StrictCloseCandidate.Lifecycle()] : MoneyWayReplayLifecyclePolicies.GetAll()), new(new()));
        return new GenerateCanonicalMultiTimeframeBacktestUseCase(new(run, new()), new()).Execute(Definition, series ?? Series, inputs ?? Inputs);
    }
    internal static StrategyReplayContextObservation Frame(MultiTimeframeStrategyBacktestDiagnosticsReport report, int minute) => report.OutcomeRun.StrategyRun.StrategyObservations.Single(o => o.AsOfUtc == At(minute));
    internal static RuleEvaluationResult Result(MultiTimeframeStrategyBacktestDiagnosticsReport report, int minute, string rule = "NQ-M5-001") => Frame(report, minute).Evaluations.Single(e => e.RuleId.Value == rule).Result;
}
