using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyReplay;
using MoneyWay.Application.StrategyReplay.Workflow;
using MoneyWay.Domain.MarketData;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqStrategyPath { Standard, WickfillScenario2 }

/// <summary>Positive human path classification for one established initial-take setup, never for a whole session.
/// It establishes no context, sweep, reversal, rule result, eligibility or runtime branch activation.</summary>
/// <remarks>Review times describe the source-backed human classification, not a new market event.
/// Earlier reviews are retained, but binding and availability cannot precede canonical lifecycle Start.</remarks>
public sealed class NasdaqHumanBranchApplicabilityObservation : IStrategyReplayInputObservation
{
    public NasdaqHumanBranchApplicabilityObservation(StrategyReplayContextObservation initialTakeObservation,
        NasdaqStrategyPath path, DateTimeOffset reviewEffectiveAtUtc, DateTimeOffset reviewObservedAtUtc, string sourceReference)
    {
        InitialTake = RequireInitialTake(initialTakeObservation);
        if (!Enum.IsDefined(path)) throw new ArgumentOutOfRangeException(nameof(path));
        if (path == NasdaqStrategyPath.WickfillScenario2 && InitialTake.Direction != NasdaqHumanH4PermittedDirection.Sell)
            throw new ArgumentException("Only SELL Scenario 2 applicability is currently supported.", nameof(path));
        if (reviewEffectiveAtUtc.Offset != TimeSpan.Zero || reviewObservedAtUtc.Offset != TimeSpan.Zero
            || reviewObservedAtUtc < reviewEffectiveAtUtc)
            throw new ArgumentException("Source-backed classification times must be UTC with review effective <= review available.");
        NasdaqStructuralLiquidityReference.ValidateProvenance(sourceReference);
        InitialTakeObservation = initialTakeObservation;
        Path = path;
        ReviewEffectiveAtUtc = reviewEffectiveAtUtc;
        ReviewObservedAtUtc = reviewObservedAtUtc;
        SourceReference = sourceReference;
    }

    public StrategyReplayContextObservation InitialTakeObservation { get; }
    public NasdaqLiquidityTakeRuleFact InitialTake { get; }
    public NasdaqStrategyPath Path { get; }
    public NasdaqHumanH4PermittedDirection Direction => InitialTake.Direction;
    public NasdaqDemoSessionIdentity Session => InitialTake.Session;
    public DateTimeOffset ReviewEffectiveAtUtc { get; }
    public DateTimeOffset ReviewObservedAtUtc { get; }
    public DateTimeOffset EffectiveAtUtc => ReviewEffectiveAtUtc > InitialTakeObservation.AsOfUtc ? ReviewEffectiveAtUtc : InitialTakeObservation.AsOfUtc;
    public DateTimeOffset ObservedAtUtc => ReviewObservedAtUtc > InitialTakeObservation.AsOfUtc ? ReviewObservedAtUtc : InitialTakeObservation.AsOfUtc;
    public string SourceReference { get; }
    public StrategyId StrategyId => Session.StrategyId;
    public StrategyVersion StrategyVersion => Session.StrategyVersion;
    public MarketDataProviderId ProviderId => Session.ProviderId;
    public MarketSymbol Symbol => Session.Symbol;

    // Review timestamps/provenance are retained support for the same classification, not revision authority.
    internal bool SameFact(NasdaqHumanBranchApplicabilityObservation other) => Path == other.Path && SameSetup(InitialTake, other.InitialTake);
    internal static bool SameSetup(NasdaqLiquidityTakeRuleFact one, NasdaqLiquidityTakeRuleFact other) =>
        one.Session == other.Session && one.Direction == other.Direction && one.InitiatingTake.SameFact(other.InitiatingTake);

    internal static NasdaqLiquidityTakeRuleFact RequireInitialTake(StrategyReplayContextObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var take = observation.RuleFacts.Where(f => f.RuleId.Value == "NQ-LIQ-003").Select(f => f.Fact)
            .OfType<NasdaqLiquidityTakeRuleFact>().SingleOrDefault();
        var active = observation.LifecycleProgression?.ActiveInstance;
        if (take is null || take.IsSessionInvalidated || !take.Take.SameFact(take.InitiatingTake)
            || take.Direction is not (NasdaqHumanH4PermittedDirection.Buy or NasdaqHumanH4PermittedDirection.Sell)
            || take.Session.StrategyId != MoneyWayNasdaqStrategyDefinition.Instance.StrategyId
            || take.Session.StrategyId != observation.StrategyId || take.Session.StrategyVersion != observation.StrategyVersion
            || take.Session.ProviderId != observation.ProviderId || take.Session.Symbol != observation.Symbol
            || take.Take.ObservedAtUtc > observation.AsOfUtc
            || observation.Evaluations.SingleOrDefault(e => e.RuleId.Value == "NQ-LIQ-003")?.Result != RuleEvaluationResult.Passed
            || observation.WorkflowProgression?.RuleEligibility.SingleOrDefault(e => e.RuleId.Value == "NQ-LIQ-003")?.EstablishesProgression != true
            || active is null || active.StartedAtUtc != observation.AsOfUtc
            || !observation.LifecycleProgression!.TransitionHistory.Any(t => t.Kind == StrategyReplayLifecycleTransitionKind.Start
                && t.InstanceId == active.InstanceId && t.AsOfUtc == observation.AsOfUtc))
            throw new ArgumentException("The canonical Passed initial take and its eligible lifecycle Start observation are required.", nameof(observation));
        return take;
    }
}
