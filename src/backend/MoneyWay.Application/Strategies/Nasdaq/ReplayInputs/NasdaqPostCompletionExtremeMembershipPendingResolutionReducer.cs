using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Resolves selected historical members without reselecting evidence or consuming lifecycle market candles.</summary>
public sealed class NasdaqPostCompletionExtremeMembershipPendingResolutionReducer
{
    private readonly NasdaqHumanPostCompletionActiveExtremeMemberResolver memberResolver = new();

    public NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult Reduce(
        NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady evidenceReady,
        StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(evidenceReady);
        ArgumentNullException.ThrowIfNull(context);
        return memberResolver.Evaluate(evidenceReady.Selection, context) switch
        {
            NasdaqHumanPostCompletionActiveExtremeMemberResolution.DataUnavailable unavailable =>
                new NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.DataUnavailable(evidenceReady, unavailable),
            NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved resolved =>
                new NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved(evidenceReady, resolved),
            _ => throw new InvalidOperationException("Unsupported post-completion member resolution."),
        };
    }
}
