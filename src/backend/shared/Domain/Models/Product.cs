using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Domain.Models;

public class Product
{
    public ProductId Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public Money Price { get; private set; }
    public ProductCategory Category { get; private set; }
    public Uri? ImageUrl { get; private set; }
    public ProductRating? Rating { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Product() // For persistence
    {
        Id = null!;
        Title = null!;
        Description = null!;
        Price = null!;
        Category = null!;
    }

    public static Product Create(string title, string description, Money price, ProductCategory category, Uri? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Product title cannot be empty");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Product description cannot be empty");

        if (price.Amount <= 0)
            throw new DomainException("Product price must be greater than zero");

        return new Product
        {
            Id = ProductId.New(),
            Title = title.Trim(),
            Description = description.Trim(),
            Price = price,
            Category = category,
            ImageUrl = imageUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdatePrice(Money newPrice)
    {
        if (newPrice.Amount <= 0)
            throw new DomainException("Price must be greater than zero");

        if (newPrice.Currency != Price.Currency)
            throw new DomainException("Cannot change product currency");

        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRating(decimal rate, int count)
    {
        if (rate < 0 || rate > 5)
            throw new DomainException("Rating must be between 0 and 5");

        if (count < 0)
            throw new DomainException("Rating count cannot be negative");

        Rating = new ProductRating(rate, count);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsAvailable() => IsActive && Price.Amount > 0;
}

public class ProductRating
{
    public decimal Rate { get; private set; }
    public int Count { get; private set; }

    private ProductRating() { } // For persistence

    internal ProductRating(decimal rate, int count)
    {
        if (rate < 0 || rate > 5)
            throw new ArgumentException("Rating must be between 0 and 5", nameof(rate));

        if (count < 0)
            throw new ArgumentException("Count cannot be negative", nameof(count));

        Rate = rate;
        Count = count;
    }

    public override string ToString() => $"{Rate:F1} ({Count} reviews)";
}

public class CheckoutSession
{
    public CheckoutSessionId Id { get; private set; }
    public OrderId OrderId { get; private set; }
    public string StripeSessionId { get; private set; }
    public CheckoutSessionStatus Status { get; private set; }
    public string? PaymentIntentId { get; private set; }
    public Email CustomerEmail { get; private set; }
    public Money Amount { get; private set; }
    public Uri? SuccessUrl { get; private set; }
    public Uri? CancelUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private CheckoutSession() // For persistence
    {
        Id = null!;
        OrderId = null!;
        StripeSessionId = null!;
        CustomerEmail = null!;
        Amount = null!;
    }

    public static CheckoutSession Create(OrderId orderId, string stripeSessionId, Email customerEmail,
        Money amount, Uri? successUrl = null, Uri? cancelUrl = null)
    {
        if (string.IsNullOrWhiteSpace(stripeSessionId))
            throw new DomainException("Stripe session ID cannot be empty");

        var now = DateTime.UtcNow;
        return new CheckoutSession
        {
            Id = CheckoutSessionId.New(),
            OrderId = orderId,
            StripeSessionId = stripeSessionId,
            Status = CheckoutSessionStatus.Open,
            CustomerEmail = customerEmail,
            Amount = amount,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CreatedAt = now,
            ExpiresAt = now.AddHours(1) // 1 hour expiry
        };
    }

    public void MarkAsComplete(string? paymentIntentId = null)
    {
        if (Status != CheckoutSessionStatus.Open)
            throw new DomainException($"Cannot complete checkout session in {Status} status");

        Status = CheckoutSessionStatus.Complete;
        PaymentIntentId = paymentIntentId;
    }

    public void MarkAsExpired()
    {
        if (Status != CheckoutSessionStatus.Open)
            throw new DomainException($"Cannot expire checkout session in {Status} status");

        Status = CheckoutSessionStatus.Expired;
    }

    public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
}

public enum CheckoutSessionStatus
{
    Open,
    Complete,
    Expired
}