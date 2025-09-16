using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Koan.Core;
using Koan.Core.Modules;
using Koan.Storage;
using Koan.Storage.Abstractions;
using Koan.Storage.Infrastructure;
using Koan.Storage.Options;

namespace Koan.Storage.Initialization;

public sealed class KoanAutoRegistrar : IKoanAutoRegistrar
{
    public string ModuleName => "Koan.Storage";
    public string? ModuleVersion => typeof(KoanAutoRegistrar).Assembly.GetName().Version?.ToString();

    public void Initialize(IServiceCollection services)
    {
        // Bind options and register orchestrator if not already present
        services.AddKoanOptions<StorageOptions>(StorageConstants.Constants.Configuration.Section);

        if (!services.Any(d => d.ServiceType == typeof(IStorageService)))
        {
            services.AddSingleton<IStorageService, StorageService>();
        }
    }

    public void Describe(Koan.Core.Hosting.Bootstrap.BootReport report, IConfiguration cfg, IHostEnvironment env)
    {
        report.AddModule(ModuleName, ModuleVersion);
        var defaultProfile = Core.Configuration.Read(cfg, StorageConstants.Constants.Configuration.Keys.DefaultProfile, string.Empty) ?? string.Empty;
        var fallback = Core.Configuration.Read(cfg, StorageConstants.Constants.Configuration.Keys.FallbackMode, nameof(StorageFallbackMode.SingleProfileOnly));
        var validate = Core.Configuration.Read(cfg, StorageConstants.Constants.Configuration.Keys.ValidateOnStart, true);
        // Profiles are a complex object; report count when available
        var profilesSection = cfg.GetSection($"{StorageConstants.Constants.Configuration.Section}:{StorageConstants.Constants.Configuration.Keys.Profiles}");
        var profilesCount = profilesSection.Exists() ? profilesSection.GetChildren().Count() : 0;
        report.AddSetting("Profiles", profilesCount.ToString());
        if (!string.IsNullOrWhiteSpace(defaultProfile)) report.AddSetting("DefaultProfile", defaultProfile);
        report.AddSetting("FallbackMode", fallback);
        report.AddSetting("ValidateOnStart", validate.ToString());
    }
}
