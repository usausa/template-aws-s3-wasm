namespace Template.Backend;

using Amazon.Lambda.Annotations;

using Microsoft.Extensions.DependencyInjection;

using Template.Backend.Services;

// Service registration for the generated function wrappers. Runs once per Lambda execution
// environment, so singletons registered here survive across warm invocations.
[LambdaStartup]
public sealed class Startup
{
    // The generated wrapper calls this on an instance, so it cannot be static.
#pragma warning disable CA1822
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<InvocationCounter>();
    }
#pragma warning restore CA1822
}
