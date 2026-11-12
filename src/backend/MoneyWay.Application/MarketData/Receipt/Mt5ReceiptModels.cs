using System.Collections.ObjectModel;

namespace MoneyWay.Application.MarketData.Receipt;

// Receipt statuses are independent from RuleEvaluationResult and StrategyVerdict.
public enum Mt5ReceiptStatus { Validated, Unresolved, DataUnavailable, Conflict, Invalid }
public enum Mt5ReceiptDisposition { New, AlreadyReceived, NotReceived }

/// <summary>A source-native coordinate, deliberately not a DateTimeOffset/UTC instant.</summary>
public sealed record Mt5NativeTimestamp(long Raw, string Display);
public sealed record Mt5NativeSeriesValue(long? Value, int ErrorCode);
public sealed record Mt5NativeBar(int SeriesPosition, Mt5NativeTimestamp OpenTimeNative, string Role,
    string OpenLiteral, string HighLiteral, string LowLiteral, string CloseLiteral, bool OhlcValid,
    long? NewerThanNextSeconds)
{
    public bool IsPositionZero => SeriesPosition == 0;
    public bool? ProviderConfirmedClosed => null;
    public DateTimeOffset? OpenTimeUtc => null;
    public DateTimeOffset? HistoricalAvailableAtUtc => null;
}

public sealed record Mt5NativeFrame(string Timeframe, int PeriodSeconds, int RequestedCount,
    string AcquisitionStatus, int? CopyReturn, int? CopyErrorCode, bool? HeadConsistent,
    Mt5NativeSeriesValue? SynchronizedBefore, Mt5NativeSeriesValue? SynchronizedAfter,
    Mt5NativeSeriesValue? LastBarBefore, Mt5NativeSeriesValue? LastBarAfter,
    Mt5NativeSeriesValue? TerminalBarCount, Mt5NativeSeriesValue? TerminalFirstDate,
    Mt5NativeSeriesValue? ServerFirstDate, ulong? CopyStartedElapsedUs, ulong? CopyFinishedElapsedUs,
    IReadOnlyList<Mt5NativeBar> Bars);

public sealed record Mt5CollectorMetadata(long Sequence, string RunId, long PollSequence, string Event,
    int Condition, int TimerSeconds, int RetryEveryPolls, int SampleBars,
    Mt5NativeTimestamp? TriggerM1HeadNative, Mt5NativeTimestamp? PreviousCommittedM1HeadNative,
    Mt5NativeTimestamp? CapturedM1HeadNative, bool M1Usable, bool SampleComplete, bool SourceStable,
    string? PreviousRecord, string? PreviousRecordSha256, string? ProbeFile, string? ProbeSha256, int? ShutdownReason);

public sealed record Mt5NativeAcquisition(string DirectoryScope, string RecordFile, string RecordSha256,
    Mt5CollectorMetadata Metadata, int? TerminalBuild, string? ProbeGate, bool? ConnectedBefore,
    bool? ConnectedAfter, Mt5NativeTimestamp? GeneratedGmtCandidate,
    Mt5NativeTimestamp? ServerQuoteBefore, Mt5NativeTimestamp? ServerQuoteAfter,
    IReadOnlyList<Mt5NativeFrame> Timeframes)
{
    public string Company => "OANDA Global Markets Limited";
    public string Server => "OANDA_Global-Demo-1";
    public string NativeSymbol => "US100";
    public bool Demo => true;
    public bool CanonicalReady => false;
    public bool UtcConversionAuthenticated => false;
}

public sealed record Mt5ReceivedRecordIdentity(string FileName, string Sha256, long Sequence,
    string RunId, long PollSequence, string Event);

/// <summary>Caller-owned, defensively copied receipt ledger. Not a persisted or globally unique run registry.</summary>
public sealed class Mt5ReceiptCheckpoint
{
    public Mt5ReceiptCheckpoint(string directoryScope, IEnumerable<Mt5ReceivedRecordIdentity> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryScope);
        ArgumentNullException.ThrowIfNull(records);
        DirectoryScope = directoryScope;
        var copy = new SortedDictionary<string, Mt5ReceivedRecordIdentity>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
            copy.Add(record.FileName, record);
            if (copy.Count > 4096) throw new ArgumentException("Receipt checkpoint exceeds its technical bound.", nameof(records));
        }
        Records = new ReadOnlyDictionary<string, Mt5ReceivedRecordIdentity>(copy);
    }
    public string DirectoryScope { get; }
    public IReadOnlyDictionary<string, Mt5ReceivedRecordIdentity> Records { get; }
}

public sealed record Mt5ReceiptRecord(string? FileName, string? ActualSha256, bool? ExpectedHashMatched,
    Mt5ReceiptStatus Status, Mt5ReceiptDisposition Disposition, Mt5NativeAcquisition? Observation,
    IReadOnlyList<string> Diagnostics);

public sealed record Mt5ReceiptBatch(string DirectoryScope, IReadOnlyList<Mt5ReceiptRecord> Records,
    IReadOnlyList<string> Diagnostics, Mt5ReceiptCheckpoint Checkpoint)
{
    public bool CanonicalReady => false;
    public Mt5ReceiptStatus Status => Diagnostics.Count > 0 || Records.Count == 0 ? Mt5ReceiptStatus.DataUnavailable
        : Records.Any(x => x.Status == Mt5ReceiptStatus.Conflict) ? Mt5ReceiptStatus.Conflict
        : Records.Any(x => x.Status == Mt5ReceiptStatus.Invalid) ? Mt5ReceiptStatus.Invalid
        : Records.Any(x => x.Status == Mt5ReceiptStatus.DataUnavailable) ? Mt5ReceiptStatus.DataUnavailable
        : Records.Any(x => x.Status == Mt5ReceiptStatus.Unresolved) ? Mt5ReceiptStatus.Unresolved : Mt5ReceiptStatus.Validated;
    public IReadOnlyList<string> TemporalBlockers { get; } = Array.AsReadOnly(new[]
    {
        "utc_conversion_unresolved", "provider_closure_unresolved", "historical_availability_unresolved",
        "non_atomic_timeframe_acquisition", "warmup_and_freshness_policy_unresolved"
    });
    public IEnumerable<Mt5NativeAcquisition> NewObservations => Records
        .Where(x => x.Disposition == Mt5ReceiptDisposition.New && x.Observation is not null).Select(x => x.Observation!);
}
