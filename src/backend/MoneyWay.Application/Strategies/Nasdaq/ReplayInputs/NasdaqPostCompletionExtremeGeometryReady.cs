using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Deterministic extreme geometry and its full audit chain, before ActiveCorrection initialization.</summary>
public sealed class NasdaqPostCompletionExtremeGeometryReady
{
    internal NasdaqPostCompletionExtremeGeometryReady(
        NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved membersResolved,
        NasdaqHumanPostCompletionActiveExtremeGeometryResult extremeGeometry)
    {
        MembersResolved = membersResolved;
        ExtremeGeometry = extremeGeometry;
    }

    public NasdaqPostCompletionExtremeMembershipPendingResolutionReductionResult.MembersResolved MembersResolved { get; }
    public NasdaqHumanPostCompletionActiveExtremeGeometryResult ExtremeGeometry { get; }
    public NasdaqPostCompletionExtremeMembershipPendingState PendingState => MembersResolved.PendingState;
    public Candle MarketCursor => PendingState.MarketCursor;
}
