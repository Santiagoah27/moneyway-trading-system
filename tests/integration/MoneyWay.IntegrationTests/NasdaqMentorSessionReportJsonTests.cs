using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MoneyWay.Application.Backtesting;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.UnitTests.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;
using MoneyWay.Infrastructure.Strategies.Nasdaq;

namespace MoneyWay.IntegrationTests;

public sealed class NasdaqMentorSessionReportJsonTests
{
    private const int FrameCount = 9660;
    // Reproduces large repeated TIME-owned JSON evidence, without expensive strategy evaluation.
    private static readonly string Evidence = "[" + string.Join(',', Enumerable.Repeat(
        "{\"DayStartUtc\":\"2026-06-29T05:00:00Z\",\"EndUtc\":\"2026-06-29T16:00:00Z\",\"Source\":\"mentor:auditoria\"}", 1500)) + "]";

    [Fact]
    public void ThousandsOfFramesPreserveCanonicalStatesEvidenceAndChronologyWithBoundedWrites()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var input = new NasdaqMentorSessionReplayInput("synthetic-large-report-only", LiquidityFixture.Session(M5Fixture.At(13)), new Dictionary<Timeframe, string>(), []);
        var series = new CandleSeries(input.Session.ProviderId, input.Session.Symbol, M5Fixture.Minute,
            Enumerable.Range(0, FrameCount).Select(i => new Candle(input.Session.ProviderId, input.Session.Symbol,
                M5Fixture.Minute, M5Fixture.At(13).AddMinutes(i), M5Fixture.At(13).AddMinutes(i + 1), 100, 110, 90, 100, null)));
        var evaluators = definition.Rules.Select(r => new ReportShapeEvaluator(r.RuleId)).ToArray();
        var run = new GenerateMultiTimeframeStrategyBacktestRunUseCase(new(), new(), new(evaluators));
        var session = new NasdaqMentorSessionReplay(input);
        var canonical = new GenerateCanonicalMultiTimeframeBacktestUseCase(new(run, new()), new()).ExecuteMentorSession(definition, [series], session);
        var report = new NasdaqMentorSessionReplayReport(input, [], [], canonical, session.Frames);
        var originalEvaluations = report.Frames.Select(f => f.Strategy.Evaluations.ToArray()).ToArray();
        var path = Path.Combine(Path.GetTempPath(), $"moneyway-report-shape-{Guid.NewGuid():N}.json");
        try
        {
            using (var stream = new BoundedWriteStream(File.Create(path)))
            {
                NasdaqMentorSessionReportJson.Write(stream, report);
                Assert.True(stream.CanWrite); // Caller owns the destination.
                Assert.True(stream.MaximumWrite < 512 * 1024);
            }
            Assert.True(new FileInfo(path).Length < 100 * 1024 * 1024);
            using var source = File.OpenRead(path);
            using var document = JsonDocument.Parse(source);
            var root = document.RootElement;
            Assert.Equal(FrameCount, root.GetProperty("FrameCount").GetInt32());
            Assert.Equal((int)canonical.Frames.Last().Verdict, root.GetProperty("FinalVerdict").GetInt32());
            Assert.Equal(FrameCount, root.GetProperty("FinalRuleStatesFrameStep").GetInt32());
            var frames = root.GetProperty("Frames").EnumerateArray().ToArray();
            Assert.Equal(FrameCount, frames.Length);
            Assert.Equal(1, root.GetProperty("EvidenceStatistics").GetProperty("UniqueCount").GetInt32());
            Assert.True(root.GetProperty("EvidenceStatistics").GetProperty("TotalReferencedUtf8Bytes").GetInt64() > int.MaxValue);
            var entry = Assert.Single(frames[0].GetProperty("EvidenceEntries").EnumerateArray());
            var id = entry.GetProperty("Id").GetString();
            using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(entry.GetProperty("Data").GetString()!)), CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            Assert.Equal(Evidence, reader.ReadToEnd());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Evidence))), id);
            for (var i = 0; i < FrameCount; i++)
            {
                var frame = frames[i];
                Assert.Equal(i + 1, frame.GetProperty("Step").GetInt32());
                Assert.Equal(report.Frames[i].Strategy.AsOfUtc, frame.GetProperty("AsOfUtc").GetDateTimeOffset());
                Assert.Equal((int)canonical.Frames[i].Verdict, frame.GetProperty("Diagnostic").GetProperty("Verdict").GetInt32());
                var evaluations = frame.GetProperty("Evaluations").GetProperty("$values").EnumerateArray().ToArray();
                Assert.Equal(originalEvaluations[i].Length, evaluations.Length);
                for (var j = 0; j < evaluations.Length; j++)
                {
                    Assert.Same(originalEvaluations[i][j], report.Frames[i].Strategy.Evaluations[j]);
                    Assert.Equal((int)originalEvaluations[i][j].Result, evaluations[j].GetProperty("Result").GetInt32());
                    Assert.Equal(id, evaluations[j].GetProperty("EvidenceId").GetString());
                    Assert.Equal(Evidence, originalEvaluations[i][j].EvidenceReference);
                }
                var transitions = frame.GetProperty("RuleStateTransitions").GetProperty("$values").GetArrayLength();
                Assert.Equal(i is 0 or 5000 ? definition.Rules.Count : 0, transitions);
                if (i > 0) Assert.Empty(frame.GetProperty("EvidenceEntries").EnumerateArray());
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LaterFramesDoNotChangeEarlierSerializedFramesOrCanonicalArtifacts()
    {
        using var fixture = new NasdaqMentorSessionImportTests();
        var input = fixture.Input();
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(input);
        var count = report.Frames.Count - 1;
        var run = report.Canonical!.OutcomeRun.StrategyRun;
        var prefixRun = new MultiTimeframeStrategyBacktestRun(run.StrategyId, run.StrategyVersion,
            run.ProviderId, run.Symbol, run.ConfiguredTimeframes, run.MarketObservations.Take(count), run.StrategyObservations.Take(count));
        var prefixCanonical = new MoneyWay.Application.Backtesting.Diagnostics.GenerateMultiTimeframeStrategyBacktestDiagnosticsReportUseCase()
            .Execute(MoneyWayNasdaqStrategyDefinition.Instance, new(prefixRun, report.Canonical.OutcomeRun.Outcomes.Take(count)));
        var prefix = new NasdaqMentorSessionReplayReport(input, report.Imports, [], prefixCanonical,
            report.Frames.Take(count), report.BindingDiagnostics);
        var snapshot = report.Frames.Last().Snapshot;
        using var earlier = JsonDocument.Parse(NasdaqMentorSessionReportJson.Serialize(prefix));
        using var later = JsonDocument.Parse(NasdaqMentorSessionReportJson.Serialize(report));
        var earlierFrames = earlier.RootElement.GetProperty("Frames").EnumerateArray().ToArray();
        var laterFrames = later.RootElement.GetProperty("Frames").EnumerateArray().ToArray();
        for (var i = 0; i < count; i++) Assert.Equal(earlierFrames[i].GetRawText(), laterFrames[i].GetRawText());
        Assert.Same(snapshot, report.Frames.Last().Snapshot);
        Assert.Same(report.Canonical.OutcomeRun.StrategyRun.StrategyObservations.Last(), report.Frames.Last().Strategy);
    }

    [Fact]
    public void MissingInputsDoNotFabricateFinalVerdictAndConvenienceApiMatchesStreaming()
    {
        using var fixture = new NasdaqMentorSessionImportTests();
        var input = fixture.Input();
        var report = new RunLocalNasdaqMentorSessionUseCase().Execute(new(input.SessionId, input.Session, new Dictionary<Timeframe, string>(), []));
        using var stream = new MemoryStream();
        NasdaqMentorSessionReportJson.Write(stream, report);
        Assert.Equal(Encoding.UTF8.GetString(stream.ToArray()), NasdaqMentorSessionReportJson.Serialize(report));
        using var document = JsonDocument.Parse(stream.ToArray());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("FinalVerdict").ValueKind);
        Assert.Equal(0, document.RootElement.GetProperty("FrameCount").GetInt32());
        Assert.Equal(4, document.RootElement.GetProperty("InputDiagnostics").GetProperty("$values").GetArrayLength());
    }

    private sealed class ReportShapeEvaluator(RuleId ruleId) : IReplayRuleEvaluator
    {
        public StrategyId StrategyId => MoneyWayNasdaqStrategyDefinition.Instance.StrategyId;
        public StrategyVersion StrategyVersion => MoneyWayNasdaqStrategyDefinition.Instance.Version;
        public RuleId RuleId => ruleId;
        public ReplayRuleEvaluationDecision Evaluate(StrategyReplayContext context) => new(
            context.Step <= 5000 ? RuleEvaluationResult.Waiting : RuleEvaluationResult.Failed,
            "Synthetic serialization shape only.", Evidence);
    }

    private sealed class BoundedWriteStream(Stream inner) : Stream
    {
        public int MaximumWrite { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override void Write(byte[] buffer, int offset, int count)
        {
            MaximumWrite = Math.Max(MaximumWrite, count);
            inner.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            MaximumWrite = Math.Max(MaximumWrite, buffer.Length);
            inner.Write(buffer);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
