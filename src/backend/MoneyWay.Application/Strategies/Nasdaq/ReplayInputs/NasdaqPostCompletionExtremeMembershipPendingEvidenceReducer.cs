using MoneyWay.Application.StrategyReplay;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Selects evidence for the original pending event without consuming market candles or resolving members.</summary>
public sealed class NasdaqPostCompletionExtremeMembershipPendingEvidenceReducer
{
    private readonly NasdaqHumanPostCompletionActiveExtremeObservationSelector selector = new();

    public NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult Reduce(
        NasdaqPostCompletionExtremeMembershipPendingState pendingState, StrategyReplayContext context)
    {
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(context);
        var identity = pendingState.Episode.PreviousCompletedEpisode;
        if (identity.StrategyId != context.StrategyId || identity.StrategyVersion != context.StrategyVersion
            || identity.ProviderId != context.ProviderId || identity.Symbol != context.Symbol
            || context.AsOfUtc < pendingState.MarketCursor.CloseTimeUtc)
            throw new ArgumentException("Replay context must match the closed pending turn and its strategy/market identity.", nameof(context));

        return selector.Select(context, pendingState.MembershipEvent) switch
        {
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Missing missing =>
                new NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Missing(pendingState, missing),
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Conflict conflict =>
                new NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.Conflict(pendingState, conflict),
            NasdaqHumanPostCompletionActiveExtremeObservationSelection.Unique unique =>
                new NasdaqPostCompletionExtremeMembershipPendingEvidenceReductionResult.UniqueEvidenceReady(pendingState, unique),
            _ => throw new InvalidOperationException("Unsupported post-completion evidence selection."),
        };
    }
}
