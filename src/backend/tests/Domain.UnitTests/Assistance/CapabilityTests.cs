using Domain.Assistance;

namespace Domain.UnitTests.Assistance;

/// <summary>What a capability name will and will not be read as.</summary>
public class CapabilityTests
{
    [Fact]
    public void Parse_ShouldReadEveryCapability_ItsOwnCodeNames()
    {
        Assert.Equal(Capability.Improve, Capability.Parse("improve"));
        Assert.Equal(Capability.Draft, Capability.Parse("draft"));
        Assert.Equal(Capability.Read, Capability.Parse("read"));
        Assert.Equal(Capability.Draw, Capability.Parse("draw"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("compose")]
    public void Parse_ShouldRefuse_WhatIsNotOneOfTheFour(string? code)
    {
        // Assert
        // "compose" in particular: three of the four capabilities are one call
        // underneath, and the word for that call is not a word anybody used.
        Assert.Null(Capability.Parse(code));
    }

    [Fact]
    public void All_ShouldRoundTripThroughParse_SoALedgerRowCanAlwaysBeReadBack()
    {
        Assert.All(Capability.All, one => Assert.Equal(one, Capability.Parse(one.Code)));
    }
}
