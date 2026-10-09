using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyWay.Application.Strategies.Nasdaq.MentorSessions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

/// <summary>Lossless audit projection over completed outcomes. Does not evaluate or mutate replay state.</summary>
public static class NasdaqMentorSessionReportJson
{
    private static readonly JsonSerializerOptions Options = new() { ReferenceHandler = ReferenceHandler.Preserve };

    /// <summary>Convenience API for small reports. Persist large reports with Write instead.</summary>
    public static string Serialize(NasdaqMentorSessionReplayReport report)
    {
        using var stream = new MemoryStream();
        Write(stream, report);
        return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
    }

    /// <summary>
    /// Writes one frame at a time, leaving the destination open. Evidence entries contain exact UTF-8
    /// EvidenceReference bytes compressed with gzip/base64, keyed by their uncompressed SHA-256.
    /// Entries appear at first use; later evaluations refer to that same entry. ReferenceHandler scope
    /// is one property/frame, not the entire runtime graph. Final rule states are the final frame's evaluations.
    /// </summary>
    public static void Write(Stream destination, NasdaqMentorSessionReplayReport report)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(report);
        using var writer = new Utf8JsonWriter(destination);
        var evidenceIds = new HashSet<string>(StringComparer.Ordinal);
        var references = new Dictionary<string, (string Id, int ByteCount)>(ReferenceEqualityComparer.Instance);
        var previousRules = new Dictionary<RuleId, RuleState>();
        long evidenceBytes = 0;
        var maximumEvidenceBytes = 0;
        void Property(string name, object? value)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, value, Options);
        }

        writer.WriteStartObject();
        Property("SchemaVersion", 2);
        Property("EvidenceEncoding", "gzip-base64-utf8; Id = SHA256 of exact uncompressed UTF-8 bytes");
        Property("SessionId", report.SessionId);
        Property("Session", report.Session);
        Property("SuppliedCsvFiles", report.SuppliedCsvFiles.Select(f => new { Timeframe = f.Key, Path = f.Value }));
        Property("Imports", report.Imports);
        Property("InputDiagnostics", report.InputDiagnostics);
        Property("BindingDiagnostics", report.BindingDiagnostics);
        Property("ReplayStartUtc", report.ReplayStartUtc);
        Property("ReplayEndUtc", report.ReplayEndUtc);
        Property("FrameCount", report.Frames.Count);
        Property("FinalVerdict", report.Canonical?.Frames.LastOrDefault()?.Verdict);
        Property("FinalRuleStatesFrameStep", report.Frames.LastOrDefault()?.Strategy.Step);
        Property("Canonical", report.Canonical is { } canonical ? new
        {
            canonical.ConfiguredTimeframes,
            canonical.BlockingRules,
            canonical.MissingRequiredRules,
            canonical.ReadyCount,
            canonical.WaitCount,
            canonical.NoTradeCount,
            canonical.HumanValidationRequiredCount,
            canonical.DataUnavailableCount,
            canonical.CompleteRequiredCoverageCount,
            canonical.IncompleteRequiredCoverageCount,
            canonical.CompleteCoverageDataUnavailableCount,
        } : null);
        writer.WriteStartArray("Frames");
        for (var index = 0; index < report.Frames.Count; index++)
        {
            var frame = report.Frames[index];
            var observation = frame.Strategy;
            var evaluations = new List<object>();
            var transitions = new List<RuleId>();
            writer.WriteStartObject();
            Property("Step", observation.Step);
            Property("AsOfUtc", observation.AsOfUtc);
            writer.WriteStartArray("EvidenceEntries");
            foreach (var evaluation in observation.Evaluations)
            {
                string? id = null;
                if (evaluation.EvidenceReference is { } evidence)
                {
                    if (!references.TryGetValue(evidence, out var reference))
                    {
                        var bytes = Encoding.UTF8.GetBytes(evidence);
                        reference = (Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length);
                        references.Add(evidence, reference);
                        if (evidenceIds.Add(reference.Id))
                        {
                            using var compressed = new MemoryStream();
                            using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                                gzip.Write(bytes);
                            JsonSerializer.Serialize(writer, new
                            {
                                Id = reference.Id,
                                Utf8ByteCount = bytes.Length,
                                Data = Convert.ToBase64String(compressed.GetBuffer(), 0, checked((int)compressed.Length)),
                            }, Options);
                        }
                    }
                    id = reference.Id;
                    evidenceBytes += reference.ByteCount;
                    maximumEvidenceBytes = Math.Max(maximumEvidenceBytes, reference.ByteCount);
                }
                var state = new RuleState(evaluation.DefinitionStatus, evaluation.Result, evaluation.Sequence,
                    evaluation.IsRequired, evaluation.Reason);
                if (!previousRules.TryGetValue(evaluation.RuleId, out var previous) || previous != state)
                    transitions.Add(evaluation.RuleId);
                previousRules[evaluation.RuleId] = state;
                evaluations.Add(new
                {
                    evaluation.RuleId,
                    evaluation.DefinitionStatus,
                    evaluation.Result,
                    evaluation.Sequence,
                    evaluation.IsRequired,
                    evaluation.Reason,
                    evaluation.EvaluatedAtUtc,
                    EvidenceId = id,
                });
            }
            writer.WriteEndArray();
            Property("RuleStateTransitions", transitions);
            Property("Evaluations", evaluations);
            Property("RuleFacts", observation.RuleFacts.Select(r => new { r.RuleId, Fact = (object)r.Fact }));
            Property("WorkflowProgression", observation.WorkflowProgression);
            Property("LifecycleProgression", observation.LifecycleProgression);
            Property("MarketDataObservability", observation.MarketDataObservability);
            Property("Diagnostic", report.Canonical?.Frames[index]);
            Property("Inputs", frame.Inputs.Select(o => (object)o));
            Property("SnapshotState", frame.Snapshot?.GetType().Name ?? "Unavailable");
            Property("Snapshot", frame.Snapshot);
            Property("SnapshotDiagnostic", frame.SnapshotDiagnostic);
            Property("ContactResolutionState", frame.ContactResolution?.GetType().Name ?? "Unavailable");
            Property("ContactResolution", frame.ContactResolution);
            Property("DocumentedExitState", frame.DocumentedExit?.GetType().Name ?? "Unavailable");
            Property("DocumentedExit", frame.DocumentedExit);
            Property("FactualEvaluationState", frame.FactualEvaluation?.GetType().Name ?? "Unavailable");
            Property("FactualEvaluation", frame.FactualEvaluation);
            writer.WriteEndObject();
            writer.Flush();
        }
        writer.WriteEndArray();
        Property("EvidenceStatistics", new { TotalReferencedUtf8Bytes = evidenceBytes, MaximumReferenceUtf8Bytes = maximumEvidenceBytes, UniqueCount = evidenceIds.Count });
        writer.WriteEndObject();
        writer.Flush();
    }

    private sealed record RuleState(RuleDefinitionStatus DefinitionStatus, RuleEvaluationResult Result,
        int Sequence, bool IsRequired, string Reason);
}
