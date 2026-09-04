using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyDefinitions;

public sealed class StrategyDefinitionCatalogTests
{
    private readonly StrategyDefinitionCatalog catalog = new();

    [Fact]
    public void GetAllReturnsOnlyMoneyWayForex()
    {
        var definition = Assert.Single(catalog.GetAll());

        Assert.Equal("moneyway-forex", definition.StrategyId.Value);
        Assert.Equal("forex-0.1.0-draft", definition.Version.Value);
    }

    [Fact]
    public void FindUsesExactStrategyAndVersion()
    {
        var found = catalog.Find(new StrategyId("moneyway-forex"), new StrategyVersion("forex-0.1.0-draft"));

        Assert.Same(catalog.GetAll().Single(), found);
        Assert.Null(catalog.Find(new StrategyId("MoneyWay-Forex"), new StrategyVersion("forex-0.1.0-draft")));
        Assert.Null(catalog.Find(new StrategyId("moneyway-forex"), new StrategyVersion("forex-0.1.0-DRAFT")));
    }

    [Fact]
    public void UnknownStrategyOrVersionHasNoFallback()
    {
        Assert.Null(catalog.Find(new StrategyId("unknown"), new StrategyVersion("forex-0.1.0-draft")));
        Assert.Null(catalog.Find(new StrategyId("moneyway-forex"), new StrategyVersion("unknown")));
    }

    [Fact]
    public void GetAllIsStableAndCannotModifyInternalState()
    {
        var first = catalog.GetAll();
        var exposed = Assert.IsAssignableFrom<ICollection<StrategyDefinition>>(first);

        Assert.Same(first, catalog.GetAll());
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
        Assert.Single(catalog.GetAll());
    }

    [Fact]
    public void NullLookupValuesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => catalog.Find(null!, new StrategyVersion("version")));
        Assert.Throws<ArgumentNullException>(() => catalog.Find(new StrategyId("strategy"), null!));
    }
}
