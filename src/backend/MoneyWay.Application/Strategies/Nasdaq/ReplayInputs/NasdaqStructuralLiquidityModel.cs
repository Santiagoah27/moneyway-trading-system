namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

/// <summary>Only the established structural models admitted by ADR 0025; no provisional or session levels.</summary>
public enum NasdaqStructuralLiquidityModel
{
    OrdinaryTurn,
    ExpansionOrigin,
    DirectionalCollision007,
    HumanCollision008,
    PostCompletionExtreme,
    HumanEstablishedExtreme,
}
