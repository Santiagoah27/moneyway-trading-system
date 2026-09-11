using MoneyWay.Application.MarketData.PriceLevels;
using MoneyWay.Application.Strategies.Nasdaq.Targets;

namespace MoneyWay.Application.UnitTests.Strategies.Nasdaq.Targets;

public sealed class NasdaqSessionTargetSelectorTests
{
    private readonly NasdaqSessionTargetSelector selector = new();

    [Fact]
    public void SelectsLowerLondonHighAsFirstEligibleBuyTargetEncounteredUpward()
    {
        var result = selector.Select(20_150m, 20_120m, PriceLevelDirection.Upper);

        Assert.Equal(20_120m, result.Price);
        Assert.Equal(PriceLevelDirection.Upper, result.Direction);
        Assert.Equal(NasdaqSessionTargetOrigin.London, result.Origin);
    }

    [Fact]
    public void SelectsLowerAsiaHighAsFirstEligibleBuyTargetEncounteredUpward()
    {
        var result = selector.Select(20_120m, 20_150m, PriceLevelDirection.Upper);

        Assert.Equal(20_120m, result.Price);
        Assert.Equal(NasdaqSessionTargetOrigin.Asia, result.Origin);
    }

    [Fact]
    public void SelectsHigherLondonLowAsFirstEligibleSellTargetEncounteredDownward()
    {
        var result = selector.Select(19_800m, 19_840m, PriceLevelDirection.Lower);

        Assert.Equal(19_840m, result.Price);
        Assert.Equal(PriceLevelDirection.Lower, result.Direction);
        Assert.Equal(NasdaqSessionTargetOrigin.London, result.Origin);
    }

    [Fact]
    public void SelectsHigherAsiaLowAsFirstEligibleSellTargetEncounteredDownward()
    {
        var result = selector.Select(19_840m, 19_800m, PriceLevelDirection.Lower);

        Assert.Equal(19_840m, result.Price);
        Assert.Equal(NasdaqSessionTargetOrigin.Asia, result.Origin);
    }

    [Fact]
    public void EqualBuyTargetsProduceOneSelectionWithCoincidentProvenance()
    {
        var result = selector.Select(20_120m, 20_120m, PriceLevelDirection.Upper);

        Assert.Equal(20_120m, result.Price);
        Assert.Equal(PriceLevelDirection.Upper, result.Direction);
        Assert.Equal(NasdaqSessionTargetOrigin.CoincidentAsiaAndLondon, result.Origin);
    }

    [Fact]
    public void EqualSellTargetsProduceOneSelectionWithCoincidentProvenance()
    {
        var result = selector.Select(19_840m, 19_840m, PriceLevelDirection.Lower);

        Assert.Equal(19_840m, result.Price);
        Assert.Equal(PriceLevelDirection.Lower, result.Direction);
        Assert.Equal(NasdaqSessionTargetOrigin.CoincidentAsiaAndLondon, result.Origin);
    }

    [Fact]
    public void SmallestPracticalDecimalDifferenceRemainsDistinctWithoutTolerance()
    {
        const decimal asia = 100m;
        const decimal london = 100.0000000000000000000000001m;

        var result = selector.Select(asia, london, PriceLevelDirection.Upper);

        Assert.Equal(asia, result.Price);
        Assert.Equal(NasdaqSessionTargetOrigin.Asia, result.Origin);
    }

    [Fact]
    public void RepeatedSelectionIsValueEquivalentAndDoesNotRetainState()
    {
        var first = selector.Select(20_150m, 20_120m, PriceLevelDirection.Upper);
        var second = selector.Select(20_150m, 20_120m, PriceLevelDirection.Upper);

        Assert.Equal(first, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void RejectsUnknownDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => selector.Select(20_150m, 20_120m, (PriceLevelDirection)99));
    }
}
