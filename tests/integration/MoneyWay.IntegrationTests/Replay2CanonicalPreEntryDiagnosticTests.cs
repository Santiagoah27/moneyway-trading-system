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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace MoneyWay.IntegrationTests;

public sealed partial class LocalNasdaqMentorSessionTests
{
    [LocalMentorSessionFact]
    public void ExportReplay2CanonicalPreEntryDiagnosticFromRealHistoricalOutcomes()
    {
        const string adapterPath = @"C:\MoneyWay\session\validation\2026-09-08\ValidatedInput.cs";
        const string adapterHash = "8a6ac872022a170fa0cbda6d23a5cbad736bed9a8009e58647a2e8c2ebe09aa2";
        Assert.Equal(adapterHash, FileSha256(adapterPath));
        const string original = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0d69f182a0b456ba4662896192dc242.json";
        const string originalHash = "41b35b81c05ebd3705b530322034567376b4d21d29cd43cf2e18468d7f587df2";
        Assert.Equal(originalHash, FileSha256(original));
        const string previous = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-d0b29e8617c9498298b4420ab854cae0.json";
        const string previousHash = "4fec5c8d3f409dc0b763ac009a2ba91a350e7bf0643b65b2905f24ca93bcd0a7";
        Assert.Equal(previousHash, FileSha256(previous));
        const string priorTp = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-a1d8dc2b3c7149ada452e3e106648ee2.json";
        const string priorTpHash = "605b994f5e3b1f98e32d970950b80f86449a0a39c6b8fe307b41a40ba55df66d";
        Assert.Equal(priorTpHash, FileSha256(priorTp));
        var first = RunS11Prefix(36);
        var longer = RunS11Prefix(37);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var canonical = first.Canonical;
        Assert.Same(first.Link.Frames[0].Strategy, canonical.OutcomeRun.StrategyRun.StrategyObservations[0]);
        Assert.Equal(S13Fingerprint(first.Link.Frames), S13Fingerprint(longer.Link.Frames.Where(f => f.Strategy.AsOfUtc <= S9Time(36))));
        string DiagnosticFingerprint(IEnumerable<MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyFrameDiagnostic> frames) =>
            JsonSerializer.Serialize(frames.Select(d => new
            {
                d.Step,
                d.AsOfUtc,
                d.Verdict,
                d.HasCompleteRequiredCoverage,
                d.Reason,
                d.BlockingRuleId,
                d.BlockingSequence,
                d.BlockingResult,
                d.MissingRequiredRuleIds
            }));
        Assert.Equal(DiagnosticFingerprint(canonical.Frames), DiagnosticFingerprint(longer.Canonical.Frames.Where(f => f.AsOfUtc <= S9Time(36))));
        var selected = first.Link.Frames.Where(f => f.Strategy.AsOfUtc >= S9Time(31)).ToArray();
        Assert.Equal(6, selected.Length);
        var final = selected[^1];
        var finalDiagnostic = canonical.Frames.Single(d => d.Step == final.Strategy.Step);
        var finalOutcome = canonical.OutcomeRun.Outcomes.Single(o => o.Step == final.Strategy.Step);
        Assert.Same(final.Strategy, finalOutcome.Observation);
        Assert.Equal(finalOutcome.Verdict, finalDiagnostic.Verdict);
        Assert.Equal(StrategyVerdict.HumanValidationRequired, finalDiagnostic.Verdict);
        Assert.Equal("NQ-SL-001", finalDiagnostic.BlockingRuleId!.Value);
        Assert.Equal(RuleEvaluationResult.HumanValidationRequired, finalDiagnostic.BlockingResult);
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
        Assert.Contains("SL selector evaluated", final.Strategy.Evaluations.Single(e => e.RuleId.Value == "NQ-SL-001").Reason);
        Assert.Empty(slProgression.MissingPrerequisiteRuleIds);
        Assert.Equal("NQ-WF2-002", Assert.Single(slProgression.PrerequisiteRuleIds).Value);
        Assert.True(slProgression.IsEligible);
        Assert.Equal(RuleEvaluationResult.NotApplicable, S13Result(final, "NQ-M1-003"));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(final, "NQ-RISK-001"));
        Assert.Equal(RuleEvaluationResult.Waiting, S13Result(final, "NQ-TP-001"));
        var capability = new StrategyReplayEvaluationCapabilityCatalog(new StrategyDefinitionCatalog(),
            MoneyWayReplayRuleEvaluators.GetAll(), MoneyWayReplayEvaluationCapabilityDeclarations.GetAll()).Find(definition.StrategyId, definition.Version)!;
        Assert.Equal(ReplayRuleEvaluationCapabilityStatus.Implemented, capability.Rules.Single(r => r.RuleId.Value == "NQ-SL-001").CapabilityStatus);

        // Export native completed diagnostic/observation objects, never a recalculated verdict or status summary.
        // The envelope is test-local provenance; it is not the production mentor-report schema.
        var options = new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve };
        var reviews = new[] { new NasdaqHistoricalEntryEvidence(first.Input.Session, "WickfillScenario2", "Sell", "S14-position-opening",
            "docs/decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md section 2.14/S14; human-reviewed MT5 position row; external screenshot bytes not supplied",
            "S14 human reviewer documented in ADR 0026", "HumanReview", "PositionOpeningField", "MetaTrader 5", null, "USTEC",
            "2026.09.08 16:32:00 MT5 server", "SourceReportedSecond", S9Time(32), S9Time(32),
            "ADR 0026 S14: September-8 position-opening field only; server = America/Bogota + 8h", null, new DateOnly(2026, 11, 5),
            29604.00m, null, null) };
        NasdaqHistoricalEntryAssessment? EntryAudit(NasdaqMentorSessionFrame frame,
            IReadOnlyList<NasdaqMentorSessionFrame> history, MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyBacktestDiagnosticsReport diagnostics) =>
            new NasdaqHistoricalEntryCausalSelector().Select(first.Input.Session, frame.Strategy.AsOfUtc, reviews,
                history.Where(f => f.Strategy.AsOfUtc == S9Time(32)).Select(f =>
                { var d = diagnostics.Frames.Single(d => d.Step == f.Strategy.Step); return NasdaqHistoricalEntryCausalSelector.Frame(f, d.Verdict.ToString(), d.HasCompleteRequiredCoverage); }));
        foreach (var frame in selected)
            Assert.Equal(JsonSerializer.Serialize(EntryAudit(frame, first.Link.Frames, canonical)),
                JsonSerializer.Serialize(EntryAudit(frame, longer.Link.Frames, longer.Canonical)));
        var artifact = new
        {
            Schema = "moneyway.canonical-preentry-window",
            SchemaVersion = 1,
            CanonicalRuntimeCommit = "c4c220a581e81b73f2fa0863f09afa80edb96cc5",
            RuntimeChanges = "WF2 historical-entry diagnostic only; S14 retrospective USTEC position field is not US100 execution or entry authorization",
            Generator = "tests/integration/MoneyWay.IntegrationTests/Replay2CanonicalPreEntryDiagnosticTests.cs",
            Adapter = new { Path = adapterPath, SHA256 = adapterHash },
            first.Input.Session,
            HistoricalEntryEvidence = JsonSerializer.SerializeToElement(reviews),
            HistoricalEntrySource = new
            {
                Document = "docs/decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md",
                SHA256 = FileSha256(Path.Combine(FindRepositoryRoot(), "docs/decisions/0026-define-nasdaq-wickfill-scenario-2-applicability.md")),
                ReviewSection = "2.14/S14",
                ScreenshotBytesAvailable = false
            },
            Inputs = first.Input.CsvFiles.Select(p => new { Timeframe = p.Key, Path = p.Value, SHA256 = FileSha256(p.Value) }).ToArray(),
            EvaluatedPrefixStartUtc = canonical.FirstAsOfUtc,
            EvaluatedPrefixEndUtc = canonical.LastAsOfUtc,
            EvaluatedPrefixFrameCount = canonical.FrameCount,
            DiagnosticWindowStartUtc = S9Time(31),
            DiagnosticWindowEndUtc = S9Time(36),
            ComparedLongerPrefixEndUtc = longer.Canonical.LastAsOfUtc,
            CommonPrefixIdentical = true,
            StrategyDefinition = definition,
            Capability = capability,
            RuntimeWorkingTreeCodeHashes = new[] {
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayEvaluators/MoneyWayNasdaqHumanStructuralStopLossEvaluator.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayEvaluators/MoneyWayNasdaqRiskExposureEvaluator.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayEvaluators/MoneyWayNasdaqHumanTakeProfitEvaluator.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqMentorSessionReplay.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqLiquidityTakeRuleFact.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqHumanWickfillScenario2StopLossRuleFact.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqRiskExposureRuleFact.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2RiskExposureObservation.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2RiskExposureObservationSelector.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2RiskExposureRuleFact.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2TakeProfitObservation.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2TakeProfitObservationSelector.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/ReplayInputs/NasdaqWickfillScenario2TakeProfitRuleFact.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqHistoricalTradeLifecycleSummary.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/ReadNasdaqHistoricalReport.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqHistoricalEntryAssessment.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqMentorSessionReplayInput.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqMentorSessionReplayReport.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/NasdaqMentorSessionReportJson.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/NasdaqHistoricalEntryReportProjection.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/ReadNasdaqCanonicalPreEntryWindow.cs",
                "src/backend/MoneyWay.Application/StrategyReplay/Workflow/AdvanceStrategyReplayProgressionUseCase.cs",
                "src/backend/MoneyWay.Application/StrategyReplay/Workflow/MoneyWayReplayWorkflowDefinitions.cs",
                "src/backend/MoneyWay.Application/StrategyReplay/Workflow/StrategyReplayRulePrerequisite.cs",
                "src/backend/MoneyWay.Application/StrategyReplay/Workflow/StrategyReplayWorkflowDefinition.cs",
                "src/backend/MoneyWay.Application/StrategyReplay/Workflow/StrategyReplayWorkflowCatalog.cs"
            }.ToDictionary(p => p, p => FileSha256(Path.Combine(FindRepositoryRoot(), p))),
            BindingDiagnostics = first.Link.Diagnostics.Where(d => d.AsOfUtc >= S9Time(31)).ToArray(),
            Frames = selected.Select(f => new
            {
                CanonicalDiagnostic = canonical.Frames.Single(d => d.Step == f.Strategy.Step),
                StrategyObservation = f.Strategy,
                f.Inputs,
                f.SnapshotDiagnostic,
                f.Snapshot,
                f.ContactResolution,
                f.DocumentedExit,
                f.FactualEvaluation,
                HistoricalEntryAssessment = JsonSerializer.SerializeToElement(EntryAudit(f, first.Link.Frames, canonical))
            }).ToArray(),
            Limitations = "Retrospective S14 source-reported USTEC opening field/price only; historical availability, native deal and US100 correspondence unverified. Original frozen entry-frame rules cannot be replaced by later WF2 Passed. No entry authorization, native fill, SL/TP/risk/PnL. Adapter/CSVs unchanged."
        };
        var directory = @"C:\MoneyWay\session\reports\2026-09-08";
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"canonical-preentry-{Guid.NewGuid():N}.json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) JsonSerializer.Serialize(stream, artifact, options);
        var repeated = Path.Combine(directory, $"canonical-preentry-{Guid.NewGuid():N}.json");
        using (var stream = new FileStream(repeated, FileMode.CreateNew, FileAccess.Write)) JsonSerializer.Serialize(stream, artifact, options);
        Assert.Equal(FileSha256(path), FileSha256(repeated));
        using var parsed = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("moneyway.canonical-preentry-window", parsed.RootElement.GetProperty("Schema").GetString());
        Assert.Equal(6, parsed.RootElement.GetProperty("Frames").GetArrayLength());
        Assert.Equal(adapterHash, FileSha256(adapterPath));
        Assert.Equal(originalHash, FileSha256(original));
        Assert.Equal(previousHash, FileSha256(previous));
        Assert.Equal(priorTpHash, FileSha256(priorTp));
        using var reader = new MoneyWay.Infrastructure.Strategies.Nasdaq.ReadNasdaqHistoricalReport();
        var view = reader.ExecuteAsync(path, FileSha256(path)).GetAwaiter().GetResult();
        Assert.Equal("HumanValidationRequired", view.SelectedFrame!.Verdict);
        Assert.Equal("HumanValidationRequired", view.SelectedFrame.TradeLifecycle!.Stages.Single(s => s.Stage == "StopLoss").RuleResult);
        Assert.Null(view.SelectedFrame.TradeLifecycle.Scenario2StopLoss);
        Assert.Null(view.SelectedFrame.TradeLifecycle.Entry);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Development")
            .UseSetting("NasdaqHistoricalDemo:ReportPath", path).UseSetting("NasdaqHistoricalDemo:ExpectedSha256", FileSha256(path)));
        using var client = factory.CreateClient();
        var http = client.GetFromJsonAsync<NasdaqHistoricalReportSummary>("/api/nasdaq/historical-demo?frameStep=" + final.Strategy.Step).GetAwaiter().GetResult()!;
        Assert.Equal("HumanValidationRequired", http.SelectedFrame!.Verdict);
        Assert.Equal("HumanValidationRequired", http.SelectedFrame.TradeLifecycle!.Stages.Single(s => s.Stage == "StopLoss").RuleResult);
        Assert.Null(http.SelectedFrame.TradeLifecycle.Scenario2StopLoss);
        Assert.Null(http.SelectedFrame.TradeLifecycle.Scenario2RiskExposure);
        Assert.Equal("Waiting", http.SelectedFrame.TradeLifecycle.Stages.Single(s => s.Stage == "Risk").RuleResult);
        Assert.Equal("Waiting", http.SelectedFrame.TradeLifecycle.Stages.Single(s => s.Stage == "TakeProfit").RuleResult);
        Assert.Null(http.SelectedFrame.TradeLifecycle.Scenario2TakeProfit);
        var entry = Assert.Single(http.SelectedFrame.TradeLifecycle.HistoricalEntry!.Observations);
        Assert.Equal("RetrospectiveOnly", http.SelectedFrame.TradeLifecycle.HistoricalEntry.SelectionStatus);
        Assert.Equal("USTEC", entry.Evidence.SourceSymbol); Assert.Equal(S9Time(32), entry.Evidence.EarliestEventUtc);
        Assert.Equal(29604.00m, entry.Evidence.SourceEntryPrice); Assert.Null(entry.Evidence.HistoricalAvailableAtUtc);
        Assert.Contains("Wf2NotPassedAtOriginalEntry", entry.Issues);
        Assert.Contains("OriginalInstrumentMismatch_NoMapping", entry.Issues);
        Assert.Equal(S9Time(32), entry.CanonicalAtEntry!.AsOfUtc);
        output.WriteLine($"REPEATED ARTIFACT: {repeated}; SHA256: {FileSha256(repeated)}");
        foreach (var rule in entry.CanonicalAtEntry.Rules.Where(r => r.RuleId is "NQ-WF2-001" or "NQ-WF2-002" or "NQ-M5-001" or "NQ-M1-001"))
            output.WriteLine($"ORIGINAL ENTRY FRAME {entry.CanonicalAtEntry.AsOfUtc:O}: {rule.RuleId}={rule.Result}; no retroauthorization.");
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
    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "MoneyWay.sln"))) return directory.FullName;
        throw new InvalidOperationException("Repository source is needed to pin the generation code.");
    }
}
