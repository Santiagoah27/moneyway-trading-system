using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Research.Nasdaq;

public enum NasdaqEvidenceExposure { Unknown, Development, ChronologicalReview, Validation, HeldOut, MentorExposed, Prospective }
public sealed record NasdaqEvidenceSession(DateOnly OperatingDate, DateTimeOffset CutoffUtc,
    [property: System.Text.Json.Serialization.JsonRequired] NasdaqEvidenceExposure Exposure,
    IReadOnlyDictionary<string, DateTimeOffset> RequestedStartsUtc);
public sealed record NasdaqEvidenceClosure(string Timeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string EvidenceSha256);
public sealed record NasdaqEvidenceGap(DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? ClosureEvidenceSha256);
public sealed record NasdaqEvidenceFrame(string Timeframe, DateTimeOffset RequestedStartUtc, DateTimeOffset RequestedEndUtc,
    DateTimeOffset CoverageThroughUtc, int ClosedCount, DateTimeOffset? FirstOpenUtc, DateTimeOffset? LastNominalCloseUtc,
    IReadOnlyList<NasdaqEvidenceGap> MissingRanges, bool CoverageSufficient);
public sealed record NasdaqH4EvidenceReadiness(H4PivotProfile Profile, bool RequestedCoverageSufficient,
    bool EnoughObservableCandles, int ConfirmedHighs, int ConfirmedLows, bool SufficientConfirmedPivots,
    bool ContextEvaluationCompleted, bool ContextAvailable, string ReasonCode)
{
    public bool IndependentlyValidated => false;
}
public sealed record NasdaqEvidenceSessionReadiness(NasdaqEvidenceSession Session,
    IReadOnlyList<NasdaqEvidenceFrame> Frames, IReadOnlyList<NasdaqH4EvidenceReadiness> H4Profiles,
    bool H1SessionReferencesComplete, bool TimestampEvidenceVerified, bool SynchronizedInputReady,
    bool ResearchReplayPermitted, IReadOnlyList<string> Blockers, IReadOnlyList<string> AcquisitionActions)
{
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

public static class NasdaqEvidenceCoverage
{
    public static IReadOnlyDictionary<string, Timeframe> Timeframes { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, Timeframe>(new Dictionary<string, Timeframe>
        {
            ["H4"] = new(4, TimeframeUnit.Hour),
            ["H1"] = new(1, TimeframeUnit.Hour),
            ["M5"] = new(5, TimeframeUnit.Minute),
            ["M1"] = new(1, TimeframeUnit.Minute)
        });

    public static NasdaqEvidenceSessionReadiness Inspect(NasdaqEvidenceSession request,
        IReadOnlyDictionary<string, CandleSeries> series, IReadOnlyList<NasdaqEvidenceClosure> closures,
        IReadOnlySet<string> auditedTimeframes, bool prospectiveReceiptAvailable, string provenance)
    {
        var window = NasdaqTradingWindowAlignment.Evaluate(request.CutoffUtc, request.CutoffUtc);
        if (window.PreparationUtc is not { } preparation ||
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.CutoffUtc, NasdaqSupervisedSessionPolicy.Zone).DateTime) != request.OperatingDate ||
            request.RequestedStartsUtc.Count != Timeframes.Count ||
            Timeframes.Keys.Any(k => !request.RequestedStartsUtc.ContainsKey(k)))
            throw new ArgumentException("Specify an exact Bogotá operating date and UTC requested start for H4, H1, M5 and M1.");
        var evaluate = request.Exposure is NasdaqEvidenceExposure.Development or NasdaqEvidenceExposure.Prospective;
        var bounds = new NasdaqSessionLiquidityCalculator().CalculateExplicitSessions(series["H1"], request.OperatingDate, request.CutoffUtc);
        var frames = Timeframes.Select(entry =>
        {
            var end = entry.Key == "H4" && preparation < request.CutoffUtc ? preparation : request.CutoffUtc;
            var status = Measure(entry.Key, series[entry.Key], request.RequestedStartsUtc[entry.Key], end, closures);
            return status;
        }).ToArray();
        var h4 = frames[0];
        var h4Profiles = H4PivotProfile.InitialResearchProfiles.Select(p =>
        {
            var enough = h4.ClosedCount >= p.LeftBars + p.RightBars + 1;
            if (!evaluate || !auditedTimeframes.Contains("H4") || request.CutoffUtc < preparation)
                return new NasdaqH4EvidenceReadiness(p, h4.CoverageSufficient, enough, 0, 0, false, false, false,
                    !evaluate ? "ExposurePolicyDoesNotEvaluateContext" : request.CutoffUtc < preparation ? "PreparationNotObservable" : "TimestampEvidenceUnresolved");
            var known = closures.Where(c => c.Timeframe == "H4" && c.EndUtc <= preparation)
                .Select(c => new H4DocumentedClosure(c.StartUtc, c.EndUtc, c.EvidenceSha256)).ToArray();
            var evidence = new EvaluateH4ContextCandidateUseCase().Execute(new(series["H4"], h4.RequestedStartUtc,
                preparation, p, provenance, known), 1);
            var structure = evidence.MarketStructure;
            var highs = structure.ConfirmedPivots.Count(pivot => pivot.Kind == H4PivotKind.High);
            var lows = structure.ConfirmedPivots.Count - highs;
            var available = structure.Status == H4ResearchStatus.Classified &&
                evidence.CandleHistory.Any(c => c.Upper.Reference is not null || c.Lower.Reference is not null);
            return new NasdaqH4EvidenceReadiness(p, h4.CoverageSufficient, enough, highs, lows,
                highs >= 2 && lows >= 2, true, available, available ? "ObservableExperimentalH4Context"
                    : structure.Status == H4ResearchStatus.Classified ? "NoObservedH4ContextWithConfirmedReference" : structure.ReasonCode);
        }).ToArray();
        var blockers = new List<string>();
        var actions = new List<string>();
        if (!evaluate) blockers.Add("ExposureDoesNotPermitDevelopmentReplay");
        if (request.Exposure == NasdaqEvidenceExposure.Prospective && !prospectiveReceiptAvailable)
            blockers.Add("ProspectiveLocalAcquisitionEvidenceRequired");
        if (request.CutoffUtc < preparation) blockers.Add("PreparationNotObservable");
        foreach (var frame in frames)
        {
            if (!auditedTimeframes.Contains(frame.Timeframe))
            {
                blockers.Add(frame.Timeframe + ":UnresolvedTimestampOrMissingOriginalSource");
                actions.Add(frame.Timeframe + ": retain original MT5 bytes and supply hash-bound reviewed UTC evidence for every native row.");
            }
            if (!frame.CoverageSufficient)
            {
                blockers.Add(frame.Timeframe + ":IncompleteRequestedCoverage");
                actions.Add(frame.Timeframe + ": obtain the unresolved UTC ranges in missingRanges or register evidence of an exact documented closure.");
            }
        }
        var sessionComplete = bounds.All(b => b.IsComplete) && auditedTimeframes.Contains("H1");
        if (!sessionComplete) blockers.Add("ExactH1AsiaLondonCoverageUnavailable");
        var londonStarts = bounds.Single(x => x.Session == "London").StartUtc;
        var windowHistory = request.RequestedStartsUtc["H1"] <= bounds.Min(x => x.StartUtc) &&
            request.RequestedStartsUtc["M5"] <= londonStarts && request.RequestedStartsUtc["M1"] <= londonStarts;
        if (!windowHistory) blockers.Add("RequestedHistoryStartsAfterRequiredResearchWindow");
        if (evaluate && !h4Profiles.Any(p => p.ContextAvailable))
        {
            blockers.Add("H4ContextUnavailable");
            actions.Add("Obtain earlier native H4 history and its reviewed timestamp mapping, then rerun every profile.");
        }
        var synchronized = request.CutoffUtc >= preparation && frames.All(f => f.CoverageSufficient) &&
            auditedTimeframes.Count == Timeframes.Count && sessionComplete && windowHistory;
        return new(request, frames, h4Profiles, sessionComplete, auditedTimeframes.Count == Timeframes.Count,
            synchronized, synchronized && evaluate &&
            (request.Exposure != NasdaqEvidenceExposure.Prospective || prospectiveReceiptAvailable),
            blockers.AsReadOnly(), actions.AsReadOnly());
    }

    private static NasdaqEvidenceFrame Measure(string timeframe, CandleSeries source, DateTimeOffset start,
        DateTimeOffset end, IReadOnlyList<NasdaqEvidenceClosure> closures)
    {
        if (start.Offset != TimeSpan.Zero || start >= end)
            throw new ArgumentException($"A UTC requested start before the {timeframe} coverage end is required.");
        var rows = source.Candles.Where(c => c.OpenTimeUtc >= start && c.CloseTimeUtc <= end).ToArray();
        var through = timeframe == "H4" ? end : end.AddTicks(-(end.Ticks % Duration(source.Timeframe).Ticks));
        var gaps = new List<NasdaqEvidenceGap>();
        var cursor = start;
        foreach (var candle in rows)
        {
            AddGap(candle.OpenTimeUtc);
            cursor = candle.CloseTimeUtc;
        }
        AddGap(through);
        return new(timeframe, start, end, through, rows.Length, rows.FirstOrDefault()?.OpenTimeUtc,
            rows.LastOrDefault()?.CloseTimeUtc, gaps.AsReadOnly(), rows.Length > 0 && gaps.All(g => g.ClosureEvidenceSha256 is not null));

        void AddGap(DateTimeOffset until)
        {
            if (until <= cursor) return;
            var evidence = closures.SingleOrDefault(c => c.Timeframe == timeframe && c.StartUtc == cursor && c.EndUtc == until);
            gaps.Add(new(cursor, until, evidence?.EvidenceSha256));
        }
    }

    public static TimeSpan Duration(Timeframe timeframe) => timeframe.Unit == TimeframeUnit.Hour
        ? TimeSpan.FromHours(timeframe.Amount) : TimeSpan.FromMinutes(timeframe.Amount);
}
