using Api.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace IntegrationTests.Infrastructure;

/// <summary>Which parts of a path may be written down, and which open something.</summary>
public class SecretPathsTests
{
    [Theory]
    [InlineData("/api/v1/shared-recipes/k3Y_t0ken", "/api/v1/shared-recipes/***")]
    [InlineData("/api/v1/shared-recipes/k3Y_t0ken/image", "/api/v1/shared-recipes/***/image")]
    [InlineData("/api/v1/invitations/BREAD-4711/redemptions", "/api/v1/invitations/***/redemptions")]
    [InlineData("/api/v1/invitations/BREAD-4711", "/api/v1/invitations/***")]
    [InlineData("/api/v2/invitations/BREAD-4711/anything/else", "/api/v2/invitations/***/anything/else")]
    [InlineData("/API/V1/Shared-Recipes/k3Y_t0ken", "/API/V1/Shared-Recipes/***")]
    [InlineData("/shared/k3Y_t0ken", "/shared/***")]
    [InlineData("/join/BREAD-4711", "/join/***")]
    public void Redact_ShouldReplaceTheSecretSegment_UnderEveryRouteThatCarriesOne(string path, string expected)
    {
        var redacted = SecretPaths.Redact(new PathString(path));

        Assert.Equal(expected, redacted);
    }

    [Theory]
    [InlineData("/api/v1/recipes/0198c0de-2222-7000-8000-000000000000")]
    [InlineData("/api/v1/households/0198c0de-2222-7000-8000-000000000000/invitations/0198c0de-3333-7000-8000-000000000000")]
    [InlineData("/api/v1/invitations")]
    [InlineData("/api/v1/shared-recipes")]
    [InlineData("/joined/anything")]
    [InlineData("/")]
    public void Redact_ShouldLeaveThePathAlone_WhenNothingInItOpensAnything(string path)
    {
        var redacted = SecretPaths.Redact(new PathString(path));

        // An id names a row and opens nothing on its own; an operator needs it.
        Assert.Equal(path, redacted);
    }
}
