using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Infrastructure.MarketData.Csv;

namespace MoneyWay.Worker;

internal sealed record NasdaqEvidenceSourceRequest(string Format, string ManifestPath, string ManifestSha256,
    string? SourceRoot, NasdaqEvidenceReceipt? LocalAcquisition);
internal sealed record NasdaqEvidenceSource(string Timeframe, string OriginalPath, string OriginalSha256,
    string CanonicalPath, string CanonicalSha256, string EvidenceManifestSha256, string TimestampEvidence,
    DateTimeOffset? LocalImportedAtUtc, NasdaqEvidenceReceipt? LocalAcquisition, string? MappingSha256,
    string? MappingEvidenceSha256, int CandleCount,
    DateTimeOffset FirstOpenUtc, DateTimeOffset LastNominalCloseUtc);
internal sealed record NasdaqEvidenceLoaded(IReadOnlyList<NasdaqEvidenceSource> Sources,
    IReadOnlyDictionary<string, CandleSeries> Series,
    IReadOnlyDictionary<string, int> IdenticalDuplicateCounts);

internal static class NasdaqEvidenceSources
{
    internal static readonly MarketDataProviderId Provider = new("oanda-global-markets-mt5-demo");
    internal static readonly MarketSymbol Instrument = new("US100");
    private const string JuneId = "nasdaq-standard-2026-06-29-documentary";
    private const string JuneManifestSha256 = "12684556c3a095ead00d1138b41902b9aba1844cf315383ec5ed87c966665155";
    private const string NativeFormat = "yyyy.MM.dd HH:mm:ss";

    internal static NasdaqEvidenceLoaded Load(IReadOnlyList<NasdaqEvidenceSourceRequest> requests, NasdaqEvidenceReceipt? sharedAcquisition)
    {
        var sources = new List<(NasdaqEvidenceSource Evidence, CandleSeries Candles)>();
        foreach (var request in requests.OrderBy(s => s.ManifestSha256, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(ReadPinned(request.ManifestPath, request.ManifestSha256));
            var manifest = document.RootElement;
            var directory = Path.GetDirectoryName(request.ManifestPath)!;
            switch (request.Format)
            {
                case "OriginalImport":
                    var tf = Value(manifest, "originalTimeframe");
                    if (manifest.GetProperty("schemaVersion").GetInt32() != 1 ||
                        Value(manifest, "sourceKind") != $"OriginalMt5{tf}Documentary" || manifest.GetProperty("derived").GetBoolean() ||
                        Value(manifest, "providerId") != Provider.Value || Value(manifest, "symbol") != Instrument.Value ||
                        manifest.GetProperty("authenticatedHistoricalAvailability").GetBoolean())
                        throw new ArgumentException("Original import identity, provenance or availability mismatch.");
                    var rawHash = Value(manifest, "sourceSha256");
                    var mapHash = Value(manifest, "mappingSha256");
                    var proofHash = Value(manifest, "mappingEvidenceSha256");
                    _ = ReadPinned(Child(directory, Value(manifest, "mappingEvidenceFile")), proofHash);
                    using (var map = JsonDocument.Parse(ReadPinned(Child(directory, Value(manifest, "mappingFile")), mapHash)))
                    {
                        var mapping = map.RootElement;
                        if (mapping.GetProperty("schemaVersion").GetInt32() != 1 ||
                            Value(mapping, "providerId") != Provider.Value || Value(mapping, "symbol") != Instrument.Value ||
                            Value(mapping, "timeframe") != tf || Value(mapping, "sourceSha256") != rawHash ||
                            Value(mapping, "evidenceSha256") != proofHash || Value(mapping, "mappingStatus") != "DocumentaryAudited")
                            throw new ArgumentException("Timestamp mapping evidence does not match the source identity and hashes.");
                        var imported = ParseUtc(Value(manifest, "importedAtUtc"));
                        Add(tf, Child(directory, Value(manifest, "sourceFile")), rawHash,
                            Child(directory, Value(manifest, "normalizedFile")), Value(manifest, "normalizedSha256"),
                            "HashBoundDocumentaryAudited", imported, mapHash, proofHash, Mapping(mapping));
                    }
                    break;
                case "JuneDocumentary":
                    if (request.ManifestSha256 != JuneManifestSha256 || Value(manifest, "packageId") != JuneId ||
                        manifest.GetProperty("packageVersion").GetInt32() != 1 ||
                        Value(manifest, "providerId") != Provider.Value || Value(manifest, "originalSymbol") != Instrument.Value)
                        throw new ArgumentException("The documentary June registration must refer to the registered package.");
                    var artifacts = manifest.GetProperty("artifacts").EnumerateArray().ToDictionary(a => Value(a, "id"), StringComparer.Ordinal);
                    var root = request.SourceRoot ?? throw new ArgumentException("June source root required.");
                    var normalization = artifacts["normalization"];
                    _ = ReadPinned(Child(root, Value(normalization, "relativePath")), Value(normalization, "sha256"));
                    foreach (var entry in NasdaqEvidenceCoverage.Timeframes)
                    {
                        var name = entry.Key[1..] + entry.Key[0];
                        var raw = artifacts["raw-" + name];
                        var normalized = artifacts["normalized-" + name];
                        Add(entry.Key, Child(root, Value(raw, "relativePath")), Value(raw, "sha256"),
                            Child(root, Value(normalized, "relativePath")), Value(normalized, "sha256"),
                            "RegisteredJuneDocumentaryMapping", null, null, Value(normalization, "sha256"),
                            native => ConvertNative(native, 180));
                    }
                    break;
                case "SeptemberMentorInventory":
                    if (Value(manifest, "SessionDate") != "2026-09-08" || Value(manifest, "Provider") != Provider.Value ||
                        Value(manifest, "RawSymbol") != Instrument.Value)
                        throw new ArgumentException("Only the retained September mentor inventory is supported.");
                    var septemberRoot = request.SourceRoot ?? throw new ArgumentException("September source root required.");
                    foreach (var file in manifest.GetProperty("Files").EnumerateArray())
                    {
                        var name = Value(file, "Timeframe");
                        Add(name[1..] + name[0], Child(septemberRoot, Path.GetRelativePath(septemberRoot, Value(file, "RawPath"))),
                            Value(file, "RawSHA256"), Child(septemberRoot, Path.GetRelativePath(septemberRoot, Value(file, "NormalizedPath"))),
                            Value(file, "NormalizedSHA256"), "SeptemberSessionAnchorsOnly_FullIntervalUnresolved", null, null, null, null);
                    }
                    break;
                default:
                    throw new ArgumentException("Unknown source registration format.");
            }

            void Add(string timeframe, string path, string digest, string normalizedPath, string normalizedDigest,
                string mappingStatus, DateTimeOffset? imported, string? mappingDigest, string? proofDigest,
                Func<string, DateTimeOffset>? mapper)
            {
                var raw = ReadPinned(path, digest);
                var normalized = ReadPinned(normalizedPath, normalizedDigest);
                using var reader = new StringReader(new UTF8Encoding(false, true).GetString(normalized));
                if (!NasdaqEvidenceCoverage.Timeframes.TryGetValue(timeframe, out var definition))
                    throw new ArgumentException($"Unsupported original timeframe {timeframe}.");
                var candles = new CsvMarketDataImporter().Import(reader);
                if (candles.ProviderId != Provider || candles.Symbol != Instrument || candles.Timeframe != definition ||
                    candles.Candles.Any(c => c.CloseTimeUtc - c.OpenTimeUtc != NasdaqEvidenceCoverage.Duration(definition) ||
                        timeframe != "H4" && c.OpenTimeUtc.Ticks % NasdaqEvidenceCoverage.Duration(definition).Ticks != 0 ||
                        imported is { } t && c.CloseTimeUtc > t) || imported is { Offset: var offset } && offset != TimeSpan.Zero)
                    throw new ArgumentException($"Invalid identity, interval or nominal close for {timeframe}.");
                if (mapper is not null) VerifyNative(raw, candles, mapper);
                sources.Add((new(timeframe, path, digest, normalizedPath, normalizedDigest, request.ManifestSha256,
                    mappingStatus, imported, imported is null ? null : request.LocalAcquisition ?? sharedAcquisition,
                    mappingDigest, proofDigest, candles.Count, candles.StartTimeUtc!.Value,
                    candles.EndTimeUtc!.Value), candles));
            }
        }

        var series = new Dictionary<string, CandleSeries>(StringComparer.Ordinal);
        var duplicates = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in NasdaqEvidenceCoverage.Timeframes)
        {
            var rows = new SortedDictionary<DateTimeOffset, Candle>();
            var inputs = sources.Where(s => s.Evidence.Timeframe == entry.Key).ToArray();
            var repeats = 0;
            foreach (var source in inputs)
                foreach (var row in source.Candles.Candles)
                {
                    if (rows.TryAdd(row.OpenTimeUtc, row)) continue;
                    if (!Same(rows[row.OpenTimeUtc], row))
                        throw new ArgumentException($"Conflicting {entry.Key} source observations at {row.OpenTimeUtc:O}.");
                    repeats++;
                }
            series.Add(entry.Key, new(Provider, Instrument, entry.Value, rows.Values));
            duplicates.Add(entry.Key, repeats);
        }
        return new(sources.Select(s => s.Evidence).ToArray(), series, duplicates);
    }

    private static Func<string, DateTimeOffset> Mapping(JsonElement mapping)
    {
        var hasRows = mapping.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0;
        var hasSegments = mapping.TryGetProperty("segments", out var segments) && segments.GetArrayLength() > 0;
        if (hasRows == hasSegments)
            throw new ArgumentException("An audited timestamp map must contain only rows or segments.");
        if (hasRows)
        {
            var points = rows.EnumerateArray().ToDictionary(r => Value(r, "nativeOpenTime"), r => ParseUtc(Value(r, "openUtc")));
            return native => points.TryGetValue(native, out var at)
                ? at : throw new ArgumentException($"Unresolved native UTC mapping: {native}.");
        }
        var bounds = segments.EnumerateArray().Select(s =>
            (Start: Value(s, "nativeStartInclusive"), End: Value(s, "nativeEndExclusive"),
             Offset: s.GetProperty("serverUtcOffsetMinutes").GetInt32())).OrderBy(s => s.Start, StringComparer.Ordinal).ToArray();
        if (bounds.Any(s => s.Offset is < -840 or > 840 ||
                DateTime.ParseExact(s.Start, NativeFormat, CultureInfo.InvariantCulture) >=
                DateTime.ParseExact(s.End, NativeFormat, CultureInfo.InvariantCulture)) ||
            bounds.Zip(bounds.Skip(1)).Any(pair => string.CompareOrdinal(pair.First.End, pair.Second.Start) > 0))
            throw new ArgumentException("Invalid or overlapping timestamp mapping segments.");
        return native => bounds.Where(s => string.CompareOrdinal(s.Start, native) <= 0 && string.CompareOrdinal(native, s.End) < 0)
            .Select(s => ConvertNative(native, s.Offset)).SingleOrDefault() is { } at && at != default
                ? at : throw new ArgumentException($"Unresolved native UTC mapping: {native}.");
    }

    private static DateTimeOffset ConvertNative(string native, int offset) =>
        new DateTimeOffset(DateTime.ParseExact(native, NativeFormat, CultureInfo.InvariantCulture), TimeSpan.Zero).AddMinutes(-offset);

    internal static DateTimeOffset ParseUtc(string? value)
    {
        if (value is null || !(value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal)) ||
            !DateTimeOffset.TryParseExact(value, ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) || timestamp.Offset != TimeSpan.Zero)
            throw new FormatException("An explicit UTC timestamp ending in Z or +00:00 is required.");
        return timestamp;
    }

    private static void VerifyNative(byte[] data, CandleSeries normalized, Func<string, DateTimeOffset> map)
    {
        using var reader = new StringReader(new UTF8Encoding(false, true).GetString(data).TrimStart('\uFEFF'));
        var header = reader.ReadLine()?.Split('\t') ?? [];
        var columns = header.Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index);
        var byOpen = normalized.Candles.ToDictionary(c => c.OpenTimeUtc);
        var observed = new HashSet<DateTimeOffset>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var fields = line.Split('\t');
            if (fields.Length != header.Length) throw new ArgumentException("Invalid native MT5 TSV row.");
            string Field(string name) => fields[columns[name]];
            decimal Number(string name) => decimal.Parse(Field(name), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture);
            var at = map(Field("<DATE>") + " " + Field("<TIME>"));
            if (!byOpen.TryGetValue(at, out var candle) || candle.Open != Number("<OPEN>") || candle.High != Number("<HIGH>") ||
                candle.Low != Number("<LOW>") || candle.Close != Number("<CLOSE>") || candle.Volume != Number("<TICKVOL>"))
                throw new ArgumentException($"Native and canonical evidence disagree at {at:O}.");
            if (!observed.Add(at)) throw new ArgumentException($"Duplicate native observation at {at:O}.");
        }
        if (observed.Count != normalized.Count) throw new ArgumentException("Native and canonical evidence coverage differs.");
    }

    private static bool Same(Candle a, Candle b) => a.CloseTimeUtc == b.CloseTimeUtc && a.Open == b.Open &&
        a.High == b.High && a.Low == b.Low && a.Close == b.Close && a.Volume == b.Volume;

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static byte[] ReadPinned(string path, string? sha256)
    {
        if (sha256 is not { Length: 64 } || sha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Explicit lowercase SHA256 pin required for each source and evidence file.");
        return Read(path, sha256);
    }

    internal static byte[] Read(string path, string? sha256 = null)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Explicit absolute local file path required.");
        NasdaqSessionLiquidityResearchCommand.Guard(path);
        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > 32 * 1024 * 1024) throw new ArgumentException("Evidence must be nonempty and at most 32 MiB per source.");
        var data = new byte[(int)file.Length];
        file.ReadExactly(data);
        if (sha256 is not null && !string.Equals(Hash(data), sha256, StringComparison.Ordinal))
            throw new ArgumentException($"Evidence SHA256 mismatch: {Path.GetFileName(path)}.");
        return data;
    }

    private static string Child(string root, string name)
    {
        if (!Path.IsPathFullyQualified(root) || Path.IsPathRooted(name)) throw new ArgumentException("Explicit source root and contained relative path required.");
        var path = Path.GetFullPath(Path.Combine(root, name));
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Evidence path escapes its source root.");
        return path;
    }
    private static string Value(JsonElement value, string field) => value.GetProperty(field).GetString()
        ?? throw new ArgumentException("Required evidence field missing: " + field);
}
