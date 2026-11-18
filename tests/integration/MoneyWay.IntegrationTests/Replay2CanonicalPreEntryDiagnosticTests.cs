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
        const string priorEntry = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-dfb31f824fbe4357a0d96d9683a7b3e1.json";
        const string priorEntryHash = "193fd37ac52f433a501f3dca673865bb459d5489e2dcf3e6a64028ec073d7244";
        Assert.Equal(priorEntryHash, FileSha256(priorEntry));
        const string priorSnapshot = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-aff9ef78df6244f7b74348f9d649fa08.json";
        const string priorSnapshotHash = "7993e57947624ac4aff1e4ff88aabbed9b2e9f15a1108a07ab8426cf1128ef2a";
        Assert.Equal(priorSnapshotHash, FileSha256(priorSnapshot));
        const string priorDocumentary = @"C:\MoneyWay\session\reports\2026-09-08\canonical-preentry-ed4a1539bef845fd84f5a68dcfc81cbe.json";
        const string priorDocumentaryHash = "48b5743e86ea3f061b9ae3c1f2c7280dcad4bf5272d7da7cf69c68f0d24dd3db";
        Assert.Equal(priorDocumentaryHash, FileSha256(priorDocumentary));
        var first = RunS11Prefix(37);
        var longer = RunS11Prefix(38);
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var canonical = first.Canonical;
        Assert.Same(first.Link.Frames[0].Strategy, canonical.OutcomeRun.StrategyRun.StrategyObservations[0]);
        Assert.Equal(S13Fingerprint(first.Link.Frames), S13Fingerprint(longer.Link.Frames.Where(f => f.Strategy.AsOfUtc <= S9Time(37))));
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
        Assert.Equal(DiagnosticFingerprint(canonical.Frames), DiagnosticFingerprint(longer.Canonical.Frames.Where(f => f.AsOfUtc <= S9Time(37))));
        var selected = first.Link.Frames.Where(f => f.Strategy.AsOfUtc >= S9Time(31)).ToArray();
        Assert.Equal(7, selected.Length);
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
        const string requestPath = @"C:\Users\Santi\.codex\attachments\37c12021-2046-4d90-b35c-bfcabcc1fad6\Pasted text.txt";
        const string requestHash = "47d049968b43d9d9d563b486d986ac7082cde61ba0264c617b186c2706044b29";
        Assert.Equal(requestHash, FileSha256(requestPath));
        var exits = new[] { new NasdaqHistoricalExitEvidence(first.Input.Session, "reported-close-current-request",
            "Current user request: WF2 SUPERVISED HISTORICAL EXIT EVIDENCE; attachment 37c12021-2046-4d90-b35c-bfcabcc1fad6; SHA256 " + requestHash,
            "User reports earlier human material; original close review requires validation", "HumanReview", "ReportedClosure", "NotEstablished")
        {
            OriginalTime = "around 08:37 America/Bogota", OriginalClockDomain = "America/Bogota (reported, not original platform clock)",
            TimePrecision = "Approximate", NominalEventUtc = S9Time(37),
            TimeMappingReference = "Current request nominal 13:37Z only; not a verified platform execution conversion or interval",
            ReportedStopProtection = "S5 reviewer reports subsequent SL movement to secure approximately 1:1; not a realized result",
            StopProtectionSourceReference = "ADR 0026 section 2.4/S5; retained human text SHA256 0aa95a610a6f13872bc93d6d69b2ea19721a486e340d9059f33fd31cf76a8682",
            Limitations = "Original closing artifact inaccessible; closing instrument, platform event, price, quantity, mechanism, native deal, explicit opening link and historical availability are not independently established. SourceSymbol and association remain null."
        } };
        NasdaqHistoricalEntryAssessment? EntryAudit(NasdaqMentorSessionFrame frame,
            IReadOnlyList<NasdaqMentorSessionFrame> history, MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyBacktestDiagnosticsReport diagnostics) =>
            new NasdaqHistoricalEntryCausalSelector().Select(first.Input.Session, frame.Strategy.AsOfUtc, reviews,
                history.Where(f => f.Strategy.AsOfUtc == S9Time(32)).Select(f =>
                { var d = diagnostics.Frames.Single(d => d.Step == f.Strategy.Step); return NasdaqHistoricalEntryCausalSelector.Frame(f, d.Verdict.ToString(), d.HasCompleteRequiredCoverage); }));
        foreach (var frame in selected)
            Assert.Equal(JsonSerializer.Serialize(EntryAudit(frame, first.Link.Frames, canonical)),
                JsonSerializer.Serialize(EntryAudit(frame, longer.Link.Frames, longer.Canonical)));
        NasdaqSupervisedHistoricalTradeSnapshot? SnapshotAudit(NasdaqMentorSessionFrame frame,
            IReadOnlyList<NasdaqMentorSessionFrame> history, MoneyWay.Application.Backtesting.Diagnostics.MultiTimeframeStrategyBacktestDiagnosticsReport diagnostics)
        {
            var d = diagnostics.Frames.Single(d => d.Step == frame.Strategy.Step);
            var rules = frame.Strategy.Evaluations.Select(r => new NasdaqHistoricalRuleSummary(r.RuleId.Value,
                r.DefinitionStatus.ToString(), r.Result.ToString(), r.Sequence, r.IsRequired, r.Reason, r.EvaluatedAtUtc, null)).ToArray();
            var lifecycle = NasdaqHistoricalTradeLifecycleSummary.FromFrame(frame) with { HistoricalEntry = EntryAudit(frame, history, diagnostics) };
            lifecycle = lifecycle with
            {
                HistoricalExit = new NasdaqHistoricalExitCausalSelector().Select(first.Input.Session,
                frame.Strategy.AsOfUtc, exits, lifecycle.HistoricalEntry)
            };
            return new NasdaqSupervisedHistoricalTradeSnapshotAssembler().Project(first.Input.Session, frame.Strategy.Step, frame.Strategy.AsOfUtc,
                d.Verdict.ToString(), rules, lifecycle).SupervisedSnapshot;
        }
        foreach (var frame in selected)
            Assert.Equal(JsonSerializer.Serialize(SnapshotAudit(frame, first.Link.Frames, canonical)),
                JsonSerializer.Serialize(SnapshotAudit(frame, longer.Link.Frames, longer.Canonical)));
        var artifact = new
        {
            Schema = "moneyway.canonical-preentry-window",
            SchemaVersion = 1,
            CanonicalRuntimeCommit = "268d39976ad9aa1918f90ff66dbab9039873cc07",
            RuntimeChanges = "Local supervised session termination outside canonical replay; documentary mentor exit never establishes MoneyWay ownership or closure; unchanged canonical strategy verdicts",
            Generator = "tests/integration/MoneyWay.IntegrationTests/Replay2CanonicalPreEntryDiagnosticTests.cs",
            Adapter = new { Path = adapterPath, SHA256 = adapterHash },
            first.Input.Session,
            HistoricalEntryEvidence = JsonSerializer.SerializeToElement(reviews),
            HistoricalExitEvidence = JsonSerializer.SerializeToElement(exits),
            ExitReviewSource = new { Path = requestPath, SHA256 = requestHash, OriginalClosingArtifactAvailable = false },
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
            DiagnosticWindowEndUtc = S9Time(37),
            ComparedLongerPrefixEndUtc = longer.Canonical.LastAsOfUtc,
            CommonPrefixIdentical = true,
            StrategyDefinition = definition,
            Capability = capability,
            RuntimeWorkingTreeCodeHashes = new[] {
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqSupervisedSession.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqSupervisedExecutionEvidence.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/LocalNasdaqSupervisedSessions.cs",
                "src/backend/MoneyWay.Infrastructure/Strategies/Nasdaq/LocalNasdaqExecutionEvidenceStore.cs",
                "src/backend/MoneyWay.Api/Endpoints/NasdaqExecutionEvidenceEndpoints.cs",
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
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqSupervisedHistoricalTradeSnapshot.cs",
                "src/backend/MoneyWay.Application/Strategies/Nasdaq/MentorSessions/NasdaqHistoricalExitAssessment.cs",
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
                HistoricalEntryAssessment = JsonSerializer.SerializeToElement(EntryAudit(f, first.Link.Frames, canonical)),
                HistoricalExitAssessment = JsonSerializer.SerializeToElement(new NasdaqHistoricalExitCausalSelector().Select(first.Input.Session, f.Strategy.AsOfUtc, exits, EntryAudit(f, first.Link.Frames, canonical))),
                SupervisedSnapshot = JsonSerializer.SerializeToElement(SnapshotAudit(f, first.Link.Frames, canonical))
            }).ToArray(),
            Limitations = "Incomplete supervised WF2 snapshot: S14 USTEC entry separate from US100; approximate close report at nominal 13:37Z is retrospective, original closing artifact/instrument/link not established. No original entry eligibility, native SL/exposure/TP, verified execution/exit or economic outcome. No backdating, orders or source edits."
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
        Assert.Equal(7, parsed.RootElement.GetProperty("Frames").GetArrayLength());
        Assert.Equal(adapterHash, FileSha256(adapterPath));
        Assert.Equal(originalHash, FileSha256(original));
        Assert.Equal(previousHash, FileSha256(previous));
        Assert.Equal(priorTpHash, FileSha256(priorTp));
        Assert.Equal(priorEntryHash, FileSha256(priorEntry));
        using var reader = new MoneyWay.Infrastructure.Strategies.Nasdaq.ReadNasdaqHistoricalReport();
        var legacy = reader.ExecuteAsync(priorSnapshot, priorSnapshotHash).GetAwaiter().GetResult();
        Assert.False(legacy.SelectedFrame!.TradeLifecycle!.SupervisedSnapshot!.CompleteSupervisedTrade);
        Assert.Null(legacy.SelectedFrame.TradeLifecycle.SupervisedSnapshot.DocumentaryExit);
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
        var supervised = Assert.IsType<NasdaqSupervisedHistoricalTradeSnapshot>(http.SelectedFrame.TradeLifecycle.SupervisedSnapshot);
        Assert.True(supervised.HistoricalEntryObserved); Assert.False(supervised.CanonicalEligibilityRecordedAtEntry);
        Assert.False(supervised.StopLossSupported); Assert.False(supervised.RiskExposureSupported); Assert.False(supervised.TakeProfitSupported);
        Assert.False(supervised.NativeExecutionDocumented); Assert.False(supervised.NativeExecutionIndependentlyVerified);
        Assert.False(supervised.ExitSupported); Assert.False(supervised.EconomicOutcomeSupported); Assert.False(supervised.CompleteSupervisedTrade);
        Assert.Equal("unresolved", supervised.InstrumentCorrespondence); Assert.Equal("NotGrantedBySnapshot", supervised.EntryAuthorization);
        Assert.Null(supervised.StopLoss); Assert.Null(supervised.Risk); Assert.Null(supervised.TakeProfit); Assert.Null(supervised.Exit); Assert.Null(supervised.EconomicOutcome);
        Assert.Equal("USTEC", supervised.DocumentaryEntry!.Observations.Single().Evidence.SourceSymbol);
        Assert.Equal(29604.00m, supervised.DocumentaryEntry.Observations.Single().Evidence.SourceEntryPrice);
        Assert.Equal(priorSnapshotHash, FileSha256(priorSnapshot));
        Assert.True(supervised.HistoricalExitObserved); Assert.False(supervised.NativeExitIndependentlyVerified);
        Assert.False(supervised.EntryExitRelationshipIndependentlyVerified);
        var close = Assert.Single(supervised.DocumentaryExit!.Observations);
        Assert.Equal("RetrospectiveOnly", close.Availability); Assert.Equal("Approximate", close.Evidence.TimePrecision);
        Assert.Equal(S9Time(37), close.Evidence.NominalEventUtc); Assert.Null(close.Evidence.EffectiveAtUtc);
        Assert.Null(close.Evidence.SourceSymbol); Assert.Null(close.Evidence.ExitPrice); Assert.Null(close.Evidence.ExecutionReference);
        Assert.Null(close.Evidence.Quantity); Assert.Null(close.Evidence.DocumentedCloseMechanism); Assert.Equal("Unresolved", close.EntryAssociation);
        Assert.All(selected.Take(6), f => Assert.Null(SnapshotAudit(f, first.Link.Frames, canonical)?.DocumentaryExit));
        // Synthetic own Standard session: the authentic mentor report must never manufacture an operational event.
        var isolated = Path.Combine(Path.GetTempPath(), "moneyway-replay-session-isolation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var operational = new MoneyWay.Infrastructure.Strategies.Nasdaq.LocalNasdaqExecutionEvidenceStore(isolated);
            var own = operational.OpenSession(Guid.NewGuid(), new("synthetic:separate-MoneyWay-session", first.Input.Session.TradingDay,
                first.Input.Session.ProviderId.Value, first.Input.Session.Symbol.Value, "Standard", "synthetic:test operator", "note:synthetic own session; not a mentor execution"));
            _ = client.GetFromJsonAsync<NasdaqHistoricalReportSummary>("/api/nasdaq/historical-demo").GetAwaiter().GetResult();
            var after = operational.Session(own.SessionKey);
            Assert.Equal("Open_NoTrade", after.Status); Assert.False(after.Terminal);
            Assert.Null(after.ExecutionConfirmation); Assert.Null(after.ClosureConfirmation); Assert.Single(after.Events);
        }
        finally { if (Directory.Exists(isolated)) Directory.Delete(isolated, true); }
        Assert.Equal(priorDocumentaryHash, FileSha256(priorDocumentary));
        output.WriteLine("OPERATIONAL ISOLATION: synthetic separate Standard session remains Open_NoTrade after authentic mentor exit report; no automatic lock.");
        output.WriteLine($"EXIT: reported approximation 08:37 Bogota / nominal 13:37Z; original artifact/instrument/link unverified; no native exit or realized 1R.");
        output.WriteLine($"SUPERVISED SNAPSHOT: Complete={supervised.CompleteSupervisedTrade}; InstrumentCorrespondence={supervised.InstrumentCorrespondence}; Missing={string.Join(',', supervised.MissingEvidence)}");
        output.WriteLine($"REPEATED ARTIFACT: {repeated}; SHA256: {FileSha256(repeated)}");
        foreach (var rule in entry.CanonicalAtEntry.Rules.Where(r => r.RuleId is "NQ-WF2-001" or "NQ-WF2-002" or "NQ-M5-001" or "NQ-M1-001"))
            output.WriteLine($"ORIGINAL ENTRY FRAME {entry.CanonicalAtEntry.AsOfUtc:O}: {rule.RuleId}={rule.Result}; no retroauthorization.");
        output.WriteLine($"ARTIFACT: {path}\nSHA256: {FileSha256(path)}\nBytes: {new FileInfo(path).Length}");
        output.WriteLine($"PREFIX: {canonical.FirstAsOfUtc:O} through {canonical.LastAsOfUtc:O}; {canonical.FrameCount} frames. WINDOW: 13:31Z-13:37Z.");
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
