namespace MoneyWay.Application.Strategies.Nasdaq.Targets;

/// <summary>Identifies the session provenance of a selected MoneyWay Nasdaq target.</summary>
public enum NasdaqSessionTargetOrigin
{
    Asia = 0,
    London = 1,
    CoincidentAsiaAndLondon = 2,
}
