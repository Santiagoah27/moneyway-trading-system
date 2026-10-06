using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Application.StrategyDefinitions;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

internal static class M5Fixture
{
    internal static readonly DateTimeOffset Day = LiquidityFixture.At(0);
    internal static readonly Timeframe Minute = new(1, TimeframeUnit.Minute);
    internal static readonly DateTimeOffset[] Boundaries = [At(13, 15), At(13, 30), At(14, 5), At(14, 10), At(14, 15), At(14, 20), At(14, 30), At(15), At(16), At(37, 15), At(37, 30), At(38, 5)];
    internal static DateTimeOffset At(int hour, int minute = 0) => Day.AddHours(hour).AddMinutes(minute);
    internal static Candle Five(DateTimeOffset close, decimal price = 100) => new(LiquidityFixture.Provider, LiquidityFixture.Symbol,
        NasdaqHumanM5TriggerEvent.M5, close.AddMinutes(-5), close, price, 110, 90, price, null);
    internal static readonly Candle[] M5Sources = Enumerable.Range(0, 21).Select(i => Five(At(13, 30).AddMinutes(i * 5))).ToArray();
    internal static NasdaqHumanRelevantLiquidityTakeObservation Relevant(NasdaqHumanLiquidityTakeObservation take) => new(take, take.ObservedAtUtc, "review:relevant");
    internal static NasdaqHumanLiquidityTakeObservation Take(bool high, int shift = 0, DateTimeOffset? effective = null, DateTimeOffset? observed = null, string eventId = "source:event")
    {
        var context = Context(At(13 + shift, 15), []);
        var reference = new NasdaqLiquidityTakeReference.SessionLevel(context, new NasdaqSessionLiquidityCalculator().Calculate(context),
            high ? NasdaqLiquidityTakeReference.SessionEndpoint.AsiaHigh : NasdaqLiquidityTakeReference.SessionEndpoint.AsiaLow,
            context.AsOfUtc, "review:endpoint");
        return new(reference, new NasdaqHumanLiquidityTakeEvent.Documented(LiquidityFixture.Provider, LiquidityFixture.Symbol, eventId,
            high ? 111 : 89, effective ?? At(14 + shift, 5), "source:exact event", [Five(At(14 + shift, 5))]), context.AsOfUtc, observed ?? At(14 + shift, 5), "review:take");
    }
    internal static NasdaqHumanH4ContextObservation H4(NasdaqHumanH4PermittedDirection direction, int shift = 0, DateTimeOffset? observed = null)
    {
        var session = LiquidityFixture.Session(At(13 + shift));
        var fact = new NasdaqHumanH4ContextFact(direction, NasdaqHumanH4ContextKind.Breakout, At(8 + shift), At(12 + shift),
            [new(NasdaqHumanH4StructuralRole.HigherHigh, 110, [At(0 + shift)], At(8 + shift))]);
        return new(session, fact, observed ?? At(13 + shift, 5), "review:h4");
    }
    internal static IStrategyReplayInputObservation[] Inputs(NasdaqHumanH4PermittedDirection direction, NasdaqHumanLiquidityTakeObservation? take, int shift = 0)
    {
        var source = Sources().First(c => c.Timeframe == new Timeframe(1, TimeframeUnit.Hour) && c.OpenTimeUtc == At(8 + shift));
        var upstream = new NasdaqHumanStructuralLiquidityObservation(LiquidityFixture.Session(At(13 + shift)),
            [LiquidityFixture.Human(source)], At(12 + shift), At(13 + shift, 5), "review:structural");
        var preparation = new NasdaqPreparationCompletionObservation(LiquidityFixture.Definition.StrategyId, LiquidityFixture.Definition.Version,
            LiquidityFixture.Provider, LiquidityFixture.Symbol, LiquidityFixture.Session(At(13 + shift)).TradingDay, At(13 + shift, 15), "review:prep");
        return take is null ? [H4(direction, shift), upstream, preparation] : [H4(direction, shift), upstream, preparation, take, Relevant(take)];
    }
    internal static Candle[] Sources() => new[] { 0, 24 }.SelectMany(shift =>
        Enumerable.Range(-2, 14).Select(h => LiquidityFixture.Candle(h + shift)).Concat(
            new[] { 0, 4, 8 }.Select(h => LiquidityFixture.Candle(h + shift, hours: 4)))).ToArray();
    internal static CandleSeries[] Series() => Sources().Concat(M5Sources).GroupBy(c => c.Timeframe).Select(g => new CandleSeries(LiquidityFixture.Provider, LiquidityFixture.Symbol, g.Key, g.OrderBy(c => c.OpenTimeUtc)))
        .Append(new(LiquidityFixture.Provider, LiquidityFixture.Symbol, Minute, Boundaries.Select(t => new Candle(LiquidityFixture.Provider, LiquidityFixture.Symbol, Minute,
            t.AddMinutes(-1), t, 100, 110, 90, 100, null)))).ToArray();
    internal static StrategyReplayContext Context(DateTimeOffset time, IStrategyReplayInputObservation[] inputs, bool includeFutureCandles = true)
    {
        var series = includeFutureCandles ? Series() : Series().Select(s => new CandleSeries(s.ProviderId, s.Symbol, s.Timeframe,
            s.Candles.Where(c => c.CloseTimeUtc <= time))).ToArray();
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == time) return new CreateStrategyReplayContextUseCase().Execute(LiquidityFixture.Definition, frame, inputs);
        throw new InvalidOperationException("Fixture missing.");
    }

    internal static (NasdaqLiquidityTakeRuleFact Fact, StrategyReplayContextObservation[] History) Established(bool high = false, NasdaqHumanH4PermittedDirection direction = NasdaqHumanH4PermittedDirection.Buy)
    {
        var take = Take(high);
        var inputs = Inputs(direction, take);
        var useCase = new EvaluateStrategyReplayContextUseCase(MoneyWayReplayRuleEvaluators.GetAll());
        var history = new List<StrategyReplayContextObservation>();
        foreach (var time in new[] { At(13, 15), At(13, 30), At(14, 5) })
            history.Add(useCase.Execute(LiquidityFixture.Definition, Context(time, inputs).WithPriorObservations(history)));
        var fact = Assert.IsType<NasdaqLiquidityTakeRuleFact>(Assert.Single(history.Last().RuleFacts, f => f.RuleId.Value == "NQ-LIQ-003").Fact);
        return (fact, history.ToArray());
    }
}
