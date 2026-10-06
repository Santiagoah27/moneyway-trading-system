using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Optional local JSON projection. Preserve shared ancestry instead of expanding it at every reference.</summary>
public static class NasdaqMentorSessionReportJson
{
    public static string Serialize(NasdaqMentorSessionReplayReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(new
        {
            report.SessionId,
            report.Session,
            SuppliedCsvFiles = report.SuppliedCsvFiles.Select(f => new { Timeframe = f.Key, Path = f.Value }),
            report.Imports,
            report.InputDiagnostics,
            report.BindingDiagnostics,
            report.ReplayStartUtc,
            report.ReplayEndUtc,
            Frames = report.Frames.Select(f => new
            {
                RuleFacts = f.Strategy.RuleFacts.Select(r => new { r.RuleId, Fact = (object)r.Fact }),
                f.Strategy,
                Inputs = f.Inputs.Select(o => (object)o),
                SnapshotState = f.Snapshot?.GetType().Name ?? "Unavailable",
                Snapshot = (object?)f.Snapshot,
                f.SnapshotDiagnostic,
                ContactResolutionState = f.ContactResolution?.GetType().Name ?? "Unavailable",
                ContactResolution = (object?)f.ContactResolution,
                DocumentedExitState = f.DocumentedExit?.GetType().Name ?? "Unavailable",
                DocumentedExit = (object?)f.DocumentedExit,
                FactualEvaluationState = f.FactualEvaluation?.GetType().Name ?? "Unavailable",
                FactualEvaluation = (object?)f.FactualEvaluation,
            }),
            report.Canonical,
        }, new JsonSerializerOptions { WriteIndented = true, ReferenceHandler = ReferenceHandler.Preserve });
    }
}
