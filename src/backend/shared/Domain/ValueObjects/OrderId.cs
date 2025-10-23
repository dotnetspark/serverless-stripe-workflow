namespace StripeWorkflow.Domain.ValueObjects;

public record OrderId(string Value)
{
    public static OrderId New() => new(Guid.NewGuid().ToString());

    public static OrderId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderId cannot be empty", nameof(value));

        return new OrderId(value);
    }

    public static implicit operator string(OrderId orderId) => orderId.Value;
    public override string ToString() => Value;
}