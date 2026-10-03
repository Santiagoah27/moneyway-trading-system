using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Closed upstream identity. Prices are consumed from established outputs, never discovered here.</summary>
public abstract class NasdaqLiquidityTakeReference
{
    private NasdaqLiquidityTakeReference(NasdaqDemoSessionIdentity session, decimal price, NasdaqStructuralLiquiditySide side)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId)
            throw new ArgumentException("Liquidity take references belong to Nasdaq.", nameof(session));
        Session = session; ReferencePrice = price; Side = side;
    }

    public NasdaqDemoSessionIdentity Session { get; }
    public decimal ReferencePrice { get; }
    public NasdaqStructuralLiquiditySide Side { get; }
    internal abstract DateTimeOffset AvailableAtUtc { get; }
    internal abstract IReadOnlyList<Candle> Sources { get; }
    internal abstract bool SameSlot(NasdaqLiquidityTakeReference other);

    public enum SessionEndpoint { AsiaHigh, AsiaLow, LondonHigh, LondonLow }

    public sealed class SessionLevel : NasdaqLiquidityTakeReference
    {
        private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        private readonly IReadOnlyList<Candle> sources;

        /// <summary>Consumes the caller's already-calculated available NQ-LIQ-001 output at its exact context.
        /// The caller retains that upstream calculation; this boundary captures sources without recalculating extrema.</summary>
        public SessionLevel(StrategyReplayContext upstreamContext, NasdaqSessionLiquidityCalculationResult calculation,
            SessionEndpoint endpoint, DateTimeOffset selectedAtUtc, string sourceReference)
            : base(NasdaqDemoSessionIdentity.FromContext(upstreamContext ?? throw new ArgumentNullException(nameof(upstreamContext))),
                Price(calculation, endpoint), endpoint is SessionEndpoint.AsiaHigh or SessionEndpoint.LondonHigh
                    ? NasdaqStructuralLiquiditySide.High : NasdaqStructuralLiquiditySide.Low)
        {
            NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
            if (!ReferenceEquals(calculation.SourceContext, upstreamContext)
                || calculation.Levels!.TradingDay != Session.TradingDay || selectedAtUtc.Offset != TimeSpan.Zero
                || selectedAtUtc < upstreamContext.AsOfUtc)
                throw new ArgumentException("Endpoint selection must match the observable upstream session.");
            var hour = new Timeframe(1, TimeframeUnit.Hour);
            if (!upstreamContext.TryGetFrame(hour, out var frame)) throw new ArgumentException("Exact session source frame is required.");
            var start = Session.TradingDay.AddDays(-1).ToDateTime(new TimeOnly(17, 0));
            var end = Session.TradingDay.ToDateTime(new TimeOnly(7, 0));
            var snapshot = frame!.AvailableCandles.Where(c =>
            {
                var local = TimeZoneInfo.ConvertTime(c.OpenTimeUtc, Zone).DateTime;
                return local >= start && local < end;
            }).ToArray();
            // Validate source identity/coverage only; do not calculate or replace the upstream prices.
            if (snapshot.Length != 14 || snapshot.Any(c => c.CloseTimeUtc > selectedAtUtc)
                || snapshot.Select(c => TimeZoneInfo.ConvertTime(c.OpenTimeUtc, Zone).DateTime)
                    .Where((time, index) => time != start.AddHours(index)).Any())
                throw new ArgumentException("Completed exact session sources must precede selection.");
            sources = new ReadOnlyCollection<Candle>(snapshot);
            Calculation = calculation; Endpoint = endpoint; SelectedAtUtc = selectedAtUtc; SourceReference = sourceReference;
        }

        public NasdaqSessionLiquidityCalculationResult Calculation { get; }
        public SessionEndpoint Endpoint { get; }
        public DateTimeOffset SelectedAtUtc { get; }
        public string SourceReference { get; }
        public IReadOnlyList<Candle> SourceCandles => sources;
        internal override IReadOnlyList<Candle> Sources => sources;
        internal override DateTimeOffset AvailableAtUtc => SelectedAtUtc;
        internal override bool SameSlot(NasdaqLiquidityTakeReference other) => other is SessionLevel s
            && Session == s.Session && Endpoint == s.Endpoint && SelectedAtUtc == s.SelectedAtUtc
            && Calculation.Levels == s.Calculation.Levels && Sources.Count == s.Sources.Count
            && Sources.Zip(s.Sources).All(pair => NasdaqStructuralLiquidityReference.SameCandle(pair.First, pair.Second));

        private static decimal Price(NasdaqSessionLiquidityCalculationResult calculation, SessionEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(calculation);
            if (calculation.Levels is not { } levels) throw new ArgumentException("An available upstream calculation is required.", nameof(calculation));
            return endpoint switch
            {
                SessionEndpoint.AsiaHigh => levels.AsiaHigh,
                SessionEndpoint.AsiaLow => levels.AsiaLow,
                SessionEndpoint.LondonHigh => levels.LondonHigh,
                SessionEndpoint.LondonLow => levels.LondonLow,
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
            };
        }
    }

    public sealed class Structural : NasdaqLiquidityTakeReference
    {
        public Structural(NasdaqHumanStructuralLiquiditySelection.Unique selection, NasdaqStructuralLiquidityReference member)
            : base(SessionOf(selection), member?.StructuralPrice ?? throw new ArgumentNullException(nameof(member)), member.Side)
        {
            if (!selection.References.Contains(member)) throw new ArgumentException("The exact member must belong to the Unique upstream set.", nameof(member));
            Selection = selection; Member = member;
        }

        public NasdaqHumanStructuralLiquiditySelection.Unique Selection { get; }
        public NasdaqStructuralLiquidityReference Member { get; }
        internal override IReadOnlyList<Candle> Sources => Selection.References.SelectMany(r => r.SourceCandles).ToArray();
        // Existence of compatible available support is not a winner or source-priority policy.
        internal override DateTimeOffset AvailableAtUtc => Selection.SupportingObservations.Min(o => o.ObservedAtUtc);
        internal override bool SameSlot(NasdaqLiquidityTakeReference other) => other is Structural s && Session == s.Session
            && Member.Equals(s.Member) && Selection.SupportingObservations[0].CompareFact(s.Selection.SupportingObservations[0]) == 0;
        private static NasdaqDemoSessionIdentity SessionOf(NasdaqHumanStructuralLiquiditySelection.Unique selection)
        {
            ArgumentNullException.ThrowIfNull(selection);
            return selection.SupportingObservations[0].Session;
        }
    }
}
