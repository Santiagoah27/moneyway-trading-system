using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

// Native implementation of the existing collector/probe v1 wire contracts. No timezone conversion.
internal static class Mt5WireContract
{
    internal const string Company = "OANDA Global Markets Limited", Server = "OANDA_Global-Demo-1";
    internal static readonly string[] FrameNames = ["M1", "M5", "H1", "H4"];
    internal static readonly int[] Periods = [60, 300, 3600, 14400];
    internal static bool RecordName(string name) => Regex.IsMatch(name, "^record-[0-9]{12}\\.json\\z", RegexOptions.CultureInvariant);
    internal static bool Digest(string text) => Regex.IsMatch(text, "^[0-9a-fA-F]{64}\\z", RegexOptions.CultureInvariant);
    internal static string RecordFile(long sequence) => $"record-{sequence:D12}.json";
    internal static void Need(bool condition, string code = "invalid_structure") { if (!condition) throw new Mt5WireException(code); }

    internal static JsonDocument Document(byte[] bytes)
    {
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        try { CheckDuplicates(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { Need(names.Add(property.Name), "duplicate_json_property"); CheckDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) CheckDuplicates(child);
    }
    private static void Keys(JsonElement value, string fields, string optional = "")
    {
        Need(value.ValueKind == JsonValueKind.Object);
        var required = fields.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allowed = required.Concat(optional.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToHashSet(StringComparer.Ordinal);
        Need(value.EnumerateObject().All(p => allowed.Contains(p.Name)), "unexpected_property");
        Need(required.All(k => value.TryGetProperty(k, out _)), "missing_property");
    }
    private static string Text(JsonElement value)
    {
        Need(value.ValueKind == JsonValueKind.String);
        var text = value.GetString()!;
        Need(text.Length <= 1024 && !text.Any(char.IsControl), "string_bound_or_control_character");
        return text;
    }
    private static string S(JsonElement value, string name) => Text(value.GetProperty(name));
    private static long N(JsonElement value, string name)
    {
        var node = value.GetProperty(name);
        Need(node.ValueKind == JsonValueKind.Number && node.TryGetInt64(out _));
        return node.GetInt64();
    }
    private static int I(JsonElement value, string name) { var n = N(value, name); Need(n >= int.MinValue && n <= int.MaxValue); return (int)n; }
    private static bool B(JsonElement value, string name)
    {
        var node = value.GetProperty(name); Need(node.ValueKind is JsonValueKind.True or JsonValueKind.False); return node.GetBoolean();
    }
    private static bool Null(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Null;
    private static string? NullableText(JsonElement value, string name) => Null(value, name) ? null : S(value, name);
    private static int? NullableInt(JsonElement value, string name) => Null(value, name) ? null : I(value, name);
    private static string? Hash(JsonElement value, string name)
    {
        var hash = NullableText(value, name); Need(hash is null || Digest(hash)); return hash?.ToLowerInvariant();
    }
    private static ulong U(JsonElement value, string name)
    {
        var node = value.GetProperty(name); Need(node.ValueKind == JsonValueKind.Number && node.TryGetUInt64(out _)); return node.GetUInt64();
    }
    private static Mt5NativeTimestamp? Stamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        Keys(value, "raw display");
        var raw = N(value, "raw"); var display = S(value, "display");
        Need(raw > 0 && DateTime.TryParseExact(display, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _), "invalid_native_timestamp");
        var native = DateTime.ParseExact(display, "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None);
        // Unspecified coordinate arithmetic only. No UTC instant or timezone is inferred.
        Need(native.Kind == DateTimeKind.Unspecified && (native.Ticks - new DateTime(1970, 1, 1).Ticks) / TimeSpan.TicksPerSecond == raw,
            "native_timestamp_mismatch");
        return new(raw, display);
    }
    internal static Mt5CollectorMetadata Metadata(JsonElement r, string file)
    {
        Keys(r, "schema version diagnostic_only canonical_ready utc_conversion_authenticated sequence run_id poll_sequence event condition timer_seconds retry_every_polls sample_bars source trigger_m1_head_native previous_committed_m1_head_native captured_m1_head_native m1_usable sample_complete source_stable previous_record previous_record_sha256 probe_file probe_sha256 shutdown_reason sequence_basis");
        Need(S(r, "schema") == "moneyway.mt5-read-only-collector" && I(r, "version") == 1, "unsupported_record_schema");
        Need(B(r, "diagnostic_only") && !B(r, "canonical_ready") && !B(r, "utc_conversion_authenticated"), "unapproved_readiness_or_utc_assertion");
        var seq = N(r, "sequence"); var run = S(r, "run_id"); var poll = N(r, "poll_sequence");
        Need(seq is > 0 and <= 999999999999 && file == RecordFile(seq), "sequence_filename_mismatch");
        Need(Regex.IsMatch(run, "^run-[0-9]{12}\\z", RegexOptions.CultureInvariant), "invalid_run_identity");
        var runStart = long.Parse(run.AsSpan(4), CultureInfo.InvariantCulture);
        Need(runStart > 0 && runStart <= seq && poll > 0, "invalid_run_or_poll");
        var timer = I(r, "timer_seconds"); var retry = I(r, "retry_every_polls"); var sample = I(r, "sample_bars");
        Need(timer is >= 1 and <= 60 && retry is >= 1 and <= 120 && sample is >= 2 and <= 128, "invalid_technical_settings");
        var source = r.GetProperty("source"); Keys(source, "company server demo symbol");
        Need(S(source, "company") == Company && S(source, "server") == Server && B(source, "demo") && S(source, "symbol") == "US100", "source_identity");
        Need(S(r, "sequence_basis") == "local_single_writer_publication_not_utc", "sequence_time_basis");
        var ev = S(r, "event"); var condition = I(r, "condition");
        Need(new[] { "initial_snapshot", "new_native_m1", "source_recovery", "source_unavailable", "native_head_regressed", "incomplete_snapshot_retry", "shutdown" }.Contains(ev), "unknown_event");
        Need(condition is >= 0 and <= 4, "unknown_condition");
        var trigger = Stamp(r.GetProperty("trigger_m1_head_native"));
        var previousHead = Stamp(r.GetProperty("previous_committed_m1_head_native"));
        var captured = Stamp(r.GetProperty("captured_m1_head_native"));
        var usable = B(r, "m1_usable"); var complete = B(r, "sample_complete"); var stable = B(r, "source_stable");
        if (ev == "new_native_m1") Need(condition == 0 && trigger is not null && previousHead is not null && trigger.Raw > previousHead.Raw, "false_new_m1");
        var previous = NullableText(r, "previous_record"); var previousHash = Hash(r, "previous_record_sha256");
        Need((previous is null) == (previousHash is null), "predecessor_reference_pair");
        if (previous is not null) Need(RecordName(previous) && long.Parse(previous.AsSpan(7, 12), CultureInfo.InvariantCulture) < seq, "unsafe_predecessor_reference");
        var probe = NullableText(r, "probe_file"); var probeHash = Hash(r, "probe_sha256"); var reason = NullableInt(r, "shutdown_reason");
        if (ev == "shutdown") Need(probe is null && probeHash is null && reason.HasValue && !usable && !complete && !stable && captured is null, "fabricated_shutdown_acquisition");
        else Need(probe == $"probe-{seq:D12}.json" && probeHash is not null && reason is null, "unsafe_probe_reference");
        return new(seq, run, poll, ev, condition, timer, retry, sample, trigger, previousHead, captured,
            usable, complete, stable, previous, previousHash, probe, probeHash, reason);
    }

    private static Mt5NativeSeriesValue Series(JsonElement value)
    {
        Keys(value, "value error_code"); var error = I(value, "error_code"); Need(error >= 0);
        var n = Null(value, "value") ? (long?)null : N(value, "value"); return new(n, error);
    }
    internal static Mt5WireProbe Probe(JsonElement r, Mt5CollectorMetadata metadata)
    {
        Keys(r, "schema version diagnostic_only canonical_ready generated_at_utc generation_time_basis generated_gmt_candidate utc_conversion_authenticated feed_equivalence_verified source gate symbol server_quote_before timeframes server_quote_after connected_after limitations");
        Need(S(r, "schema") == "moneyway.mt5-read-only-probe" && I(r, "version") == 1, "unsupported_probe_schema");
        Need(B(r, "diagnostic_only") && !B(r, "canonical_ready") && !B(r, "utc_conversion_authenticated") && !B(r, "feed_equivalence_verified") && Null(r, "generated_at_utc"), "unapproved_readiness_or_utc_assertion");
        Need(S(r, "generation_time_basis") == "TimeGMT_computer_derived_not_authenticated", "generation_time_basis");
        var source = r.GetProperty("source"); Keys(source, "platform server company demo_verified mode_error_code connected_before terminal_build");
        Need(S(source, "platform") == "MetaTrader 5" && S(source, "company") == Company && S(source, "server") == Server && B(source, "demo_verified"), "source_identity");
        var build = I(source, "terminal_build"); var connected = B(source, "connected_before"); Need(build >= 0 && I(source, "mode_error_code") == 0, "source_mode_unverified");
        var symbol = r.GetProperty("symbol"); Keys(symbol, "native query_attempted exists custom selected error_code description description_error_code profit_currency profit_currency_error_code digits digits_error_code");
        Need(S(symbol, "native") == "US100" && !B(symbol, "custom"), "symbol_identity");
        _ = B(symbol, "query_attempted"); _ = NullableText(symbol, "description"); _ = NullableText(symbol, "profit_currency"); _ = NullableInt(symbol, "digits");
        foreach (var key in new[] { "error_code", "description_error_code", "profit_currency_error_code", "digits_error_code" }) Need(NullableInt(symbol, key) is null or >= 0);
        var gate = S(r, "gate");
        Need(new[] { "passed", "tester_rejected", "terminal_disconnected", "demo_unverified", "server_mismatch", "sample_bound_rejected" }.Contains(gate), "unknown_probe_gate");
        if (gate == "passed") Need(connected, "probe_gate_inconsistent");
        var data = r.GetProperty("timeframes"); Need(data.ValueKind == JsonValueKind.Array && data.GetArrayLength() == 4, "missing_or_duplicate_timeframe");
        var frames = new List<Mt5NativeFrame>();
        foreach (var f in data.EnumerateArray())
        {
            Keys(f, "timeframe period_seconds requested_count status copy_return copy_error_code bars", "synchronized_before synchronized_after last_bar_before last_bar_after terminal_bar_count terminal_first_date server_first_date copy_started_elapsed_us copy_finished_elapsed_us head_consistent");
            var name = S(f, "timeframe"); var index = Array.IndexOf(FrameNames, name);
            Need(index >= 0 && frames.All(x => x.Timeframe != name) && I(f, "period_seconds") == Periods[index], "missing_or_duplicate_timeframe");
            var requested = I(f, "requested_count"); Need(requested == metadata.SampleBars, "sample_count_mismatch");
            var barsJson = f.GetProperty("bars"); Need(barsJson.ValueKind == JsonValueKind.Array && barsJson.GetArrayLength() <= 128, "bar_count_bound");
            var status = S(f, "status");
            if (status == "not_attempted")
            {
                Need(Null(f, "copy_return") && Null(f, "copy_error_code") && barsJson.GetArrayLength() == 0, "fabricated_unattempted_sample");
                frames.Add(new(name, Periods[index], requested, status, null, null, null, null, null, null, null, null, null, null, null, null, Array.Empty<Mt5NativeBar>())); continue;
            }
            var copy = I(f, "copy_return"); var error = I(f, "copy_error_code");
            Need(gate == "passed" && B(symbol, "exists") && B(symbol, "selected"), "probe_gate_inconsistent");
            Need(error >= 0 && copy >= -1 && copy <= requested && barsJson.GetArrayLength() == Math.Max(0, copy), "acquisition_count_mismatch");
            Need(status == (copy < 0 ? "copy_failed" : copy == 0 ? "empty" : copy < requested ? "partial_sample" : "sample_returned"), "acquisition_status_mismatch");
            var syncBefore = Series(f.GetProperty("synchronized_before")); var syncAfter = Series(f.GetProperty("synchronized_after"));
            Need(syncBefore.Value is null or 0 or 1 && syncAfter.Value is null or 0 or 1);
            var before = Series(f.GetProperty("last_bar_before")); var after = Series(f.GetProperty("last_bar_after"));
            var count = Series(f.GetProperty("terminal_bar_count")); var first = Series(f.GetProperty("terminal_first_date")); var serverFirst = Series(f.GetProperty("server_first_date"));
            var start = U(f, "copy_started_elapsed_us"); var end = U(f, "copy_finished_elapsed_us"); Need(end >= start);
            var consistent = B(f, "head_consistent"); var bars = new List<Mt5NativeBar>();
            foreach (var b in barsJson.EnumerateArray())
            {
                Keys(b, "series_position open_time_native open high low close ohlc_valid role newer_than_next_seconds open_time_utc provider_confirmed_closed historical_available_at_utc");
                var position = I(b, "series_position"); Need(position == bars.Count, "bar_position");
                var at = Stamp(b.GetProperty("open_time_native")); Need(at is not null, "missing_native_timestamp");
                if (position > 0) Need(bars[^1].OpenTimeNative.Raw > at!.Raw && bars[^1].NewerThanNextSeconds == bars[^1].OpenTimeNative.Raw - at.Raw, "duplicate_or_unordered_native_identity");
                Need(Null(b, "open_time_utc") && Null(b, "provider_confirmed_closed") && Null(b, "historical_available_at_utc"), "unapproved_bar_time_assertion");
                var open = Price(b.GetProperty("open")); var high = Price(b.GetProperty("high")); var low = Price(b.GetProperty("low")); var close = Price(b.GetProperty("close"));
                var valid = open.HasValue && high.HasValue && low.HasValue && close.HasValue && high.Value.CompareTo(open.Value) >= 0 && high.Value.CompareTo(close.Value) >= 0 && high.Value.CompareTo(low.Value) >= 0 && low.Value.CompareTo(open.Value) <= 0 && low.Value.CompareTo(close.Value) <= 0;
                Need(B(b, "ohlc_valid") == valid, "ohlc_flag_mismatch");
                var role = S(b, "role");
                if (consistent) Need(before.Value > 0 && before.Value == after.Value && (position > 0 || after.Value == at!.Raw), "inconsistent_native_head");
                var expectedRole = !consistent ? "unverified_head" : position == 0 ? "current_bar_completion_unverified" : at!.Raw < after.Value ? "historical_preceding_bar" : "unverified_head";
                Need(role == expectedRole, "bar_role_mismatch");
                var delta = Null(b, "newer_than_next_seconds") ? (long?)null : N(b, "newer_than_next_seconds");
                if (position == barsJson.GetArrayLength() - 1) Need(delta is null, "native_delta_mismatch");
                bars.Add(new(position, at!, role, b.GetProperty("open").GetRawText(), b.GetProperty("high").GetRawText(), b.GetProperty("low").GetRawText(), b.GetProperty("close").GetRawText(), valid, delta));
            }
            frames.Add(new(name, Periods[index], requested, status, copy, error, consistent, syncBefore, syncAfter, before, after, count, first, serverFirst, start, end, bars.AsReadOnly()));
        }
        frames.Sort((a, b) => Array.IndexOf(FrameNames, a.Timeframe).CompareTo(Array.IndexOf(FrameNames, b.Timeframe)));
        var m1 = frames[0];
        Need(metadata.CapturedM1HeadNative == m1.Bars.FirstOrDefault()?.OpenTimeNative, "captured_m1_mismatch");
        bool Usable(Mt5NativeFrame f) => f.CopyReturn > 0 && f.CopyErrorCode == 0 && f.HeadConsistent == true && f.SynchronizedAfter?.Value == 1 && f.Bars.All(b => b.OhlcValid);
        Need(!metadata.M1Usable || Usable(m1), "unusable_m1_assertion");
        Need(!metadata.SampleComplete || metadata.M1Usable && frames.All(f => Usable(f) && f.CopyReturn == metadata.SampleBars), "incomplete_sample_assertion");
        var limitations = r.GetProperty("limitations"); Need(limitations.ValueKind == JsonValueKind.Array && limitations.GetArrayLength() <= 32);
        foreach (var item in limitations.EnumerateArray()) _ = Text(item); // Never echo arbitrary source text.
        return new(build, gate, connected, B(r, "connected_after"), Stamp(r.GetProperty("generated_gmt_candidate")),
            Stamp(r.GetProperty("server_quote_before")), Stamp(r.GetProperty("server_quote_after")), frames.AsReadOnly());
    }

    private static ExactPrice? Price(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        Need(value.ValueKind == JsonValueKind.Number, "invalid_ohlc_type");
        var literal = value.GetRawText(); Need(literal.Length <= 384, "numeric_representation_bound");
        var match = Regex.Match(literal, "^(?<s>-?)(?<i>[0-9]+)(?:\\.(?<f>[0-9]+))?(?:[eE](?<e>[+-]?[0-9]+))?$");
        Need(match.Success);
        var digits = (match.Groups["i"].Value + match.Groups["f"].Value).TrimStart('0');
        if (digits.Length == 0) return new(1, "0", BigInteger.Zero);
        var exponent = match.Groups["e"].Success ? BigInteger.Parse(match.Groups["e"].Value, CultureInfo.InvariantCulture) : BigInteger.Zero;
        exponent -= match.Groups["f"].Length;
        var trimmed = digits.TrimEnd('0'); exponent += digits.Length - trimmed.Length;
        return new(match.Groups["s"].Value == "-" ? -1 : 1, trimmed, exponent);
    }
    private readonly record struct ExactPrice(int Sign, string Digits, BigInteger Exponent) : IComparable<ExactPrice>
    {
        public int CompareTo(ExactPrice other)
        {
            if (Digits == "0" && other.Digits == "0") return 0;
            if (Digits == "0") return -other.Sign;
            if (other.Digits == "0") return Sign;
            if (Sign != other.Sign) return Sign;
            var magnitude = (Exponent + Digits.Length).CompareTo(other.Exponent + other.Digits.Length);
            if (magnitude != 0) return Sign * magnitude;
            var length = Math.Max(Digits.Length, other.Digits.Length);
            return Sign * Math.Sign(string.CompareOrdinal(Digits.PadRight(length, '0'), other.Digits.PadRight(length, '0')));
        }
    }
}

internal sealed class Mt5WireException(string code) : Exception(code) { internal string Code { get; } = code; }
internal sealed record Mt5WireProbe(int Build, string Gate, bool ConnectedBefore, bool ConnectedAfter,
    Mt5NativeTimestamp? Generated, Mt5NativeTimestamp? QuoteBefore, Mt5NativeTimestamp? QuoteAfter, IReadOnlyList<Mt5NativeFrame> Frames);
