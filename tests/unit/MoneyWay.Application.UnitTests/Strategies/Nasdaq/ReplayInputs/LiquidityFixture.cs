using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Application.MarketData.StructuralBreaks;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayEvaluators;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.MarketData.Replay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;

internal static class LiquidityFixture
{
    internal static readonly StrategyDefinition Definition = MoneyWayNasdaqStrategyDefinition.Instance;
    internal static readonly MarketDataProviderId Provider = new("fixture");
    internal static readonly MarketSymbol Symbol = new("NASDAQ");
    internal static DateTimeOffset At(int hour) => new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero).AddHours(hour);
    internal static NasdaqDemoSessionIdentity Session(int hour) => Session(At(hour));
    internal static NasdaqDemoSessionIdentity Session(DateTimeOffset asOf) => new(Definition.StrategyId, Definition.Version, Provider, Symbol,
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(asOf, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota")).DateTime));
    internal static Candle Candle(int hour, decimal open = 100, decimal high = 110, decimal low = 90, decimal close = 100, int hours = 1) =>
        new(Provider, Symbol, new(hours, TimeframeUnit.Hour), At(hour), At(hour + hours), open, high, low, close, null);
    internal static NasdaqStructuralLiquidityReference Human(Candle candle, bool low = false, decimal price = 111.2345m) =>
        NasdaqStructuralLiquidityReference.HumanEstablishedExtreme(low ? NasdaqHumanH4StructuralRole.LowerLow : NasdaqHumanH4StructuralRole.HigherHigh,
            [candle], price, "review:established exact point");
    internal static NasdaqHumanStructuralLiquidityObservation Observation(NasdaqStructuralLiquidityReference[] references, int effective = 12,
        int observed = 13, string source = "review:liquidity", NasdaqDemoSessionIdentity? session = null) =>
        new(session ?? Session(observed), references, At(effective), At(observed), source);

    internal static StrategyReplayContext Context(Candle[] source, IStrategyReplayInputObservation[] observations, int hour)
    {
        var series = source.GroupBy(c => c.Timeframe).Select(group => new CandleSeries(Provider, Symbol, group.Key,
            group.OrderBy(c => c.OpenTimeUtc))).ToList();
        var minute = new Timeframe(1, TimeframeUnit.Minute);
        series.Add(new(Provider, Symbol, minute, [new(Provider, Symbol, minute, At(hour).AddMinutes(-1), At(hour), 100, 110, 90, 100, null)]));
        var cursor = new MultiTimeframeCandleReplayCursor(series);
        while (cursor.TryAdvance(out var frame))
            if (frame!.AsOfUtc == At(hour)) return new CreateStrategyReplayContextUseCase().Execute(Definition, frame, observations);
        throw new InvalidOperationException("Fixture frame missing.");
    }

    internal static (NasdaqPostInvalidationCandidateRebuildBreakoutState Breakout, Candle[] Source) Breakout(bool upper, bool human)
    {
        Candle C(int h, decimal o, decimal high, decimal low, decimal close) => Candle(h, o, high, low, close, 4);
        var origin = upper ? C(0, 100, 110, 95, 110) : C(0, 100, 110, 85, 90);
        var prior = upper ? C(4, 95, 100, 90, 96) : C(4, 105, 115, 100, 110);
        var invalidating = upper ? C(8, 100, 110, 80, 90) : C(8, 100, 120, 90, 110);
        var observation = new NasdaqHumanOriginVertexObservation(Definition.StrategyId, Definition.Version, Provider, Symbol,
            invalidating.OpenTimeUtc, [origin.OpenTimeUtc], invalidating.CloseTimeUtc, "review:origin");
        var context = Context([origin, prior, invalidating], [observation], 12);
        var members = new NasdaqHumanOriginVertexMemberResolver().Evaluate(observation, context);
        var boundary = new StructuralCandidateTurnBoundaryCalculator().EvaluateCurrentBoundary(context, invalidating.Timeframe,
            upper ? 130 : 70, upper ? 95 : 105, upper ? 140 : 75, [prior],
            upper ? StructuralCandidateExtremeSide.Lower : StructuralCandidateExtremeSide.Upper);
        var geometry = new NasdaqHumanOriginVertexGeometryCalculator().Evaluate(members, boundary);
        var impulse = new NasdaqPostInvalidationOppositeImpulseInitializer().Initialize(members, boundary, geometry);
        var start = upper ? C(12, 70, 105, 65, 95) : C(12, 130, 145, 95, 115);
        var correction = new NasdaqPostInvalidationCorrectionInitializer().Initialize(new NasdaqPostInvalidationOppositeImpulseTransitionCalculator().Evaluate(impulse, start));
        var terminal = upper ? C(16, 100, 130, 90, 99) : C(16, 80, 100, 60, 90);
        var candidate = new NasdaqPostInvalidationCorrectionTransitionCalculator().Evaluate(correction, terminal).Candidate!;
        var migration = upper ? C(20, 100, 140, 80, 100) : C(20, 100, 120, 50, 90);
        var pending = new NasdaqPostInvalidationCandidateRebuildTransitionCalculator().Evaluate(candidate, migration);
        var confirming = upper ? C(24, human ? 60 : 100, 141, 50, 69) : C(24, human ? 140 : 100, 145, 49, 131);
        var breakout = new NasdaqPostInvalidationCandidateRebuildPendingTransitionCalculator().Evaluate(pending, confirming).Breakout!;
        return (breakout, [origin, prior, invalidating, start, terminal, migration, confirming]);
    }

    internal static (NasdaqHumanStructuralPriceBreakoutCompletionResult Completed, Candle[] Source) HumanCompletion(bool upper)
    {
        var (breakout, source) = Breakout(upper, true);
        var first = new NasdaqHumanCollisionStructuralPriceObservation(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc,
            breakout.CandidateSide, 111.2345m, At(32), "review:008 first");
        var second = new NasdaqHumanCollisionStructuralPriceObservation(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc,
            breakout.CandidateSide, 111.2345m, At(40), "review:008 second");
        var selected = new NasdaqHumanCollisionStructuralPriceObservationSelector().Select(Context(source, [first, second], 40),
            new(breakout.Episode, breakout.ValidatingCandle.OpenTimeUtc, breakout.CandidateSide));
        return (new NasdaqHumanStructuralPriceBreakoutCompletionCalculator().Evaluate(breakout, selected), source);
    }

    internal static (NasdaqHumanPostCompletionActiveExtremeGeometryResult Geometry, Candle[] Source) Extreme(bool bearish, bool omitMiddleInResolution = false)
    {
        var (breakout, source) = Breakout(bearish, false);
        var completion = new NasdaqH4ReconstructionSnapshot.Completed.Directional(new NasdaqDirectionalMigrationBreakoutCompletionCalculator().Evaluate(breakout));
        var turn = bearish ? Candle(28, 60, 100, 50, 80, 4) : Candle(28, 140, 150, 120, 130, 4);
        var evt = new NasdaqPostCompletionActiveExtremeMembershipEvent(NasdaqPostCompletionEpisode.FromCompleted(completion), turn);
        var members = new[] { At(omitMiddleInResolution ? 16 : 20), At(24) };
        var first = new NasdaqHumanPostCompletionActiveExtremeObservation(evt, members, At(40), "review:cluster first");
        var second = new NasdaqHumanPostCompletionActiveExtremeObservation(evt, members.Reverse(), At(40), "review:cluster second");
        var all = source.Append(turn).ToArray();
        var context = Context(omitMiddleInResolution ? all.Where(c => c.OpenTimeUtc != At(20)).ToArray() : all, [first, second], 40);
        var selection = (NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique)new NasdaqHumanPostCompletionActiveExtremeObservationSelector().Select(context, evt);
        var resolved = (NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved)new NasdaqHumanPostCompletionActiveExtremeMemberResolver().Evaluate(selection, context);
        return (new NasdaqHumanPostCompletionActiveExtremeGeometryCalculator().Evaluate(resolved), all);
    }
}
