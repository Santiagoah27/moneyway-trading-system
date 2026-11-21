using MoneyWay.Application.Research.Nasdaq;

namespace MoneyWay.Worker;

internal static class NasdaqEndToEndResearchOutput
{
    internal static object Project(NasdaqEndToEndResearchResult integrated) => new
    {
        integrated.CandidateId,
        integrated.Version,
        RunId = FormattableString.Invariant($"{integrated.CandidateId}/{integrated.DatasetId}/{integrated.ReplayCutoff.RequestedUtc:O}"),
        integrated.DatasetId,
        integrated.OperatingDate,
        integrated.Provider,
        integrated.Instrument,
        integrated.ReplayStrategyVersion,
        integrated.SourceProvenance,
        integrated.ReplayCutoff,
        integrated.Window,
        integrated.Preparation,
        integrated.RealignmentCutoffs,
        integrated.SessionLiquidity,
        integrated.HistoricalAvailability,
        integrated.LiquidityAuthority,
        integrated.CanonicalAdmissionPermitted,
        integrated.OperationalSignal,
        integrated.Outcome,
        integrated.Summary,
        integrated.Chains,
        Sequence = integrated.Sequence is { } included ? ContextualSequenceResearchOutput.Project(included) : null,
        integrated.Realignment,
        integrated.StructuralStop,
        integrated.StructuralTakeProfit
    };
}
