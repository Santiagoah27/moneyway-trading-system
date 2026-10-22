using MoneyWay.Application.Backtesting.Diagnostics;

namespace MoneyWay.Application.UnitTests.Backtesting;

public sealed class ReplayPerformanceProfileTests
{
    [Fact]
    public void ProfilingDoesNotExposeFutureCandlesOrChangeEvaluatedFacts()
    {
        var time = Strategies.Nasdaq.ReplayInputs.M5Fixture.At(14, 15);
        var bounded = Strategies.Nasdaq.ReplayInputs.M5Fixture.Context(time, [], includeFutureCandles: false);
        var withFuture = Strategies.Nasdaq.ReplayInputs.M5Fixture.Context(time, [], includeFutureCandles: true);
        var useCase = new MoneyWay.Application.StrategyReplay.EvaluateStrategyReplayContextUseCase(
            MoneyWay.Application.StrategyReplay.MoneyWayReplayRuleEvaluators.GetAll());
        var definition = Strategies.Nasdaq.ReplayInputs.LiquidityFixture.Definition;
        var profile = new ReplayPerformanceProfile();
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(useCase.Execute(definition, bounded)),
            System.Text.Json.JsonSerializer.Serialize(useCase.Execute(definition, withFuture, profile)));
        Assert.NotEmpty(profile.Snapshot().Metrics);
    }

    [Fact]
    public void RepeatedCallsAggregateWithoutRetainingSamples()
    {
        var profile = new ReplayPerformanceProfile();
        for (var i = 0; i < 1000; i++)
        {
            using (profile.Measure("operation")) profile.FrameProcessed();
        }
        var snapshot = profile.Snapshot();
        Assert.Equal(1000, snapshot.FramesProcessed);
        var metric = Assert.Single(snapshot.Metrics);
        Assert.Equal("operation", metric.Operation);
        Assert.Equal(1000, metric.StartedCalls);
        Assert.Equal(1000, metric.ClosedCalls);
        Assert.False(metric.Active);
        Assert.True(metric.MaximumClosedMilliseconds <= metric.TotalClosedMilliseconds);
        Assert.Equal(0, snapshot.DroppedMeasurements);
        Assert.Equal("running", snapshot.Status);
    }

    [Fact]
    public void PartialSnapshotReportsActiveNestedCallsWithoutClaimingCompletion()
    {
        var profile = new ReplayPerformanceProfile();
        using (profile.Measure("parent-inclusive"))
        using (profile.Measure("child"))
        {
            var partial = profile.Snapshot();
            Assert.Equal("running", partial.Status);
            Assert.Contains("do not sum parent and child", partial.Accounting);
            Assert.All(partial.Metrics, m => { Assert.True(m.Active); Assert.Equal(1, m.StartedCalls); Assert.Equal(0, m.ClosedCalls); });
        }
        profile.Complete();
        Assert.Equal("completed", profile.Snapshot().Status);
        Assert.All(profile.Snapshot().Metrics, m => { Assert.False(m.Active); Assert.Equal(1, m.ClosedCalls); });
    }

    [Fact]
    public void MetricCardinalityIsBoundedAndOverflowIsVisible()
    {
        var profile = new ReplayPerformanceProfile();
        for (var i = 0; i < ReplayPerformanceProfile.MaximumMetricCount + 10; i++)
            using (profile.Measure("operation/" + i)) { }
        var snapshot = profile.Snapshot();
        Assert.Equal(ReplayPerformanceProfile.MaximumMetricCount, snapshot.Metrics.Count);
        Assert.Equal(10, snapshot.DroppedMeasurements);
        Assert.Equal(snapshot.Metrics.Select(m => m.Operation).OrderBy(n => n, StringComparer.Ordinal), snapshot.Metrics.Select(m => m.Operation));
    }

    [Fact]
    public void FailedAndRepeatedlyDisposedScopesDoNotInventCompletedRuns()
    {
        var profile = new ReplayPerformanceProfile();
        var measurement = profile.Measure("operation");
        measurement.Dispose(); measurement.Dispose();
        profile.Fault();
        Assert.Equal("faulted", profile.Snapshot().Status);
        Assert.Equal(1, Assert.Single(profile.Snapshot().Metrics).ClosedCalls);
    }
}
