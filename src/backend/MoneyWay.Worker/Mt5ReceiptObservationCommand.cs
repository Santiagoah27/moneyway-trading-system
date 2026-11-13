using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Worker;

/// <summary>Bounded receipt inspection with per-session identity tracking and cancellable operational waits.</summary>
public sealed class Mt5ReceiptObservationCommand
{
    public const int Cancelled = 130;
    private const string Usage = "mt5-receipt observe --source <absolute-directory> --checkpoint <absolute-file> --max-passes <positive-integer> --interval-ms <positive-integer>";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IMt5CollectorReceiptReader reader;
    private readonly Func<TimeSpan, CancellationToken, Task> wait;

    public Mt5ReceiptObservationCommand(IMt5CollectorReceiptReader? reader = null,
        Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        this.reader = reader ?? new LocalMt5CollectorReceiptReader();
        this.wait = wait ?? Task.Delay;
    }

    public async Task<int> ExecuteAsync(IReadOnlyList<string> args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Count == 3 && args[0] == "mt5-receipt" && args[1] == "observe" && args[2] == "--help")
        {
            output.WriteLine(Usage);
            return Mt5ReceiptCommand.Success;
        }
        if (!TryParse(args, out var options))
        {
            error.WriteLine("invalid_observation_arguments\n" + Usage);
            return Mt5ReceiptCommand.InvalidArguments;
        }

        var completed = 0;
        var failedPasses = 0;
        var exit = Mt5ReceiptCommand.Success;
        var reason = "pass_limit";
        object? lastObservation = null;
        string? sourcePath = null;
        string? checkpointPath = null;
        var diagnostics = Array.Empty<string>();
        var changes = new ChangeTracker();
        try
        {
            // Configuration validation performs no receipt acknowledgment.
            _ = new LocalMt5ReceiptCheckpointStore(options.Checkpoint, options.Source);
            sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Source));
            checkpointPath = Path.GetFullPath(options.Checkpoint);
            while (completed < options.MaxPasses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (state, batch, passExit) = Mt5ReceiptCommand.InspectCurrent(options.Source, options.Checkpoint, reader);
                var delta = changes.Observe(state, batch);
                if (delta.ChangedReceiptFiles > 0) passExit = Mt5ReceiptCommand.IntegrityFailure;
                completed++;
                exit = passExit;
                if (passExit != 0) failedPasses++;
                var allCodes = state.Diagnostics.Concat(batch?.Diagnostics ?? [])
                    .Concat(batch?.Records.SelectMany(r => r.Diagnostics) ?? [])
                    .Concat(delta.ChangedReceiptFiles > 0 ? ["observed_record_content_changed"] : Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                lastObservation = new
                {
                    Schema = "moneyway.mt5-receipt-observation",
                    Version = 1,
                    Type = "observation",
                    Pass = completed,
                    PassLimit = options.MaxPasses,
                    options.IntervalMs,
                    SourceDirectory = sourcePath,
                    CheckpointPath = checkpointPath,
                    ExpectedSource = new { Company = "OANDA Global Markets Limited", Server = "OANDA_Global-Demo-1", NativeSymbol = "US100", Demo = true },
                    CheckpointStatus = state.Status.ToString(),
                    CheckpointRevision = state.Revision,
                    DiscoveredRecords = batch?.Records.Count(Mt5ReceiptCommand.IsDiscovered) ?? 0,
                    DurablyAcknowledgedRecords = state.Status == Mt5CheckpointStatus.Missing ? 0 : state.Checkpoint?.Records.Count,
                    VerifiedAcknowledgedRecords = batch?.Records.Count(r => Mt5ReceiptCommand.Acknowledged(r, state.Checkpoint) && r.ReceiptAcknowledgmentEligible && !changes.HasChanged(r)) ?? 0,
                    PendingValidReceipts = batch?.Records.Count(r => !Mt5ReceiptCommand.Acknowledged(r, state.Checkpoint) && r.ReceiptAcknowledgmentEligible && !changes.HasChanged(r)) ?? 0,
                    BlockedReceipts = batch?.Records.Count(r => !r.ReceiptAcknowledgmentEligible || changes.HasChanged(r)) ?? 0,
                    ReceiptValidDiagnosticEvents = batch?.Records.Count(r => r.ReceiptIntegrityValidated && !changes.HasChanged(r)
                        && r.Observation?.EvidenceKind is Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample or Mt5ReceiptEvidenceKind.Shutdown) ?? 0,
                    SourceUnavailableDiagnosticEvents = batch?.Records.Count(r => r.ReceiptIntegrityValidated && !changes.HasChanged(r)
                        && r.Observation?.EvidenceKind == Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample) ?? 0,
                    ShutdownEvents = batch?.Records.Count(r => r.ReceiptIntegrityValidated && !changes.HasChanged(r)
                        && r.Observation?.EvidenceKind == Mt5ReceiptEvidenceKind.Shutdown) ?? 0,
                    IncompleteOrUnavailableAcquisitions = batch?.Records.Count(r => r.Status == Mt5ReceiptStatus.Unresolved) ?? 0,
                    CompleteNativeSamples = batch?.Records.Count(r => r.CompleteNativeSampleObserved && !changes.HasChanged(r)) ?? 0,
                    MissingPredecessors = batch?.Records.Count(r => r.Diagnostics.Contains("missing_predecessor_not_deletion")) ?? 0,
                    UnavailableProbeDependencies = batch?.Records.Count(Mt5ReceiptCommand.ProbeUnavailable) ?? 0,
                    SequenceGaps = batch?.Records.Count(r => r.Diagnostics.Contains("publication_sequence_gap_cause_unresolved")) ?? 0,
                    SourceIdentityValidated = batch?.SourceIdentityValidated == true && delta.ChangedReceiptFiles == 0,
                    ReceiptIntegrityValidated = batch?.ReceiptIntegrityValidated == true && delta.ChangedReceiptFiles == 0,
                    IntegrityStatus = delta.ChangedReceiptFiles > 0 ? "Conflict" : batch?.ReceiptIntegrityValidated == true ? "Validated" : batch?.Status.ToString(),
                    AcquisitionStatus = batch?.Status.ToString(),
                    AcknowledgmentEligible = delta.ChangedReceiptFiles == 0 && batch?.ReceiptAcknowledgmentEligible == true && state.Status is Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded,
                    Changes = delta,
                    Diagnostics = allCodes.Take(12).ToArray(),
                    AdditionalDiagnosticCodes = Math.Max(0, allCodes.Length - 12),
                    PublicationContinuity = "unresolved",
                    DiagnosticOnly = true,
                    CanonicalReady = false,
                    UtcConversionAuthenticated = false,
                    ExitCode = passExit
                };
                output.WriteLine(JsonSerializer.Serialize(lastObservation, Json));
                output.Flush();
                cancellationToken.ThrowIfCancellationRequested();
                if (delta.ChangedReceiptFiles > 0 || IsTerminal(state, batch))
                {
                    reason = "safety_or_storage_failure";
                    break;
                }
                if (completed < options.MaxPasses)
                    await wait(TimeSpan.FromMilliseconds(options.IntervalMs), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            reason = "cancelled";
            exit = Cancelled;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            reason = "configuration_failure";
            diagnostics = ["invalid_paths_use_explicit_separate_local_paths"];
            exit = Mt5ReceiptCommand.InvalidArguments;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            reason = "io_failure";
            diagnostics = ["observation_io_unavailable_inspect_paths_and_permissions"];
            exit = Mt5ReceiptCommand.PersistenceFailure;
        }
        // Cancellation after the final synchronous read still receives an explicit final outcome.
        if (cancellationToken.IsCancellationRequested)
        {
            reason = "cancelled";
            exit = Cancelled;
        }
        var final = new
        {
            Schema = "moneyway.mt5-receipt-observation",
            Version = 1,
            Type = "session_end",
            Reason = reason,
            SourceDirectory = sourcePath,
            CheckpointPath = checkpointPath,
            CompletedPasses = completed,
            PassLimit = options.MaxPasses,
            options.IntervalMs,
            FailedPasses = failedPasses,
            ExitCode = exit,
            Diagnostics = diagnostics,
            LastObservation = lastObservation,
            DiagnosticOnly = true,
            CanonicalReady = false,
            UtcConversionAuthenticated = false,
            NextAction = exit is Mt5ReceiptCommand.Success or Cancelled
                ? "Run a fresh mt5-receipt inspect and review its current evidence before explicit manual acknowledgment."
                : "Investigate diagnostics, preserve evidence and reload checkpoint state before any manual acknowledgment."
        };
        try
        {
            output.WriteLine(JsonSerializer.Serialize(final, Json));
            output.Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine("observation_output_unavailable");
            return Mt5ReceiptCommand.PersistenceFailure;
        }
        return exit;
    }

    private static bool IsTerminal(Mt5CheckpointResult state, Mt5ReceiptBatch? batch)
    {
        if (state.Status is not (Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded or Mt5CheckpointStatus.Busy)) return true;
        if (batch is null) return false;
        if (batch.Diagnostics.Any(c => c != "source_directory_unavailable")) return true;
        return batch.Records.Any(r => r.Diagnostics.Contains("unsafe_source_path")
            || r.Status == Mt5ReceiptStatus.Conflict && !r.Diagnostics.Contains("predecessor_not_validated")
            || r.Status == Mt5ReceiptStatus.Invalid && !r.Diagnostics.Contains("malformed_or_incomplete_json"));
    }

    private static bool TryParse(IReadOnlyList<string> args, out Options options)
    {
        options = new("", "", 0, 0);
        if (args.Count != 10 || args[0] != "mt5-receipt" || args[1] != "observe") return false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 2; index < args.Count; index += 2)
        {
            var name = args[index];
            var value = args[index + 1];
            if (name is not ("--source" or "--checkpoint" or "--max-passes" or "--interval-ms")
                || string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl)
                || !values.TryAdd(name, value)) return false;
        }
        if (!values.TryGetValue("--source", out var source) || !values.TryGetValue("--checkpoint", out var checkpoint)
            || !values.TryGetValue("--max-passes", out var passesText) || !values.TryGetValue("--interval-ms", out var intervalText)
            || !int.TryParse(passesText, NumberStyles.None, CultureInfo.InvariantCulture, out var passes) || passes <= 0
            || !int.TryParse(intervalText, NumberStyles.None, CultureInfo.InvariantCulture, out var interval) || interval <= 0) return false;
        options = new(source, checkpoint, passes, interval);
        return true;
    }

    private sealed record Options(string Source, string Checkpoint, int MaxPasses, int IntervalMs);

    private sealed class ChangeTracker
    {
        private readonly Dictionary<string, string> verifiedHashes = new(StringComparer.Ordinal);
        private HashSet<string> previousBlockers = [];
        private string? previousFingerprint;
        private string? knownRevision;
        private bool hasKnownCheckpoint;
        private bool previousSourceUnavailable;
        private long? unavailableSequence;

        internal bool HasChanged(Mt5ReceiptRecord record) => record.FileName is not null && record.ActualSha256 is not null
            && verifiedHashes.TryGetValue(record.FileName, out var previous) && previous != record.ActualSha256;

        internal ObservationChanges Observe(Mt5CheckpointResult state, Mt5ReceiptBatch? batch)
        {
            var first = previousFingerprint is null;
            var sourceUnavailable = batch?.Diagnostics.Contains("source_directory_unavailable") == true;
            var records = batch?.Records ?? [];
            var verified = records.Where(r => r.ReceiptIntegrityValidated).OrderBy(r => r.Observation!.Metadata.Sequence).ToArray();
            var newlyVerified = verified.Where(r => !verifiedHashes.ContainsKey(r.FileName!)).ToArray();
            var changed = records.Where(HasChanged).ToArray();
            var resumed = false;
            foreach (var record in newlyVerified)
            {
                var sequence = record.Observation!.Metadata.Sequence;
                if (record.Diagnostics.Any(c => c is "acquisition_unavailable_or_disconnected" or "m1_unusable"))
                    unavailableSequence = sequence;
                else if (record.CompleteNativeSampleObserved && unavailableSequence is { } prior && sequence > prior)
                {
                    resumed = !first;
                    unavailableSequence = null;
                }
                verifiedHashes.Add(record.FileName!, record.ActualSha256!);
            }
            var blockers = records.Where(r => !r.ReceiptIntegrityValidated).Select(r => JsonSerializer.Serialize(new
            {
                r.FileName,
                r.ActualSha256,
                r.Status,
                r.Diagnostics
            })).Concat(state.Diagnostics.Where(c => c != "uncommitted_temporary_files_preserved_not_acknowledged")
                .Select(c => "checkpoint/" + c)).Concat(batch?.Diagnostics.Select(c => "source/" + c) ?? [])
                .ToHashSet(StringComparer.Ordinal);
            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                state.Status,
                state.Revision,
                state.Diagnostics,
                SourceDiagnostics = batch?.Diagnostics,
                Records = records.Select(r => new { r.FileName, r.ActualSha256, r.Status, r.ReceiptAcknowledgmentEligible, r.Diagnostics }).ToArray()
            })));
            var checkpointKnown = state.Status is Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded;
            var checkpointChanged = checkpointKnown && hasKnownCheckpoint && knownRevision != state.Revision;
            var result = new ObservationChanges(first, !first && previousFingerprint == fingerprint,
                first ? 0 : newlyVerified.Count(r => r.ReceiptAcknowledgmentEligible),
                first ? 0 : newlyVerified.Count(r => r.ReceiptAcknowledgmentEligible && r.Observation!.EvidenceKind is Mt5ReceiptEvidenceKind.SourceUnavailableWithoutSample or Mt5ReceiptEvidenceKind.Shutdown),
                first ? 0 : newlyVerified.Count(r => r.Observation!.EvidenceKind == Mt5ReceiptEvidenceKind.Shutdown),
                first ? 0 : newlyVerified.Count(r => r.CompleteNativeSampleObserved),
                first ? 0 : blockers.Except(previousBlockers).Count() + changed.Length, resumed, checkpointChanged,
                sourceUnavailable, !first && previousSourceUnavailable && batch is not null && !sourceUnavailable,
                changed.Length, changed.Take(3).Select(r => new ReceiptChange(r.FileName!, verifiedHashes[r.FileName!], r.ActualSha256!)).ToArray());
            if (checkpointKnown) { hasKnownCheckpoint = true; knownRevision = state.Revision; }
            previousFingerprint = fingerprint;
            previousBlockers = blockers;
            if (batch is not null) previousSourceUnavailable = sourceUnavailable;
            return result;
        }
    }
    private sealed record ReceiptChange(string FileName, string PreviousSha256, string CurrentSha256);
    private sealed record ObservationChanges(bool FirstObservation, bool NoChange, int NewValidReceipts,
        int NewReceiptValidDiagnosticEvents, int NewShutdownEvents, int NewCompleteNativeSamples, int NewIntegrityBlockers,
        bool NewSampleAfterUnavailableEvidence, bool CheckpointRevisionChanged, bool SourceDirectoryUnavailable,
        bool SourceDirectoryReturned, int ChangedReceiptFiles, ReceiptChange[] ChangedReceipts);
}
