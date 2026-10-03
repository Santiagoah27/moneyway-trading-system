using MoneyWay.Application.MarketData.Candles;
using MoneyWay.Domain.MarketData;

namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Geometry-ready rebuilt candidate and its complete member/evidence lineage; no tracking or validation is materialized.</summary>
public sealed class NasdaqPostCompletionRebuiltCandidateGeometry
{
    internal NasdaqPostCompletionRebuiltCandidateGeometry(NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved membersResolved,
        StructuralTurnGeometryResult candidateGeometry)
    {
        MembersResolved = membersResolved;
        CandidateGeometry = candidateGeometry;
    }

    public NasdaqPostCompletionRebuiltCandidateMemberResolution.MembersResolved MembersResolved { get; }
    public StructuralTurnGeometryResult CandidateGeometry { get; }
    public StructuralCandidateExtremeSide CandidateSide => MembersResolved.PendingState.CandidateSide;
    public Candle MarketCursor => MembersResolved.MarketCursor;
}
