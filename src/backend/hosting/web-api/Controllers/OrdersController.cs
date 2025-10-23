using Microsoft.AspNetCore.Mvc;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;

namespace StripeWorkflow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IEventBus eventBus, ILogger<OrdersController> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitOrder([FromBody] SubmitOrderRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received order submission for customer {CustomerId}", request.CustomerId);

            var orderId = Guid.NewGuid().ToString();

            // Calculate totals
            var subTotal = request.Items.Sum(item => item.Quantity * item.UnitPrice);
            var taxAmount = subTotal * 0.08m; // 8% tax example
            var shippingAmount = 10.00m; // flat shipping example
            var discountAmount = 0m;

            // Create order details DTO
            var orderDetails = new OrderDetailsDto(
                Items: request.Items.Select(item => new OrderItemDto(
                    ProductId: item.ProductId,
                    ProductName: item.ProductName,
                    ProductSku: item.ProductId, // assuming productId as SKU for now
                    Quantity: item.Quantity,
                    UnitPrice: item.UnitPrice,
                    LineTotal: item.Quantity * item.UnitPrice
                )).ToList(),
                SubTotal: subTotal,
                TaxAmount: taxAmount,
                ShippingAmount: shippingAmount,
                DiscountAmount: discountAmount
            );

            // Create shipping address DTO
            var shippingAddress = new ShippingAddressDto(
                Name: $"{request.CustomerId}_customer", // placeholder - should come from request
                Line1: request.ShippingAddress.Street,
                Line2: null,
                City: request.ShippingAddress.City,
                State: request.ShippingAddress.State,
                PostalCode: request.ShippingAddress.PostalCode,
                Country: request.ShippingAddress.Country,
                Phone: null
            );

            // Create and publish the order submitted event - this triggers the checkout function
            var orderSubmittedEvent = new OrderSubmittedEvent(
                OrderId: orderId,
                CustomerId: request.CustomerId,
                CustomerEmail: $"{request.CustomerId}@example.com", // placeholder
                SubmittedAt: DateTimeOffset.UtcNow,
                OrderDetails: orderDetails,
                ShippingAddress: shippingAddress,
                TotalAmount: request.TotalAmount,
                Currency: request.Currency
            );

            // Publish the event - CheckoutFunction will subscribe and create Stripe checkout session
            await _eventBus.PublishAsync(orderSubmittedEvent, cancellationToken);

            _logger.LogInformation("Order {OrderId} submitted successfully", orderId);

            return Ok(new { OrderId = orderId, Status = "Submitted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit order for customer {CustomerId}", request.CustomerId);
            return StatusCode(500, "Failed to submit order");
        }
    }
}

// Request DTOs
public record SubmitOrderRequest
{
    public required string CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required string PaymentMethodId { get; init; }
    public required string PaymentMethodType { get; init; }
    public required string PaymentMethodLastFour { get; init; }
    public required List<OrderItemRequest> Items { get; init; } = [];
    public required ShippingAddressRequest ShippingAddress { get; init; }
}

public record OrderItemRequest
{
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public record ShippingAddressRequest
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string PostalCode { get; init; }
    public required string Country { get; init; }
}