namespace MoneyWay.Backtesting.Tests;

public sealed class BootstrapMarkerTests
{
    [Fact]
    public void BacktestingIsNotImplementedDuringBootstrap()
    {
        Assert.True(true, "This test project is only a bootstrap marker; no backtesting exists yet.");
    }
}
