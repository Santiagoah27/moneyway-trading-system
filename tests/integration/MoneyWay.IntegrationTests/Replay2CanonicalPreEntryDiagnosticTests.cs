using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Capabilities;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    [LocalMentorSessionFact]
    public void ExportReplay2CanonicalPreEntryDiagnosticFromRealHistoricalOutcomes()
    {
        const string adapterPath = @"C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs";
        const string adapterHash = "8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2";
        Assert.Equal(adapterHash, FileSha256(adapterPath));
        var first = RunS11Prefix(36);
        var longer = RunS11Prefix(37);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var canonical = first.Canonical;
        Assert.Same(first.Link.Frames[0].Strategy, canonical.OutcomeRun.StrategyRun.StrategyObservations[0]);
        Assert.Equal(S13Fingerprint(first.Link.Frames), S13Fingerprint(longer.Link.Frames.Where(f => f.Strategy.AsOfUtc <= S9Time(36))));
        string DiagnosticFingerprint(IEnumerable<MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyFrameDiagnostic> frames) =>
            JsonSerializer.Serialize(frames.Select(d => new { d.Step, d.AsOfUtc, d.Verdict, d.HasCompleteRequiredCoverage,
                d.Reason, d.BlockingRuleId, d.BlockingSequence, d.BlockingResult, d.MissingRequiredRuleIds }));
        Assert.Equal(DiagnosticFingerprint(canonical.Frames), DiagnosticFingerprint(longer.Canonical.Frames.Where(f => f.AsOfUtc <= S9Time(36))));
        var selected = first.Link.Frames.Where(f => f.Strategy.AsOfUtc >= S9Time(31)).ToArray();
        Assert.Equal(6, selected.Length);
        var final = selected[^1];
        var finalDiagnostic = canonical.Frames.Single(d => d.Step == final.Strategy.Step);
        var finalOutcome = canonical.OutcomeRun.Outcomes.Single(o => o.Step == final.Strategy.Step);
        Assert.Same(final.Strategy, finalOutcome.Observation);
        Assert.Equal(finalOutcome.Verdict, finalDiagnostic.Verdict);
        Assert.Equal(StrategyVerdict.Wait, finalDiagnostic.Verdict);
        Assert.Equal("NQ-SL-001", finalDiagnostic.BlockingRuleId!.Value);
        Assert.Equal(RuleEvaluationResult.Waiting, finalDiagnostic.BlockingResult);
        Assert.True(finalDiagnostic.HasCompleteRequiredCoverage);
        Assert.Empty(finalDiagnostic.MissingRequiredRuleIds);
        var required = definition.Rules.Where(r => r.IsRequired).Select(r => r.RuleId).ToArray();
        foreach (var f in selected)
        {
            var diagnostic = canonical.Frames.Single(d => d.Step == f.Strategy.Step);
            Assert.Equal(required, f.Strategy.Evaluations.Where(e => e.IsRequired).Select(e => e.RuleId));
            Assert.Equal(f.Strategy.AsOfUtc, diagnostic.AsOfUtc);
            Assert.NotEqual(StrategyVerdict.Ready, diagnostic.Verdict);
            Assert.Null(f.Snapshot); Assert.Null(f.ContactResolution); Assert.Null(f.DocumentedExit); Assert.Null(f.FactualEvaluation);
            Assert.DoesNotContain(f.Strategy.RuleFacts, r => r.RuleId.Value is "NQ-M5-001" or "NQ-M1-001" or "NQ-M1-002"
                or "NQ-M1-003" or "NQ-SL-001" or "NQ-TP-001" or "NQ-RISK-001");
        }
        var firstReversal = selected.First(f => S13Result(f, "NQ-WF2-001") == RuleEvaluationResult.Passed);
        var firstEligibility = selected.First(f => S13Result(f, "NQ-WF2-002") == RuleEvaluationResult.Passed);
        Assert.Equal(S9Time(34), firstReversal.Strategy.AsOfUtc);
        Assert.Equal(S9Time(35), firstEligibility.Strategy.AsOfUtc);
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(firstReversal, "NQ-WF2-002"));
        Assert.All(selected.Where(f => f.Strategy.AsOfUtc <= S9Time(33)), f =>
            Assert.DoesNotContain(f.Inputs, o => o is NasdaqHumanWickfillScenario2PostSweepReversalObservation));
        var reversal = Assert.Single(firstReversal.Inputs.OfType<NasdaqHumanWickfillScenario2PostSweepReversalObservation>());
        Assert.Equal(S9Time(33, 10), reversal.EffectiveAtUtc);
        Assert.Equal(S9Time(33, 10), reversal.ObservedAtUtc);
        Assert.Contains("S13", reversal.SourceReference);
        var start = selected[0].Strategy;
        Assert.Same(start, reversal.OpeningSweep.TakeObservation);
        Assert.Equal(S9Time(31), start.LifecycleProgression!.ActiveInstance!.StartedAtUtc);
        var branch = Assert.Single(firstReversal.Inputs.OfType<NasdaqHumanBranchApplicabilityObservation>());
        Assert.Contains("S11", branch.SourceReference);
        Assert.Same(start, branch.InitialTakeObservation);
        Assert.Contains("S9", reversal.OpeningSweep.PreOpenContext.Fact.SourceReference);
        var slProgression = final.Strategy.WorkflowProgression!.RuleEligibility.Single(e => e.RuleId.Value == "NQ-SL-001");
        Assert.Contains(slProgression.MissingPrerequisiteRuleIds, r => r.Value == "NQ-M1-003");
        Assert.Equal(RuleEvaluationResult.NotApplicable, S13Result(final, "NQ-M1-003"));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(final, "NQ-RISK-001"));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(final, "NQ-TP-001"));
        var capability = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(definition.StrategyId, definition.Version)!;
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, capability.Rules.Single(r => r.RuleId.Value == "NQ-SL-001").CapabilityStatus);

        // Export native completed diagnostic/observation objects, never a recalculated verdict or status summary.
        // The envelope is test-local provenance; it is not the production mentor-report schema.
        var options = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
        var artifact = new
        {
            Schema = "moneyway.canonical-preentry-window", SchemaVersion = 1,
            CanonicalRuntimeCommit = "6ae551f5e2fed7e112732db8c13191b8a517167b",
            Generator = "tests/integration/MoneyWay.IntegrationTests/Replay2CanonicalPreEntryDiagnosticTests.cs",
            Adapter = new { Path = adapterPath, SHA256 = adapterHash }, first.Input.Session,
            Inputs = first.Input.CsvFiles.Select(p => new { Timeframe = p.Key, Path = p.Value, SHA256 = FileSha256(p.Value) }).ToArray(),
            EvaluatedPrefixStartUtc = canonical.FirstAsOfUtc, EvaluatedPrefixEndUtc = canonical.LastAsOfUtc,
            EvaluatedPrefixFrameCount = canonical.FrameCount, DiagnosticWindowStartUtc = S9Time(31), DiagnosticWindowEndUtc = S9Time(36),
            ComparedLongerPrefixEndUtc = longer.Canonical.LastAsOfUtc, CommonPrefixIdentical = true,
            StrategyDefinition = definition, Capability = capability,
            BindingDiagnostics = first.Link.Diagnostics.Where(d => d.AsOfUtc >= S9Time(31)).ToArray(),
            Frames = selected.Select(f => new
            {
                CanonicalDiagnostic = canonical.Frames.Single(d => d.Step == f.Strategy.Step),
                StrategyObservation = f.Strategy, f.Inputs, f.SnapshotDiagnostic,
                f.Snapshot, f.ContactResolution, f.DocumentedExit, f.FactualEvaluation
            }).ToArray(),
            Limitations = "Bounded canonical pre-entry diagnostic only; no trade, fill, SL/TP/risk/PnL or retroactive authorization of 08:32 entry. Runtime and adapter unchanged."
        };
        var directory = @"C:\MoneyWay\session\reports\2026-09-08";
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"canonical-preentry-{Guid.NewGuid():N}.json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) JsonSerializer.Serialize(stream, artifact, options);
        using var parsed = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("moneyway.canonical-preentry-window", parsed.RootElement.GetProperty("Schema").GetString());
        Assert.Equal(6, parsed.RootElement.GetProperty("Frames").GetArrayLength());
        Assert.Equal(adapterHash, FileSha256(adapterPath));
        output.WriteLine($"ARTIFACT: {path}\nSHA256: {FileSha256(path)}\nBytes: {new FileInfo(path).Length}");
        output.WriteLine($"PREFIX: {canonical.FirstAsOfUtc:O} through {canonical.LastAsOfUtc:O}; {canonical.FrameCount} frames. WINDOW: 13:31Z-13:36Z.");
        foreach (var f in selected)
        {
            var d = canonical.Frames.Single(x => x.Step == f.Strategy.Step);
            output.WriteLine($"FRAME {d.AsOfUtc:O} step={d.Step} verdict={d.Verdict} blocker={d.BlockingRuleId?.Value}/{d.BlockingResult} coverage={d.HasCompleteRequiredCoverage}");
        }
        foreach (var e in final.Strategy.Evaluations)
            output.WriteLine($"RULE {e.RuleId.Value} Required={e.IsRequired} Definition={e.DefinitionStatus} Result={e.Result} Reason={e.Reason}");
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
