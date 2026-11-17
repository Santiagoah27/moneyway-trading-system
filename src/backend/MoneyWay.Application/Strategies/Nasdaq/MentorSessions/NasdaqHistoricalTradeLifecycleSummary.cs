using System.Security.Cryptography;
using System.Text;
using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

public sealed record NasdaqHistoricalTradeStage(string Stage, string Status, string? RuleId,
    string? RuleResult, string Reason);
public sealed record NasdaqHistoricalTradeSource(string InputType, DateTimeOffset ObservedAtUtc,
    string SourceReference, string SourceReferenceSha256, bool Truncated);
public sealed record NasdaqHistoricalRecordedExecution(decimal Price, DateTimeOffset EffectiveAtUtc);
public sealed record NasdaqHistoricalScenario2Stop(string ReferenceId, decimal ProtectivePrice, decimal StopPrice,
    DateTimeOffset EffectiveAtUtc, DateTimeOffset ObservedAtUtc, NasdaqHistoricalTradeSource Source,
    NasdaqHistoricalTradeSource ProtectiveSource);

/// <summary>Presentation of existing frame artifacts, never a strategy evaluator, fill simulator or PnL model.</summary>
public sealed record NasdaqHistoricalTradeLifecycleSummary(
    string Path, IReadOnlyList<NasdaqHistoricalTradeStage> Stages,
    NasdaqHistoricalRecordedExecution? Entry, decimal? StopPrice, decimal? TakeProfitPrice,
    decimal? RiskRatio, NasdaqHistoricalRecordedExecution? Exit,
    string? ExitScope, string? DocumentedExitReason,
    string FailureClassification, IReadOnlyList<string> FailedRuleIds,
    IReadOnlyList<NasdaqHistoricalTradeSource> Sources, int OmittedSources,
    string EvidenceScope)
{
    // A documented close is not a modeled economic result or proof of strategy compliance.
    public bool CompleteSupervisedTrade => false;
    public string? Direction { get; init; }
    public decimal? AccountBasisAmount { get; init; }
    public decimal? ReportedMaximumLoss { get; init; }
    public string? RiskCurrency { get; init; }
    public NasdaqHistoricalScenario2Stop? Scenario2StopLoss { get; init; }

    public static NasdaqHistoricalTradeSource Source(string inputType, DateTimeOffset observedAtUtc, string reference) =>
        new(inputType, observedAtUtc, reference[..Math.Min(512, reference.Length)],
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reference))), reference.Length > 512);

    public static NasdaqHistoricalTradeLifecycleSummary FromRules(IReadOnlyList<NasdaqHistoricalRuleSummary> rules)
    {
        var wf2 = rules.SingleOrDefault(r => r.RuleId == "NQ-WF2-002")?.Result == "Passed";
        var standard = rules.SingleOrDefault(r => r.RuleId == "NQ-M1-003")?.Result == "Passed";
        NasdaqHistoricalTradeStage Rule(string stage, string id, string? limitation = null)
        {
            var rule = rules.SingleOrDefault(r => r.RuleId == id);
            return new(stage, limitation is not null ? "NotImplemented" : rule?.Result == "Passed" ? "RecordedEvaluation" : "MissingOrBlocked",
                id, rule?.Result, limitation ?? rule?.Reason ?? "No evaluation was recorded in this frame.");
        }
        return new(wf2 ? "WickfillScenario2" : standard ? "Standard" : "Unresolved",
        [
            Rule("PreEntry", wf2 ? "NQ-WF2-002" : "NQ-M1-003"),
            new("Entry", "MissingInput", null, null, "No coherent canonical observed-entry artifact is available in this projection; eligibility is not execution."),
            Rule("StopLoss", "NQ-SL-001"),
            Rule("Risk", "NQ-RISK-001", wf2 ? "Risk consumes a Standard SL fact; Scenario 2 downstream ancestry is not integrated. No account basis or planned loss is inferred." : null),
            Rule("TakeProfit", "NQ-TP-001", wf2 ? "TP consumes a Standard SL fact; Scenario 2 target evidence and downstream ancestry are not integrated." : null),
            new("DocumentedExit", "MissingInput", null, null, "No causally selected documented execution is available; level contact is not a fill."),
            new("EconomicOutcome", "NotImplemented", null, null, "Existing factual composition does not calculate economic outcome or establish valid_strategy_loss/rule_violation."),
            new("SessionAttempts", "BlockedByUnresolvedSpecification", "NQ-REENTRY-001", rules.SingleOrDefault(r => r.RuleId == "NQ-REENTRY-001")?.Result,
                "Trade-count limits (NQ-RISK-003) and reentry scope are unresolved. No one-trade/no-reentry rule is invented; canonical terminal session gates remain authoritative.")
        ], null, null, null, null, null, null, null, "inconclusive",
            rules.Where(r => r.Result == "Failed").Select(r => r.RuleId).ToArray(), [], 0, "RecordedRuleEvaluationsOnly");
    }

    public static NasdaqHistoricalTradeLifecycleSummary FromFrame(NasdaqMentorSessionFrame frame)
    {
        var rules = frame.Strategy.Evaluations.Select(r => new NasdaqHistoricalRuleSummary(r.RuleId.Value,
            r.DefinitionStatus.ToString(), r.Result.ToString(), r.Sequence, r.IsRequired, r.Reason, r.EvaluatedAtUtc, null)).ToArray();
        var summary = FromRules(rules);
        var snapshot = (frame.Snapshot as NasdaqHistoricalTradeSnapshotResult.Available)?.Snapshot;
        var factual = frame.FactualEvaluation as NasdaqHistoricalFactualTradeEvaluation.Available;
        var exit = factual?.DocumentedExit;
        var scenario2 = frame.Strategy.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-SL-001")?.EstablishesProgression == true
            ? frame.Strategy.RuleFacts.Where(f => f.RuleId.Value == "NQ-SL-001").Select(f => f.Fact)
                .OfType<NasdaqHumanWickfillScenario2StopLossRuleFact>().SingleOrDefault() : null;
        var stages = summary.Stages.ToArray();
        void Recorded(string stage, string reason)
        {
            var i = Array.FindIndex(stages, s => s.Stage == stage);
            stages[i] = stages[i] with { Status = "RecordedArtifact", Reason = reason };
        }
        if (snapshot is not null)
        {
            Recorded("Entry", "Observed execution from the coherent canonical snapshot; no order is sent.");
            Recorded("StopLoss", "Exact human-selected stop from the coherent canonical snapshot.");
            Recorded("Risk", "Documented exposure from the same snapshot; no size is calculated.");
            Recorded("TakeProfit", "Exact human-selected target from the same snapshot.");
        }
        if (exit is not null) Recorded("DocumentedExit", "Causally selected documented execution; scope/reason are literal source fields, not inferred from price.");
        else if (frame.FactualEvaluation is NasdaqHistoricalFactualTradeEvaluation.Unavailable unavailable)
        {
            var i = Array.FindIndex(stages, s => s.Stage == "DocumentedExit");
            stages[i] = stages[i] with { Reason = unavailable.Reason };
        }
        var sources = frame.Inputs.Where(o => o.ObservedAtUtc <= frame.Strategy.AsOfUtc)
            .Select(o => Source(o.GetType().Name, o.ObservedAtUtc, o.SourceReference)).ToArray();
        return summary with
        {
            // Existing snapshot contracts are explicitly Standard-owned, even after its entry window ends.
            Path = snapshot is null ? summary.Path : "Standard",
            Stages = stages,
            Entry = snapshot is null ? null : new(snapshot.EntryPrice, snapshot.EntryEffectiveAtUtc),
            Direction = snapshot?.Direction.ToString(),
            AccountBasisAmount = snapshot?.Risk.Exposure.AccountBasisAmount,
            ReportedMaximumLoss = snapshot?.Risk.Exposure.ReportedMaximumLoss,
            RiskCurrency = snapshot?.Risk.Exposure.Currency,
            Scenario2StopLoss = scenario2 is null ? null : new(scenario2.Selection.Fact.ProtectiveReference.ReferenceId,
                scenario2.Selection.Fact.ProtectiveReference.Price, scenario2.StopPrice, scenario2.EffectiveAtUtc,
                scenario2.Selection.Fact.ObservedAtUtc,
                Source(nameof(NasdaqHumanWickfillScenario2StopLossObservation), scenario2.Selection.Fact.ObservedAtUtc, scenario2.Selection.Fact.SourceReference),
                Source(nameof(NasdaqHumanWickfillScenario2StopLossReference), scenario2.Selection.Fact.ProtectiveReference.ObservedAtUtc, scenario2.Selection.Fact.ProtectiveReference.SourceReference)),
            StopPrice = snapshot?.StopPrice,
            TakeProfitPrice = snapshot?.TakeProfitPrice,
            RiskRatio = snapshot?.Risk.RiskRatio,
            Exit = exit is null ? null : new(exit.ExitPrice, exit.ExitEffectiveAtUtc),
            ExitScope = exit?.DocumentedScope?.ToString(),
            DocumentedExitReason = exit?.DocumentedReason,
            Sources = sources.Take(16).ToArray(),
            OmittedSources = Math.Max(0, sources.Length - 16),
            EvidenceScope = "CanonicalFrameArtifacts"
        };
    }
}
