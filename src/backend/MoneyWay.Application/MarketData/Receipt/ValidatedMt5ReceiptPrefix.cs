using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MoneyWay.Application.MarketData.Receipt;

/// <summary>A directory-scoped publication boundary. Zero explicitly selects the empty prefix.</summary>
public sealed record Mt5ReceiptPrefixBoundary
{
    public Mt5ReceiptPrefixBoundary(long sequence, string? recordSha256 = null)
    {
        if (sequence is < 0 or > 999999999999 || (sequence == 0 ? recordSha256 is not null
            : recordSha256 is null || !Regex.IsMatch(recordSha256, "^[0-9a-fA-F]{64}\\z")))
            throw new ArgumentException("A nonempty boundary requires its exact record SHA256.");
        Sequence = sequence;
        RecordSha256 = recordSha256?.ToLowerInvariant();
    }

    public long Sequence { get; }
    public string? RecordSha256 { get; }
    public string? RecordFile => Sequence == 0 ? null : $"record-{Sequence:D12}.json";
}

public interface IMt5ReceiptPrefixReader
{
    Mt5ReceiptBatch ReadPrefix(string sourceDirectory, Mt5ReceiptPrefixBoundary boundary,
        IReadOnlyDictionary<string, string>? expectedRecordHashes = null);
}

public sealed record Mt5ReceiptPrefixSelection(Mt5ReceiptStatus Status, ValidatedMt5ReceiptPrefix? Prefix,
    int InspectedRecords, IReadOnlyList<string> Diagnostics);

/// <summary>Immutable observations from one validated, anchored predecessor chain through a pinned boundary.</summary>
public sealed class ValidatedMt5ReceiptPrefix
{
    private ValidatedMt5ReceiptPrefix(Mt5ReceiptBatch batch, Mt5ReceiptPrefixBoundary boundary)
    {
        DirectoryScope = batch.DirectoryScope;
        Boundary = boundary;
        Records = Array.AsReadOnly(batch.Records.OrderBy(r => r.Observation!.Metadata.Sequence).Select(r => r with
        {
            Diagnostics = Array.AsReadOnly(r.Diagnostics.ToArray()),
            Observation = r.Observation! with
            {
                Timeframes = Array.AsReadOnly(r.Observation.Timeframes.Select(f => f with
                {
                    Bars = Array.AsReadOnly(f.Bars.ToArray())
                }).ToArray())
            }
        }).ToArray());
        var manifest = string.Join("\n", Records.Select(r =>
            $"{r.FileName}:{r.ActualSha256}:{r.Observation!.Metadata.ProbeFile}:{r.Observation.Metadata.ProbeSha256}"));
        EvidenceManifestSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(manifest)));
    }

    public string DirectoryScope { get; }
    public Mt5ReceiptPrefixBoundary Boundary { get; }
    public IReadOnlyList<Mt5ReceiptRecord> Records { get; }
    public string EvidenceManifestSha256 { get; }
    public Mt5ReceivedRecordIdentity? FinalReceipt => Records.LastOrDefault()?.Observation is { } a
        ? new(a.RecordFile, a.RecordSha256, a.Metadata.Sequence, a.Metadata.RunId, a.Metadata.PollSequence, a.Metadata.Event) : null;

    public static Mt5ReceiptPrefixSelection Select(Mt5ReceiptBatch batch, Mt5ReceiptPrefixBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(boundary);
        var codes = batch.Diagnostics.ToList();
        var records = batch.Records.OrderBy(r => r.FileName, StringComparer.Ordinal).ToArray();
        if (records.Any(r => !r.ReceiptIntegrityValidated || r.Observation?.DirectoryScope != batch.DirectoryScope))
            codes.Add("prefix_contains_unvalidated_receipts");
        if (records.GroupBy(r => r.FileName, StringComparer.Ordinal).Any(g => g.Count() > 1)) codes.Add("duplicate_prefix_receipt");
        if (codes.Count == 0)
        {
            Mt5NativeAcquisition? previous = null;
            var acquisitionIds = new HashSet<(string Run, long Poll)>();
            foreach (var r in records)
            {
                var a = r.Observation!;
                var m = a.Metadata;
                if (m.Sequence > boundary.Sequence || a.RecordFile != $"record-{m.Sequence:D12}.json"
                    || r.FileName != a.RecordFile || r.ActualSha256 != a.RecordSha256)
                    codes.Add("prefix_receipt_identity_mismatch");
                if (previous is null)
                {
                    if (m.Sequence != 1 || m.PreviousRecord is not null) codes.Add("prefix_origin_not_established");
                }
                else if (m.Sequence <= previous.Metadata.Sequence || m.PreviousRecord != previous.RecordFile
                    || m.PreviousRecordSha256 != previous.RecordSha256) codes.Add("prefix_is_not_one_complete_predecessor_chain");
                if (m.Event != "shutdown" && !acquisitionIds.Add((m.RunId, m.PollSequence))) codes.Add("duplicate_prefix_acquisition_identity");
                previous = a;
            }
            if (boundary.Sequence == 0)
            {
                if (records.Length != 0) codes.Add("empty_prefix_has_records");
            }
            else if (previous is null || previous.RecordFile != boundary.RecordFile || previous.RecordSha256 != boundary.RecordSha256)
                codes.Add("pinned_prefix_boundary_unavailable_or_changed");
        }
        if (codes.Count == 0) return new(Mt5ReceiptStatus.Validated, new(batch, boundary), records.Length, Array.Empty<string>());
        var status = batch.Status is Mt5ReceiptStatus.Conflict or Mt5ReceiptStatus.Invalid or Mt5ReceiptStatus.DataUnavailable
            ? batch.Status : Mt5ReceiptStatus.Conflict;
        return new(status, null, records.Length, Array.AsReadOnly(codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    public Mt5ReceiptBatch ToBatch() => new(DirectoryScope, Records, Array.Empty<string>(), new(DirectoryScope,
        Array.Empty<Mt5ReceivedRecordIdentity>()));
}
