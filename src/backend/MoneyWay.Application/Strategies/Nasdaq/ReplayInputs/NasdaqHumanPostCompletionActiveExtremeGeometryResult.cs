using MoneyWay.Application.MarketData.Candles;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Retains the exact resolved membership and all supporting evidence alongside its geometry.</summary>
public sealed class NasdaqHumanPostCompletionActiveExtremeGeometryResult
{
    internal NasdaqHumanPostCompletionActiveExtremeGeometryResult(
        NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved memberResolution,
        StructuralTurnGeometryResult geometry)
    {
        MemberResolution = memberResolution;
        Geometry = geometry;
    }

    public NasdaqHumanPostCompletionActiveExtremeMemberResolution.Resolved MemberResolution { get; }
    public StructuralTurnGeometryResult Geometry { get; }
}
