using System.Collections.ObjectModel;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record StructuralRewardRiskPair(string TargetId, decimal TargetPrice, decimal RewardDistancePoints,
    string? StopPivotId, decimal? StopPrice, decimal? StopDistancePoints, decimal? HypotheticalRewardRisk, string ReasonCode);

public sealed record StructuralRewardRiskComparison(string TargetEvaluationId, string StopEvaluationId,
    M5StructuralPivotProfile StopProfile, IReadOnlyList<StructuralRewardRiskPair> Pairs);

public sealed record StructuralTargetAssociation(M5StructuralStopAssociation Source, StructuralTargetProfile TargetProfile,
    string? TargetEvaluationId, string? RewardRiskComparisonId, StructuralTargetOutcome Outcome, string ReasonCode);

public sealed record StructuralTakeProfitResearchReport(string SessionId, DateTimeOffset AsOfUtc,
    string Provider, string Instrument, string SourceProvenance, IReadOnlyList<StructuralTargetProfile> Profiles,
    IReadOnlyDictionary<string, StructuralTargetEvaluation> Evaluations,
    IReadOnlyDictionary<string, StructuralRewardRiskComparison> RewardRiskComparisons,
    IReadOnlyList<StructuralTargetAssociation> Associations)
{
    public const string ResearchId = "EXP-NQ-STRUCTURAL-TP-V1";
    public const string ResearchVersion = "1.0.0";
    public string CandidateId => ResearchId;
    public string Version => ResearchVersion;
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public string ExecutableTakeProfitPolicy => "Unresolved";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
    public int DistinctEpisodes => Associations.Select(a => a.Source.EpisodeId).Distinct(StringComparer.Ordinal).Count();
    public int DistinctM5Anchors => Associations.Select(a => a.Source.Anchor.Id).Distinct(StringComparer.Ordinal).Count();
    public int DistinctM1References => Evaluations.Values.Select(e => (e.AsOfUtc, e.Direction)).Distinct().Count();
    public int DistinctAvailableTargetReferences => Evaluations.Values.SelectMany(e => e.Candidates)
        .Where(c => c.EligibleAtCutoff).Select(c => c.Id).Distinct(StringComparer.Ordinal).Count();
    public int DistinctSelectedTargetReferences => Evaluations.Values.SelectMany(e => e.SelectedIds)
        .Distinct(StringComparer.Ordinal).Count();
    public int M5M1ConfigurationCombinations => Associations.Select(a =>
        (a.Source.EpisodeId, a.Source.M5Configuration.Id, a.Source.M1Profile)).Distinct().Count();
    public int StructuralConfigurationCombinations => Associations.Select(a =>
        (a.Source.EpisodeId, a.Source.M5Configuration.Id, a.Source.M1Profile, a.Source.PivotProfile.Id, a.TargetProfile.Id))
        .Distinct().Count();
}

public sealed class CompareNasdaqStructuralTakeProfitUseCase
{
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour);

    public static IReadOnlyList<StructuralTargetProfile> ProfilesFor(IReadOnlyList<M5StructuralPivotProfile> stops) =>
        Array.AsReadOnly(stops.OrderBy(p => p.LeftBars).ThenBy(p => p.RightBars)
            .Select(p => new StructuralTargetProfile(StructuralTargetPolicy.NearestConfirmedM5Swing, p))
            .Append(new StructuralTargetProfile(StructuralTargetPolicy.NearestAuditedSessionExtreme, null)).ToArray());

    public StructuralTakeProfitResearchReport Execute(M5StructuralStopResearchReport stops,
        CandleSeries m5, CandleSeries hour, IReadOnlyList<M5FvgClosure>? documentedClosures = null)
    {
        ArgumentNullException.ThrowIfNull(stops);
        ArgumentNullException.ThrowIfNull(m5);
        ArgumentNullException.ThrowIfNull(hour);
        if (m5.Timeframe != M5 || hour.Timeframe != H1 || hour.ProviderId != m5.ProviderId || hour.Symbol != m5.Symbol ||
            m5.ProviderId.Value != stops.Provider || m5.Symbol.Value != stops.Instrument)
            throw new ArgumentException("Original synchronized M5 and exact H1 sources must match the SL context.");
        var closures = documentedClosures ?? [];
        if (closures.Any(c => c.StartUtc.Offset != TimeSpan.Zero || c.EndUtc.Offset != TimeSpan.Zero ||
                              c.StartUtc >= c.EndUtc || string.IsNullOrWhiteSpace(c.EvidenceReference)) ||
            closures.Select(c => (c.StartUtc, c.EndUtc)).Distinct().Count() != closures.Count)
            throw new ArgumentException("Documented closures require distinct UTC bounds and evidence.", nameof(documentedClosures));

        var profiles = ProfilesFor(stops.Profiles);
        var calculator = new NasdaqSessionLiquidityCalculator();
        var evaluations = new SortedDictionary<string, StructuralTargetEvaluation>(StringComparer.Ordinal);
        var comparisons = new SortedDictionary<string, StructuralRewardRiskComparison>(StringComparer.Ordinal);
        var associations = new List<StructuralTargetAssociation>();

        foreach (var group in stops.Associations.GroupBy(s =>
                     (s.EpisodeId, ConfigurationId: s.M5Configuration.Id, s.M1Profile, s.ScanId, s.RealignmentId))
                     .OrderBy(g => g.Key.EpisodeId, StringComparer.Ordinal)
                     .ThenBy(g => g.Key.ConfigurationId, StringComparer.Ordinal).ThenBy(g => g.Key.M1Profile)
                     .ThenBy(g => g.Key.ScanId, StringComparer.Ordinal).ThenBy(g => g.Key.RealignmentId, StringComparer.Ordinal))
        {
            var sources = group.DistinctBy(s => s.PivotProfile.Id)
                .OrderBy(s => s.PivotProfile.LeftBars).ThenBy(s => s.PivotProfile.RightBars).ToArray();
            var first = sources[0];
            if (first.RealignmentId is null)
            {
                foreach (var source in sources)
                    foreach (var profile in profiles)
                        associations.Add(new(source, profile, null, null,
                            source.Outcome == M5StructuralStopOutcome.InsufficientEvidence
                                ? StructuralTargetOutcome.InsufficientEvidence : StructuralTargetOutcome.WaitingForM1Realignment,
                            source.ReasonCode));
                continue;
            }

            var stop = stops.Evaluations[first.EvaluationId!];
            var close = stop.CloseReference;
            if (close.ObservedAtUtc > stops.AsOfUtc || close.ObservedAtUtc != first.RealignmentConfirmedAtUtc ||
                stop.Direction != first.Anchor.Direction)
                throw new ArgumentException("Structural TP must share the observable realignment cutoff and direction.", nameof(stops));

            foreach (var profile in profiles)
            {
                var id = FormattableString.Invariant($"{close.ObservedAtUtc:O}/{stop.Direction}/{first.Anchor.OperatingDate:yyyy-MM-dd}/{profile.Id}");
                if (!evaluations.TryGetValue(id, out var target))
                {
                    target = profile.PivotProfile is { } pivotProfile
                        ? NasdaqStructuralTakeProfitCandidate.Swings(stops.Evaluations[sources.Single(s => s.PivotProfile == pivotProfile).EvaluationId!], m5, closures)
                        : NasdaqStructuralTakeProfitCandidate.Sessions(close, stop.Direction, m5,
                            calculator.CalculateExplicitSessions(hour, first.Anchor.OperatingDate, close.ObservedAtUtc),
                            stops.SourceProvenance, closures);
                    evaluations.Add(id, target);
                }

                foreach (var source in sources)
                {
                    var stopId = source.EvaluationId!;
                    var pairedStop = stops.Evaluations[stopId];
                    if (target.Direction != pairedStop.Direction ||
                        !SameCandle(target.CloseReference.Candle, pairedStop.CloseReference.Candle))
                        throw new ArgumentException("Structural TP and SL must share the exact M1 evidence.", nameof(stops));
                    string? comparisonId = null;
                    if (target.SelectedIds.Count > 0)
                    {
                        comparisonId = $"{id}|{stopId}";
                        if (!comparisons.ContainsKey(comparisonId))
                        {
                            var pairs = target.SelectedIds.Select(targetId => Pair(targetId, target, pairedStop)).ToArray();
                            comparisons.Add(comparisonId, new(id, stopId, pairedStop.Profile, Array.AsReadOnly(pairs)));
                        }
                    }
                    associations.Add(new(source, profile, id, comparisonId, target.Outcome, target.ReasonCode));
                }
            }
        }
        return new(stops.SessionId, stops.AsOfUtc, stops.Provider, stops.Instrument, stops.SourceProvenance, profiles,
            new ReadOnlyDictionary<string, StructuralTargetEvaluation>(evaluations),
            new ReadOnlyDictionary<string, StructuralRewardRiskComparison>(comparisons), associations.AsReadOnly());
    }

    private static bool SameCandle(Candle left, Candle right) =>
        left.ProviderId == right.ProviderId && left.Symbol == right.Symbol && left.Timeframe == right.Timeframe &&
        left.OpenTimeUtc == right.OpenTimeUtc && left.CloseTimeUtc == right.CloseTimeUtc &&
        left.Open == right.Open && left.High == right.High && left.Low == right.Low &&
        left.Close == right.Close && left.Volume == right.Volume;

    internal static StructuralRewardRiskPair Pair(string targetId, StructuralTargetEvaluation target, M5StructuralStopObservation stop)
    {
        var reward = target.RewardDistancePoints!.Value;
        var risk = stop.DistancePoints;
        var pivot = stop.Selected?.Pivot;
        if (risk is not > 0 || stop.Outcome != M5StructuralStopOutcome.StructuralReferenceIdentified)
            return new(targetId, target.TargetPrice!.Value, reward, pivot?.Id, pivot?.Price, null, null, stop.Outcome.ToString());

        try
        {
            return new(targetId, target.TargetPrice!.Value, reward, pivot?.Id, pivot?.Price, risk, reward / risk,
                "PositiveStructuralRewardAndRisk");
        }
        catch (OverflowException)
        {
            return new(targetId, target.TargetPrice!.Value, reward, pivot?.Id, pivot?.Price, risk, null, "RatioOutsideDecimalRange");
        }
    }
}
