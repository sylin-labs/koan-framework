using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Koan.Core;

namespace Koan.Flow.Runtime.Dapr.Initialization;

public sealed class KoanAutoRegistrar : IKoanAutoRegistrar
{
    public string ModuleName => "Koan.Flow.Runtime.Dapr";
    public string? ModuleVersion => typeof(KoanAutoRegistrar).Assembly.GetName().Version?.ToString();

    public void Initialize(IServiceCollection services)
    {
        // Prefer Dapr runtime when this package is present in the app.
        services.Replace(ServiceDescriptor.Singleton<Koan.Flow.Runtime.IFlowRuntime, DaprFlowRuntime>());
    }

    public void Describe(Koan.Core.Hosting.Bootstrap.BootReport report, IConfiguration cfg, IHostEnvironment env)
    {
    report.AddModule(ModuleName, ModuleVersion);
    report.AddSetting("provider", "Dapr");
    }
}
