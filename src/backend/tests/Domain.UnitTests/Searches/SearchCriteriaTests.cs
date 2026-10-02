using Domain.Searches;
using TestSupport;

namespace Domain.UnitTests.Searches;

/// <summary>What a saved search keeps of the filters it was given.</summary>
public class SearchCriteriaTests
{
    [Fact]
    public void Create_ShouldSkipANullTag_WhenTheRequestCarriedOne()
    {
        // Arrange
        // The JSON reader lets ["vegan", null] through, so a null reaches here.
        IReadOnlyList<string> tags = ["vegan", null!];

        // Act
        var criteria = SearchCriteria.Create(null, tags, null, null);

        // Assert
        Assert.Equal(["vegan"], criteria.ShouldBeSuccess().Tags);
    }
}
