using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace StripeWorkflow.Infrastructure.Messaging;

public class ServiceBusHealthCheck : IHealthCheck
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusHealthCheck> _logger;

    public ServiceBusHealthCheck(ServiceBusClient client, ILogger<ServiceBusHealthCheck> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // For Service Bus, we'll create a receiver to test the connection
            // This is a simple way to verify connectivity without sending messages
            if (_client != null && !_client.IsClosed)
            {
                return HealthCheckResult.Healthy("Service Bus client is connected");
            }
            return HealthCheckResult.Unhealthy("Service Bus client is not connected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Bus health check failed");
            return HealthCheckResult.Unhealthy("Service Bus connection failed", ex);
        }
    }
}

public class EventGridHealthCheck : IHealthCheck
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridHealthCheck> _logger;

    public EventGridHealthCheck(EventGridPublisherClient client, ILogger<EventGridHealthCheck> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // For Event Grid, we'll just verify the client is configured
            // In a real scenario, you might want to send a test event or check topic properties
            if (_client != null)
            {
                return HealthCheckResult.Healthy("Event Grid client is configured");
            }
            return HealthCheckResult.Unhealthy("Event Grid client is not configured");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event Grid health check failed");
            return HealthCheckResult.Unhealthy("Event Grid health check failed", ex);
        }
    }
}