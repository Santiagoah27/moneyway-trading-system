using MoneyWay.Application.Strategies.Nasdaq.HistoricalTrades;
using MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.MentorSessions;

/// <summary>Single-run session linkage at canonical frame boundaries. No market cursor or evaluator implementation.</summary>
public sealed class NasdaqMentorSessionReplay(NasdaqMentorSessionReplayInput input)
{
    private readonly NasdaqMentorSessionReplayInput input = input ?? throw new ArgumentNullException(nameof(input));
    private readonly Dictionary<string, IStrategyReplayInputObservation> bound = new(StringComparer.Ordinal);
    private readonly HashSet<string> rejected = new(StringComparer.Ordinal);
    private readonly List<NasdaqMentorSessionFrame> frames = [];
    private readonly List<NasdaqMentorSessionBindingDiagnostic> diagnostics = [];
    private NasdaqHistoricalTradeSnapshotResult? snapshotResult;
    private NasdaqHistoricalTradeSnapshot? snapshot;
    private bool started;
    public IReadOnlyList<NasdaqMentorSessionFrame> Frames => Array.AsReadOnly(frames.ToArray());
    public IReadOnlyList<NasdaqMentorSessionBindingDiagnostic> Diagnostics => Array.AsReadOnly(diagnostics.ToArray());

    internal void Start(StrategyDefinition definition)
    {
        if (started) throw new InvalidOperationException("Session linkage cannot be reused across runs.");
        if (definition.StrategyId != input.Session.StrategyId || definition.Version != input.Session.StrategyVersion)
            throw new ArgumentException("Session and canonical strategy identities must agree.", nameof(definition));
        started = true;
    }

    internal StrategyReplayContext Bind(StrategyReplayContext context)
    {
        if (context.ProviderId != input.Session.ProviderId || context.Symbol != input.Session.Symbol)
            throw new ArgumentException("Canonical series must match the declared session market identity.", nameof(context));
        if (!input.Session.Matches(context)) return context;
        BindRecords(context);
        context = context.WithInputObservations(bound.Values);
        if (snapshot is null)
        {
            var owner = context.PriorObservations.LastOrDefault();
            var eligibility = owner?.RuleFacts.Where(f => f.RuleId.Value == "NQ-M1-003")
                .Select(f => f.Fact).OfType<NasdaqPreEntryEligibilityRuleFact>().SingleOrDefault();
            snapshotResult = null;
            if (eligibility is not null)
            {
                snapshotResult = new NasdaqHistoricalTradeSnapshotAssembler().Assemble(context, eligibility);
                snapshot = (snapshotResult as NasdaqHistoricalTradeSnapshotResult.Available)?.Snapshot;
                if (snapshot is not null)
                {
                    BindRecords(context);
                    context = context.WithInputObservations(bound.Values);
                }
            }
        }
        return context;
    }

    private void BindRecords(StrategyReplayContext context)
    {
        foreach (var record in input.Evidence.Where(r => r.ObservedAtUtc <= context.AsOfUtc))
        {
            if (bound.ContainsKey(record.RecordId) || rejected.Contains(record.RecordId)) continue;
            IStrategyReplayInputObservation? observation;
            try { observation = record.Bind(context, snapshot); }
            catch (ArgumentException) { Reject(record, "InvalidTypedEvidence", context); continue; }
            if (observation is null) continue;
            if (observation.ObservedAtUtc != record.ObservedAtUtc || observation.StrategyId != context.StrategyId
                || observation.StrategyVersion != context.StrategyVersion || observation.ProviderId != context.ProviderId
                || observation.Symbol != context.Symbol)
            { Reject(record, "EvidenceIdentityOrAvailabilityMismatch", context); continue; }
            var session = observation switch
            {
                NasdaqHumanH4ContextObservation o => o.Session,
                NasdaqHumanStructuralLiquidityObservation o => o.Session,
                NasdaqHumanLiquidityTakeObservation o => o.Session,
                NasdaqHumanRelevantLiquidityTakeObservation o => o.Session,
                NasdaqHumanM5TriggerObservation o => o.Session,
                NasdaqHumanM5FvgObservation o => o.Session,
                NasdaqHumanM5FvgQualityObservation o => o.Session,
                NasdaqHumanM1CorrectiveRetracementObservation o => o.Session,
                NasdaqHumanM1RealignmentObservation o => o.Session,
                NasdaqHistoricalObservedEntryObservation o => o.Session,
                NasdaqHumanStructuralStopLossObservation o => o.Session,
                NasdaqHumanTakeProfitObservation o => o.Session,
                NasdaqRiskExposureObservation o => o.Session,
                NasdaqHistoricalDocumentedExitObservation o => o.Session,
                _ => null,
            };
            if (session is not null && session != input.Session
                || observation is NasdaqPreparationCompletionObservation prep && prep.TradingDay != input.Session.TradingDay)
            { Reject(record, "EvidenceSessionMismatch", context); continue; }
            var required = observation switch
            {
                NasdaqHumanM1RealignmentObservation o => new IReplayRuleFact[] { o.Pullback },
                NasdaqHistoricalObservedEntryObservation o => [o.PreEntryEligibility],
                NasdaqHumanStructuralStopLossObservation o => [o.PreEntryEligibility],
                NasdaqHumanTakeProfitObservation o => [o.StopLoss],
                NasdaqRiskExposureObservation o => [o.PreEntryEligibility, o.StopLoss],
                _ => [],
            };
            if (required.Any(f => !context.PriorObservations.SelectMany(o => o.RuleFacts).Any(r => ReferenceEquals(r.Fact, f)))
                || observation is NasdaqHistoricalDocumentedExitObservation exit && !ReferenceEquals(exit.Snapshot, snapshot))
            { Reject(record, "ForeignCanonicalAncestry", context); continue; }
            bound.Add(record.RecordId, observation);
            diagnostics.Add(new(record.RecordId, "Bound", context.AsOfUtc));
        }
    }

    private void Reject(NasdaqMentorSessionEvidenceRecord record, string code, StrategyReplayContext context)
    { rejected.Add(record.RecordId); diagnostics.Add(new(record.RecordId, code, context.AsOfUtc)); }

    internal void Capture(StrategyReplayContext context, StrategyReplayContextObservation observation)
    {
        NasdaqHistoricalTradeContactResolution? contacts = null;
        NasdaqHistoricalDocumentedExitSelection? exit = null;
        NasdaqHistoricalFactualTradeEvaluation? factual = null;
        if (snapshot is not null && input.Session.Matches(context))
        {
            contacts = new NasdaqHistoricalTradeContactOrderResolver().Resolve(snapshot,
                new NasdaqHistoricalTradeLevelContactCollector().Collect(context, snapshot), context.AsOfUtc);
            exit = new NasdaqHistoricalDocumentedExitObservationSelector().Select(context, snapshot);
            factual = new NasdaqHistoricalFactualTradeEvaluationComposer().Compose(snapshot, exit, context.AsOfUtc, contacts);
        }
        frames.Add(new(observation, context.InputObservations, input.Session.Matches(context) ? snapshotResult : null,
            !input.Session.Matches(context) ? "OutsideSelectedSession" : snapshotResult is null ? "CanonicalEligibilityUnavailable" : null,
            contacts, exit, factual));
        foreach (var record in input.Evidence.Where(r => input.Session.Matches(context) && r.ObservedAtUtc <= context.AsOfUtc
            && !bound.ContainsKey(r.RecordId) && !rejected.Contains(r.RecordId)))
            diagnostics.Add(new(record.RecordId, "PrerequisiteUnavailable", context.AsOfUtc));
    }
}
