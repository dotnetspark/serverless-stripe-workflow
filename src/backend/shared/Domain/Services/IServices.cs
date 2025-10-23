using StripeWorkflow.Domain.Models;

namespace StripeWorkflow.Domain.Services;

public interface INotificationService
{
    Task<bool> SendNotificationAsync(string url, object payload, int maxRetries = 3);
}

public interface IProductCatalogService
{
    Task<IEnumerable<Product>> GetProductsAsync();
    Task<Product?> GetProductByIdAsync(string id);
    Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category);
}

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(string orderId, IEnumerable<OrderItem> items, string customerEmail, string successUrl, string cancelUrl);
    Task<bool> ValidateWebhookSignatureAsync(string payload, string signature, string secret);
}