using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Worker;

/// <summary>One-shot local receipt orchestration. Runs before host creation; owns no strategy or execution state.</summary>
public sealed class Mt5ReceiptCommand
{
    public const int Success = 0, InvalidArguments = 2, InvalidSource = 3, IntegrityFailure = 4,
        CheckpointConflict = 5, PersistenceFailure = 6;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private const string Usage = "mt5-receipt inspect --source <absolute-directory> --checkpoint <absolute-file>\n"
        + "mt5-receipt commit --source <absolute-directory> --checkpoint <absolute-file> --expected-revision <sha256|missing> --review-token <sha256> --acknowledge-receipt";

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        if (args.Count == 2 && args[0] == "mt5-receipt" && args[1] == "--help") { output.WriteLine(Usage); return Success; }
        if (!TryParse(args, out var options)) { error.WriteLine("invalid_arguments\n" + Usage); return InvalidArguments; }
        try
        {
            // Follow the existing one-shot command composition convention. No hosted services or configuration defaults.
            IMt5ReceiptCheckpointStore store = new LocalMt5ReceiptCheckpointStore(options.Checkpoint, options.Source);
            IMt5CollectorReceiptReader reader = new LocalMt5CollectorReceiptReader();
            var loaded = store.Load();
            if (loaded.Status is not (Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded))
                return Report(options, loaded, loaded.Verification, null, null, StoreExit(loaded), output);

            var batch = loaded.Verification ?? reader.Read(options.Source, loaded.Checkpoint);
            var safe = batch.Status == Mt5ReceiptStatus.Validated;
            var token = safe ? ReviewToken(options, loaded.Revision, batch.Checkpoint) : null;
            if (options.Operation == "inspect")
                return Report(options, loaded, batch, token, null, safe ? Success : ReceiptExit(batch), output);

            if (loaded.Revision != options.ExpectedRevision)
                return Report(options, loaded, batch, token, "checkpoint_revision_changed_inspect_again", CheckpointConflict, output);
            if (!safe) return Report(options, loaded, batch, null, "receipt_not_safe_to_acknowledge", ReceiptExit(batch), output);
            if (token != options.ReviewToken)
                return Report(options, loaded, batch, token, "reviewed_progress_changed_inspect_again", CheckpointConflict, output);

            // Store revalidates hashes/ancestry under its lock and commits only this exact reviewed ledger.
            var committed = store.Commit(batch.Checkpoint, options.ExpectedRevision);
            var code = committed.Status is Mt5CheckpointStatus.Committed or Mt5CheckpointStatus.Unchanged ? Success : StoreExit(committed);
            return Report(options, committed, committed.Verification ?? batch, null, null, code, output);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        { error.WriteLine("invalid_paths_use_explicit_separate_local_paths\n" + Usage); return InvalidArguments; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { error.WriteLine("local_io_unavailable_inspect_paths_and_permissions"); return PersistenceFailure; }
    }

    private static int Report(Options options, Mt5CheckpointResult state, Mt5ReceiptBatch? batch,
        string? token, string? code, int exit, TextWriter output)
    {
        var safe = batch?.Status == Mt5ReceiptStatus.Validated && state.Status is Mt5CheckpointStatus.Missing or Mt5CheckpointStatus.Loaded;
        var candidate = safe ? batch!.Checkpoint : null;
        var response = new
        {
            Schema = "moneyway.mt5-receipt-command-report",
            Version = 1,
            options.Operation,
            SourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Source)),
            CheckpointPath = Path.GetFullPath(options.Checkpoint),
            ExpectedSource = new { Company = "OANDA Global Markets Limited", Server = "OANDA_Global-Demo-1", NativeSymbol = "US100", Demo = true },
            SourceIdentityValidated = batch?.Status == Mt5ReceiptStatus.Validated,
            CheckpointStatus = state.Status.ToString(),
            CheckpointRevision = state.Revision,
            ExpectedRevision = state.Status == Mt5CheckpointStatus.Missing ? "missing" : state.Revision,
            DurablyAcknowledgedRecords = state.Status == Mt5CheckpointStatus.Missing ? 0 : state.Checkpoint?.Records.Count,
            RecordsReported = batch?.Records.Count ?? 0,
            // Reader placeholders for unavailable old ledger files were not discovered in this inventory.
            DiscoveredRecords = batch?.Records.Count(IsDiscovered) ?? 0,
            VerifiedAcknowledgedRecords = batch?.Records.Count(r => Acknowledged(r, state.Checkpoint) && r.Status == Mt5ReceiptStatus.Validated) ?? 0,
            NewlyValidatedPendingRecords = batch?.Records.Count(r => !Acknowledged(r, state.Checkpoint) && r.Status == Mt5ReceiptStatus.Validated) ?? 0,
            BlockedRecords = batch?.Records.Count(r => r.Status != Mt5ReceiptStatus.Validated) ?? 0,
            MissingPredecessors = batch?.Records.Count(r => r.Diagnostics.Contains("missing_predecessor_not_deletion")) ?? 0,
            UnavailableProbeDependencies = batch?.Records.Count(ProbeUnavailable) ?? 0,
            SequenceGaps = batch?.Records.Count(r => r.Diagnostics.Contains("publication_sequence_gap_cause_unresolved")) ?? 0,
            IntegrityStatus = batch?.Status.ToString(),
            SafeToAcknowledge = safe,
            ReviewToken = options.Operation == "inspect" ? token : null,
            CandidateCheckpoint = candidate is null ? null : new
            {
                candidate.DirectoryScope,
                RecordCount = candidate.Records.Count,
                LastAcknowledgedCandidate = candidate.Records.Values.LastOrDefault()
            },
            Diagnostics = state.Diagnostics.Concat(batch?.Diagnostics ?? []).Concat(code is null ? [] : new[] { code })
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            Records = batch?.Records.Select(r => new
            {
                r.FileName,
                Discovered = IsDiscovered(r),
                r.ActualSha256,
                Status = r.Status.ToString(),
                ReceiptDisposition = r.Disposition.ToString(),
                DurablyAcknowledged = Acknowledged(r, state.Checkpoint),
                ProbeDependencyUnavailable = ProbeUnavailable(r),
                r.Observation?.Metadata.Sequence,
                r.Observation?.Metadata.RunId,
                r.Observation?.Metadata.PollSequence,
                r.Observation?.Metadata.Event,
                CapturedM1HeadNative = r.Observation?.Metadata.CapturedM1HeadNative,
                r.Diagnostics
            }).ToArray(),
            DiagnosticOnly = true,
            CanonicalReady = false,
            UtcConversionAuthenticated = false,
            TemporalBlockers = batch?.TemporalBlockers,
            ExitCode = exit,
            NextAction = exit == Success ? options.Operation == "inspect"
                ? "Review records and diagnostics; acknowledge receipt explicitly using expectedRevision and reviewToken."
                : "Receipt acknowledged only. Inspect again to discover pending acquisitions; no downstream effect is guaranteed."
                : exit == CheckpointConflict ? "Inspect again and review the current checkpoint and evidence; do not force or reset."
                : exit == IntegrityFailure || exit == InvalidSource ? "Inspect blocking diagnostics and retain original evidence; do not acknowledge this batch."
                : "Inspect storage diagnostics, permissions and orphan temporaries; reload before retrying; do not reset."
        };
        output.WriteLine(JsonSerializer.Serialize(response, Json));
        return exit;
    }

    private static bool IsDiscovered(Mt5ReceiptRecord record) => !record.Diagnostics.Contains("expected_or_received_file_unavailable_not_deletion", StringComparer.Ordinal);
    private static bool Acknowledged(Mt5ReceiptRecord record, Mt5ReceiptCheckpoint? committed) => record.FileName is { } name
        && committed?.Records.TryGetValue(name, out var identity) == true && identity.Sha256 == record.ActualSha256;
    private static bool ProbeUnavailable(Mt5ReceiptRecord record) => record.ActualSha256 is not null
        && record.Diagnostics.Contains("artifact_unavailable_or_unreadable", StringComparer.Ordinal);

    private static int StoreExit(Mt5CheckpointResult result) => result.Status switch
    {
        Mt5CheckpointStatus.Busy or Mt5CheckpointStatus.Conflict => CheckpointConflict,
        Mt5CheckpointStatus.Invalid => IntegrityFailure,
        Mt5CheckpointStatus.DataUnavailable when result.Verification is { } batch => ReceiptExit(batch),
        Mt5CheckpointStatus.DataUnavailable when result.Diagnostics.Contains("unsafe_source_path") => InvalidSource,
        _ => PersistenceFailure
    };
    private static int ReceiptExit(Mt5ReceiptBatch batch) => batch.Records.Count == 0
        || batch.Diagnostics.Contains("source_directory_unavailable") || batch.Diagnostics.Contains("unsafe_source_path")
        || batch.Records.Any(r => r.Diagnostics.Any(c => c is "source_identity" or "symbol_identity")) ? InvalidSource : IntegrityFailure;

    private static string ReviewToken(Options options, string? revision, Mt5ReceiptCheckpoint checkpoint)
    {
        var path = Path.GetFullPath(options.Checkpoint);
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Protocol = "moneyway.mt5-receipt-review.v1",
            CheckpointPath = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path,
            ExpectedRevision = revision,
            checkpoint.DirectoryScope,
            Records = checkpoint.Records.Values.ToArray()
        })));
    }

    private static bool TryParse(IReadOnlyList<string> args, out Options options)
    {
        options = new("", "", "", null, null);
        if (args.Count < 2 || args[0] != "mt5-receipt" || args[1] is not ("inspect" or "commit")) return false;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var acknowledge = false;
        for (var i = 2; i < args.Count; i++)
        {
            var name = args[i];
            if (name == "--acknowledge-receipt") { if (acknowledge) return false; acknowledge = true; continue; }
            if (name is not ("--source" or "--checkpoint" or "--expected-revision" or "--review-token") || ++i >= args.Count
                || string.IsNullOrWhiteSpace(args[i]) || args[i].Any(char.IsControl) || args[i].StartsWith("--", StringComparison.Ordinal)
                || args[i] != args[i].Trim() || !values.TryAdd(name, args[i])) return false;
        }
        if (!values.TryGetValue("--source", out var source) || !values.TryGetValue("--checkpoint", out var path)) return false;
        var commit = args[1] == "commit";
        if (!commit && (acknowledge || values.Count != 2)) return false;
        if (commit && (!acknowledge || values.Count != 4 || !values.TryGetValue("--expected-revision", out var rev)
            || rev != "missing" && !Digest(rev) || !values.TryGetValue("--review-token", out var token) || !Digest(token))) return false;
        options = new(args[1], source, path, commit && values["--expected-revision"] != "missing" ? values["--expected-revision"].ToLowerInvariant() : null,
            commit ? values["--review-token"].ToLowerInvariant() : null);
        return true;
    }
    private static bool Digest(string value) => Regex.IsMatch(value, "^[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant);
    private sealed record Options(string Operation, string Source, string Checkpoint, string? ExpectedRevision, string? ReviewToken);
}
