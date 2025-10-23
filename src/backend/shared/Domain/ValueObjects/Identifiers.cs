namespace StripeWorkflow.Domain.ValueObjects;

public record CustomerId(string Value)
{
    public static CustomerId New() => new(Guid.NewGuid().ToString());

    public static CustomerId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CustomerId cannot be empty", nameof(value));

        return new CustomerId(value);
    }

    public static implicit operator string(CustomerId customerId) => customerId.Value;
    public override string ToString() => Value;
}

public record ProductId(string Value)
{
    public static ProductId New() => new(Guid.NewGuid().ToString());

    public static ProductId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProductId cannot be empty", nameof(value));

        return new ProductId(value);
    }

    public static implicit operator string(ProductId productId) => productId.Value;
    public override string ToString() => Value;
}

public record CheckoutSessionId(string Value)
{
    public static CheckoutSessionId New() => new(Guid.NewGuid().ToString());

    public static CheckoutSessionId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CheckoutSessionId cannot be empty", nameof(value));

        return new CheckoutSessionId(value);
    }

    public static implicit operator string(CheckoutSessionId sessionId) => sessionId.Value;
    public override string ToString() => Value;
}