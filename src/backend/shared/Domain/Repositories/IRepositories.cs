using StripeWorkflow.Domain.Models;

namespace StripeWorkflow.Domain.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string id);
    Task<Order?> GetByStripeSessionIdAsync(string stripeSessionId);
    Task<Order> CreateAsync(Order order);
    Task<Order> UpdateAsync(Order order);
    Task<bool> UpdateStatusAsync(string id, OrderStatus status, int expectedVersion);
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
    Task<IEnumerable<Order>> GetPendingRetryOrdersAsync();
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(string id);
}

public interface ICheckoutSessionRepository
{
    Task<CheckoutSession?> GetByIdAsync(string id);
    Task<CheckoutSession?> GetByStripeSessionIdAsync(string stripeSessionId);
    Task<CheckoutSession> CreateAsync(CheckoutSession session);
    Task<CheckoutSession> UpdateAsync(CheckoutSession session);
}