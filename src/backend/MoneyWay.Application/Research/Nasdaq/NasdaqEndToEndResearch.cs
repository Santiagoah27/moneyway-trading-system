using MoneyWay.Application.MarketData.Replay;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Research.Nasdaq;

public sealed record NasdaqResearchReplayRequest(string DatasetId, DateOnly OperatingDate, DateTimeOffset CutoffUtc,
    IReadOnlyList<CandleSeries> OriginalSources, DateTimeOffset? H4HistoryStartUtc, string SourceProvenance,
    IReadOnlyList<H4DocumentedClosure>? H4DocumentedClosures = null);

public sealed record NasdaqResearchFrameSource(string Timeframe, int ClosedCount, DateTimeOffset? FirstOpenUtc,
    DateTimeOffset? LastNominalCloseUtc);
public sealed record NasdaqResearchReplayCutoff(DateTimeOffset RequestedUtc, DateTimeOffset? CanonicalFrameUtc,
    int? CanonicalStep, IReadOnlyList<string> UpdatedTimeframes, IReadOnlyList<NasdaqResearchFrameSource> Sources);
public sealed record NasdaqResearchPreparationProfile(string Id, H4PivotProfile PivotProfile, int Horizon,
    bool ContextAvailable, string ReasonCode, H4ContextObservation? Evidence)
{
    public bool IndependentlyValidated => false;
}

public sealed record NasdaqResearchPreparation(DateTimeOffset ScheduledUtc, NasdaqResearchReplayCutoff? ReplayCutoff,
    IReadOnlyList<NasdaqResearchPreparationProfile> Profiles);

public sealed record NasdaqEndToEndResearchResult(string DatasetId, DateOnly OperatingDate, string Provider, string Instrument,
    string ReplayStrategyVersion, string SourceProvenance, NasdaqResearchReplayCutoff ReplayCutoff,
    NasdaqTradingWindowComparison Window, NasdaqResearchPreparation Preparation,
    IReadOnlyList<NasdaqResearchReplayCutoff> RealignmentCutoffs,
    IReadOnlyList<NasdaqSessionReference> SessionLiquidity, NasdaqResearchChainOutcome Outcome,
    IReadOnlyList<NasdaqResearchChain> Chains, NasdaqResearchChainSummary Summary,
    M5ContextualSequenceReport? Sequence, M1RealignmentReport? Realignment,
    M5StructuralStopResearchReport? StructuralStop, StructuralTakeProfitResearchReport? StructuralTakeProfit)
{
    public const string ResearchId = "EXP-NQ-END-TO-END-MTF-V1";
    public const string ResearchVersion = "1.1.0";
    public string CandidateId => ResearchId;
    public string Version => ResearchVersion;
    public string HistoricalAvailability => "DocumentaryNominalClosure_NotAuthenticated";
    public string LiquidityAuthority => "ExistingContextualEpisodes_ResearchOnly_StandardAndWF2Unchanged";
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

/// <summary>Runs isolated candidates over canonical candle prefixes with explicit clock cutoffs.</summary>
public sealed class EvaluateNasdaqEndToEndResearchUseCase
{
    private static readonly Timeframe H4 = new(4, TimeframeUnit.Hour);
    private static readonly Timeframe H1 = new(1, TimeframeUnit.Hour);
    private static readonly Timeframe M5 = new(5, TimeframeUnit.Minute);
    private static readonly Timeframe M1 = new(1, TimeframeUnit.Minute);
    private static readonly Timeframe[] Required = [H4, H1, M5, M1];
    private static readonly StrategyDefinition Published = MoneyWayNasdaqStrategyDefinition.Instance;
    public static IReadOnlyList<int> H4ContextHorizons { get; } = Array.AsReadOnly(new[] { 1, 2 });

    public static IReadOnlyList<M5SequenceConfiguration> SequenceConfigurations() =>
        (from sweep in Enum.GetValues<M5SweepPolicy>()
         from window in Enum.GetValues<M5SequenceWindowPolicy>()
         from formation in Enum.GetValues<M5SequenceFvgPolicy>()
         from profile in Enum.GetValues<M5FvgProfile>()
         select new M5SequenceConfiguration(sweep, window, formation, profile,
             NasdaqTradingWindowAlignment.Opening, NasdaqTradingWindowAlignment.Closing)).ToArray();

    public NasdaqEndToEndResearchResult Execute(NasdaqResearchReplayRequest request) => Execute(request, out _);

    internal NasdaqEndToEndResearchResult Execute(NasdaqResearchReplayRequest request, out CandleSeries m1)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DatasetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceProvenance);
        var zone = NasdaqSupervisedSessionPolicy.Zone;
        var window = NasdaqTradingWindowAlignment.Evaluate(request.CutoffUtc, request.CutoffUtc);
        if (window.PreparationUtc is not { } prepUtc ||
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.CutoffUtc, zone).DateTime) != request.OperatingDate ||
            request.H4HistoryStartUtc is { Offset: var offset } && offset != TimeSpan.Zero)
            throw new ArgumentException("Explicit UTC cutoffs and the corresponding Bogotá operating date required.", nameof(request));
        if (request.OriginalSources.Count != Required.Length || !Required.ToHashSet()
                .SetEquals(request.OriginalSources.Select(s => s.Timeframe)))
            throw new ArgumentException("Configure exact original H4, H1, M5 and M1 streams, including empty streams for missing evidence.", nameof(request));

        var cursor = new CanonicalMultiTimeframeReplayCursor(request.OriginalSources);
        if (cursor.ProviderId.Value != "oanda-global-markets-mt5-demo" || cursor.Symbol.Value != "US100")
            throw new ArgumentException("Registered US100 original provider identity required.", nameof(request));
        CanonicalMultiTimeframeReplayFrame? current = null;
        CanonicalMultiTimeframeReplayFrame? prep = null;
        var contexts = new CreateStrategyReplayContextUseCase();
        var realignmentFrames = new Dictionary<DateTimeOffset, NasdaqResearchReplayCutoff>();
        while (cursor.TryAdvance(out var frame) && frame!.AsOfUtc <= request.CutoffUtc)
        {
            current = frame;
            if (frame.AsOfUtc <= prepUtc) prep = frame;
            if (frame.AsOfUtc >= window.Candidate!.StartUtc && frame.AsOfUtc < window.Candidate.EndUtc &&
                frame.UpdatedTimeframes.Contains(M1))
                realignmentFrames.Add(frame.AsOfUtc, Cutoff(contexts.ExecuteCanonical(Published, frame), frame.AsOfUtc));
        }
        var at = current is null ? null : contexts.ExecuteCanonical(Published, current);
        var atPrep = prep is null ? null : contexts.ExecuteCanonical(Published, prep);
        CandleSeries Series(StrategyReplayContext? context, Timeframe timeframe) =>
            new(cursor.ProviderId, cursor.Symbol, timeframe,
                context is not null && context.TryGetFrame(timeframe, out var source) && source is not null ? source.AvailableCandles : []);

        NasdaqResearchReplayCutoff Cutoff(StrategyReplayContext? context, DateTimeOffset requested) =>
            new(requested, context?.AsOfUtc, context?.Step,
                context?.AsOfUtc == requested ? context.UpdatedTimeframes.Select(t => t.ToString()).ToArray() : [],
                Required.Select(tf =>
                {
                    var rows = context is not null && context.TryGetFrame(tf, out var source) && source is not null ? source.AvailableCandles : [];
                    return new NasdaqResearchFrameSource(tf.ToString(), rows.Count, rows.FirstOrDefault()?.OpenTimeUtc,
                        rows.LastOrDefault()?.CloseTimeUtc);
                }).ToArray());

        var h4 = Series(atPrep, H4);
        var preparationProfiles = from profile in H4PivotProfile.InitialResearchProfiles
                                  from horizon in H4ContextHorizons
                                  select EvaluatePreparation(profile, horizon);
        var preparation = new NasdaqResearchPreparation(prepUtc, request.CutoffUtc >= prepUtc ? Cutoff(atPrep, prepUtc) : null,
            preparationProfiles.ToArray());
        var h1 = Series(at, H1);
        var m5 = Series(at, M5);
        m1 = Series(at, M1);
        var sessions = new NasdaqSessionLiquidityCalculator().CalculateExplicitSessions(h1, request.OperatingDate, request.CutoffUtc);
        var rangeStart = sessions.Single(s => s.Session == "London").StartUtc;
        if (request.CutoffUtc < rangeStart)
            return Result(NasdaqResearchChainOutcome.WaitingForPreparationEvidence);

        var sc = new CompareContextualSwingScUseCase().Execute(h1, m5, request.OperatingDate, rangeStart,
            request.CutoffUtc, M5ResearchDataOrigin.RetrospectiveDocumentary, request.SourceProvenance);
        var sequence = new EvaluateM5ContextualSequenceUseCase().Execute(request.DatasetId, sc, m5, SequenceConfigurations());
        var realignment = new EvaluateM1RealignmentCandidateUseCase().Execute(sequence, m1, Enum.GetValues<M1RealignmentProfile>());
        var stops = new CompareM5StructuralStopLossUseCase().Execute(realignment, m5, M5StructuralPivotProfile.Initial);
        var targets = new CompareNasdaqStructuralTakeProfitUseCase().Execute(stops, m5, h1);
        var chains = NasdaqResearchChainProjection.Build(sequence, realignment, targets, preparation);
        return Result(chains.Count == 0
            ? request.CutoffUtc < prepUtc && h1.Count > 0 ? NasdaqResearchChainOutcome.WaitingForPreparationEvidence : NasdaqResearchChainOutcome.DataUnavailable
            : NasdaqResearchChainProjection.SummarizeOutcome(chains), chains, sequence, realignment, stops, targets);

        NasdaqResearchPreparationProfile EvaluatePreparation(H4PivotProfile profile, int horizon)
        {
            var id = H4ContextObservation.ProfileId(profile, horizon);
            if (request.CutoffUtc < prepUtc)
                return new(id, profile, horizon, false, "PreparationCutoffNotReached", null);
            if (request.H4HistoryStartUtc is not { } start || start >= prepUtc)
                return new(id, profile, horizon, false, "H4RequestedHistoryUnavailable", null);
            if (h4.Candles.Any(c => c.OpenTimeUtc >= start && c.CloseTimeUtc - c.OpenTimeUtc != TimeSpan.FromHours(4)))
                return new(id, profile, horizon, false, "InvalidClosedH4Interval", null);
            var evidence = new EvaluateH4ContextCandidateUseCase().Execute(new(h4, start, prepUtc,
                profile, request.SourceProvenance, request.H4DocumentedClosures?.Where(c => c.EndUtc <= prepUtc).ToArray()), horizon);
            var available = evidence.MarketStructure.Status == H4ResearchStatus.Classified &&
                evidence.CandleHistory.Any(c => c.Upper.Reference is not null || c.Lower.Reference is not null);
            return new(id, profile, horizon, available,
                available ? "ObservableExperimentalH4Context" : evidence.MarketStructure.Status == H4ResearchStatus.Classified
                    ? "NoObservedH4ContextWithConfirmedReference" : evidence.MarketStructure.ReasonCode, evidence);
        }

        NasdaqEndToEndResearchResult Result(NasdaqResearchChainOutcome outcome, IReadOnlyList<NasdaqResearchChain>? rows = null,
            M5ContextualSequenceReport? m5Report = null, M1RealignmentReport? m1Report = null,
            M5StructuralStopResearchReport? stop = null, StructuralTakeProfitResearchReport? target = null) =>
            new(request.DatasetId, request.OperatingDate, cursor.ProviderId.Value, cursor.Symbol.Value, Published.Version.Value,
                request.SourceProvenance, Cutoff(at, request.CutoffUtc), window, preparation,
                (m1Report?.Scans ?? []).Select(s => s.FirstRealignmentUtc).OfType<DateTimeOffset>().Distinct().Order()
                    .Select(t => realignmentFrames[t]).ToArray(), sessions, outcome,
                rows ?? [], NasdaqResearchChainProjection.Summarize(rows ?? [], preparation, m5Report, target), m5Report, m1Report, stop, target);
    }
}
