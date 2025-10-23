using System.Text.Json;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Repositories.Cosmos;

/// <summary>
/// Cosmos DB document representation of an Order
/// </summary>
public class CosmosOrderDocument
{
    public string id { get; set; } = string.Empty;
    public string customerId { get; set; } = string.Empty;
    public string customerEmail { get; set; } = string.Empty;
    public List<CosmosOrderItem> items { get; set; } = new();
    public decimal totalAmount { get; set; }
    public string currency { get; set; } = "usd";
    public string status { get; set; } = string.Empty;
    public string? stripeSessionId { get; set; }
    public string? notificationUrl { get; set; }
    public int failureAttempts { get; set; } = 0;
    public string? failureReason { get; set; }
    public Dictionary<string, object> metadata { get; set; } = new();
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
    public int version { get; set; } = 1;

    public static CosmosOrderDocument FromOrder(Order order)
    {
        return new CosmosOrderDocument
        {
            id = order.Id.Value,
            customerId = order.CustomerId.Value,
            customerEmail = order.CustomerEmail.Value,
            items = order.Items.Select(CosmosOrderItem.FromOrderItem).ToList(),
            totalAmount = order.TotalAmount.Amount,
            currency = order.TotalAmount.Currency,
            status = order.Status.ToString(),
            stripeSessionId = order.StripeSessionId,
            notificationUrl = order.NotificationUrl,
            failureAttempts = order.FailureAttempts,
            failureReason = order.FailureReason,
            metadata = order.Metadata,
            createdAt = order.CreatedAt,
            updatedAt = order.UpdatedAt,
            version = order.Version
        };
    }

    public Order ToOrder()
    {
        return Order.Reconstruct(
            new OrderId(id),
            new CustomerId(customerId),
            new Email(customerEmail),
            Enum.Parse<OrderStatus>(status),
            new Money(totalAmount, currency),
            stripeSessionId,
            notificationUrl,
            failureAttempts,
            failureReason,
            metadata,
            createdAt,
            updatedAt,
            version,
            items.Select(item => item.ToOrderItem())
        );
    }
}

public class CosmosOrderItem
{
    public string productId { get; set; } = string.Empty;
    public string productName { get; set; } = string.Empty;
    public int quantity { get; set; }
    public decimal unitPrice { get; set; }
    public string currency { get; set; } = "usd";

    public static CosmosOrderItem FromOrderItem(OrderItem item)
    {
        return new CosmosOrderItem
        {
            productId = item.ProductId,
            productName = item.ProductName,
            quantity = item.Quantity,
            unitPrice = item.UnitPrice.Amount,
            currency = item.UnitPrice.Currency
        };
    }

    public OrderItem ToOrderItem()
    {
        return OrderItem.Reconstruct(
            productId,
            productName,
            quantity,
            new Money(unitPrice, currency)
        );
    }
}