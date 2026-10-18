using Domain.Import;

namespace Domain.UnitTests.Import;

public class SourceKindTests
{
    [Theory]
    [InlineData("mealie")]
    [InlineData(" Mealie ")]
    public void Parse_ShouldReadMealie(string written)
    {
        Assert.Same(SourceKind.Mealie, SourceKind.Parse(written));
    }

    [Theory]
    [InlineData("culina")]
    [InlineData(" Culina ")]
    public void Parse_ShouldReadCulina(string written)
    {
        Assert.Same(SourceKind.Culina, SourceKind.Parse(written));
    }

    [Fact]
    public void Connectable_ShouldBeTheAppsAHouseholdCanConnect_AndNotTheOtherOrigins()
    {
        Assert.Equal([SourceKind.Tandoor, SourceKind.Mealie, SourceKind.Culina], SourceKind.Connectable);
        Assert.False(SourceKind.Web.IsConnectable);
        Assert.False(SourceKind.Assistant.IsConnectable);
    }
}
