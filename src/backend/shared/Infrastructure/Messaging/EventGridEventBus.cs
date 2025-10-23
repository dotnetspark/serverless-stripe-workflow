using System.Text.Json;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Messaging;

namespace StripeWorkflow.Infrastructure.Messaging;

public class EventGridEventBus : IEventBus, IAsyncDisposable
{
    private readonly EventGridPublisherClient _client;
    private readonly ILogger<EventGridEventBus> _logger;
    private readonly string _topicEndpoint;

    public EventGridEventBus(EventGridPublisherClient client, IConfiguration configuration, ILogger<EventGridEventBus> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _topicEndpoint = configuration.GetConnectionString("EventGrid") ?? throw new InvalidOperationException("EventGrid connection string not found");
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class
    {
        var topic = GetTopicName<T>();
        await PublishAsync(@event, topic, cancellationToken);
    }

    public async Task PublishAsync<T>(T @event, string topic, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = new MessageEnvelope<T> { Data = @event };
        var eventData = new EventGridEvent(
            subject: topic,
            eventType: envelope.MessageType,
            dataVersion: "1.0",
            data: envelope.Data)
        {
            Id = envelope.MessageId,
            EventTime = envelope.Timestamp
        };

        try
        {
            await _client.SendEventAsync(eventData, cancellationToken);
            _logger.LogInformation("Published event {EventType} to topic {Topic}", typeof(T).Name, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to topic {Topic}", typeof(T).Name, topic);
            throw;
        }
    }

    private static string GetTopicName<T>() where T : class
    {
        var typeName = typeof(T).Name;
        // Remove "Event" suffix if present and convert to kebab-case
        if (typeName.EndsWith("Event"))
            typeName = typeName[..^5];

        return string.Concat(typeName.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + c : c.ToString())).ToLower();
    }

    public async ValueTask DisposeAsync()
    {
        // EventGridPublisherClient doesn't implement IAsyncDisposable, but we keep this for consistency
        await Task.CompletedTask;
    }
}