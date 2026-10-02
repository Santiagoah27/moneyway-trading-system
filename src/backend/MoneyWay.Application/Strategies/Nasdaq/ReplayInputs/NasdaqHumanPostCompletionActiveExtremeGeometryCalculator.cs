using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Derives active HH/LL geometry exclusively from successfully resolved human membership.</summary>
public sealed class NasdaqHumanPostCompletionActiveExtremeGeometryCalculator
{
    private readonly StructuralTurnGeometryCalculator geometryCalculator = new();

    public NasdaqHumanPostCompletionActiveExtremeGeometryResult Evaluate(
        NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var geometry = geometryCalculator.Evaluate(members.SelectedMembers, members.MembershipEvent.Episode.ActiveExtremeSide);
        return new(members, geometry);
    }
}
