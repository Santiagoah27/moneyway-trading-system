namespace MoneyWay.Application.UnitTests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void ApplicationAndDomainAssembliesCanBeLoaded()
    {
        Assert.Equal("MoneyWay.Application", typeof(Application.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("MoneyWay.Domain", typeof(Domain.AssemblyMarker).Assembly.GetName().Name);
    }
}
