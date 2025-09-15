# Background Services

**Document Type**: Reference Documentation (REF)  
**Target Audience**: Developers, Architects, AI Agents  
**Last Updated**: 2025-01-10  
**Framework Version**: v0.3.0+
**Implementation Status**: ✅ **IMPLEMENTED** - Fully integrated into Koan.Core

---

## 🏗️ Koan Background Services Reference

This document provides comprehensive reference information for **Koan Background Services**, a pillar that enables elegant background service development with auto-discovery, fluent APIs, and seamless integration with the Koan Framework ecosystem.

---

## 🎯 Overview

**Koan Background Services** standardizes background service patterns with exceptional developer experience. Services are automatically discovered, registered, and managed with minimal scaffolding, while providing powerful "poking" capabilities for real-time responsiveness.

### Core Philosophy

- **Zero Configuration**: Services auto-register and start without manual setup
- **Fluent Communication**: Beautiful, chainable APIs for service interaction  
- **Event-Driven**: Rich event subscription and emission patterns
- **Responsive**: Services can be "poked" to respond immediately to external events
- **Koan-Aligned**: Follows all framework architectural principles

---

## 🧱 Core Components

### Package: `Koan.Core` (Background Services)

#### Key Interfaces

```csharp
// Primary interface for background services
public interface IKoanBackgroundService
{
    string Name { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

// Services that can be triggered on-demand
public interface IKoanPokableService : IKoanBackgroundService
{
    Task HandleCommandAsync(ServiceCommand command, CancellationToken cancellationToken = default);
    IReadOnlyCollection<Type> SupportedCommands { get; }
}

// Periodic execution pattern
public interface IKoanPeriodicService : IKoanBackgroundService
{
    TimeSpan Period { get; }
    TimeSpan InitialDelay => TimeSpan.Zero;
    bool RunOnStartup => false;
}

// Startup services with execution order
public interface IKoanStartupService : IKoanBackgroundService
{
    int StartupOrder { get; }
}
```

#### Key Base Classes

```csharp
// Standard base class with health checks and logging
public abstract class KoanBackgroundServiceBase : IKoanBackgroundService, IHealthContributor
{
    protected readonly ILogger Logger;
    protected readonly IConfiguration Configuration;

    protected KoanBackgroundServiceBase(ILogger logger, IConfiguration configuration);

    public virtual string Name => GetType().Name;
    public virtual bool IsCritical => false;

    public abstract Task ExecuteAsync(CancellationToken cancellationToken);
    public virtual Task<HealthReport> CheckAsync(CancellationToken cancellationToken = default);
}

// **MAIN BASE CLASS** - Base class for services supporting fluent API and events
// This is what most services inherit from in the current implementation
public abstract class KoanFluentServiceBase : KoanBackgroundServiceBase
{
    protected KoanFluentServiceBase(ILogger logger, IConfiguration configuration)
        : base(logger, configuration) { }

    // Abstract method that concrete services implement
    public abstract Task ExecuteCoreAsync(CancellationToken cancellationToken);
    
    // Final ExecuteAsync calls ExecuteCoreAsync - do not override
    public sealed override Task ExecuteAsync(CancellationToken cancellationToken)
        => ExecuteCoreAsync(cancellationToken);
    
    // Event emission
    protected async Task EmitEventAsync(string eventName, object? eventArgs = null);
    
    // Action execution support (for fluent API)
    public virtual async Task ExecuteActionAsync(string actionName, object? parameters, CancellationToken cancellationToken);
}

// Periodic service with pokeable capabilities
public abstract class KoanPokablePeriodicServiceBase : KoanFluentServiceBase, IKoanPeriodicService
{
    public abstract TimeSpan Period { get; }
    public virtual TimeSpan InitialDelay => TimeSpan.Zero;
    public virtual bool RunOnStartup => false;

    protected abstract Task ExecutePeriodicAsync(CancellationToken cancellationToken);
    protected virtual Task OnTriggerNow(CancellationToken cancellationToken);
    protected virtual Task OnProcessBatch(int? batchSize, string? filter, CancellationToken cancellationToken);
}
```

---

## 🏷️ Attribute-Based Configuration

### Service Configuration Attributes

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class KoanBackgroundServiceAttribute : Attribute
{
    public bool Enabled { get; set; } = true;
    public string? ConfigurationSection { get; set; }
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    public int Priority { get; set; } = 100;
    public bool RunInDevelopment { get; set; } = true;
    public bool RunInProduction { get; set; } = true;
    public bool RunInTesting { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Class)]
public class KoanPeriodicServiceAttribute : KoanBackgroundServiceAttribute
{
    public int IntervalSeconds { get; set; } = 60;
    public int InitialDelaySeconds { get; set; } = 0;
    public bool RunOnStartup { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Class)]
public class KoanStartupServiceAttribute : KoanBackgroundServiceAttribute
{
    public int StartupOrder { get; set; } = 100;
    public bool ContinueOnFailure { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
}
```

### Action and Event Declaration

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class ServiceActionAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }
    public bool RequiresParameters { get; set; }
    public Type? ParametersType { get; set; }
    
    public ServiceActionAttribute(string name);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ServiceEventAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }
    public Type? EventArgsType { get; set; }
    
    public ServiceEventAttribute(string name);
}
```

### Constants for Actions and Events

To eliminate magic strings, use the provided constants:

```csharp
// Event constants
using Koan.Core.Events;
[ServiceEvent(KoanServiceEvents.Flow.EntityProcessed)]
[ServiceEvent(KoanServiceEvents.Health.ProbeScheduled)]
[ServiceEvent(KoanServiceEvents.Outbox.Processed)]

// Action constants  
using Koan.Core.Actions;
[ServiceAction(KoanServiceActions.Health.ForceProbe)]
[ServiceAction(KoanServiceActions.Flow.TriggerProcessing)]
[ServiceAction(KoanServiceActions.Outbox.ProcessBatch)]
```

---

## 🚀 Fluent API Reference

### Static Entry Point

```csharp
// Main fluent API entry point
public static class KoanServices<T> where T : IKoanBackgroundService
{
    // Execute service actions
    public static IServiceActionBuilder Do(string action, object? parameters = null);
    
    // Subscribe to service events
    public static IServiceEventBuilder On(string eventName);
    
    // Query service information
    public static IServiceQueryBuilder Query();
}
```

### Chainable Builders

```csharp
// Unified builder supporting method chaining
public interface IServiceBuilder<T> where T : IKoanBackgroundService
{
    IServiceActionBuilder Do(string action, object? parameters = null);
    IServiceEventBuilder On(string eventName);
    IServiceQueryBuilder Query();
}

// Action execution with fluent configuration
public interface IServiceActionBuilder : IServiceBuilder<T>
{
    Task ExecuteAsync(CancellationToken cancellationToken = default);
    IServiceActionBuilder WithPriority(int priority);
    IServiceActionBuilder WithTimeout(TimeSpan timeout);
    IServiceActionBuilder WithCorrelationId(string correlationId);
}

// Event subscription with chainable handlers
public interface IServiceEventBuilder : IServiceBuilder<T>
{
    // Unified Do() method for event handlers
    IServiceEventBuilder Do<TEventArgs>(Func<TEventArgs, Task> handler);
    IServiceEventBuilder Do(Func<Task> handler);
    
    // Event configuration
    IServiceEventBuilder Once();
    IServiceEventBuilder WithFilter<TEventArgs>(Func<TEventArgs, bool> filter);
    
    // Execute all subscriptions
    Task<IDisposable> SubscribeAsync();
}

// Service information queries
public interface IServiceQueryBuilder
{
    Task<ServiceStatus> GetStatusAsync();
    Task<ServiceHealth> GetHealthAsync(); 
    Task<ServiceInfo> GetInfoAsync();
}
```

---

## 💡 Usage Patterns

### Basic Background Service

```csharp
[KoanBackgroundService(Enabled = true, RunInProduction = true)]
public class SystemHealthMonitor : KoanFluentServiceBase
{
    public SystemHealthMonitor(ILogger<SystemHealthMonitor> logger, IConfiguration configuration)
        : base(logger, configuration) { }

    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        using var periodicTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        
        while (!cancellationToken.IsCancellationRequested && await periodicTimer.WaitForNextTickAsync(cancellationToken))
        {
            await CheckSystemHealth(cancellationToken);
        }
    }

    private async Task CheckSystemHealth(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Performing system health check...");
        // Health check logic
    }
}
```

### Periodic Service

```csharp
[KoanPeriodicService(IntervalSeconds = 3600, RunOnStartup = true)]
public class DataCleanupService : KoanPeriodicServiceBase
{
    public override TimeSpan Period => TimeSpan.FromHours(1);
    public override bool RunOnStartup => true;

    public DataCleanupService(ILogger<DataCleanupService> logger, IConfiguration configuration)
        : base(logger, configuration) { }

    protected override async Task ExecutePeriodicAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting data cleanup...");
        
        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-30);
        var deletedCount = await LogEntry.Query()
            .Where(log => log.Created < cutoffDate)
            .DeleteAsync(cancellationToken);

        Logger.LogInformation("Data cleanup completed. Deleted {Count} old log entries", deletedCount);
    }
}
```

### Fluent Service with Actions and Events

```csharp
using Koan.Core.Events;

[KoanBackgroundService(RunInProduction = true)]
[ServiceEvent(KoanServiceEvents.Translation.Started, EventArgsType = typeof(TranslationEventArgs))]
[ServiceEvent(KoanServiceEvents.Translation.Completed, EventArgsType = typeof(TranslationEventArgs))]
[ServiceEvent(KoanServiceEvents.Translation.Failed, EventArgsType = typeof(TranslationErrorArgs))]
public class TranslationService : KoanFluentServiceBase
{
    public TranslationService(ILogger<TranslationService> logger, IConfiguration configuration) 
        : base(logger, configuration) { }

    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        // Main service loop - could listen for work or wait indefinitely
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    [ServiceAction("translate", RequiresParameters = true, ParametersType = typeof(TranslationOptions))]
    public async Task TranslateAsync(TranslationOptions options, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting translation from {From} to {To}", options.From, options.To);
        
        await EmitEventAsync(KoanServiceEvents.Translation.Started, new TranslationEventArgs 
        { 
            FileId = options.FileId, 
            From = options.From, 
            To = options.To 
        });

        try
        {
            await DoTranslationWork(options, cancellationToken);
            
            await EmitEventAsync(KoanServiceEvents.Translation.Completed, new TranslationEventArgs 
            { 
                FileId = options.FileId, 
                From = options.From, 
                To = options.To 
            });
        }
        catch (Exception ex)
        {
            await EmitEventAsync(KoanServiceEvents.Translation.Failed, new TranslationErrorArgs 
            { 
                FileId = options.FileId, 
                Error = ex.Message 
            });
            throw;
        }
    }

    [ServiceAction("check-queue")]
    public async Task CheckQueueAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Checking translation queue...");
        // Queue checking logic
    }

    private async Task DoTranslationWork(TranslationOptions options, CancellationToken cancellationToken)
    {
        // Translation implementation
    }
}

public record TranslationOptions(string FileId, string From, string To);
public record TranslationEventArgs 
{ 
    public string FileId { get; init; } = ""; 
    public string From { get; init; } = ""; 
    public string To { get; init; } = ""; 
}
public record TranslationErrorArgs 
{ 
    public string FileId { get; init; } = ""; 
    public string Error { get; init; } = ""; 
}
```

### Startup Service

```csharp
[KoanStartupService(StartupOrder = 1, ContinueOnFailure = false, TimeoutSeconds = 60)]
public class DatabaseMigrationService : KoanFluentServiceBase, IKoanStartupService
{
    private readonly IDataContext _dataContext;
    
    public int StartupOrder => 1;

    public DatabaseMigrationService(
        ILogger<DatabaseMigrationService> logger, 
        IConfiguration configuration, 
        IDataContext dataContext)
        : base(logger, configuration)
    {
        _dataContext = dataContext;
    }

    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Starting database migration...");
        await _dataContext.MigrateDatabaseAsync(cancellationToken);
        Logger.LogInformation("Database migration completed successfully");
    }

    public override async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        return await _dataContext.CanConnectAsync(cancellationToken);
    }
}
```

---

## 🎯 Fluent API Usage Examples

### Service Action Execution

```csharp
// Execute single action
await KoanServices<TranslationService>
    .Do("translate", new TranslationOptions("file.txt", "auto", "pt-BR"))
    .WithPriority(10)
    .WithTimeout(TimeSpan.FromMinutes(30))
    .ExecuteAsync();

// Execute multiple actions in sequence
await KoanServices<DataSyncService>
    .Do("sync-source", new SyncOptions("users", DateTime.Today))
    .Do("validate-data")
    .Do("generate-report", new ReportOptions("daily"))
    .ExecuteAsync();
```

### Event Subscription (Chainable)

```csharp
// Multiple event subscriptions in one chain
await KoanServices<TranslationService>
    .On("TranslationCompleted").Do<TranslationEventArgs>(async args => 
        await SendNotificationEmail(args.FileId, "Translation completed!"))
    .On("TranslationFailed").Do<TranslationErrorArgs>(async args => 
        await SendErrorNotification(args.FileId, args.Error))
    .SubscribeAsync();

// Event subscriptions with filters and configuration
await KoanServices<ImageProcessingService>
    .On("ProcessingStarted").Do(async () => 
        await LogActivity("Image processing started"))
    .On("ProcessingProgress")
        .WithFilter<ProcessingProgressArgs>(args => args.CompletedSteps % 10 == 0)
        .Do<ProcessingProgressArgs>(async args => 
            await UpdateProgress(args.ImageId, args.CompletedSteps, args.TotalSteps))
    .On("ProcessingCompleted").Do<ProcessingCompletedArgs>(async args => 
        await NotifyCompletion(args.ImageId))
    .On("ProcessingFailed")
        .Once() // Only handle the first failure
        .Do<ProcessingErrorArgs>(async args => 
            await HandleProcessingError(args.ImageId, args.Error))
    .SubscribeAsync();
```

### Mixed Actions and Events

```csharp
// Execute actions and set up event subscriptions together
await KoanServices<DataSyncService>
    .Do("sync-source", new SyncOptions("users", DateTime.Today))
    .On("SyncCompleted").Do<SyncEventArgs>(async args =>
        await LogSyncSuccess(args.Source, args.RecordCount))
    .On("SyncFailed").Do<SyncErrorArgs>(async args =>
        await AlertSyncFailure(args.Source, args.Error))
    .SubscribeAsync();
```

### Service Communication Patterns

```csharp
// File upload triggering translation
public class FileController : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromQuery] string targetLanguage = "pt-BR")
    {
        var fileId = await SaveFileAsync(file);

        // Immediately trigger translation service
        await KoanServices<TranslationService>
            .Do("translate", new TranslationOptions(fileId, "auto", targetLanguage))
            .WithPriority(10)
            .ExecuteAsync();

        return Ok(new { FileId = fileId });
    }
}

// Webhook triggering data sync
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    [HttpPost("data-changed")]
    public async Task<IActionResult> OnDataChanged([FromBody] DataChangeNotification notification)
    {
        await KoanServices<DataSyncService>
            .Do("sync-source", new SyncOptions(notification.Source, notification.Since))
            .WithPriority(8)
            .ExecuteAsync();

        return Ok();
    }
}
```

### Workflow Orchestration

```csharp
// Complex workflow with multiple service coordination
public class WorkflowOrchestrator : KoanFluentServiceBase
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Set up file processing workflow
        await KoanServices<FileProcessingService>
            .On("FileUploaded").Do<FileUploadedArgs>(async args => 
            {
                await KoanServices<TranslationService>
                    .Do("translate", new TranslationOptions(args.FileId, "auto", "en"))
                    .ExecuteAsync();
            })
            .On("FileProcessed").Do<FileProcessedArgs>(async args =>
            {
                await KoanServices<ImageProcessingService>
                    .Do("generate-thumbnail", new ThumbnailOptions(args.FileId, 200, 200))
                    .ExecuteAsync();
            })
            .SubscribeAsync();

        // Handle translation results
        await KoanServices<TranslationService>
            .On("TranslationCompleted").Do<TranslationEventArgs>(async args =>
                await EmitEventAsync("WorkflowStepCompleted", args))
            .On("TranslationFailed").Do<TranslationErrorArgs>(async args =>
                await EmitEventAsync("WorkflowStepFailed", args))
            .SubscribeAsync();

        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
```

---

## 📡 HTTP API Integration

### Built-in Service Control Endpoints

The framework automatically provides HTTP endpoints for service management:

```http
# Trigger service immediately
POST /api/services/{serviceName}/trigger

# Check service queue
POST /api/services/{serviceName}/check-queue  

# Process batch with parameters
POST /api/services/{serviceName}/process-batch
Content-Type: application/json
{
  "batchSize": 50,
  "filter": "high-priority"
}

# Get service status
GET /api/services/{serviceName}/status
```

### Custom Service Controller

```csharp
[Route("api/services")]
[ApiController]
public class ServiceCommandController : ControllerBase
{
    private readonly IServiceCommandBus _commandBus;

    [HttpPost("{serviceName}/trigger")]
    public async Task<IActionResult> TriggerService(string serviceName)
    {
        await _commandBus.SendCommandAsync(serviceName, new TriggerNowCommand { ServiceName = serviceName });
        return Ok(new { Message = $"Service '{serviceName}' triggered successfully" });
    }

    [HttpPost("{serviceName}/check-queue")]
    public async Task<IActionResult> CheckQueue(string serviceName)
    {
        await _commandBus.SendCommandAsync(serviceName, new CheckQueueCommand { ServiceName = serviceName });
        return Ok(new { Message = $"Service '{serviceName}' queue check triggered" });
    }
}
```

---

## ⚙️ Configuration

### appsettings.json Configuration

```json
{
  "Koan": {
    "BackgroundServices": {
      "Enabled": true,
      "StartupTimeoutSeconds": 120,
      "FailFastOnStartupFailure": true,
      "Services": {
        "TranslationService": {
          "Enabled": true,
          "Settings": {
            "MaxConcurrentTranslations": 5,
            "SupportedLanguages": ["en", "pt-BR", "es", "fr"]
          }
        },
        "DataCleanupService": {
          "Enabled": true,
          "IntervalSeconds": 7200,
          "Settings": {
            "RetentionDays": 30,
            "BatchSize": 1000
          }
        },
        "SystemHealthMonitor": {
          "Enabled": true,
          "Settings": {
            "CheckIntervalMinutes": 5,
            "AlertThresholds": {
              "CpuPercent": 80,
              "MemoryPercent": 85
            }
          }
        }
      }
    }
  }
}
```

### Configuration Options Classes

```csharp
public class KoanBackgroundServiceOptions
{
    public const string SectionName = "Koan:BackgroundServices";
    
    public bool Enabled { get; set; } = true;
    public int StartupTimeoutSeconds { get; set; } = 120;
    public bool FailFastOnStartupFailure { get; set; } = true;
    public Dictionary<string, ServiceConfiguration> Services { get; set; } = new();
}

public class ServiceConfiguration
{
    public bool Enabled { get; set; } = true;
    public int? IntervalSeconds { get; set; }
    public int? StartupOrder { get; set; }
    public int? Priority { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
}
```

---

## 🔍 Health Check Integration

All background services automatically contribute to Koan's health check system:

### Built-in Health Endpoints

- `GET /health` - Overall system health including background services
- `GET /health/live` - Liveness probe (services are running)
- `GET /health/ready` - Readiness probe (services are ready to accept work)

### Custom Health Checks

```csharp
public class CustomBackgroundService : KoanBackgroundServiceBase
{
    public override async Task<HealthReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Custom health check logic
            var isOperational = await CheckOperationalStatus();
            
            return isOperational 
                ? HealthReport.Healthy("Service is operating normally")
                : HealthReport.Degraded("Service is experiencing issues");
        }
        catch (Exception ex)
        {
            return HealthReport.Unhealthy("Service health check failed", ex);
        }
    }
}
```

---

## 🔧 Advanced Features

### Service Discovery and Registry

```csharp
// Query service information
var status = await KoanServices<TranslationService>
    .Query()
    .GetStatusAsync();

var health = await KoanServices<TranslationService>
    .Query()
    .GetHealthAsync();

var info = await KoanServices<TranslationService>
    .Query()
    .GetInfoAsync();
```

### Distributed Service Communication

Services can communicate across process boundaries using Koan's messaging system:

```csharp
// Automatically works both in-process and distributed
await KoanServices<RemoteTranslationService>
    .Do("translate", options)
    .ExecuteAsync(); // Routes via message bus if service is remote
```

### Testing Support

```csharp
[Test]
public async Task Should_Handle_Translation_Workflow()
{
    var completedEvents = new List<TranslationEventArgs>();
    var failedEvents = new List<TranslationErrorArgs>();

    // Set up event capture
    var subscription = await KoanServices<TranslationService>
        .On("TranslationCompleted").Do<TranslationEventArgs>(async args => completedEvents.Add(args))
        .On("TranslationFailed").Do<TranslationErrorArgs>(async args => failedEvents.Add(args))
        .SubscribeAsync();

    // Execute action
    await KoanServices<TranslationService>
        .Do("translate", new TranslationOptions("test.txt", "en", "pt-BR"))
        .ExecuteAsync();

    // Verify results
    Assert.That(completedEvents, Has.Count.EqualTo(1));
    Assert.That(failedEvents, Has.Count.EqualTo(0));
    
    subscription.Dispose();
}
```

---

## 🚀 Getting Started

### 1. Installation

**Background Services are included in Koan.Core** - no separate package needed:

```bash
dotnet add package Koan.Core
# Background services are automatically available
```

### 2. Service Registration

Services are automatically discovered and registered when you call:

```csharp
// Program.cs
builder.Services.AddKoan(); // Discovers and registers all background services
```

### 3. Create Your First Service

```csharp
[KoanBackgroundService]
public class MyFirstService : KoanBackgroundServiceBase
{
    public MyFirstService(ILogger<MyFirstService> logger, IConfiguration configuration)
        : base(logger, configuration) { }

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("My first Koan background service is running!");
        
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            Logger.LogInformation("Service heartbeat at {Time}", DateTimeOffset.UtcNow);
        }
    }
}
```

### 4. Add Fluent API Support

```csharp
using Koan.Core.Events;

[ServiceEvent("WorkCompleted")]
public class MyFluentService : KoanFluentServiceBase
{
    public MyFluentService(ILogger<MyFluentService> logger, IConfiguration configuration)
        : base(logger, configuration) { }

    [ServiceAction("do-work")]
    public async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Doing important work...");
        await Task.Delay(1000, cancellationToken);
        await EmitEventAsync("WorkCompleted", new { CompletedAt = DateTimeOffset.UtcNow });
    }

    // Override ExecuteCoreAsync, not ExecuteAsync
    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
```

### 5. Use the Fluent API

```csharp
// Trigger work
await KoanServices<MyFluentService>
    .Do("do-work")
    .ExecuteAsync();

// Subscribe to events
await KoanServices<MyFluentService>
    .On("WorkCompleted").Do(async () => 
        Logger.LogInformation("Work was completed!"))
    .SubscribeAsync();
```

---

## 📋 Best Practices

### Service Design

1. **Single Responsibility**: Each service should have one clear purpose
2. **Idempotent Operations**: Actions should be safe to retry
3. **Graceful Cancellation**: Always respect cancellation tokens
4. **Structured Logging**: Use structured logging for observability
5. **Health Checks**: Implement meaningful health checks

### Constructor Patterns

**Important**: For services that need to be instantiated generically (like FlowOrchestratorBase), use `ILogger` instead of `ILogger<T>` to avoid generic type instantiation issues:

```csharp
// ✅ Correct - works with generic instantiation
protected MyService(ILogger logger, IConfiguration configuration)
    : base(logger, configuration)

// ❌ Problematic - can cause generic instantiation errors
protected MyService(ILogger<MyService> logger, IConfiguration configuration) 
    : base(logger, configuration)
```

This pattern is used in FlowOrchestratorBase and should be followed when services are instantiated through reflection or generic mechanisms.

### Event Design

1. **Meaningful Names**: Use descriptive event names
2. **Versioned Payloads**: Design event payloads for evolution
3. **Immutable Args**: Use record types for event arguments
4. **Error Events**: Always emit failure events for error handling

### Performance

1. **Async All The Way**: Use async/await throughout
2. **Bounded Parallelism**: Limit concurrent operations appropriately  
3. **Memory Management**: Properly dispose of resources
4. **Efficient Polling**: Use `PeriodicTimer` for polling scenarios

### Testing

1. **Unit Tests**: Test service logic independently
2. **Integration Tests**: Test service interactions
3. **Event Testing**: Verify event emission and subscription
4. **Health Testing**: Test health check implementations

---

## 🎯 Architecture Integration

**Koan Background Services** integrates seamlessly with other Koan pillars:

- **Core**: Auto-registration, health checks, configuration
- **Web**: HTTP endpoints for service control
- **Data**: Entity operations within services
- **Messaging**: Event-driven communication between services
- **AI**: Background AI processing and workflows
- **Storage**: File processing and media workflows
- **Flow**: Data pipeline integration

---

## 🔍 Real-World Examples

### Flow Orchestrator (Koan.Flow.Core)

```csharp
using Koan.Core.Events;
using Koan.Core.BackgroundServices;

[FlowOrchestrator]
[KoanBackgroundService(RunInProduction = true)]
[ServiceEvent(KoanServiceEvents.Flow.EntityProcessed, EventArgsType = typeof(FlowEntityProcessedEventArgs))]
[ServiceEvent(KoanServiceEvents.Flow.EntityParked, EventArgsType = typeof(FlowEntityParkedEventArgs))]
[ServiceEvent(KoanServiceEvents.Flow.EntityFailed, EventArgsType = typeof(FlowEntityFailedEventArgs))]
public abstract class FlowOrchestratorBase : KoanFluentServiceBase, IFlowOrchestrator
{
    protected readonly IServiceProvider ServiceProvider;

    // NOTE: Constructor signature change - uses ILogger instead of ILogger<T> for generic instantiation
    protected FlowOrchestratorBase(ILogger logger, IConfiguration configuration, IServiceProvider serviceProvider)
        : base(logger, configuration)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Configure();
    }
    
    protected virtual void Configure() { }

    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        // Auto-subscribe to "Koan.Flow.FlowEntity" queue
        // Process flow entities with event emission
        // Implementation details omitted for brevity
    }

    // Methods emit events using constants
    protected async Task EmitEntityProcessedAsync(string entityId, string entityType)
    {
        await EmitEventAsync(KoanServiceEvents.Flow.EntityProcessed, 
            new FlowEntityProcessedEventArgs { EntityId = entityId, EntityType = entityType });
    }
}
```

### Outbox Processor (Koan.Data.Cqrs)

```csharp
using Koan.Core.Events;
using Koan.Core.Actions;

[KoanBackgroundService(RunInProduction = true)]
[ServiceEvent(KoanServiceEvents.Outbox.Processed, EventArgsType = typeof(OutboxProcessedEventArgs))]
[ServiceEvent(KoanServiceEvents.Outbox.Failed, EventArgsType = typeof(OutboxFailedEventArgs))]
internal sealed class OutboxProcessor : KoanFluentServiceBase
{
    private readonly IOutboxStore _outbox;
    private readonly CqrsOptions _options;
    private readonly ICqrsRouting _routing;
    private readonly IServiceProvider _serviceProvider;

    public OutboxProcessor(
        ILogger<OutboxProcessor> logger, 
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IOutboxStore outbox, 
        IOptions<CqrsOptions> options)
        : base(logger, configuration)
    { 
        _serviceProvider = serviceProvider;
        _outbox = outbox; 
        _options = options.Value; 
        _routing = serviceProvider.GetRequiredService<ICqrsRouting>(); 
    }

    public override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("OutboxProcessor started - processing CQRS outbox entries");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batch = await _outbox.DequeueAsync(100, stoppingToken);
                if (batch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                    continue;
                }

                await ProcessBatch(batch, stoppingToken);
                await EmitEventAsync(KoanServiceEvents.Outbox.Processed, 
                    new OutboxProcessedEventArgs { ProcessedCount = batch.Count });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing outbox batch");
                await EmitEventAsync(KoanServiceEvents.Outbox.Failed, 
                    new OutboxFailedEventArgs { Error = ex.Message });
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    [ServiceAction(KoanServiceActions.Outbox.ProcessBatch)]
    public async Task ProcessBatchAsync(int? batchSize = null, CancellationToken cancellationToken = default)
    {
        var size = batchSize ?? 100;
        var batch = await _outbox.DequeueAsync(size, cancellationToken);
        await ProcessBatch(batch, cancellationToken);
    }

    private async Task ProcessBatch(List<OutboxEntry> batch, CancellationToken cancellationToken)
    {
        // Process batch logic
    }
}
```

### Health Probe Scheduler (Koan.Core)

```csharp
using Koan.Core.Events;
using Koan.Core.Actions;

[KoanBackgroundService(RunInProduction = true)]
[ServiceEvent(KoanServiceEvents.Health.ProbeScheduled, EventArgsType = typeof(ProbeScheduledEventArgs))]
[ServiceEvent(KoanServiceEvents.Health.ProbeBroadcast, EventArgsType = typeof(ProbeBroadcastEventArgs))]
internal sealed class HealthProbeScheduler : KoanFluentServiceBase
{
    private readonly IHealthAggregator _agg;
    private readonly HealthAggregatorOptions _opt;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastInvited = new(StringComparer.OrdinalIgnoreCase);

    public HealthProbeScheduler(
        ILogger<HealthProbeScheduler> logger,
        IConfiguration configuration,
        IHealthAggregator agg,
        HealthAggregatorOptions opt,
        IHostApplicationLifetime lifetime)
        : base(logger, configuration)
    {
        _agg = agg;
        _opt = opt;
        _lifetime = lifetime;
    }

    public override async Task ExecuteCoreAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled)
        {
            Logger.LogInformation("Health aggregator disabled; scheduler not running.");
            return;
        }

        // Wait for application to start
        if (!_lifetime.ApplicationStarted.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        Logger.LogInformation("Health probe scheduler starting. QuantizationWindow: {Window}ms", _opt.QuantizationWindow.TotalMilliseconds);

        var quantizationWindow = TimeSpan.FromMilliseconds(Math.Max(1000, _opt.QuantizationWindow.TotalMilliseconds));
        using var timer = new PeriodicTimer(quantizationWindow);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ScheduleProbes(stoppingToken);
        }
    }

    [ServiceAction(KoanServiceActions.Health.ForceProbe)]
    public async Task ForceProbeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Force probe requested - scheduling immediate health probes");
        await ScheduleProbes(cancellationToken);
        await EmitEventAsync(KoanServiceEvents.Health.ProbeBroadcast, 
            new ProbeBroadcastEventArgs { Forced = true, Timestamp = DateTimeOffset.UtcNow });
    }

    private async Task ScheduleProbes(CancellationToken cancellationToken)
    {
        // Probe scheduling logic with event emission
        await EmitEventAsync(KoanServiceEvents.Health.ProbeScheduled, 
            new ProbeScheduledEventArgs { Timestamp = DateTimeOffset.UtcNow });
    }
}
```

### Messaging Lifecycle Service (Koan.Messaging.Core)

```csharp
using Koan.Core.Events;
using Koan.Core.Actions;

[KoanBackgroundService(RunInProduction = true)]
[ServiceEvent(KoanServiceEvents.Messaging.Ready, EventArgsType = typeof(MessagingReadyEventArgs))]
[ServiceEvent(KoanServiceEvents.Messaging.Failed, EventArgsType = typeof(MessagingFailedEventArgs))]
internal class MessagingLifecycleService : KoanFluentServiceBase
{
    private readonly IMessagingService _messagingService;
    private readonly IHostApplicationLifetime _lifetime;

    public MessagingLifecycleService(
        ILogger<MessagingLifecycleService> logger,
        IConfiguration configuration,
        IMessagingService messagingService,
        IHostApplicationLifetime lifetime)
        : base(logger, configuration)
    {
        _messagingService = messagingService;
        _lifetime = lifetime;
    }

    public override async Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Messaging lifecycle service starting...");
            
            await _messagingService.StartAsync(cancellationToken);
            
            await EmitEventAsync(KoanServiceEvents.Messaging.Ready, 
                new MessagingReadyEventArgs { StartedAt = DateTimeOffset.UtcNow });
                
            Logger.LogInformation("Messaging service started successfully");

            // Wait for application shutdown
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start messaging service");
            await EmitEventAsync(KoanServiceEvents.Messaging.Failed, 
                new MessagingFailedEventArgs { Error = ex.Message });
            throw;
        }
    }

    [ServiceAction(KoanServiceActions.Messaging.RestartMessaging)]
    public async Task RestartMessagingAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Restarting messaging service...");
        await _messagingService.RestartAsync(cancellationToken);
        await EmitEventAsync(KoanServiceEvents.Messaging.Ready, 
            new MessagingReadyEventArgs { StartedAt = DateTimeOffset.UtcNow });
    }
}
```

---

## 📚 Next Steps

1. **Explore Samples**: Check the `samples/` directory for complete examples
2. **Integration Patterns**: See how services integrate with other Koan pillars
3. **Production Deployment**: Learn about scaling and monitoring patterns
4. **Advanced Scenarios**: Explore distributed service communication

---

**Koan Background Services: Elegant background processing with exceptional developer experience.**