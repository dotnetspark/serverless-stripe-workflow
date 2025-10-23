using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Domain.ValueObjects;
using StripeWorkflow.Handlers;
using Xunit.Abstractions;

namespace StripeWorkflow.IntegrationTests.Infrastructure;

/// <summary>
/// Simplified base test class that provides basic test infrastructure without complex containerization
/// </summary>
public abstract class SimpleIntegrationTestBase : IDisposable
{
    protected readonly ITestOutputHelper _output;
    protected IServiceProvider ServiceProvider { get; private set; }
    protected IConfiguration Configuration { get; private set; }

    protected SimpleIntegrationTestBase(ITestOutputHelper output)
    {
        _output = output;

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Information",
                ["TestSettings:DatabaseConnectionString"] = "InMemoryTestDatabase",
                ["TestSettings:StripeSecretKey"] = "sk_test_fake_key",
                ["TestSettings:WebhookSecret"] = "whsec_test_secret"
            })
            .Build();

        ServiceProvider = CreateServiceProvider();
    }

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton(Configuration);

        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new SimpleXunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add mock repositories
        services.AddSingleton<IOrderRepository, SimpleOrderRepository>();
        services.AddSingleton<IProductRepository, SimpleProductRepository>();
        services.AddSingleton<ICheckoutSessionRepository, SimpleCheckoutSessionRepository>();

        // Add mock services
        services.AddSingleton<IProductCatalogService, SimpleProductCatalogService>();
        services.AddSingleton<IPaymentService, SimplePaymentService>();
        services.AddSingleton<INotificationService, SimpleNotificationService>();

        // Add handlers
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<WebhookHandler>();
        services.AddScoped<NotificationHandler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}

// Simple mock implementations that work with the actual interfaces
public class SimpleOrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders = new();

    public Task<Order?> GetByIdAsync(string id)
    {
        _orders.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<Order?> GetByStripeSessionIdAsync(string stripeSessionId)
    {
        var order = _orders.Values.FirstOrDefault(o => o.StripeSessionId == stripeSessionId);
        return Task.FromResult(order);
    }

    public Task<Order> CreateAsync(Order order)
    {
        _orders[order.Id.Value] = order;
        return Task.FromResult(order);
    }

    public Task<Order> UpdateAsync(Order order)
    {
        _orders[order.Id.Value] = order;
        return Task.FromResult(order);
    }

    public Task<bool> UpdateStatusAsync(string id, OrderStatus status, int expectedVersion)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            // Simple version - doesn't implement optimistic concurrency
            _orders[id] = order;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        var orders = _orders.Values.Where(o => o.Status == status);
        return Task.FromResult(orders);
    }

    public Task<IEnumerable<Order>> GetPendingRetryOrdersAsync()
    {
        var orders = _orders.Values.Where(o => o.Status == OrderStatus.Failed);
        return Task.FromResult(orders);
    }
}

public class SimpleProductRepository : IProductRepository
{
    private readonly Dictionary<string, Product> _products = new();

    public SimpleProductRepository()
    {
        // Add some test products
        var product1 = Product.Create("Test Product 1", "A test product", new Money(29.99m, "USD"), ProductCategory.Electronics);
        var product2 = Product.Create("Test Product 2", "Another test product", new Money(19.99m, "USD"), ProductCategory.Jewelery);

        _products[product1.Id.Value] = product1;
        _products[product2.Id.Value] = product2;
        _products["product-1"] = product1; // For easy testing
        _products["product-2"] = product2;
    }

    public Task<Product?> GetByIdAsync(string id)
    {
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task<IEnumerable<Product>> GetAllAsync()
    {
        return Task.FromResult(_products.Values.AsEnumerable());
    }

    public Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        var products = _products.Values.Where(p => p.Category.ToString() == category);
        return Task.FromResult(products);
    }

    public Task<Product> CreateAsync(Product product)
    {
        _products[product.Id.Value] = product;
        return Task.FromResult(product);
    }

    public Task<Product> UpdateAsync(Product product)
    {
        _products[product.Id.Value] = product;
        return Task.FromResult(product);
    }

    public Task<bool> DeleteAsync(string id)
    {
        return Task.FromResult(_products.Remove(id));
    }
}

public class SimpleCheckoutSessionRepository : ICheckoutSessionRepository
{
    private readonly Dictionary<string, CheckoutSession> _sessions = new();

    public Task<CheckoutSession?> GetByIdAsync(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return Task.FromResult(session);
    }

    public Task<CheckoutSession?> GetByStripeSessionIdAsync(string stripeSessionId)
    {
        var session = _sessions.Values.FirstOrDefault(s => s.StripeSessionId == stripeSessionId);
        return Task.FromResult(session);
    }

    public Task<CheckoutSession> CreateAsync(CheckoutSession session)
    {
        _sessions[session.Id.Value] = session;
        return Task.FromResult(session);
    }

    public Task<CheckoutSession> UpdateAsync(CheckoutSession session)
    {
        _sessions[session.Id.Value] = session;
        return Task.FromResult(session);
    }
}

public class SimpleProductCatalogService : IProductCatalogService
{
    private readonly IProductRepository _productRepository;

    public SimpleProductCatalogService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IEnumerable<Product>> GetProductsAsync()
    {
        return _productRepository.GetAllAsync();
    }

    public Task<Product?> GetProductByIdAsync(string id)
    {
        return _productRepository.GetByIdAsync(id);
    }

    public Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        return _productRepository.GetByCategoryAsync(category);
    }
}

public class SimplePaymentService : IPaymentService
{
    public Task<string> CreateCheckoutSessionAsync(string orderId, IEnumerable<OrderItem> items, string customerEmail, string successUrl, string cancelUrl)
    {
        // Return a mock Stripe session ID
        return Task.FromResult($"cs_test_{Guid.NewGuid():N}");
    }

    public Task<bool> ValidateWebhookSignatureAsync(string payload, string signature, string secret)
    {
        // For testing, always return true unless signature is explicitly "invalid"
        return Task.FromResult(signature != "invalid");
    }
}

public class SimpleNotificationService : INotificationService
{
    private readonly List<NotificationCall> _sentNotifications = new();

    public List<NotificationCall> SentNotifications => _sentNotifications;

    public Task<bool> SendNotificationAsync(string url, object payload, int maxRetries = 3)
    {
        var call = new NotificationCall(url, payload, DateTime.UtcNow);
        _sentNotifications.Add(call);

        // Simulate success unless URL contains "fail"
        return Task.FromResult(!url.Contains("fail"));
    }
}

public record NotificationCall(string Url, object Payload, DateTime Timestamp);

// Simple Xunit logger provider for test output
public class SimpleXunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public SimpleXunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleXunitLogger(_output, categoryName);
    }

    public void Dispose() { }
}

public class SimpleXunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public SimpleXunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => NoOpScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {_categoryName}: {message}");
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }
        catch
        {
            // Ignore any logging errors during tests
        }
    }

    private class NoOpScope : IDisposable
    {
        public static NoOpScope Instance { get; } = new();
        public void Dispose() { }
    }
}