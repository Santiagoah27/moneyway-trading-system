using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Positive human-documented bearish turn after one exact Scenario 2 opening sweep (ADR 0026 section 2.2).
/// Presence asserts reviewed reversal, not geometry, a close, eligibility or an order.</summary>
/// <remarks>EventId and EffectiveAtUtc come from the authentic source event. OHLC and video offsets cannot supply
/// that timestamp. ObservedAtUtc is historical semantic availability, not review/import time.</remarks>
public sealed class NasdaqHumanWickfillScenario2PostSweepReversalObservation : IStrategyReplayInputObservation
{
    private static readonly TimeZoneInfo StrategyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public NasdaqHumanWickfillScenario2PostSweepReversalObservation(NasdaqWickfillScenario2OpeningSweepContext openingSweep,
        string eventId, DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference,
        IEnumerable<Candle>? supportingCandles = null)
    {
        OpeningSweep = openingSweep ?? throw new ArgumentNullException(nameof(openingSweep));
        NasdaqStructuralLiquidityReference.ValidateProvenance(eventId);
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (openingSweep.Take.Direction != NasdaqHumanH4PermittedDirection.Sell)
            throw new ArgumentException("Only the currently reviewed SELL bearish-turn semantics are supported.", nameof(openingSweep));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc <= openingSweep.EffectiveAtUtc
            || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(effectiveAtUtc, StrategyTimeZone).DateTime) != Session.TradingDay)
            throw new ArgumentException("The documented UTC reversal must strictly follow its sweep in the exact session.", nameof(effectiveAtUtc));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc || observedAtUtc < openingSweep.ObservedAtUtc)
            throw new ArgumentException("UTC availability cannot precede the reversal or its relied-upon opening-sweep evidence.", nameof(observedAtUtc));
        var sources = (supportingCandles ?? []).ToArray();
        if (sources.Any(c => c is null || c.ProviderId != ProviderId || c.Symbol != Symbol || c.CloseTimeUtc > observedAtUtc)
            || sources.Select(c => (c.Timeframe, c.OpenTimeUtc)).Distinct().Count() != sources.Length)
            throw new ArgumentException("Optional support must be distinct exact session-source candles closed by availability, not necessarily by the event.", nameof(supportingCandles));
        EventId = eventId;
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
        SupportingCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.Timeframe.Unit)
            .ThenBy(c => c.Timeframe.Amount).ThenBy(c => c.OpenTimeUtc).ToArray());
    }

    public NasdaqWickfillScenario2OpeningSweepContext OpeningSweep { get; }
    public string EventId { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }
    public IReadOnlyList<Candle> SupportingCandles { get; }
    public NasdaqDemoSessionIdentity Session => OpeningSweep.Session;
    public NasdaqHumanH4PermittedDirection Direction => OpeningSweep.Take.Direction;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    internal bool SameFact(NasdaqHumanWickfillScenario2PostSweepReversalObservation other) =>
        SameOpeningSweep(OpeningSweep, other.OpeningSweep) && EventId == other.EventId && EffectiveAtUtc == other.EffectiveAtUtc;

    internal static bool SameOpeningSweep(NasdaqWickfillScenario2OpeningSweepContext one, NasdaqWickfillScenario2OpeningSweepContext other) =>
        one.Session == other.Session && one.PreOpenContext.Fact.SameFact(other.PreOpenContext.Fact)
        && one.Take.Direction == other.Take.Direction && one.Take.Take.SameFact(other.Take.Take)
        && one.Take.InitiatingTake.SameFact(other.Take.InitiatingTake)
        && one.CanonicalH4Context.Selection.Fact.Equals(other.CanonicalH4Context.Selection.Fact);
}
