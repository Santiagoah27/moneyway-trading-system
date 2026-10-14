using System.Collections.ObjectModel;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Human assertion that SELL Wickfill context and the macro correction into the selected H4 zone
/// were established before opening (ADR 0026). No geometry, sweep, eligibility or rule result is inferred.</summary>
/// <remarks>Reviewed sources support both the H4 ancestry and the human-confirmed completed movement.
/// Availability is authentic historical semantic availability, never the review/import clock.</remarks>
public sealed class NasdaqHumanWickfillScenario2ContextObservation : IStrategyReplayInputObservation
{
    private static readonly TimeZoneInfo StrategyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    public NasdaqHumanWickfillScenario2ContextObservation(NasdaqHumanH4ContextObservation h4Context,
        NasdaqHumanH4StructuralZoneAnchor applicableZone, IEnumerable<Candle> reviewedSourceCandles,
        DateTimeOffset effectiveAtUtc, DateTimeOffset observedAtUtc, string sourceReference)
    {
        H4Context = h4Context ?? throw new ArgumentNullException(nameof(h4Context));
        ApplicableZone = applicableZone ?? throw new ArgumentNullException(nameof(applicableZone));
        ArgumentNullException.ThrowIfNull(reviewedSourceCandles);
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        if (h4Context.Fact.PermittedDirection != NasdaqHumanH4PermittedDirection.Sell
            || h4Context.Fact.ContextKind != NasdaqHumanH4ContextKind.Wickfill)
            throw new ArgumentException("An explicitly reviewed SELL Wickfill context is required; other classifications are not converted.", nameof(h4Context));
        if (!h4Context.Fact.ZoneAnchors.Contains(applicableZone))
            throw new ArgumentException("The applicable zone must belong to the exact reviewed H4 context.", nameof(applicableZone));
        var local = TimeZoneInfo.ConvertTime(effectiveAtUtc, StrategyTimeZone);
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < h4Context.EffectiveAtUtc
            || DateOnly.FromDateTime(local.DateTime) != Session.TradingDay || local.TimeOfDay >= new TimeSpan(8, 30, 0))
            throw new ArgumentException("Established context time must be UTC, follow its H4 fact and precede the same-session opening.", nameof(effectiveAtUtc));
        var sources = reviewedSourceCandles.ToArray();
        if (sources.Length == 0 || sources.Any(c => c is null || c.ProviderId != ProviderId || c.Symbol != Symbol)
            || sources.Select(c => (c.Timeframe, c.OpenTimeUtc)).Distinct().Count() != sources.Length)
            throw new ArgumentException("Distinct non-null reviewed sources must match the session provider and instrument.", nameof(reviewedSourceCandles));
        if (sources.Any(c => c.CloseTimeUtc > effectiveAtUtc))
            throw new ArgumentException("Reviewed source candles must be closed by established context time.", nameof(effectiveAtUtc));
        var h4Sources = sources.Where(c => c.Timeframe == NasdaqHumanH4ContextObservation.H4).ToDictionary(c => c.OpenTimeUtc);
        if (RequiredH4OpenTimes(h4Context.Fact).Any(open => !h4Sources.TryGetValue(open, out var candle)
            || candle.CloseTimeUtc > h4Context.EffectiveAtUtc))
            throw new ArgumentException("Exact closed context, anchor and validation sources are required for the H4 ancestry.", nameof(reviewedSourceCandles));
        if (observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc < effectiveAtUtc || observedAtUtc < h4Context.ObservedAtUtc)
            throw new ArgumentException("Availability must be UTC and follow the effective context and relied-upon human H4 availability.", nameof(observedAtUtc));
        ReviewedSourceCandles = new ReadOnlyCollection<Candle>(sources.OrderBy(c => c.Timeframe.Unit)
            .ThenBy(c => c.Timeframe.Amount).ThenBy(c => c.OpenTimeUtc).ToArray());
        EffectiveAtUtc = effectiveAtUtc;
        ObservedAtUtc = observedAtUtc;
        SourceReference = sourceReference;
    }

    public NasdaqHumanH4ContextObservation H4Context { get; }
    public NasdaqHumanH4StructuralZoneAnchor ApplicableZone { get; }
    public IReadOnlyList<Candle> ReviewedSourceCandles { get; }
    public NasdaqDemoSessionIdentity Session => H4Context.Session;
    public NasdaqHumanH4PermittedDirection Direction => H4Context.Fact.PermittedDirection;
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;
    public DateTimeOffset EffectiveAtUtc { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    public string SourceReference { get; }

    internal bool SameFact(NasdaqHumanWickfillScenario2ContextObservation other) => Session == other.Session
        && H4Context.Fact.Equals(other.H4Context.Fact) && ApplicableZone.Equals(other.ApplicableZone)
        && EffectiveAtUtc == other.EffectiveAtUtc && ReviewedSourceCandles.Count == other.ReviewedSourceCandles.Count
        && ReviewedSourceCandles.Zip(other.ReviewedSourceCandles)
            .All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));

    private static IEnumerable<DateTimeOffset> RequiredH4OpenTimes(NasdaqHumanH4ContextFact fact) =>
        fact.Anchors.SelectMany(a => a.MemberOpenTimesUtc.Concat(a.ValidatingCandleOpenTimeUtc is { } t ? [t] : []))
            .Concat(fact.ZoneAnchors.SelectMany(a => a.MemberOpenTimesUtc.Concat(a.ValidatingCandleOpenTimeUtc is { } t ? [t] : [])))
            .Append(fact.ContextCandleOpenTimeUtc).Distinct();
}
