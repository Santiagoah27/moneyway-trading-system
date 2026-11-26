using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.MarketData.Receipt;
using MoneyWay.Infrastructure.MarketData.Mt5;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

public sealed record NasdaqReceiptCutRegistration(string Id, string File, long Sequence, string Sha256, string? StandardSessionKey = null);
public sealed record NasdaqReceiptCutReportView(string Id, string ReportSha256, Mt5ReceiptCutReport Diagnostic)
{
    public string ProviderId => "oanda-global-markets-mt5-demo";
}

/// <summary>Read a registered Worker report, independently of canonical or supervised attempt inputs.</summary>
public sealed class ReadNasdaqReceiptCutReport
{
    private const int MaxBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 20,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public async Task<NasdaqReceiptCutReportView> ReadAsync(string root, NasdaqReceiptCutRegistration registration, CancellationToken cancellationToken)
    {
        var path = ReadNasdaqHistoricalEvidencePackages.ResolveArtifactPath(root, registration.File);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length is <= 0 or > MaxBytes) throw new InvalidDataException("Report size is outside the read limit.");
        var bytes = new byte[stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (stream.ReadByte() != -1) throw new InvalidDataException("Unexpected bytes after the pinned report.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!hash.Equals(registration.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Report hash mismatch.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 20, AllowDuplicateProperties = false });
        var payload = document.RootElement.GetProperty("audit");
        if (payload.GetProperty("schema").GetString() != "moneyway.nasdaq-prospective-receipt-cut-diagnostic"
            || payload.GetProperty("version").GetInt32() != 1
            || payload.GetProperty("standard_evaluation").GetString() != "NotEvaluated"
            || payload.GetProperty("standard_evaluation_reason").GetString() != "NoAdmissibleClosedUtcSeriesOrVerifiedTemporalBasisForStandard"
            || payload.GetProperty("native_to_utc_mapping").GetString() != "Unresolved"
            || payload.GetProperty("provider_finalization").GetString() != "Unresolved"
            || payload.GetProperty("market_availability_utc").GetString() != "Unresolved"
            || payload.GetProperty("human_review_status").GetString() != "NoVerifiedReviewBoundToReceiptCut"
            || payload.GetProperty("canonical_ready").ValueKind != JsonValueKind.False
            || payload.GetProperty("canonical_admission_permitted").ValueKind != JsonValueKind.False
            || payload.GetProperty("operational_signal").ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Unexpected receipt-cut contract.");
        var report = payload.Deserialize<Mt5ReceiptCutReport>(Json);
        if (report is null || report.ExpectedSource != Mt5ReceiptCutDiagnostic.SupportedSource
            || report.Boundary?.Sequence != registration.Sequence || report.Integrity != Mt5ReceiptStatus.Validated
            || report.CutReceipt is not { } cut || cut.Sequence != report.Boundary.Sequence
            || cut.FileName != report.Boundary.RecordFile || cut.Sha256 != report.Boundary.RecordSha256
            || !IsDigest(report.DirectoryScope) || !IsDigest(report.PrefixManifestSha256)
            || report.Anchor is { } anchor && (anchor.Sequence <= 0 || anchor.Sequence >= cut.Sequence)
            || report.Samples is null || report.Exclusions is null || report.NativeGaps is null || report.Timeframes is null
            || report.Notices is null || report.Diagnostics is null || report.ExcludedHumanReviews is null
            || report.Samples.Any(s => s is null || s.Timeframe is not ("H4" or "H1" or "M5" or "M1")
                || s.OpenTimeNative is null || s.FirstUsableHistorical is not { SourcePosition: > 0, Usable: true, Values: not null, Receipt: { } receipt }
                || receipt.Sequence <= (report.Anchor?.Sequence ?? 0) || receipt.Sequence > cut.Sequence
                || receipt.RecordFile != $"record-{receipt.Sequence:D12}.json" || receipt.ProbeFile != $"probe-{receipt.Sequence:D12}.json"
                || !IsDigest(receipt.RecordSha256) || !IsDigest(receipt.ProbeSha256))
            || report.Samples.DistinctBy(s => (s.Timeframe, s.OpenTimeNative.Raw)).Count() != report.Samples.Count)
            throw new InvalidDataException("Unexpected receipt-cut identity or sample lineage.");
        return new(registration.Id, hash, report);
    }

    public static bool IsDigest(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
