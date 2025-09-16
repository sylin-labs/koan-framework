using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using S8.Location.Core.Services;
using Koan.Flow;
using Koan.Flow.Core.Orchestration;
using Koan.Flow.Attributes;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Koan.Flow.Core.Orchestration.Update;

namespace S8.Location.Core.Orchestration;

/// <summary>
/// Simplified Flow orchestrator for Location entities that handles only pre-resolved records.
/// Hash computation and parking logic moved to LocationInterceptor.
/// Resolution logic moved to BackgroundResolutionService.
/// </summary>
[FlowOrchestrator]
public class LocationOrchestrator : FlowOrchestratorBase
{    
    public LocationOrchestrator(IServiceProvider serviceProvider, IConfiguration configuration) 
        : base(serviceProvider.GetRequiredService<ILogger<LocationOrchestrator>>(), configuration, serviceProvider)
    {
    }
    
    protected override void Configure()
    {
        Logger.LogInformation("[LocationOrchestrator] Configuring Flow.OnUpdate handler for pre-resolved Location entities");
        
        // Simple handler for locations that have been resolved by BackgroundResolutionService
        Flow.OnUpdate<S8.Location.Core.Models.Location>((ref S8.Location.Core.Models.Location proposed, S8.Location.Core.Models.Location? current, UpdateMetadata meta) =>
        {
            Logger.LogDebug("[LocationOrchestrator] Processing Location from {Source}: {Address}", 
                meta.SourceSystem, proposed.Address);
            
            // Check if location has been resolved (has AgnosticLocationId from BackgroundResolutionService)
            if (proposed.AgnosticLocationId != null)
            {
                Logger.LogInformation("[LocationOrchestrator] Processing resolved location with AgnosticLocationId: {Id}", 
                    proposed.AgnosticLocationId);
                
                // Location is fully resolved - continue to canonical stage
                return Task.FromResult(Continue("Location is resolved and ready for canonical processing"));
            }
            
            // Location without AgnosticLocationId should have been parked by interceptor
            // If we reach here, something is wrong with the flow
            Logger.LogWarning("[LocationOrchestrator] Unresolved location reached orchestrator - should have been parked: {Address}", 
                proposed.Address);
            
            return Task.FromResult(Skip("Location should have been parked for resolution"));
        });
        
        Logger.LogInformation("[LocationOrchestrator] Flow.OnUpdate handler configured successfully");
    }
}