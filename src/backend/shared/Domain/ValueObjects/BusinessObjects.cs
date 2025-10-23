namespace StripeWorkflow.Domain.ValueObjects;

public record ProductCategory
{
    public static readonly ProductCategory Electronics = new("electronics");
    public static readonly ProductCategory Jewelery = new("jewelery");
    public static readonly ProductCategory MensClothing = new("men's clothing");
    public static readonly ProductCategory WomensClothing = new("women's clothing");

    public string Value { get; init; }

    public ProductCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product category cannot be empty", nameof(value));

        Value = value.ToLowerInvariant().Trim();
    }
    public static ProductCategory From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Product category cannot be empty", nameof(value));

        return new ProductCategory(value);
    }

    public static implicit operator string(ProductCategory category) => category.Value;
    public override string ToString() => Value;
}