using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record H4DocumentaryM5Event(string EvidenceKey, DateTimeOffset ObservedAtUtc, M5FvgDirection Direction,
    IReadOnlyList<string> ObservationIds, IReadOnlyList<string> EpisodeIds);

public sealed record H4DocumentaryM1Event(DateTimeOffset ConfirmedAtUtc, M5FvgDirection Direction,
    IReadOnlyList<string> ObservationIds, IReadOnlyList<string> EpisodeIds);

public sealed record H4CausalProfileComparison(string ConfigurationId, H4PivotProfile PivotProfile, int Horizon,
    bool V1ContextAvailable, string V1GateReason, int V1CandlesWithReferences, int V1NontrivialLevelInteractions,
    H4CausalContextObservation? V2, int V1CompleteAssociations, int V2CompleteAssociations,
    IReadOnlyList<H4DocumentaryM1Event> V2CompletedM1Events);

public sealed record NasdaqH4CausalContextComparison(string DatasetId, DateOnly OperatingDate, string Provider, string Instrument,
    string SourceProvenance, NasdaqResearchReplayCutoff ReportCutoff, DateTimeOffset PreparationUtc,
    NasdaqResearchReplayCutoff? PreparationCutoff, NasdaqResearchChainOutcome V1Outcome,
    NasdaqResearchChainSummary V1DownstreamSummary, IReadOnlyList<H4CausalProfileComparison> Profiles,
    IReadOnlyList<H4DocumentaryM5Event> DocumentaryM5Events, IReadOnlyList<H4DocumentaryM1Event> DocumentaryM1Events,
    int V2DistinctCompletedM1Events)
{
    public string CandidateId => H4CausalContextObservation.ResearchId;
    public string Version => H4CausalContextObservation.ResearchVersion;
    public string BaselineCandidateId => NasdaqEndToEndResearchResult.ResearchId;
    public string BaselineVersion => NasdaqEndToEndResearchResult.ResearchVersion;
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public bool ExperimentalChainComplete => V2DistinctCompletedM1Events > 0;
    public bool CanonicalReady => false;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Parallel comparison using one synchronized baseline replay and its existing downstream evidence.</summary>
public sealed class CompareNasdaqH4CausalContextUseCase
{
    private static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);

    public NasdaqH4CausalContextComparison Execute(NasdaqResearchReplayRequest request)
    {
        var baseline = new EvaluateNasdaqEndToEndResearchUseCase().Execute(request);
        var preparation = baseline.Preparation;
        var evaluations = new EvaluateH4CausalContextResearchUseCase();
        var h4 = request.OriginalSources.Single(s => s.Timeframe == H4);
        var sequences = baseline.Sequence?.Results ?? [];
        var m5 = sequences.SelectMany(s => s.Confirmations
                .Where(c => s.FirstEligibleIds.Contains(c.Event.Id))
                .Select(c => (s.Episode.Id, c.Event)))
            .GroupBy(x => x.Event.AssociationKey, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new H4DocumentaryM5Event(g.Key,
                g.First().Event.ConfirmedAtUtc, g.First().Event.Direction,
                Ids(g.Select(x => x.Event.Id)), Ids(g.Select(x => x.Id)))).ToArray();
        var directions = sequences.ToDictionary(s => (s.Episode.Id, s.Configuration.Id), s => s.Direction);
        var realignments = baseline.Chains.Where(c => c.RealignmentUtc is not null && c.RealignmentId is not null)
            .Select(c => (Chain: c, Direction: directions[(c.EpisodeId, c.M5ConfigurationId)]))
            .Where(x => x.Direction is not null && x.Chain.Branches.Any(b => b.RealignmentBindingVerified))
            .Select(x => (x.Chain, Direction: x.Direction!.Value)).ToArray();
        var v1Completions = baseline.Chains.SelectMany(c => c.Geometry).SelectMany(g => g.ContextOutcomes)
            .Where(c => c.CompletedAtUtc is not null).SelectMany(c => c.H4ConfigurationIds).ToLookup(id => id, StringComparer.Ordinal);
        var profiles = preparation.Profiles.Select(profile =>
        {
            var v2 = profile.Evidence is null || request.H4HistoryStartUtc is not { } start ? null : evaluations.Execute(
                new(h4, start, preparation.ScheduledUtc, profile.PivotProfile, request.SourceProvenance, request.H4DocumentedClosures), profile.Horizon);
            var qualified = v2?.ContextAlignedDirection is { } direction
                ? realignments.Where(r => (r.Direction == M5FvgDirection.Bullish) == (direction == H4ResearchDirection.Buy)
                    && r.Chain.LiquidityTakeUtc >= preparation.ScheduledUtc
                    && NasdaqResearchChainProjection.ContextAtRealignment(r.Chain.Branches, r.Chain.RealignmentUtc!.Value).CompletedAtUtc is not null
                    && r.Chain.Geometry.Any(g => g.StructuralReferencesCompletedAtUtc is not null)).ToArray()
                : [];
            var history = profile.Evidence?.CandleHistory ?? [];
            return new H4CausalProfileComparison(profile.Id, profile.PivotProfile, profile.Horizon, profile.ContextAvailable,
                profile.ReasonCode, history.Count(c => c.Upper.Reference is not null || c.Lower.Reference is not null),
                history.Sum(c => (IsInteraction(c.Upper) ? 1 : 0) + (IsInteraction(c.Lower) ? 1 : 0)), v2,
                v1Completions[profile.Id].Count(),
                qualified.Sum(x => x.Chain.Geometry.Count(g => g.StructuralReferencesCompletedAtUtc is not null)),
                DistinctM1(qualified));
        }).ToArray();
        var uniqueCompletions = profiles.SelectMany(p => p.V2CompletedM1Events).Select(e => (e.ConfirmedAtUtc, e.Direction)).Distinct().Count();
        return new(baseline.DatasetId, baseline.OperatingDate, baseline.Provider, baseline.Instrument, baseline.SourceProvenance,
            baseline.ReplayCutoff, preparation.ScheduledUtc, preparation.ReplayCutoff, baseline.Outcome, baseline.Summary,
            Array.AsReadOnly(profiles), Array.AsReadOnly(m5), DistinctM1(realignments), uniqueCompletions);
    }

    private static bool IsInteraction(H4LevelInteraction level) =>
        level.Outcome is not (H4LevelOutcome.NoInteraction or H4LevelOutcome.InsufficientEvidence);

    private static IReadOnlyList<H4DocumentaryM1Event> DistinctM1(IEnumerable<(NasdaqResearchChain Chain, M5FvgDirection Direction)> rows) =>
        Array.AsReadOnly(rows.GroupBy(x => (At: x.Chain.RealignmentUtc!.Value, x.Direction))
            .OrderBy(g => g.Key.At).ThenBy(g => g.Key.Direction)
            .Select(g => new H4DocumentaryM1Event(g.Key.At, g.Key.Direction, Ids(g.Select(x => x.Chain.RealignmentId!)),
                Ids(g.Select(x => x.Chain.EpisodeId)))).ToArray());

    private static IReadOnlyList<string> Ids(IEnumerable<string> ids) =>
        Array.AsReadOnly(ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
}
