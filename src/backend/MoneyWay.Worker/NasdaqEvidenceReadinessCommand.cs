using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Research.Nasdaq;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.Worker;

internal sealed record NasdaqEvidenceFile(string Path, string Sha256);
internal sealed record NasdaqEvidenceReceipt(DateTimeOffset AcquiredAtUtc, NasdaqEvidenceFile Evidence);
internal sealed record NasdaqEvidenceClosureRequest(string Timeframe, DateTimeOffset StartUtc, DateTimeOffset EndUtc, NasdaqEvidenceFile Evidence);
internal sealed record NasdaqEvidenceRequest(string DatasetId, string ProviderId, string Symbol, DateTimeOffset RegisteredAtUtc,
    string Provenance, IReadOnlyList<NasdaqEvidenceSourceRequest> Sources, IReadOnlyList<NasdaqEvidenceSession> Sessions,
    NasdaqEvidenceReceipt? LocalAcquisition, IReadOnlyList<NasdaqEvidenceClosureRequest>? DocumentedClosures);

internal sealed record NasdaqEvidenceRegistration(string RequestSha256, string CanonicalRequestSha256, NasdaqEvidenceRequest Request)
{
    public string Schema => NasdaqEvidenceReadinessCommand.RegistrationSchema;
    public bool CanonicalAdmissionPermitted => false;
    public bool OperationalSignal => false;
}

public sealed class NasdaqEvidenceReadinessCommand
{
    internal const string RegistrationSchema = "moneyway.nasdaq-multitimeframe-evidence.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false), new ExplicitUtcConverter() }
    };
    private static readonly DateTimeOffset ProspectiveStart = new(2026, 11, 23, 5, 0, 0, TimeSpan.Zero);
    private static readonly UTF8Encoding Utf8 = new(false);

    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Count != 5 || args[0] != "nasdaq-evidence-readiness" || args[1] != "--request" || args[3] != "--output")
                throw new ArgumentException("nasdaq-evidence-readiness --request <absolute-json> --output <absolute-registration-directory>");
            var bytes = NasdaqEvidenceSources.Read(args[2]);
            var payload = bytes.AsSpan();
            if (payload.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) payload = payload[3..];
            var request = JsonSerializer.Deserialize<NasdaqEvidenceRequest>(payload, Json)
                ?? throw new ArgumentException("Explicit dataset request required.");
            var registration = new NasdaqEvidenceRegistration(NasdaqEvidenceSources.Hash(bytes), RequestDigest(request), request);
            var (loaded, _, sessions) = Assess(registration.Request);
            var report = new
            {
                CandidateId = "EXP-NQ-MULTITIMEFRAME-EVIDENCE-READINESS-V1",
                registration.Request.DatasetId,
                registration.Request.ProviderId,
                registration.Request.Symbol,
                registration.Request.RegisteredAtUtc,
                registration.Request.Provenance,
                registration.RequestSha256,
                registration.CanonicalRequestSha256,
                registration.Request.LocalAcquisition,
                DocumentaryHistoricalAvailability = "NominalClosure_NotAuthenticated",
                IndependentlyAuthenticatedAvailability = false,
                Sources = loaded.Sources,
                IdenticalDuplicateCounts = loaded.IdenticalDuplicateCounts,
                Sessions = sessions,
                CanonicalAdmissionPermitted = false,
                OperationalSignal = false
            };
            Publish(args[4], new Dictionary<string, string>
            {
                ["registration.json"] = Serialize(registration),
                ["readiness.json"] = Serialize(report),
                ["readiness.md"] = Markdown(registration.Request.DatasetId, sessions)
            }, allowIdentical: true);
            output.WriteLine(Path.Combine(args[4], "readiness.json"));
            return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException or
                                   InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            error.WriteLine("Nasdaq evidence quarantined: " + e.Message);
            return 2;
        }
    }

    internal static string Replay(ReadOnlySpan<byte> registrationBytes, IReadOnlyList<string> args)
    {
        var registration = JsonSerializer.Deserialize<NasdaqEvidenceRegistration>(registrationBytes, Json)
            ?? throw new ArgumentException("Evidence registration required.");
        var request = registration.Request;
        if (request is null || registration.CanonicalRequestSha256 != RequestDigest(request))
            throw new ArgumentException("Registration request fingerprint mismatch. Create a new versioned registration.");
        var (loaded, closures, sessions) = Assess(request);
        DateTimeOffset? cutoff = null;
        if (args.Count == 8)
        {
            if (args[6] != "--cutoff-utc" ||
                !DateTimeOffset.TryParseExact(args[7], ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                throw new ArgumentException("An explicit registered UTC cutoff is required.");
            cutoff = parsed;
        }
        if (cutoff is null && sessions.Length != 1) throw new ArgumentException("Choose --cutoff-utc for a multi-cutoff registration.");
        var session = sessions.SingleOrDefault(s => cutoff is null || s.Session.CutoffUtc == cutoff)
            ?? throw new ArgumentException("Choose an exact registered cutoff.");
        if (!session.ResearchReplayPermitted)
            throw new ArgumentException("Registered research replay is blocked: " + string.Join(", ", session.Blockers));
        var h4Closures = closures.Where(c => c.Timeframe == "H4")
            .Select(c => new H4DocumentedClosure(c.StartUtc, c.EndUtc, c.EvidenceSha256)).ToArray();
        var research = new EvaluateNasdaqEndToEndResearchUseCase().Execute(new(request.DatasetId,
            session.Session.OperatingDate, session.Session.CutoffUtc, loaded.Series.Values.ToArray(),
            session.Session.RequestedStartsUtc["H4"], request.Provenance, h4Closures));
        var content = new
        {
            SchemaVersion = 1,
            RegistrationSha256 = NasdaqEvidenceSources.Hash(registrationBytes.ToArray()),
            SourceProvenance = loaded.Sources,
            EvidenceReadiness = session,
            Research = NasdaqEndToEndResearchOutput.Project(research)
        };
        Publish(args[4], new Dictionary<string, string>
        {
            ["nasdaq-end-to-end-research.json"] = Serialize(content, compact: true)
        }, allowIdentical: false);
        return args[4];
    }

    private static (NasdaqEvidenceLoaded, NasdaqEvidenceClosure[], NasdaqEvidenceSessionReadiness[]) Assess(NasdaqEvidenceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DatasetId) || string.IsNullOrWhiteSpace(request.Provenance) ||
            request.ProviderId != NasdaqEvidenceSources.Provider.Value || request.Symbol != NasdaqEvidenceSources.Instrument.Value ||
            request.RegisteredAtUtc.Offset != TimeSpan.Zero || request.RegisteredAtUtc == default ||
            request.Sessions is not { Count: > 0 } || request.Sessions.Any(s => s is null || s.RequestedStartsUtc is null) ||
            request.Sessions.Select(s => s.CutoffUtc).Distinct().Count() != request.Sessions.Count ||
            request.Sources is not { Count: > 0 } || request.Sources.Any(s => s is null))
            throw new ArgumentException("Explicit dataset identity, registered UTC instant, sources and unique cutoffs required.");
        var exposures = request.Sessions.GroupBy(s => s.OperatingDate);
        if (exposures.Any(g => g.Select(s => s.Exposure).Distinct().Count() != 1))
            throw new ArgumentException("One exposure classification per operating date is required.");
        foreach (var session in request.Sessions)
        {
            var known = KnownExposure(session.OperatingDate);
            if (!Enum.IsDefined(session.Exposure) || known is not null && session.Exposure != known ||
                session.Exposure == NasdaqEvidenceExposure.Prospective && session.CutoffUtc < ProspectiveStart)
                throw new ArgumentException($"Exposure classification conflicts with the registered policy for {session.OperatingDate:yyyy-MM-dd}.");
        }

        var loaded = NasdaqEvidenceSources.Load(request.Sources, request.LocalAcquisition);
        if (loaded.Sources.Any(s => s.LocalImportedAtUtc > request.RegisteredAtUtc))
            throw new ArgumentException("Dataset registration must follow the pinned local imports.");
        foreach (var receipt in request.Sources.Select(s => s.LocalAcquisition).Append(request.LocalAcquisition)
                     .OfType<NasdaqEvidenceReceipt>().Distinct())
        {
            Verify(receipt.Evidence);
            if (receipt.AcquiredAtUtc.Offset != TimeSpan.Zero || receipt.AcquiredAtUtc == default ||
                receipt.AcquiredAtUtc > request.RegisteredAtUtc)
                throw new ArgumentException("Local acquisition must be an explicit reviewed UTC instant by registration.");
        }
        if (loaded.Sources.Any(s => s.LocalAcquisition is { } r &&
                (s.LocalImportedAtUtc < r.AcquiredAtUtc || s.LastNominalCloseUtc > r.AcquiredAtUtc)))
            throw new ArgumentException("Acquisition receipt must follow nominal source closes and precede local import.");

        var closureRequests = request.DocumentedClosures ?? [];
        if (closureRequests.Any(c => !NasdaqEvidenceCoverage.Timeframes.ContainsKey(c.Timeframe) ||
            c.StartUtc.Offset != TimeSpan.Zero || c.EndUtc.Offset != TimeSpan.Zero || c.StartUtc >= c.EndUtc) ||
            closureRequests.Select(c => (c.Timeframe, c.StartUtc, c.EndUtc)).Distinct().Count() != closureRequests.Count)
            throw new ArgumentException("Distinct original timeframe and UTC bounds required for documented closures.");
        var closures = closureRequests.OrderBy(c => c.Timeframe, StringComparer.Ordinal).ThenBy(c => c.StartUtc).Select(c =>
        {
            Verify(c.Evidence);
            if (loaded.Series[c.Timeframe].Candles.Any(row => row.OpenTimeUtc < c.EndUtc && row.CloseTimeUtc > c.StartUtc))
                throw new ArgumentException("A declared closure overlaps observed source candles.");
            return new NasdaqEvidenceClosure(c.Timeframe, c.StartUtc, c.EndUtc, c.Evidence.Sha256);
        }).ToArray();
        var sessions = request.Sessions.OrderBy(s => s.CutoffUtc).Select(s => NasdaqEvidenceCoverage.Inspect(s,
            loaded.Series, closures, AuditedAt(s),
            HasProspectiveReceipt(s), request.Provenance)).ToArray();
        return (loaded, closures, sessions);

        IReadOnlySet<string> AuditedAt(NasdaqEvidenceSession session)
        {
            var preparation = NasdaqTradingWindowAlignment.Evaluate(session.CutoffUtc, session.CutoffUtc).PreparationUtc;
            return loaded.Sources.Where(source =>
                source.FirstOpenUtc + NasdaqEvidenceCoverage.Duration(NasdaqEvidenceCoverage.Timeframes[source.Timeframe]) <=
                (source.Timeframe == "H4" && preparation < session.CutoffUtc ? preparation : session.CutoffUtc))
                .GroupBy(source => source.Timeframe, StringComparer.Ordinal)
                .Where(group => group.All(source => !source.TimestampEvidence.EndsWith("Unresolved", StringComparison.Ordinal)))
                .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        }

        bool HasProspectiveReceipt(NasdaqEvidenceSession session)
        {
            if (session.Exposure != NasdaqEvidenceExposure.Prospective) return false;
            var dayStart = TimeZoneInfo.ConvertTimeToUtc(session.OperatingDate.ToDateTime(TimeOnly.MinValue),
                NasdaqSupervisedSessionPolicy.Zone);
            var intraday = loaded.Sources.Where(s => s.LastNominalCloseUtc > dayStart &&
                s.FirstOpenUtc + NasdaqEvidenceCoverage.Duration(NasdaqEvidenceCoverage.Timeframes[s.Timeframe]) <= session.CutoffUtc).ToArray();
            return intraday.Length > 0 && intraday.All(s => s.LocalAcquisition?.AcquiredAtUtc >= ProspectiveStart);
        }
    }

    private static NasdaqEvidenceExposure? KnownExposure(DateOnly day) => (day.Year, day.Month, day.Day) switch
    {
        (2026, 6, 22 or 24 or 26) => NasdaqEvidenceExposure.ChronologicalReview,
        (2026, 6, 23) => NasdaqEvidenceExposure.Development,
        (2026, 6, 25) => NasdaqEvidenceExposure.Validation,
        (2026, 6, 30) => NasdaqEvidenceExposure.HeldOut,
        (2026, 6, 29) or (2026, 9, 8) => NasdaqEvidenceExposure.MentorExposed,
        _ => null
    };

    private static void Verify(NasdaqEvidenceFile evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _ = NasdaqEvidenceSources.ReadPinned(evidence.Path, evidence.Sha256);
    }

    private static string Markdown(string datasetId, IEnumerable<NasdaqEvidenceSessionReadiness> sessions)
    {
        var text = new StringBuilder().Append("# Nasdaq evidence readiness\n\nDataset `").Append(datasetId).Append("`\n");
        foreach (var session in sessions)
        {
            text.Append("\n## ").Append(session.Session.CutoffUtc.ToString("O", CultureInfo.InvariantCulture))
                .Append(" · ").Append(session.Session.Exposure).Append("\n\n");
            foreach (var frame in session.Frames)
                text.Append("- ").Append(frame.Timeframe).Append(" · ").Append(frame.ClosedCount)
                    .Append(" closed candles · ").Append(frame.CoverageSufficient ? "coverage sufficient" : "coverage incomplete").Append('\n');
            foreach (var profile in session.H4Profiles)
                text.Append("- H4 ").Append(profile.Profile.LeftBars).Append('/').Append(profile.Profile.RightBars)
                    .Append(" · ").Append(profile.ReasonCode).Append('\n');
            text.Append("\nResearch replay permitted · ").Append(session.ResearchReplayPermitted ? "yes" : "no").Append('\n');
            foreach (var blocker in session.Blockers) text.Append("- ").Append(blocker).Append('\n');
            foreach (var action in session.AcquisitionActions) text.Append("- ").Append(action).Append('\n');
        }
        return text.ToString();
    }

    private static string Serialize<T>(T value, bool compact = false) =>
        JsonSerializer.Serialize(value, compact ? new JsonSerializerOptions(Json) { WriteIndented = false } : Json)
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    private static string RequestDigest(NasdaqEvidenceRequest request) =>
        NasdaqEvidenceSources.Hash(JsonSerializer.SerializeToUtf8Bytes(request, Json));

    private sealed class ExplicitUtcConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            NasdaqEvidenceSources.ParseUtc(reader.GetString());

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private static void Publish(string directory, IReadOnlyDictionary<string, string> files, bool allowIdentical)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Absolute output path required.");
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        NasdaqSessionLiquidityResearchCommand.Guard(target);
        if (Path.GetDirectoryName(target) is not { } parent) throw new ArgumentException("Dataset output cannot be a drive root.");
        if (Directory.Exists(target) && allowIdentical && files.Count == Directory.GetFiles(target).Length &&
            files.All(file =>
            {
                var path = Path.Combine(target, file.Key);
                NasdaqSessionLiquidityResearchCommand.Guard(path);
                return File.Exists(path) && File.ReadAllText(path, Utf8) == file.Value;
            }))
            return;
        if (Directory.Exists(target) || File.Exists(target)) throw new ArgumentException("Immutable output already exists. Use a new versioned directory.");
        Directory.CreateDirectory(parent);
        var pending = Path.Combine(parent, ".nasdaq-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pending);
        foreach (var file in files) File.WriteAllText(Path.Combine(pending, file.Key), file.Value, Utf8);
        Directory.Move(pending, target);
    }
}
