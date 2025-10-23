using System.Text.Json;

namespace StripeWorkflow.Domain.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class;
    Task PublishAsync<T>(T @event, string topic, CancellationToken cancellationToken = default) where T : class;
}

public class MessageEnvelope<T> where T : class
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public string MessageType { get; init; } = typeof(T).Name;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public T Data { get; init; } = default!;
    public Dictionary<string, string> Headers { get; init; } = new();
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}

public static class MessageEnvelopeExtensions
{
    public static string ToJson<T>(this MessageEnvelope<T> envelope) where T : class
    {
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public static MessageEnvelope<T>? FromJson<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<MessageEnvelope<T>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}