using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Reviewed records construct existing typed observations only after their exact dependencies exist.</summary>
public sealed class NasdaqMentorSessionEvidenceRecord
{
    public NasdaqMentorSessionEvidenceRecord(string recordId, DateTimeOffset observedAtUtc,
        Func<StrategyReplayContext, NasdaqHistoricalTradeSnapshot?, IStrategyReplayInputObservation?> bind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        if (recordId != recordId.Trim()) throw new ArgumentException("Record identity must be trimmed.", nameof(recordId));
        if (observedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Availability must be UTC.", nameof(observedAtUtc));
        RecordId = recordId; ObservedAtUtc = observedAtUtc;
        Bind = bind ?? throw new ArgumentNullException(nameof(bind));
    }
    public string RecordId { get; }
    public DateTimeOffset ObservedAtUtc { get; }
    // Return null until exact prerequisites exist. Effective times and provenance remain in the existing observation.
    public Func<StrategyReplayContext, NasdaqHistoricalTradeSnapshot?, IStrategyReplayInputObservation?> Bind { get; }
    public static NasdaqMentorSessionEvidenceRecord FromObservation(string recordId, IStrategyReplayInputObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new(recordId, observation.ObservedAtUtc, (_, _) => observation);
    }
}

public sealed class NasdaqMentorSessionReplayInput
{
    public static IReadOnlyList<Timeframe> RequiredTimeframes { get; } = Array.AsReadOnly(new Timeframe[]
        { new(4, TimeframeUnit.Hour), new(1, TimeframeUnit.Hour), new(5, TimeframeUnit.Minute), new(1, TimeframeUnit.Minute) });

    public NasdaqMentorSessionReplayInput(string sessionId, NasdaqDemoSessionIdentity session,
        IReadOnlyDictionary<Timeframe, string> csvFiles, IEnumerable<NasdaqMentorSessionEvidenceRecord> evidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId); ArgumentNullException.ThrowIfNull(csvFiles);
        ArgumentNullException.ThrowIfNull(evidence);
        if (sessionId != sessionId.Trim()) throw new ArgumentException("Session identity must be trimmed.", nameof(sessionId));
        SessionId = sessionId; Session = session ?? throw new ArgumentNullException(nameof(session));
        CsvFiles = new System.Collections.ObjectModel.ReadOnlyDictionary<Timeframe, string>(csvFiles.ToDictionary());
        var records = evidence.ToArray();
        if (records.Any(r => r is null) || records.Select(r => r.RecordId).Distinct(StringComparer.Ordinal).Count() != records.Length)
            throw new ArgumentException("Evidence record identities must be non-null and unique.", nameof(evidence));
        Evidence = Array.AsReadOnly(records);
    }
    public string SessionId { get; }
    public NasdaqDemoSessionIdentity Session { get; }
    public IReadOnlyDictionary<Timeframe, string> CsvFiles { get; }
    public IReadOnlyList<NasdaqMentorSessionEvidenceRecord> Evidence { get; }
}
