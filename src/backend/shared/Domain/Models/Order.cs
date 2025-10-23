using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Domain.Models;

public class Order
{
    private readonly List<OrderItem> _items = new();

    public OrderId Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public Email CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public bool CustomerSmsOptIn { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public Money TotalAmount { get; private set; }
    public string? StripeSessionId { get; private set; }
    public string? NotificationUrl { get; private set; }
    public int FailureAttempts { get; private set; }
    public string? FailureReason { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Private constructor for persistence
    private Order()
    {
        Id = null!;
        CustomerId = null!;
        CustomerEmail = null!;
        TotalAmount = null!;
    }

    // Factory method for creating new orders
    public static Order Create(CustomerId customerId, Email customerEmail, string? customerPhone = null, bool customerSmsOptIn = false, string? notificationUrl = null)
    {
        if (customerSmsOptIn && string.IsNullOrWhiteSpace(customerPhone))
            throw new DomainException("Customer phone is required when SMS opt-in is enabled");

        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone?.Trim(),
            CustomerSmsOptIn = customerSmsOptIn,
            Status = OrderStatus.Draft,
            TotalAmount = Money.Zero("usd"),
            NotificationUrl = notificationUrl,
            FailureAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return order;
    }

    // Factory method for reconstructing orders from persistence
    public static Order Reconstruct(
        OrderId id,
        CustomerId customerId,
        Email customerEmail,
        OrderStatus status,
        Money totalAmount,
        string? stripeSessionId,
        string? notificationUrl,
        int failureAttempts,
        string? failureReason,
        Dictionary<string, object> metadata,
        DateTime createdAt,
        DateTime updatedAt,
        int version,
        IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            CustomerEmail = customerEmail,
            Status = status,
            TotalAmount = totalAmount,
            StripeSessionId = stripeSessionId,
            NotificationUrl = notificationUrl,
            FailureAttempts = failureAttempts,
            FailureReason = failureReason,
            Metadata = metadata,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Version = version
        };

        foreach (var item in items)
        {
            order._items.Add(item);
        }

        return order;
    }

    public void AddItem(string productId, string productName, int quantity, Money unitPrice)
    {
        ValidateCanModifyItems();

        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero");

        if (unitPrice.Amount <= 0)
            throw new DomainException("Unit price must be greater than zero");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            existingItem.UpdateQuantity(existingItem.Quantity + quantity);
        }
        else
        {
            _items.Add(new OrderItem(productId, productName, quantity, unitPrice));
        }

        RecalculateTotal();
        UpdateTimestamp();
    }

    public void RemoveItem(string productId)
    {
        ValidateCanModifyItems();

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            _items.Remove(item);
            RecalculateTotal();
            UpdateTimestamp();
        }
    }

    public void UpdateItemQuantity(string productId, int newQuantity)
    {
        ValidateCanModifyItems();

        if (newQuantity <= 0)
            throw new DomainException("Quantity must be greater than zero");

        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            throw new DomainException($"Item with product ID {productId} not found");

        item.UpdateQuantity(newQuantity);
        RecalculateTotal();
        UpdateTimestamp();
    }

    public void StartCheckout(string stripeSessionId)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Cannot start checkout for order in {Status} status");

        if (!_items.Any())
            throw new DomainException("Cannot start checkout for empty order");

        StripeSessionId = stripeSessionId;
        Status = OrderStatus.Pending;
        UpdateTimestamp();
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException($"Cannot mark order as paid from {Status} status");

        Status = OrderStatus.Paid;
        UpdateTimestamp();
    }

    public void MarkAsFulfilled()
    {
        if (Status != OrderStatus.Paid)
            throw new DomainException($"Cannot fulfill order in {Status} status");

        Status = OrderStatus.Fulfilled;
        UpdateTimestamp();
    }

    public void MarkAsFailed(string reason)
    {
        if (Status == OrderStatus.Fulfilled || Status == OrderStatus.Cancelled)
            throw new DomainException($"Cannot mark {Status} order as failed");

        Status = OrderStatus.Failed;
        FailureReason = reason;
        UpdateTimestamp();
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Fulfilled)
            throw new DomainException("Cannot cancel fulfilled order");

        Status = OrderStatus.Cancelled;
        FailureReason = reason;
        UpdateTimestamp();
    }

    public void RecordFailureAttempt(string? reason = null)
    {
        FailureAttempts++;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            FailureReason = reason;
        }
        UpdateTimestamp();
    }

    // Private methods
    private void RecalculateTotal()
    {
        var currency = TotalAmount.Currency;
        var totalAmount = _items.Sum(item => item.TotalPrice.Amount);
        TotalAmount = new Money(totalAmount, currency);
    }

    private void UpdateStatus(OrderStatus newStatus)
    {
        if (Status == newStatus) return;

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ValidateCanModifyItems()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"Cannot modify items for order in {Status} status");
    }

    private void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }
}

public class OrderItem
{
    public string ProductId { get; private set; }
    public string ProductName { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money TotalPrice => new(Quantity * UnitPrice.Amount, UnitPrice.Currency);

    private OrderItem() // For persistence
    {
        ProductId = null!;
        ProductName = null!;
        UnitPrice = null!;
    }

    internal OrderItem(string productId, string productName, int quantity, Money unitPrice)
    {
        ProductId = productId ?? throw new ArgumentNullException(nameof(productId));
        ProductName = productName ?? throw new ArgumentNullException(nameof(productName));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be positive", nameof(quantity));
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
    }

    // Factory method for reconstructing OrderItems from persistence
    public static OrderItem Reconstruct(string productId, string productName, int quantity, Money unitPrice)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }

    internal void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(newQuantity));

        Quantity = newQuantity;
    }
}

public enum OrderStatus
{
    Draft,
    Pending,
    Paid,
    Fulfilled,
    Failed,
    Cancelled,
    Refunded
}