using MoneyWay.Domain;

namespace MoneyWay.Domain.UnitTests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        Assert.Equal("MoneyWay.Domain", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
