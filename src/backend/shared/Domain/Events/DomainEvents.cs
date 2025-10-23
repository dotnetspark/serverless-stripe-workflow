using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Domain.Events;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class EventNameAttribute : Attribute
{
    public string Name { get; }

    public EventNameAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

public abstract record DomainEvent(DateTime OccurredAt)
{
    protected DomainEvent() : this(DateTime.UtcNow) { }
}

// Integration Events (for cross-service communication)
[EventName("order.submitted")]
public record OrderSubmittedEvent(
    string OrderId,
    string CustomerId,
    string CustomerEmail,
    DateTimeOffset SubmittedAt,
    OrderDetailsDto OrderDetails,
    ShippingAddressDto? ShippingAddress,
    decimal TotalAmount,
    string Currency = "USD"
) : DomainEvent;

[EventName("payment.processing.started")]
public record PaymentProcessingStartedEvent(
    string OrderId,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    DateTimeOffset StartedAt
) : DomainEvent;

[EventName("payment.completed")]
public record PaymentCompletedEvent(
    string OrderId,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    DateTimeOffset CompletedAt,
    string? ReceiptUrl
) : DomainEvent;

[EventName("payment.failed")]
public record PaymentFailedEvent(
    string OrderId,
    string? PaymentIntentId,
    string Reason,
    string ErrorCode,
    DateTimeOffset FailedAt
) : DomainEvent;

[EventName("webhook.received")]
public record WebhookReceivedEvent(
    string EventType,
    string EventId,
    string? OrderId,
    string? PaymentIntentId,
    DateTimeOffset ReceivedAt,
    Dictionary<string, object> EventData
) : DomainEvent;

[EventName("notification.sent")]
public record NotificationSentEvent(
    string OrderId,
    string CustomerId,
    string NotificationType,
    string Channel, // email, sms, push
    bool Success,
    string? ErrorMessage,
    DateTimeOffset SentAt
) : DomainEvent;

// DTOs for event data
public record OrderDetailsDto(
    IReadOnlyList<OrderItemDto> Items,
    decimal SubTotal,
    decimal TaxAmount,
    decimal ShippingAmount,
    decimal DiscountAmount
);

public record OrderItemDto(
    string ProductId,
    string ProductName,
    string ProductSku,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    ProductMetadataDto? Metadata = null
);

public record ProductMetadataDto(
    string? Color,
    string? Size,
    string? Variant,
    Dictionary<string, string>? CustomAttributes
);

public record ShippingAddressDto(
    string Name,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone
);