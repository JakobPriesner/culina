using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Http.Features;

namespace Api.Agents;

/// <summary>The host's request pipeline, kept as the host builds it so a tool call can run through it in memory.</summary>
/// <remarks>
/// Takes the place of the framework's <see cref="IApplicationBuilderFactory"/>, which the host asks for exactly one
/// builder and builds it once. That build is the whole pipeline Program.cs describes, middleware and endpoints, so a
/// tool call meets the same forwarded headers, rate limits, authentication, CSRF rules and problem documents as any
/// other request, and nothing here can drift from that order.
/// </remarks>
internal sealed class ApplicationPipeline(IServiceProvider services) : IApplicationBuilderFactory
{
    private readonly ApplicationBuilderFactory factory = new(services);
    private RequestDelegate? built;

    /// <summary>The pipeline, once the host has started.</summary>
    internal RequestDelegate Built =>
        built ?? throw new InvalidOperationException("The host has not built its request pipeline yet.");

    public IApplicationBuilder CreateBuilder(IFeatureCollection serverFeatures) =>
        new Recording(factory.CreateBuilder(serverFeatures), this);

    /// <summary>The framework's builder, unchanged but for keeping what it builds.</summary>
    private sealed class Recording(IApplicationBuilder inner, ApplicationPipeline pipeline) : IApplicationBuilder
    {
        public IServiceProvider ApplicationServices
        {
            get => inner.ApplicationServices;
            set => inner.ApplicationServices = value;
        }

        public IFeatureCollection ServerFeatures => inner.ServerFeatures;

        public IDictionary<string, object?> Properties => inner.Properties;

        // Returns this builder, not the inner one, so middleware added by chained extensions stays recorded.
        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            inner.Use(middleware);

            return this;
        }

        public IApplicationBuilder New() => inner.New();

        public RequestDelegate Build() => pipeline.built = inner.Build();
    }
}
