using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace MoneyWay.IntegrationTests;

// Explicitly synthetic collector/probe v1 fixture. No terminal or authentic captured data.
internal sealed class Mt5ReceiptSyntheticFixture : IDisposable
{
    internal const long Head = 1767614400;
    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), $"moneyway-mt5-receipt-{Guid.NewGuid():N}");
    internal Mt5ReceiptSyntheticFixture() => Directory.CreateDirectory(DirectoryPath);
    internal string RecordPath(long n) => Path.Combine(DirectoryPath, $"record-{n:D12}.json");
    internal string ProbePath(long n) => Path.Combine(DirectoryPath, $"probe-{n:D12}.json");
    internal static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    internal static JsonObject Native(long raw) => new()
    {
        ["raw"] = raw,
        ["display"] = new DateTime(1970, 1, 1).AddSeconds(raw).ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture)
    };
    internal static void Write(string path, JsonNode value) => File.WriteAllText(path, value.ToJsonString() + "\n", new UTF8Encoding(false));
    private static JsonObject Series(long value) => new() { ["value"] = value, ["error_code"] = 0 };
    internal static JsonObject Probe(long head)
    {
        var frames = new JsonArray();
        foreach (var (name, seconds) in new[] { ("M1", 60), ("M5", 300), ("H1", 3600), ("H4", 14400) })
        {
            var at = head / seconds * seconds;
            var bars = new JsonArray();
            for (var i = 0; i < 2; i++) bars.Add(new JsonObject
            {
                ["series_position"] = i,
                ["open_time_native"] = Native(at - i * seconds),
                ["open"] = 100.25m,
                ["high"] = 102.5m,
                ["low"] = 99.75m,
                ["close"] = 101.25m,
                ["ohlc_valid"] = true,
                ["role"] = i == 0 ? "current_bar_completion_unverified" : "historical_preceding_bar",
                ["newer_than_next_seconds"] = i == 0 ? JsonValue.Create(seconds) : null,
                ["open_time_utc"] = null,
                ["provider_confirmed_closed"] = null,
                ["historical_available_at_utc"] = null
            });
            frames.Add(new JsonObject
            {
                ["timeframe"] = name,
                ["period_seconds"] = seconds,
                ["requested_count"] = 2,
                ["status"] = "sample_returned",
                ["copy_return"] = 2,
                ["copy_error_code"] = 0,
                ["head_consistent"] = true,
                ["copy_started_elapsed_us"] = 10,
                ["copy_finished_elapsed_us"] = 20,
                ["synchronized_before"] = Series(1),
                ["synchronized_after"] = Series(1),
                ["last_bar_before"] = Series(at),
                ["last_bar_after"] = Series(at),
                ["terminal_bar_count"] = Series(2),
                ["terminal_first_date"] = Series(at - seconds),
                ["server_first_date"] = Series(at - seconds),
                ["bars"] = bars
            });
        }
        return new JsonObject
        {
            ["schema"] = "moneyway.mt5-read-only-probe",
            ["version"] = 1,
            ["diagnostic_only"] = true,
            ["canonical_ready"] = false,
            ["generated_at_utc"] = null,
            ["generation_time_basis"] = "TimeGMT_computer_derived_not_authenticated",
            ["generated_gmt_candidate"] = Native(head),
            ["utc_conversion_authenticated"] = false,
            ["feed_equivalence_verified"] = false,
            ["source"] = new JsonObject
            {
                ["platform"] = "MetaTrader 5",
                ["company"] = "OANDA Global Markets Limited",
                ["server"] = "OANDA_Global-Demo-1",
                ["demo_verified"] = true,
                ["mode_error_code"] = 0,
                ["connected_before"] = true,
                ["terminal_build"] = 6246
            },
            ["gate"] = "passed",
            ["symbol"] = new JsonObject
            {
                ["native"] = "US100",
                ["query_attempted"] = true,
                ["exists"] = true,
                ["custom"] = false,
                ["selected"] = true,
                ["error_code"] = 0,
                ["description"] = "SYNTHETIC FIXTURE",
                ["description_error_code"] = 0,
                ["profit_currency"] = "USD",
                ["profit_currency_error_code"] = 0,
                ["digits"] = 2,
                ["digits_error_code"] = 0
            },
            ["server_quote_before"] = Native(head),
            ["server_quote_after"] = Native(head),
            ["connected_after"] = true,
            ["timeframes"] = frames,
            ["limitations"] = new JsonArray("synthetic_not_provider_evidence")
        };
    }
    internal void Add(long sequence = 1, long? previous = null, long? head = null, long run = 1, long? poll = null, string? ev = null)
    {
        var at = head ?? Head + (sequence - 1) * 60;
        Write(ProbePath(sequence), Probe(at));
        Write(RecordPath(sequence), new JsonObject
        {
            ["schema"] = "moneyway.mt5-read-only-collector",
            ["version"] = 1,
            ["diagnostic_only"] = true,
            ["canonical_ready"] = false,
            ["utc_conversion_authenticated"] = false,
            ["sequence"] = sequence,
            ["run_id"] = $"run-{run:D12}",
            ["poll_sequence"] = poll ?? sequence,
            ["event"] = ev ?? (previous is null || run == sequence ? "initial_snapshot" : "new_native_m1"),
            ["condition"] = 0,
            ["timer_seconds"] = 5,
            ["retry_every_polls"] = 12,
            ["sample_bars"] = 2,
            ["source"] = new JsonObject { ["company"] = "OANDA Global Markets Limited", ["server"] = "OANDA_Global-Demo-1", ["demo"] = true, ["symbol"] = "US100" },
            ["trigger_m1_head_native"] = Native(at),
            ["captured_m1_head_native"] = Native(at),
            ["previous_committed_m1_head_native"] = previous is null || run == sequence ? null : Native(Head + (previous.Value - 1) * 60),
            ["m1_usable"] = true,
            ["sample_complete"] = true,
            ["source_stable"] = true,
            ["previous_record"] = previous is null ? null : $"record-{previous:D12}.json",
            ["previous_record_sha256"] = previous is null ? null : File.Exists(RecordPath(previous.Value)) ? Hash(RecordPath(previous.Value)) : new string('0', 64),
            ["probe_file"] = $"probe-{sequence:D12}.json",
            ["probe_sha256"] = Hash(ProbePath(sequence)),
            ["shutdown_reason"] = null,
            ["sequence_basis"] = "local_single_writer_publication_not_utc"
        });
    }
    internal void ChangeRecord(long n, Action<JsonObject> change)
    { var value = JsonNode.Parse(File.ReadAllText(RecordPath(n)))!.AsObject(); change(value); Write(RecordPath(n), value); }
    internal void ChangeProbe(long n, Action<JsonObject> change, bool rebind = true)
    {
        var value = JsonNode.Parse(File.ReadAllText(ProbePath(n)))!.AsObject(); change(value); Write(ProbePath(n), value);
        if (rebind) ChangeRecord(n, r => r["probe_sha256"] = Hash(ProbePath(n)));
    }
    public void Dispose()
    {
        // Only this fixture's unique, explicitly owned temporary directory.
        var full = Path.GetFullPath(DirectoryPath);
        if (!full.StartsWith(Path.Combine(Path.GetTempPath(), "moneyway-mt5-receipt-"), StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected cleanup scope.");
        foreach (var path in Directory.EnumerateFileSystemEntries(full))
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            { if (File.GetAttributes(path).HasFlag(FileAttributes.Directory)) Directory.Delete(path); else File.Delete(path); }
        Directory.Delete(full, true);
    }
}
