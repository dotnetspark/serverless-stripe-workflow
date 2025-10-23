using System.Text.Json;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Handlers;

public class CheckoutHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductCatalogService _productCatalogService;
    private readonly IPaymentService _paymentService;
    private readonly ILogger<CheckoutHandler> _logger;

    public CheckoutHandler(
        IOrderRepository orderRepository,
        IProductCatalogService productCatalogService,
        IPaymentService paymentService,
        ILogger<CheckoutHandler> logger)
    {
        _orderRepository = orderRepository;
        _productCatalogService = productCatalogService;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task<CheckoutResponse> CreateCheckoutSessionAsync(CheckoutRequest request)
    {
        try
        {
            _logger.LogInformation("Creating checkout session for customer {CustomerEmail}", request.CustomerEmail);

            // Create order using domain factory method
            var customerId = CustomerId.From(request.CustomerId);
            var customerEmail = new Email(request.CustomerEmail);
            var order = Order.Create(customerId, customerEmail, request.CustomerPhone, request.CustomerSmsOptIn, request.NotificationUrl);

            // Add items to order with product validation
            foreach (var item in request.Items)
            {
                var product = await _productCatalogService.GetProductByIdAsync(item.ProductId);
                if (product == null)
                {
                    throw new ArgumentException($"Product {item.ProductId} not found");
                }

                if (!product.IsAvailable())
                {
                    throw new ArgumentException($"Product {item.ProductId} is not available");
                }

                order.AddItem(product.Id, product.Title, item.Quantity, product.Price);
            }

            // Add metadata if provided
            if (request.Metadata != null)
            {
                foreach (var metadata in request.Metadata)
                {
                    order.Metadata[metadata.Key] = metadata.Value;
                }
            }

            order = await _orderRepository.CreateAsync(order);

            // Create Stripe checkout session
            var stripeSessionId = await _paymentService.CreateCheckoutSessionAsync(
                order.Id,
                order.Items,
                order.CustomerEmail,
                request.SuccessUrl,
                request.CancelUrl);

            // Update order with Stripe session using domain method
            order.StartCheckout(stripeSessionId);

            await _orderRepository.UpdateAsync(order);

            _logger.LogInformation("Checkout session created successfully for order {OrderId}", order.Id);

            return new CheckoutResponse
            {
                OrderId = order.Id,
                SessionId = stripeSessionId,
                TotalAmount = order.TotalAmount.Amount,
                Currency = order.TotalAmount.Currency
            };
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain validation failed for checkout request - Customer: {CustomerEmail}", request.CustomerEmail);
            throw new ArgumentException(ex.Message, ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid checkout request for customer {CustomerEmail}", request.CustomerEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session for customer {CustomerEmail}", request.CustomerEmail);
            throw;
        }
    }
}

public class CheckoutRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public bool CustomerSmsOptIn { get; set; }
    public List<CheckoutItemRequest> Items { get; set; } = new();
    public string? Currency { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string? NotificationUrl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class CheckoutItemRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CheckoutResponse
{
    public string OrderId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
