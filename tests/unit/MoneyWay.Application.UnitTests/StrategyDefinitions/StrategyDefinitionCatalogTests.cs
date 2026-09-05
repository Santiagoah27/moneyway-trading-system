using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyDefinitions;

public sealed class StrategyDefinitionCatalogTests
{
    private readonly StrategyDefinitionCatalog catalog = new();

    [Fact]
    public void GetAllReturnsForexThenNasdaqWithDistinctIdentifiers()
    {
        var definitions = catalog.GetAll();

        Assert.Equal(2, definitions.Count);
        Assert.Equal(["moneyway-forex", "moneyway-nasdaq"], definitions.Select(item => item.StrategyId.Value));
        Assert.Equal(["forex-0.1.0-draft", "nasdaq-0.1.0-draft"], definitions.Select(item => item.Version.Value));
        Assert.Equal(2, definitions.Select(item => item.StrategyId).Distinct().Count());
    }

    [Theory]
    [InlineData("moneyway-forex", "forex-0.1.0-draft")]
    [InlineData("moneyway-nasdaq", "nasdaq-0.1.0-draft")]
    public void FindReturnsRegisteredExactDefinition(string strategyId, string version)
    {
        var found = catalog.Find(new StrategyId(strategyId), new StrategyVersion(version));

        Assert.NotNull(found);
        Assert.Equal(strategyId, found.StrategyId.Value);
        Assert.Equal(version, found.Version.Value);
    }

    [Theory]
    [InlineData("unknown", "nasdaq-0.1.0-draft")]
    [InlineData("moneyway-nasdaq", "unknown")]
    [InlineData("MoneyWay-Nasdaq", "nasdaq-0.1.0-draft")]
    [InlineData("moneyway-nasdaq", "nasdaq-0.1.0-DRAFT")]
    public void FindHasNoFallbackOrCaseNormalization(string strategyId, string version)
    {
        Assert.Null(catalog.Find(new StrategyId(strategyId), new StrategyVersion(version)));
    }

    [Fact]
    public void GetAllIsStableAndCannotModifyInternalState()
    {
        var first = catalog.GetAll();
        var exposed = Assert.IsAssignableFrom<ICollection<StrategyDefinition>>(first);

        Assert.Same(first, catalog.GetAll());
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
        Assert.Equal(2, catalog.GetAll().Count);
    }

    [Fact]
    public void NullLookupValuesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => catalog.Find(null!, new StrategyVersion("version")));
        Assert.Throws<ArgumentNullException>(() => catalog.Find(new StrategyId("strategy"), null!));
    }
}
