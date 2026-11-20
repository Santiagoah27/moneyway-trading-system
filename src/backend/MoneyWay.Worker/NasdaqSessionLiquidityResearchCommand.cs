using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.Liquidity;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Domain.MarketData;
using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

/// <summary>Explicit bounded documentary IO; reuses the candidate's pure session primitive, never admits market data.</summary>
public sealed class NasdaqSessionLiquidityResearchCommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public int Execute(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var interaction = args.Count == 6 && args[5] == "--analyze-interaction";
            var tracking = args.Count == 6 && args[5] == "--track-context";
            var contextualSc = args.Count == 6 && args[5] == "--compare-context-sc";
            var sequence = args.Count is 6 or 8 && args[5] == "--compare-context-sequence";
            if ((args.Count != 5 && !interaction && !tracking && !contextualSc && !sequence) || args[0] != "nasdaq-session-liquidity" || args[1] != "--input" || args[3] != "--output"
                || !Path.IsPathFullyQualified(args[2]) || !Path.IsPathFullyQualified(args[4])) throw new ArgumentException("nasdaq-session-liquidity --input <absolute-context-json> --output <new-absolute-directory> [--analyze-interaction | --track-context | --compare-context-sc | --compare-context-sequence [--cutoff-utc <UTC>]]");
            var input = Path.GetFullPath(args[2]); var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[4]));
            Guard(input); Guard(destination);
            if (Directory.Exists(destination) || File.Exists(destination) || destination == Path.GetPathRoot(destination)) throw new ArgumentException("New output directory required; never replace existing evidence.");
            using var file = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length is <= 0 or > 8 * 1024 * 1024) throw new ArgumentException("Bounded 8 MiB context required.");
            var bytes = new byte[(int)file.Length]; file.ReadExactly(bytes);
            using var json = JsonDocument.Parse(bytes); var root = json.RootElement;
            var reviewSession = root.GetProperty("id").GetString() == "nasdaq-context-20260622-review-v1";
            if ((!reviewSession && root.GetProperty("id").GetString() != "nasdaq-context-20260623-v1") || root.GetProperty("timeZone").GetString() != "America/Bogota"
                || root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("canonicalAdmissionPermitted").GetBoolean() || root.GetProperty("operationalSignal").GetBoolean()) throw new ArgumentException("Registered isolated June 23 or frozen June 22 review context required.");
            var provider = new MarketDataProviderId(root.GetProperty("providerId").GetString()!); var symbol = new MarketSymbol(root.GetProperty("symbol").GetString()!);
            if (provider.Value != "oanda-global-markets-mt5-demo" || symbol.Value != "US100" || root.GetProperty("tradingDay").GetString() != (reviewSession ? "2026-06-22" : "2026-06-23")) throw new ArgumentException("Original source identity required; no instrument/provider substitution.");
            var day = new DateOnly(2026, 6, reviewSession ? 22 : 23); var calculator = new NasdaqSessionLiquidityCalculator();
            CandleSeries Read(string key, Timeframe timeframe)
            {
                var source = root.GetProperty("series").GetProperty(key);
                if (source.GetProperty("derived").GetBoolean()) throw new ArgumentException("Original source timeframe required.");
                var candles = source.GetProperty("candles").EnumerateArray().Select(c =>
                {
                    var tf = c.GetProperty("timeframe");
                    if (c.GetProperty("providerId").GetProperty("value").GetString() != provider.Value || c.GetProperty("symbol").GetProperty("value").GetString() != symbol.Value
                        || tf.GetProperty("amount").GetInt32() != timeframe.Amount || tf.GetProperty("unit").GetString() != timeframe.Unit.ToString()) throw new ArgumentException("Homogeneous source identity/timeframe required.");
                    return new Candle(provider, symbol, timeframe, c.GetProperty("openTimeUtc").GetDateTimeOffset(), c.GetProperty("closeTimeUtc").GetDateTimeOffset(),
                        c.GetProperty("open").GetDecimal(), c.GetProperty("high").GetDecimal(), c.GetProperty("low").GetDecimal(), c.GetProperty("close").GetDecimal(),
                        c.TryGetProperty("volume", out var volume) && volume.ValueKind != JsonValueKind.Null ? volume.GetDecimal() : null);
                });
                return new(provider, symbol, timeframe, candles);
            }
            var hour = Read("H1", new(1, TimeframeUnit.Hour));
            var maximum = root.GetProperty("maximumCutoffUtc").GetDateTimeOffset();
            if (sequence && reviewSession) throw new ArgumentException("Sequence comparison requires the exposed Development context.");
            if (sequence && args.Count == 8)
            {
                if (args[6] != "--cutoff-utc" ||
                    !DateTimeOffset.TryParseExact(args[7], ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"],
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var cutoff) ||
                    cutoff > maximum)
                    throw new ArgumentException("Sequence cutoff must be explicit UTC and within the supplied context.");
                maximum = cutoff;
            }
            var cutoffs = hour.Candles.Select(c => c.CloseTimeUtc).Where(t => t >= new DateTimeOffset(day.Year, day.Month, day.Day, 0, 0, 0, TimeSpan.Zero) && t <= maximum)
                .Concat(new[] { new DateTimeOffset(day.Year, day.Month, day.Day, 6, 59, 0, TimeSpan.Zero), new(day.Year, day.Month, day.Day, 12, 59, 0, TimeSpan.Zero), maximum }).Distinct().Order().ToArray();
            object Project(NasdaqSessionReference s) => new
            {
                s.StrategyVersion,
                s.OperatingDate,
                s.Session,
                s.Provider,
                s.Instrument,
                s.SourceTimeframe,
                s.StartUtc,
                s.EndUtc,
                s.EvaluatedAtUtc,
                s.CoverageStatus,
                s.ExpectedCount,
                SourceCount = s.SourceCandles.Count,
                s.MissingOpensUtc,
                s.High,
                s.Low,
                s.HighCandles,
                s.LowCandles,
                s.AvailableAtUtc,
                s.AvailabilityMeaning,
                s.SourceAuthority
            };
            object report;
            ContextualLiquidityTrackingResult? tracked = null;
            ContextualSwingScReport? scComparison = null;
            if (contextualSc || sequence)
            {
                var m5 = Read("M5", new(5, TimeframeUnit.Minute));
                var rangeStart = calculator.CalculateExplicitSessions(hour, day, maximum)[1].StartUtc;
                scComparison = new CompareContextualSwingScUseCase().Execute(hour, m5, day, rangeStart, maximum, M5ResearchDataOrigin.RetrospectiveDocumentary,
                    reviewSession ? "Frozen earliest eligible June 22 review source; self-declared unreviewed; nominal documentary UTC only; selection e52279ed-2ae9-456e-9c6f-56f11cd22347." : "June 23 Development documentary sources; explicit contextual tracking hypothesis 187c3ef3-2fe4-4993-bce3-dee605345b4f; nominal UTC only.");
                var scReport = new
                {
                    SchemaVersion = 1,
                    ContextId = root.GetProperty("id").GetString(),
                    InputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    DocumentaryMapping = root.GetProperty("documentaryMapping").GetString(),
                    ExperimentSource = "Direct user experimental hypotheses 292ee493-543a-4742-9cf5-4b9b75186392; not mentor-confirmed operational rules.",
                    SourceProvenance = new[] { "H1", "M5" }.ToDictionary(k => k, k => root.GetProperty("series").GetProperty(k).EnumerateObject().Where(p => p.Name != "candles").ToDictionary(p => p.Name, p => p.Value.Clone())),
                    Comparison = scComparison
                };
                if (sequence)
                {
                    var configs = from sweep in Enum.GetValues<M5SweepPolicy>()
                                  from window in Enum.GetValues<M5SequenceWindowPolicy>()
                                  from formation in Enum.GetValues<M5SequenceFvgPolicy>()
                                  from profile in Enum.GetValues<M5FvgProfile>()
                                  select new M5SequenceConfiguration(sweep, window, formation, profile, new(8, 30), new(11, 30));
                    report = new
                    {
                        scReport.SchemaVersion,
                        scReport.ContextId,
                        scReport.InputSha256,
                        scReport.DocumentaryMapping,
                        scReport.SourceProvenance,
                        Sequence = ContextualSequenceResearchOutput.Project(
                            new EvaluateM5ContextualSequenceUseCase().Execute(scReport.ContextId!, scComparison, m5, configs.ToArray()))
                    };
                    scComparison = null;
                }
                else report = scReport;
            }
            else if (tracking)
            {
                var m5 = Read("M5", new(5, TimeframeUnit.Minute));
                var rangeStart = calculator.CalculateExplicitSessions(hour, day, maximum)[1].StartUtc;
                tracked = new TrackContextualLiquidityUseCase().Execute(hour, m5, day, rangeStart, maximum, M5ResearchDataOrigin.RetrospectiveDocumentary,
                    reviewSession ? "Frozen earliest eligible June 22 review source; self-declared unreviewed; nominal documentary UTC only; selection e52279ed-2ae9-456e-9c6f-56f11cd22347." : "June 23 Development documentary sources; explicit contextual tracking hypothesis 187c3ef3-2fe4-4993-bce3-dee605345b4f; nominal UTC only.");
                report = new
                {
                    SchemaVersion = 1,
                    ContextId = root.GetProperty("id").GetString(),
                    InputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    DocumentaryMapping = root.GetProperty("documentaryMapping").GetString(),
                    SourceProvenance = new[] { "H1", "M5" }.ToDictionary(k => k, k => root.GetProperty("series").GetProperty(k).EnumerateObject().Where(p => p.Name != "candles").ToDictionary(p => p.Name, p => p.Value.Clone())),
                    Tracking = tracked
                };
            }
            else if (interaction) report = new
            {
                SchemaVersion = 1,
                ContextId = root.GetProperty("id").GetString(),
                InputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                SourceProvenance = root.GetProperty("series").GetProperty("H1").EnumerateObject().Where(p => p.Name != "candles").ToDictionary(p => p.Name, p => p.Value.Clone()),
                DocumentaryMapping = root.GetProperty("documentaryMapping").GetString(),
                Classification = "IsolatedDescriptiveResearch_NotAdmittedBacktest",
                CanonicalAdmissionPermitted = false,
                OperationalSignal = false,
                Frames = cutoffs.Select(t => new
                {
                    CutoffUtc = t,
                    Interaction = new AnalyzeLondonAsiaInteractionUseCase().Execute(hour, day, t,
                    M5ResearchDataOrigin.RetrospectiveDocumentary, reviewSession ? "Frozen June 22 original H1 context; descriptive interaction, no contextual selection recommendation; nominal documentary UTC only." : "June 23 original H1 context; direct user OHLC interaction contract 67059360-6f02-4d85-ba08-62b73a7a7d3b; documentary nominal UTC only.")
                }).ToArray()
            };
            else report = new
            {
                SchemaVersion = 1,
                ContextId = root.GetProperty("id").GetString(),
                InputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                CandidateVersion = MoneyWayNasdaqSessionCandidate.Version.Value,
                ParentVersion = MoneyWayNasdaqM5StrictCloseCandidate.Version.Value,
                Provenance = "Direct user configuration 2026-11-19; attachment 88027fbe-59e6-479e-ba0b-1dc5067d91a2; ADR 0028. No independent mentor verification.",
                Classification = "RetrospectiveDocumentary_NotAdmittedBacktest",
                CanonicalAdmissionPermitted = false,
                OperationalSignal = false,
                SourceProvenance = root.GetProperty("series").GetProperty("H1").EnumerateObject().Where(p => p.Name != "candles").ToDictionary(p => p.Name, p => p.Value.Clone()),
                Frames = cutoffs.Select(t => new { CutoffUtc = t, Sessions = calculator.CalculateExplicitSessions(hour, day, t).Select(Project).ToArray() }).ToArray(),
                NonAuthoritativeComparisons = new[] { Read("M5", new(5, TimeframeUnit.Minute)), Read("M1", new(1, TimeframeUnit.Minute)) }
                    .Select(s => new { SourceTimeframe = s.Timeframe.ToString(), Sessions = calculator.CalculateExplicitSessions(s, day, new DateTimeOffset(day.Year, day.Month, day.Day, 13, 0, 0, TimeSpan.Zero)).Select(Project).ToArray() }).ToArray()
            };
            var content = JsonSerializer.Serialize(report, Json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
            var parent = Path.GetDirectoryName(destination)!; Directory.CreateDirectory(parent); Guard(parent);
            var pending = Path.Combine(parent, ".nasdaq-session-pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending); File.WriteAllText(Path.Combine(pending, sequence ? "contextual-sequence.json" : contextualSc ? "contextual-swing-sc.json" : tracking ? "liquidity-context.json" : interaction ? "london-asia-interaction.json" : "session-liquidity.json"), content, new UTF8Encoding(false));
            if (tracked is not null) File.WriteAllText(Path.Combine(pending, "liquidity-context.md"), ContextualLiquidityResearchMarkdown.Render(tracked), new UTF8Encoding(false));
            if (scComparison is not null) File.WriteAllText(Path.Combine(pending, "contextual-swing-sc.md"), ContextualSwingScMarkdown.Render(scComparison), new UTF8Encoding(false));
            Guard(destination); Directory.Move(pending, destination);
            output.WriteLine(destination); return 0;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { error.WriteLine("Documentary session calculation unavailable: " + e.Message); return 2; }
    }
    private static void Guard(string path)
    {
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) throw new ArgumentException("Local paths only.");
        for (var item = new FileInfo(path) as FileSystemInfo; item is not null; item = item is DirectoryInfo d ? d.Parent : new DirectoryInfo(Path.GetDirectoryName(item.FullName)!))
            if (item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked paths not supported.");
    }
}
