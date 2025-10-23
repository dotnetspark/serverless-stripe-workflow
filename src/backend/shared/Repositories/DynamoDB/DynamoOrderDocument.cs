using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Repositories.DynamoDB;

/// <summary>
/// DynamoDB document representation of an Order
/// </summary>
public class DynamoOrderDocument
{
    public string PK { get; set; } = string.Empty;
    public string SK { get; set; } = string.Empty;
    public string GSI1PK { get; set; } = string.Empty;
    public string GSI1SK { get; set; } = string.Empty;
    public string GSI2PK { get; set; } = string.Empty;
    public string GSI2SK { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public List<DynamoOrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "usd";
    public string Status { get; set; } = string.Empty;
    public string? StripeSessionId { get; set; }
    public string? NotificationUrl { get; set; }
    public int FailureAttempts { get; set; } = 0;
    public string? FailureReason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; } = 1;

    public static DynamoOrderDocument FromOrder(Order order)
    {
        return new DynamoOrderDocument
        {
            PK = $"ORDER#{order.Id.Value}",
            SK = $"ORDER#{order.Id.Value}",
            GSI1PK = !string.IsNullOrEmpty(order.StripeSessionId) ? $"SESSION#{order.StripeSessionId}" : $"ORDER#{order.Id.Value}",
            GSI1SK = $"ORDER#{order.Id.Value}",
            GSI2PK = $"STATUS#{order.Status}",
            GSI2SK = $"ORDER#{order.Id.Value}",
            Id = order.Id.Value,
            CustomerId = order.CustomerId.Value,
            CustomerEmail = order.CustomerEmail.Value,
            Items = order.Items.Select(DynamoOrderItem.FromOrderItem).ToList(),
            TotalAmount = order.TotalAmount.Amount,
            Currency = order.TotalAmount.Currency,
            Status = order.Status.ToString(),
            StripeSessionId = order.StripeSessionId,
            NotificationUrl = order.NotificationUrl,
            FailureAttempts = order.FailureAttempts,
            FailureReason = order.FailureReason,
            Metadata = order.Metadata,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Version = order.Version
        };
    }

    public Order ToOrder()
    {
        return Order.Reconstruct(
            new OrderId(Id),
            new CustomerId(CustomerId),
            new Email(CustomerEmail),
            Enum.Parse<OrderStatus>(Status),
            new Money(TotalAmount, Currency),
            StripeSessionId,
            NotificationUrl,
            FailureAttempts,
            FailureReason,
            Metadata,
            CreatedAt,
            UpdatedAt,
            Version,
            Items.Select(item => item.ToOrderItem())
        );
    }

    public static DynamoOrderDocument FromDynamoItem(Dictionary<string, AttributeValue> item)
    {
        return new DynamoOrderDocument
        {
            PK = item.GetValueOrDefault("PK")?.S ?? "",
            SK = item.GetValueOrDefault("SK")?.S ?? "",
            GSI1PK = item.GetValueOrDefault("GSI1PK")?.S ?? "",
            GSI1SK = item.GetValueOrDefault("GSI1SK")?.S ?? "",
            GSI2PK = item.GetValueOrDefault("GSI2PK")?.S ?? "",
            GSI2SK = item.GetValueOrDefault("GSI2SK")?.S ?? "",
            Id = item.GetValueOrDefault("id")?.S ?? "",
            CustomerId = item.GetValueOrDefault("customerId")?.S ?? "",
            CustomerEmail = item.GetValueOrDefault("customerEmail")?.S ?? "",
            Items = ParseOrderItems(item.GetValueOrDefault("items")?.L),
            TotalAmount = decimal.Parse(item.GetValueOrDefault("totalAmount")?.N ?? "0"),
            Currency = item.GetValueOrDefault("currency")?.S ?? "usd",
            Status = item.GetValueOrDefault("status")?.S ?? "",
            StripeSessionId = item.GetValueOrDefault("stripeSessionId")?.S,
            NotificationUrl = item.GetValueOrDefault("notificationUrl")?.S,
            FailureAttempts = int.Parse(item.GetValueOrDefault("failureAttempts")?.N ?? "0"),
            FailureReason = item.GetValueOrDefault("failureReason")?.S,
            Metadata = ParseMetadata(item.GetValueOrDefault("metadata")?.M),
            CreatedAt = DateTime.Parse(item.GetValueOrDefault("createdAt")?.S ?? DateTime.UtcNow.ToString("O")),
            UpdatedAt = DateTime.Parse(item.GetValueOrDefault("updatedAt")?.S ?? DateTime.UtcNow.ToString("O")),
            Version = int.Parse(item.GetValueOrDefault("version")?.N ?? "1")
        };
    }

    public Dictionary<string, AttributeValue> ToDynamoItem()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = PK },
            ["SK"] = new AttributeValue { S = SK },
            ["GSI1PK"] = new AttributeValue { S = GSI1PK },
            ["GSI1SK"] = new AttributeValue { S = GSI1SK },
            ["GSI2PK"] = new AttributeValue { S = GSI2PK },
            ["GSI2SK"] = new AttributeValue { S = GSI2SK },
            ["id"] = new AttributeValue { S = Id },
            ["customerId"] = new AttributeValue { S = CustomerId },
            ["customerEmail"] = new AttributeValue { S = CustomerEmail },
            ["items"] = new AttributeValue { L = Items.Select(i => i.ToAttributeValue()).ToList() },
            ["totalAmount"] = new AttributeValue { N = TotalAmount.ToString() },
            ["currency"] = new AttributeValue { S = Currency },
            ["status"] = new AttributeValue { S = Status },
            ["failureAttempts"] = new AttributeValue { N = FailureAttempts.ToString() },
            ["metadata"] = new AttributeValue { M = ConvertMetadataToAttributeValues(Metadata) },
            ["createdAt"] = new AttributeValue { S = CreatedAt.ToString("O") },
            ["updatedAt"] = new AttributeValue { S = UpdatedAt.ToString("O") },
            ["version"] = new AttributeValue { N = Version.ToString() }
        };

        if (!string.IsNullOrEmpty(StripeSessionId))
            item["stripeSessionId"] = new AttributeValue { S = StripeSessionId };

        if (!string.IsNullOrEmpty(NotificationUrl))
            item["notificationUrl"] = new AttributeValue { S = NotificationUrl };

        if (!string.IsNullOrEmpty(FailureReason))
            item["failureReason"] = new AttributeValue { S = FailureReason };

        return item;
    }

    private static List<DynamoOrderItem> ParseOrderItems(List<AttributeValue>? itemsList)
    {
        if (itemsList == null) return new List<DynamoOrderItem>();

        return itemsList.Select(DynamoOrderItem.FromAttributeValue).ToList();
    }

    private static Dictionary<string, object> ParseMetadata(Dictionary<string, AttributeValue>? metadataMap)
    {
        if (metadataMap == null) return new Dictionary<string, object>();

        var result = new Dictionary<string, object>();
        foreach (var kvp in metadataMap)
        {
            if (!string.IsNullOrEmpty(kvp.Value.S))
                result[kvp.Key] = kvp.Value.S;
            else if (!string.IsNullOrEmpty(kvp.Value.N))
                result[kvp.Key] = decimal.Parse(kvp.Value.N);
        }
        return result;
    }

    private static Dictionary<string, AttributeValue> ConvertMetadataToAttributeValues(Dictionary<string, object> metadata)
    {
        var result = new Dictionary<string, AttributeValue>();
        foreach (var kvp in metadata)
        {
            if (kvp.Value is string strValue)
                result[kvp.Key] = new AttributeValue { S = strValue };
            else if (kvp.Value is decimal decValue)
                result[kvp.Key] = new AttributeValue { N = decValue.ToString() };
            else if (kvp.Value is int intValue)
                result[kvp.Key] = new AttributeValue { N = intValue.ToString() };
        }
        return result;
    }
}

public class DynamoOrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "usd";

    public static DynamoOrderItem FromOrderItem(OrderItem item)
    {
        return new DynamoOrderItem
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice.Amount,
            Currency = item.UnitPrice.Currency
        };
    }

    public OrderItem ToOrderItem()
    {
        return OrderItem.Reconstruct(
            ProductId,
            ProductName,
            Quantity,
            new Money(UnitPrice, Currency)
        );
    }

    public AttributeValue ToAttributeValue()
    {
        return new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["productId"] = new AttributeValue { S = ProductId },
                ["productName"] = new AttributeValue { S = ProductName },
                ["quantity"] = new AttributeValue { N = Quantity.ToString() },
                ["unitPrice"] = new AttributeValue { N = UnitPrice.ToString() },
                ["currency"] = new AttributeValue { S = Currency }
            }
        };
    }

    public static DynamoOrderItem FromAttributeValue(AttributeValue attributeValue)
    {
        var map = attributeValue.M;
        return new DynamoOrderItem
        {
            ProductId = map.GetValueOrDefault("productId")?.S ?? "",
            ProductName = map.GetValueOrDefault("productName")?.S ?? "",
            Quantity = int.Parse(map.GetValueOrDefault("quantity")?.N ?? "0"),
            UnitPrice = decimal.Parse(map.GetValueOrDefault("unitPrice")?.N ?? "0"),
            Currency = map.GetValueOrDefault("currency")?.S ?? "usd"
        };
    }
}