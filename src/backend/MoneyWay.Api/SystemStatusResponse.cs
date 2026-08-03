namespace MoneyWay.Api;

public sealed record SystemStatusResponse(
    string Application,
    string Status,
    string ExecutionMode,
    bool RealMoneyTradingEnabled);
