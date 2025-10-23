using System.Reflection;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;

namespace StripeWorkflow.Infrastructure.Messaging;

public class ServiceBusEventBus : IEventBus
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusEventBus> _logger;
    private readonly Dictionary<Type, string> _eventToTopicMap;

    public ServiceBusEventBus(ServiceBusClient serviceBusClient, ILogger<ServiceBusEventBus> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;

        // Map events to their corresponding destinations (topics or queues)
        _eventToTopicMap = new Dictionary<Type, string>
        {
            { typeof(OrderSubmittedEvent), "order-submitted" },
            { typeof(PaymentProcessingStartedEvent), "payment-events" },
            { typeof(PaymentCompletedEvent), "payment-events" },
            { typeof(PaymentFailedEvent), "payment-events" },
            { typeof(WebhookReceivedEvent), "webhook-events" },
            { typeof(NotificationSentEvent), "webhook-events" }
        };
    }

    public async Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class
    {
        var eventType = typeof(T);
        if (!_eventToTopicMap.TryGetValue(eventType, out var topicName))
        {
            // If no specific mapping, try to derive topic name
            topicName = GetTopicName<T>();
        }

        await PublishAsync(eventData, topicName, cancellationToken);
    }

    public async Task PublishAsync<T>(T eventData, string topic, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var envelope = new MessageEnvelope<T>
            {
                Data = eventData
            };
            var messageBody = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var sender = _serviceBusClient.CreateSender(topic);
            var eventName = GetEventName<T>();
            var message = new ServiceBusMessage(messageBody)
            {
                Subject = eventName,
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                ApplicationProperties =
                {
                    ["EventType"] = eventName,
                    ["Version"] = "1.0",
                    ["CorrelationId"] = envelope.CorrelationId
                }
            };

            await sender.SendMessageAsync(message, cancellationToken);
            _logger.LogInformation("Published event {EventType} with ID {MessageId} to destination {Destination}",
                GetEventName<T>(), envelope.MessageId, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to topic {Topic}", GetEventName<T>(), topic);
            throw;
        }
    }

    private static string GetEventName<T>() where T : class
    {
        var eventNameAttribute = typeof(T).GetCustomAttribute<Domain.Events.EventNameAttribute>();
        return eventNameAttribute?.Name ?? typeof(T).Name;
    }

    private static string GetTopicName<T>() where T : class
    {
        var typeName = typeof(T).Name;
        // Remove "Event" suffix if present and convert to kebab-case
        if (typeName.EndsWith("Event"))
            typeName = typeName[..^5];

        return string.Concat(typeName.Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + c : c.ToString())).ToLower() + "-events";
    }
}