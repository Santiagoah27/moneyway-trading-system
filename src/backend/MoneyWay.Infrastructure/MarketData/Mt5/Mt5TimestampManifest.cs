using System.Security.Cryptography;
using System.Text.Json;
using MoneyWay.Application.MarketData.Receipt;

namespace MoneyWay.Infrastructure.MarketData.Mt5;

/// <summary>Versioned fixed-property-order UTF8 JSON. No paths, OHLC or account data; only its digest is transmitted.</summary>
public static class Mt5TimestampManifest
{
    public static byte[] Create(ValidatedMt5ReceiptPrefix prefix, string recordFile, string recordSha256, string probeSha256)
    {
        var r = prefix.Records.SingleOrDefault(r => r.FileName == recordFile)
            ?? throw new ArgumentException("Selected receipt is not in the validated prefix.");
        var a = r.Observation!; var m = a.Metadata;
        if (a.RecordSha256 != recordSha256 || m.ProbeSha256 != probeSha256 || m.ProbeFile is null)
            throw new ArgumentException("Selected record/probe hash mismatch or missing probe.");
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("schema", "moneyway.mt5-receipt-pair-timestamp-subject"); json.WriteNumber("version", 1);
            json.WriteString("company", a.Company); json.WriteString("server", a.Server);
            json.WriteString("native_symbol", a.NativeSymbol); json.WriteBoolean("demo", a.Demo);
            json.WriteString("directory_scope_digest", prefix.DirectoryScope);
            json.WriteNumber("through_sequence", prefix.Boundary.Sequence);
            json.WriteString("terminal_record_sha256", prefix.Boundary.RecordSha256);
            json.WriteString("prefix_manifest_sha256", prefix.EvidenceManifestSha256);
            json.WriteString("record_file", a.RecordFile); json.WriteString("record_sha256", a.RecordSha256);
            json.WriteNumber("sequence", m.Sequence); json.WriteString("run_id", m.RunId); json.WriteNumber("poll_sequence", m.PollSequence);
            json.WriteString("probe_file", m.ProbeFile); json.WriteString("probe_sha256", m.ProbeSha256);
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
