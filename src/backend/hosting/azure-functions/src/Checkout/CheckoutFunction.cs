using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Handlers;

namespace CheckoutFn;

public class CheckoutFunction
{
    private readonly ILogger<CheckoutFunction> _logger;
    private readonly CheckoutHandler _checkoutHandler;
    private readonly IEventBus _eventBus;

    public CheckoutFunction(ILogger<CheckoutFunction> logger, CheckoutHandler checkoutHandler, IEventBus eventBus)
    {
        _logger = logger;
        _checkoutHandler = checkoutHandler;
        _eventBus = eventBus;
    }

    [Function("ProcessOrderSubmitted")]
    public async Task ProcessOrderSubmittedEvent(
        [ServiceBusTrigger("order-submitted", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing order submitted event: {Message}", message);

        try
        {
            // Deserialize the event from Service Bus
            var envelope = MessageEnvelopeExtensions.FromJson<OrderSubmittedEvent>(message);

            if (envelope?.Data == null)
            {
                _logger.LogError("Invalid event envelope: {Message}", message);
                return;
            }

            var orderEvent = envelope.Data;
            _logger.LogInformation("Processing checkout for order {OrderId}, customer {CustomerId}",
                orderEvent.OrderId, orderEvent.CustomerId);

            // Create checkout request from event
            var checkoutRequest = new CheckoutRequest
            {
                CustomerId = orderEvent.CustomerId,
                CustomerEmail = orderEvent.CustomerEmail,
                Items = orderEvent.OrderDetails.Items.Select(item => new CheckoutItemRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                }).ToList(),
                SuccessUrl = "https://localhost:7001/success",
                CancelUrl = "https://localhost:7001/cancel"
            };

            // Call shared handler to process checkout
            var result = await _checkoutHandler.CreateCheckoutSessionAsync(checkoutRequest);

            // Publish PaymentProcessingStartedEvent
            var paymentStartedEvent = new PaymentProcessingStartedEvent(
                OrderId: orderEvent.OrderId,
                PaymentIntentId: result.SessionId,
                Amount: result.TotalAmount,
                Currency: result.Currency,
                StartedAt: DateTimeOffset.UtcNow
            );

            await _eventBus.PublishAsync(paymentStartedEvent);
            _logger.LogInformation("Payment processing started for order {OrderId}, session {SessionId}",
                orderEvent.OrderId, result.SessionId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize order submitted event: {Message}", message);
            // Don't retry for JSON deserialization errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order submitted event: {Message}", message);
            throw; // Let the function framework handle retries
        }
    }
}