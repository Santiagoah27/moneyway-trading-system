using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record ContextualLiquidityReference(string Id, string Category, decimal Price, bool HighSide,
    DateOnly OperatingDate, string Provider, string Instrument, string SourceTimeframe, IReadOnlyList<Candle> SourceCandles,
    DateTimeOffset? FormationAtUtc, DateTimeOffset AvailableAtUtc, string CoverageStatus, string Provenance)
{
    public string Classification => "ExperimentalCandidateInventory_NotStrategyRelevance";
}
/// <summary>Optional adapter requires an existing source-resolved, validated human structural selection.</summary>
public sealed class ContextualStructuralReference
{
    private ContextualStructuralReference(ContextualLiquidityReference reference) => Reference = reference;
    public ContextualLiquidityReference Reference { get; }
    public static ContextualStructuralReference FromSelection(StrategyReplayContext context, NasdaqDemoSessionIdentity session, NasdaqStructuralLiquidityReference member)
    {
        var selected = new NasdaqHumanStructuralLiquidityObservationSelector().Select(context, session);
        if (selected is not NasdaqHumanStructuralLiquiditySelection.Unique unique || !unique.References.Contains(member)
            || (member.Validation is not { IsConfirmed: true } && member.DirectionalCompletion is null && member.HumanCollisionCompletion is null && member.ActiveExtremeGeometry is null))
            throw new ArgumentException("An existing source-resolved, explicitly validated structural member is required; no automatic point selection.");
        var category = $"Structural-{member.Timeframe}-{member.Role}-{member.Model}";
        var sources = member.SourceCandles.ToArray();
        var provenance = JsonSerializer.Serialize(new
        {
            session.StrategyId,
            session.StrategyVersion,
            member.SourceReference,
            member.Role,
            member.Model,
            unique.EffectiveAtUtc,
            Support = unique.SupportingObservations.Select(o => new { o.ObservedAtUtc, o.EffectiveAtUtc, o.SourceReference })
        });
        return new(new(TrackContextualLiquidityUseCase.Identity(session.TradingDay, category, member.ProviderId, member.Symbol, member.StructuralPrice, sources, provenance),
            category, member.StructuralPrice, member.Side == NasdaqStructuralLiquiditySide.High, session.TradingDay, member.ProviderId.Value, member.Symbol.Value,
            member.Timeframe.ToString(), Array.AsReadOnly(sources), member.Members.Count > 0 ? member.Members.Max(c => c.CloseTimeUtc) : null,
            context.AsOfUtc, "SourceResolvedValidatedSelection", provenance));
    }
}
public sealed record ContextualLiquidityObservation(string Id, string ReferenceId, Candle Candle, DateTimeOffset AvailableAtUtc,
    string IntervalPhase, string AvailabilityPhase, LondonAsiaSideObservation Relationship, bool CloseOnOtherSide,
    int PriorObservationCount, int PriorPenetrationCount, bool PriorPenetrationBeforeOpen, string? PriorPenetrationId,
    string? PriorLaterOtherSideCloseId)
{
    public bool IsInteraction => Relationship.ExactExtremeContact || Relationship.StrictPenetration || Relationship.CloseBeyond || Relationship.OpensBeyondReference;
    public bool AdditionalPenetrationAfterLaterOtherSideClose => Relationship.StrictPenetration && PriorLaterOtherSideCloseId is not null;
    public string Unresolved => "Relevance, replacement, reuse and swing anchor unresolved; no intrabar crossing or valid take inferred.";
}
public sealed record ContextualReferenceTracking(ContextualLiquidityReference Reference, string ObservationCoverage,
    IReadOnlyList<DateTimeOffset> MissingClosedM5OpensUtc, IReadOnlyList<ContextualLiquidityObservation> Observations,
    string HistoryStatus, bool PenetrationObservedBeforeOpen, int PenetrationsAvailableAtOrAfterOpen)
{
    public ContextualLiquidityObservation? FirstObservedPenetration => Observations.FirstOrDefault(o => o.Relationship.StrictPenetration);
}
public sealed record ContextualLiquidityCandleGroup(Candle Candle, IReadOnlyList<string> InteractingReferenceIds,
    IReadOnlyList<string> PenetratedReferenceIds, bool OppositeSidesPenetrated)
{
    public string Ordering => PenetratedReferenceIds.Count > 1 ? "MultipleReferences_SameCandleOrderUnknown" : "NoIntrabarOrderEstablished";
}
public sealed record ContextualLiquidityTrackingResult(DateOnly OperatingDate, DateTimeOffset RangeStartUtc, DateTimeOffset CutoffUtc,
    DateTimeOffset PreparationUtc, DateTimeOffset ObservationStartUtc, M5ResearchDataOrigin DataOrigin, string Provenance,
    LondonAsiaInteractionResult LondonHistoricalContext, IReadOnlyList<ContextualReferenceTracking> References,
    IReadOnlyList<ContextualLiquidityCandleGroup> CandleGroups, IReadOnlyList<string> UnavailableReferences)
{
    public string AlgorithmId => "EXP-NQ-LIQ-CONTEXT-V1";
    public string AlgorithmVersion => "1.0.0";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public string Classification => "BoundedDocumentaryResearch_NotRelevanceOrTradingAuthorization";
    public string ObservationTimeframe => "5m";
    public string BoundaryMeaning => "Explicit research range/cutoff, not resolution of 11:00/11:30 trading-window conflict. Closed-candle documentary availability is unauthenticated.";
}

/// <summary>Pure bounded fold. All references survive; serialization order is not a relevance ranking.</summary>
public sealed class TrackContextualLiquidityUseCase
{
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    internal static string Identity(DateOnly day, string category, MarketDataProviderId provider, MarketSymbol symbol, decimal price, IReadOnlyList<Candle> sources, string provenance) =>
        "EXP-NQ-LIQ-CONTEXT-V1/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Day = day, category, provider, symbol, Price = price.ToString(CultureInfo.InvariantCulture), Sources = sources, provenance }))));
    public ContextualLiquidityTrackingResult Execute(CandleSeries sessionSource, CandleSeries observations, DateOnly day,
        DateTimeOffset rangeStartUtc, DateTimeOffset cutoffUtc, M5ResearchDataOrigin origin, string provenance,
        IReadOnlyList<ContextualStructuralReference>? structural = null)
    {
        ArgumentNullException.ThrowIfNull(sessionSource); ArgumentNullException.ThrowIfNull(observations);
        if (sessionSource.Timeframe != H1 || observations.Timeframe != M5 || sessionSource.ProviderId != observations.ProviderId || sessionSource.Symbol != observations.Symbol)
            throw new ArgumentException("Homogeneous provider/instrument with exact H1 references and M5 observations required.");
        if (rangeStartUtc.Offset != TimeSpan.Zero || cutoffUtc.Offset != TimeSpan.Zero || rangeStartUtc > cutoffUtc || rangeStartUtc.Ticks % TimeSpan.FromMinutes(5).Ticks != 0)
            throw new ArgumentException("Ordered explicit UTC research bounds and grid-aligned M5 range start required.");
        var context = new AnalyzeLondonAsiaInteractionUseCase().Execute(sessionSource, day, cutoffUtc, origin, provenance);
        var prep = context.London.EndUtc; var opening = prep.AddMinutes(30);
        if (rangeStartUtc < context.London.StartUtc || cutoffUtc > prep.AddDays(1)) throw new ArgumentException("Bounded operating-day experiment starts no earlier than London; no implicit trading-window cutoff.");
        var inventory = new List<ContextualLiquidityReference>(); var unavailable = new List<string>();
        foreach (var session in new[] { context.Asia, context.London })
        {
            if (!session.IsComplete) { unavailable.Add(session.Session + ":" + session.CoverageStatus); continue; }
            // Re-evaluate the same visible H1 prefix at session end, never infer publication from a later corrected export.
            var completed = new NasdaqSessionLiquidityCalculator().CalculateExplicitSessions(sessionSource, day, session.EndUtc).Single(s => s.Session == session.Session);
            if (!completed.IsComplete) { unavailable.Add(session.Session + ":UnavailableAtSessionEnd"); continue; }
            foreach (var high in new[] { true, false })
            {
                var category = (session.Session == "Asia" ? "A" : "L") + (high ? "H" : "L");
                var price = high ? completed.High!.Value : completed.Low!.Value;
                var sources = high ? completed.HighCandles : completed.LowCandles;
                var sourceProvenance = provenance + " | " + completed.StrategyVersion + " | " + completed.Session + " | " + completed.StartUtc.ToString("O") + "/" + completed.EndUtc.ToString("O");
                inventory.Add(new(Identity(day, category, sessionSource.ProviderId, sessionSource.Symbol, price, sources, sourceProvenance), category, price, high,
                    day, session.Provider, session.Instrument, session.SourceTimeframe, sources, sources.Max(c => c.CloseTimeUtc), completed.EndUtc, "Complete", sourceProvenance));
            }
        }
        foreach (var item in structural ?? [])
        {
            var r = item.Reference;
            if (r.AvailableAtUtc > cutoffUtc) continue; // future selections cannot affect an earlier inventory, including its IDs/counts
            if (r.Provider != sessionSource.ProviderId.Value || r.Instrument != sessionSource.Symbol.Value || r.OperatingDate != day) throw new ArgumentException("Structural evidence identity mismatch.");
            inventory.Add(r);
        }
        if (inventory.Select(r => r.Id).Distinct().Count() != inventory.Count) throw new ArgumentException("Duplicate reference identities are not silently merged.");
        var closed = observations.Candles.Where(c => c.OpenTimeUtc >= rangeStartUtc && c.CloseTimeUtc <= cutoffUtc).ToArray();
        var tracked = inventory.Select(reference =>
        {
            var expected = new List<DateTimeOffset>();
            for (var t = rangeStartUtc; t.AddMinutes(5) <= cutoffUtc; t = t.AddMinutes(5)) if (t >= reference.AvailableAtUtc) expected.Add(t);
            var rows = closed.Where(c => c.OpenTimeUtc >= reference.AvailableAtUtc && expected.Contains(c.OpenTimeUtc) && c.CloseTimeUtc == c.OpenTimeUtc.AddMinutes(5)).ToArray();
            var missing = expected.Except(rows.Select(c => c.OpenTimeUtc)).ToArray();
            var invalid = closed.Any(c => c.OpenTimeUtc >= reference.AvailableAtUtc && (!expected.Contains(c.OpenTimeUtc) || c.CloseTimeUtc != c.OpenTimeUtc.AddMinutes(5)));
            var coverage = invalid ? "InvalidObservationIntervals" : missing.Length > 0 ? "MissingClosedIntervals" : expected.Count == 0 ? "NoClosedIntervalsYet" : "CompleteThroughLastClosedInterval";
            var events = new List<ContextualLiquidityObservation>(); int penetrations = 0; bool preOpen = false;
            string? priorPenetration = null; string? laterOtherSideClose = null;
            foreach (var c in rows)
            {
                var relationship = AnalyzeLondonAsiaInteractionUseCase.ObserveSide(c, reference.Price, reference.HighSide);
                var otherSide = reference.HighSide ? c.Close < reference.Price : c.Close > reference.Price;
                var id = reference.Id + "/M5/" + c.OpenTimeUtc.ToString("O");
                var intervalPhase = c.CloseTimeUtc <= prep ? "LondonHistoricalContext" : c.OpenTimeUtc < opening ? "Preparation" : "Observation";
                var availablePhase = c.CloseTimeUtc < prep ? "LondonHistoricalContext" : c.CloseTimeUtc < opening ? "Preparation" : "Observation";
                events.Add(new(id, reference.Id, c, c.CloseTimeUtc, intervalPhase, availablePhase, relationship, otherSide, events.Count, penetrations, preOpen, priorPenetration, laterOtherSideClose));
                if (penetrations > 0 && otherSide) laterOtherSideClose = id;
                if (relationship.StrictPenetration) { penetrations++; priorPenetration = id; if (c.CloseTimeUtc < opening) preOpen = true; }
            }
            var history = penetrations > 0 ? "StrictPenetrationObserved" : coverage == "CompleteThroughLastClosedInterval" ? "NoObservedStrictPenetrationInCoveredInterval" : "InsufficientHistoricalCoverage";
            return new ContextualReferenceTracking(reference, coverage, Array.AsReadOnly(missing), events.AsReadOnly(), history, preOpen,
                events.Count(e => e.AvailableAtUtc >= opening && e.Relationship.StrictPenetration));
        }).ToArray();
        var groups = tracked.SelectMany(t => t.Observations.Select(o => (t.Reference, Event: o))).Where(p => p.Event.IsInteraction)
            .GroupBy(p => p.Event.Candle.OpenTimeUtc).OrderBy(g => g.Key).Select(g => new ContextualLiquidityCandleGroup(g.First().Event.Candle,
                Array.AsReadOnly(g.Select(p => p.Reference.Id).ToArray()), Array.AsReadOnly(g.Where(p => p.Event.Relationship.StrictPenetration).Select(p => p.Reference.Id).ToArray()),
                g.Any(p => p.Event.Relationship.StrictPenetration && p.Reference.HighSide) && g.Any(p => p.Event.Relationship.StrictPenetration && !p.Reference.HighSide))).ToArray();
        return new(day, rangeStartUtc, cutoffUtc, prep, opening, origin, provenance, context, Array.AsReadOnly(tracked), Array.AsReadOnly(groups), unavailable.AsReadOnly());
    }
}
