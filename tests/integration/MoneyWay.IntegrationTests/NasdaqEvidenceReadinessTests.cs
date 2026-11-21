using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MoneyWay.Worker;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqEvidenceReadinessTests
{
    private static readonly DateTimeOffset Prep = new(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Prep.AddHours(3.5);
    private static readonly DateTimeOffset Import = Cutoff.AddHours(1);

    [Fact]
    public void ProspectiveRegistrationIsIdempotentAndFeedsOnlyTheExistingResearchReplay()
    {
        using var fixture = new Fixture();
        var request = fixture.NewRequest("Prospective");
        var original = fixture.SourceBytes("H4");
        var registration = fixture.Register(request, "reg");
        var readback = File.ReadAllBytes(Path.Combine(registration, "readiness.json"));
        Assert.Equal(registration, fixture.Register(request, "reg"));
        Assert.Equal(readback, File.ReadAllBytes(Path.Combine(registration, "readiness.json")));
        Assert.Equal(original, fixture.SourceBytes("H4"));

        using var report = JsonDocument.Parse(readback);
        var root = report.RootElement;
        Assert.False(root.GetProperty("independentlyAuthenticatedAvailability").GetBoolean());
        Assert.False(root.GetProperty("canonicalAdmissionPermitted").GetBoolean());
        Assert.False(root.GetProperty("operationalSignal").GetBoolean());
        Assert.Equal(Cutoff, root.GetProperty("localAcquisition").GetProperty("acquiredAtUtc").GetDateTimeOffset());
        Assert.All(root.GetProperty("sources").EnumerateArray(), source =>
        {
            Assert.Equal(Import, source.GetProperty("localImportedAtUtc").GetDateTimeOffset());
            Assert.Equal("HashBoundDocumentaryAudited", source.GetProperty("timestampEvidence").GetString());
        });
        var session = Assert.Single(root.GetProperty("sessions").EnumerateArray());
        Assert.True(session.GetProperty("researchReplayPermitted").GetBoolean());
        Assert.True(session.GetProperty("synchronizedInputReady").GetBoolean());
        Assert.All(session.GetProperty("frames").EnumerateArray(), f => Assert.True(f.GetProperty("coverageSufficient").GetBoolean()));
        Assert.All(session.GetProperty("h4Profiles").EnumerateArray(), h4 =>
        {
            Assert.True(h4.GetProperty("enoughObservableCandles").GetBoolean());
            Assert.False(h4.GetProperty("sufficientConfirmedPivots").GetBoolean());
            Assert.False(h4.GetProperty("independentlyValidated").GetBoolean());
        });
        using var replay = fixture.Replay(registration, "replay", Cutoff);
        Assert.Equal("H4ContextUnavailable", replay.RootElement.GetProperty("research").GetProperty("outcome").GetString());
        fixture.Replay(registration, "unregistered", Cutoff.AddTicks(1), expectSuccess: false).Dispose();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Validation")]
    [InlineData("HeldOut")]
    [InlineData("ChronologicalReview")]
    [InlineData("MentorExposed")]
    public void ProtectedAndUnknownExposureCanBeInspectedWithoutRunningContext(string exposure)
    {
        using var fixture = new Fixture();
        var registration = fixture.Register(fixture.NewRequest(exposure), "register");
        using var report = Fixture.Report(registration);
        var session = Assert.Single(report.RootElement.GetProperty("sessions").EnumerateArray());
        Assert.False(session.GetProperty("researchReplayPermitted").GetBoolean());
        Assert.All(session.GetProperty("h4Profiles").EnumerateArray(), h4 =>
            Assert.False(h4.GetProperty("contextEvaluationCompleted").GetBoolean()));
        fixture.Replay(registration, "denied", Cutoff, expectSuccess: false).Dispose();
    }

    [Fact]
    public void DocumentedClosureIsSeparateFromUnexplainedGapAndMissingTimeframe()
    {
        using var fixture = new Fixture(h4Gap: true);
        var ordinary = fixture.Register(fixture.NewRequest("Development"), "gap");
        using var report = Fixture.Report(ordinary);
        var missing = H4Of(report);
        Assert.False(missing.GetProperty("coverageSufficient").GetBoolean());
        Assert.Equal(JsonValueKind.Null, Assert.Single(missing.GetProperty("missingRanges").EnumerateArray())
            .GetProperty("closureEvidenceSha256").ValueKind);

        var withClosure = fixture.NewRequest("Development");
        withClosure["documentedClosures"] = JsonSerializer.SerializeToNode(new[]
        {
            new { timeframe = "H4", startUtc = Prep.AddHours(-24), endUtc = Prep.AddHours(-20),
                evidence = new { fixture.Evidence.path, fixture.Evidence.sha256 } }
        });
        using var closure = Fixture.Report(fixture.Register(withClosure, "closure"));
        Assert.True(H4Of(closure).GetProperty("coverageSufficient").GetBoolean());
        Assert.Equal(fixture.Evidence.sha256, Assert.Single(H4Of(closure).GetProperty("missingRanges").EnumerateArray())
            .GetProperty("closureEvidenceSha256").GetString());

        withClosure["sources"]!.AsArray().RemoveAt(3);
        using var noM1 = Fixture.Report(fixture.Register(withClosure, "no-m1"));
        var session = Assert.Single(noM1.RootElement.GetProperty("sessions").EnumerateArray());
        Assert.False(session.GetProperty("synchronizedInputReady").GetBoolean());
        Assert.Contains(session.GetProperty("blockers").EnumerateArray(), b => b.GetString() == "M1:IncompleteRequestedCoverage");
    }

    [Fact]
    public void FrozenRegistrationRejectsSourceConflictsAndInvalidEvidence()
    {
        using var fixture = new Fixture();
        var valid = fixture.NewRequest("Development");
        var repeated = valid["sources"]!.AsArray();
        repeated.Add(repeated[0]!.DeepClone());
        using var duplicates = Fixture.Report(fixture.Register(valid, "repeat"));
        Assert.Equal(10, duplicates.RootElement.GetProperty("identicalDuplicateCounts").GetProperty("H4").GetInt32());
        repeated.Add(fixture.Source("H4", 240, 10, Prep.AddHours(-40), price: 101));
        Assert.Contains("Conflicting H4", fixture.RegisterError(valid, "conflict"));

        foreach (var (field, replacement) in new[] { ("providerId", "other"), ("symbol", "OTHER") })
        {
            var other = fixture.NewRequest("Development");
            other[field] = replacement;
            Assert.Contains("identity", fixture.RegisterError(other, "identity-" + field));
        }
        var unknown = fixture.NewRequest("Development");
        unknown["sessions"]![0]!.AsObject().Remove("exposure");
        Assert.Contains("exposure", fixture.RegisterError(unknown, "undeclared"));
        var noOffset = fixture.NewRequest("Development");
        noOffset["sessions"]![0]!["cutoffUtc"] = "2026-11-23T16:30:00";
        Assert.Contains("explicit UTC", fixture.RegisterError(noOffset, "undated"));
        var actual = fixture.NewRequest("Development");
        var saved = fixture.Register(actual, "frozen");
        File.AppendAllText(fixture.RawPath("H4"), "changed");
        Assert.Contains("SHA256", fixture.RegisterError(actual, "frozen"));
        fixture.Replay(saved, "changed", Cutoff, expectSuccess: false).Dispose();
    }

    [Fact]
    public void ClosedPrefixReadinessIsInvariantToAppendingOrChangingFutureCandles()
    {
        using var fixture = new Fixture();
        var baseline = fixture.NewRequest("Development");
        using var original = Fixture.Report(fixture.Register(baseline, "base"));
        foreach (var closePrice in new[] { 100, 101 })
        {
            var future = fixture.NewRequest("Development");
            future["sources"]!.AsArray().Add(fixture.Source("M1", 1, 1, Cutoff, closePrice));
            using var result = Fixture.Report(fixture.Register(future, "future-" + closePrice));
            Assert.True(JsonElement.DeepEquals(original.RootElement.GetProperty("sessions"), result.RootElement.GetProperty("sessions")));
        }
    }

    private static JsonElement H4Of(JsonDocument report) => report.RootElement.GetProperty("sessions")[0]
        .GetProperty("frames")[0];

    private sealed class Fixture : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("moneyway-readiness-");
        private readonly List<JsonObject> sources = [];
        private int index;
        public (string path, string sha256) Evidence { get; }

        public Fixture(bool h4Gap = false)
        {
            var proof = Path.Combine(directory.FullName, "evidence.txt");
            File.WriteAllText(proof, "Synthetic fixture mapping and acquisition evidence.");
            Evidence = (proof, Hash(proof));
            sources.Add(Source("H4", 240, 10, Prep.AddHours(-40), skip: h4Gap ? 4 : -1));
            sources.Add(Source("H1", 60, 18, Prep.AddHours(-15)));
            sources.Add(Source("M5", 5, 114, Prep.AddHours(-6)));
            sources.Add(Source("M1", 1, 570, Prep.AddHours(-6)));
        }

        public JsonObject NewRequest(string exposure) => JsonSerializer.SerializeToNode(new
        {
            datasetId = "synthetic-readiness",
            providerId = "oanda-global-markets-mt5-demo",
            symbol = "US100",
            registeredAtUtc = Import,
            provenance = "fixture",
            sources,
            localAcquisition = exposure == "Prospective" ? new { acquiredAtUtc = Cutoff, evidence = new { Evidence.path, Evidence.sha256 } } : null,
            sessions = new[] { new { operatingDate = "2026-11-23", cutoffUtc = Cutoff, exposure,
                requestedStartsUtc = new Dictionary<string, DateTimeOffset>
                    { ["H4"] = Prep.AddHours(-40), ["H1"] = Prep.AddHours(-15), ["M5"] = Prep.AddHours(-6), ["M1"] = Prep.AddHours(-6) } } }
        })!.AsObject();

        public JsonObject Source(string tf, int minutes, int count, DateTimeOffset start, int price = 100, int skip = -1)
        {
            var folder = Directory.CreateDirectory(Path.Combine(directory.FullName, "source-" + index++)).FullName;
            var raw = Path.Combine(folder, "original.tsv");
            var canonical = Path.Combine(folder, "normalized.csv");
            var mapPath = Path.Combine(folder, "timestamp-map.json");
            var observations = Enumerable.Range(0, count).Where(i => i != skip).Select(i => start.AddMinutes(i * minutes)).ToArray();
            File.WriteAllLines(raw, new[] { "<DATE>\t<TIME>\t<OPEN>\t<HIGH>\t<LOW>\t<CLOSE>\t<TICKVOL>" }.Concat(observations.Select(o =>
                FormattableString.Invariant($"{o.AddHours(3):yyyy.MM.dd}\t{o.AddHours(3):HH:mm:ss}\t{price}\t{price + 1}\t{price - 1}\t{price}\t1"))));
            var period = tf.StartsWith('H') ? "Hour" : "Minute";
            File.WriteAllLines(canonical, new[] { "provider_id,symbol,timeframe_amount,timeframe_unit,open_time_utc,close_time_utc,open,high,low,close,volume" }
                .Concat(observations.Select(o => FormattableString.Invariant($"oanda-global-markets-mt5-demo,US100,{tf[1..]},{period},{o:O},{o.AddMinutes(minutes):O},{price},{price + 1},{price - 1},{price},1"))));
            Write(mapPath, new
            {
                schemaVersion = 1,
                providerId = "oanda-global-markets-mt5-demo",
                symbol = "US100",
                timeframe = tf,
                sourceSha256 = Hash(raw),
                mappingStatus = "DocumentaryAudited",
                evidenceSha256 = Evidence.sha256,
                rows = observations.Select(o => new
                {
                    nativeOpenTime = o.AddHours(3).ToString("yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture),
                    openUtc = o
                })
            });
            var manifestPath = Path.Combine(folder, "manifest.json");
            File.Copy(Evidence.path, Path.Combine(folder, "mapping-evidence"));
            Write(manifestPath, new
            {
                schemaVersion = 1,
                sourceKind = $"OriginalMt5{tf}Documentary",
                originalTimeframe = tf,
                derived = false,
                providerId = "oanda-global-markets-mt5-demo",
                symbol = "US100",
                authenticatedHistoricalAvailability = false,
                importedAtUtc = Import,
                sourceFile = "original.tsv",
                sourceSha256 = Hash(raw),
                normalizedFile = "normalized.csv",
                normalizedSha256 = Hash(canonical),
                mappingFile = "timestamp-map.json",
                mappingSha256 = Hash(mapPath),
                mappingEvidenceFile = "mapping-evidence",
                mappingEvidenceSha256 = Evidence.sha256
            });
            return JsonSerializer.SerializeToNode(new { format = "OriginalImport", manifestPath, manifestSha256 = Hash(manifestPath) })!.AsObject();
        }

        public string RawPath(string timeframe) => Path.Combine(Path.GetDirectoryName(sources.Single(s =>
            File.ReadAllText(s["manifestPath"]!.GetValue<string>()).Contains($"OriginalMt5{timeframe}Documentary", StringComparison.Ordinal))
            ["manifestPath"]!.GetValue<string>())!, "original.tsv");
        public byte[] SourceBytes(string timeframe) => File.ReadAllBytes(RawPath(timeframe));
        public string Register(JsonObject request, string name) => RegisterResult(request, name, 0);
        public string RegisterError(JsonObject request, string name) => RegisterResult(request, name, 2);
        private string RegisterResult(JsonObject request, string name, int expected)
        {
            var path = Path.Combine(directory.FullName, name + ".json");
            var output = Path.Combine(directory.FullName, name);
            File.WriteAllText(path, request.ToJsonString());
            using var error = new StringWriter();
            var code = new NasdaqEvidenceReadinessCommand().Execute(
                ["nasdaq-evidence-readiness", "--request", path, "--output", output], TextWriter.Null, error);
            Assert.True(code == expected, error.ToString());
            return code == 0 ? output : error.ToString();
        }
        public JsonDocument Replay(string registration, string name, DateTimeOffset cutoff, bool expectSuccess = true)
        {
            var output = Path.Combine(directory.FullName, name);
            using var error = new StringWriter();
            var code = new NasdaqSessionLiquidityResearchCommand().Execute(
                ["nasdaq-session-liquidity", "--input", Path.Combine(registration, "registration.json"), "--output", output,
                    "--compare-end-to-end", "--cutoff-utc", cutoff.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)],
                TextWriter.Null, error);
            Assert.True(code == (expectSuccess ? 0 : 2), error.ToString());
            if (!expectSuccess) Assert.False(Directory.Exists(output));
            return expectSuccess ? JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "nasdaq-end-to-end-research.json")))
                : JsonDocument.Parse("{}");
        }
        public static JsonDocument Report(string folder) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "readiness.json")));
        private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        private static void Write(string path, object value) => File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value));
        public void Dispose() => directory.Delete(recursive: true);
    }
}
