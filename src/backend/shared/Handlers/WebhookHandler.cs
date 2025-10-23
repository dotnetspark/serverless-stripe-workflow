using System.Text.Json;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Handlers;

public class WebhookHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly IEventBus _eventBus;
    private readonly ILogger<WebhookHandler> _logger;

    public WebhookHandler(
        IOrderRepository orderRepository,
        IPaymentService paymentService,
        INotificationService notificationService,
        IEventBus eventBus,
        ILogger<WebhookHandler> logger)
    {
        _orderRepository = orderRepository;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<WebhookResponse> ProcessWebhookAsync(WebhookRequest request)
    {
        try
        {
            _logger.LogInformation("Processing webhook event: {EventType}", request.EventType);

            // Validate webhook signature
            if (!await _paymentService.ValidateWebhookSignatureAsync(request.Payload, request.Signature, request.Secret))
            {
                _logger.LogWarning("Invalid webhook signature received");
                return new WebhookResponse { Success = false, Message = "Invalid signature" };
            }

            // Parse webhook payload
            var webhookEvent = JsonSerializer.Deserialize<StripeWebhookEvent>(request.Payload);
            if (webhookEvent?.Data?.Object == null)
            {
                _logger.LogWarning("Invalid webhook payload structure");
                return new WebhookResponse { Success = false, Message = "Invalid payload" };
            }

            // Process different event types
            var success = await ProcessEventAsync(webhookEvent);

            return new WebhookResponse
            {
                Success = success,
                Message = success ? "Event processed successfully" : "Event processing failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook event: {EventType}", request.EventType);
            return new WebhookResponse { Success = false, Message = ex.Message };
        }
    }

    private async Task<bool> ProcessEventAsync(StripeWebhookEvent webhookEvent)
    {
        return webhookEvent.Type switch
        {
            "checkout.session.completed" => await HandleCheckoutSessionCompleted(webhookEvent),
            _ => await HandleUnknownEvent(webhookEvent)
        };
    }

    private async Task<bool> HandleCheckoutSessionCompleted(StripeWebhookEvent webhookEvent)
    {
        try
        {
            var sessionData = JsonSerializer.Deserialize<CheckoutSessionData>(webhookEvent.Data.Object);
            if (sessionData?.Id == null)
                return false;

            var order = await _orderRepository.GetByStripeSessionIdAsync(sessionData.Id);
            if (order == null)
            {
                _logger.LogWarning("Order not found for Stripe session: {SessionId}", sessionData.Id);
                return false;
            }

            // Mark order as paid using domain method
            order.MarkAsPaid();

            // Save the updated order
            await _orderRepository.UpdateAsync(order);

            // Publish PaymentCompletedEvent for downstream processing (notifications, etc.)
            var paymentCompletedEvent = new PaymentCompletedEvent(
                OrderId: order.Id.Value,
                PaymentIntentId: StripeDataExtractor.ExtractPaymentIntentFromSession(webhookEvent.Data.Object),
                Amount: order.TotalAmount.Amount,
                Currency: order.TotalAmount.Currency,
                CompletedAt: DateTimeOffset.UtcNow,
                ReceiptUrl: StripeDataExtractor.ExtractReceiptUrlFromSession(webhookEvent.Data.Object)
            );

            await _eventBus.PublishAsync(paymentCompletedEvent);
            _logger.LogInformation("PaymentCompletedEvent published for order: {OrderId}", order.Id.Value);

            // Send notification if URL is configured (legacy notification)
            if (!string.IsNullOrEmpty(order.NotificationUrl))
            {
                await SendOrderNotificationAsync(order);
            }

            _logger.LogInformation("Checkout session completed for order: {OrderId}", order.Id.Value);
            return true;
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain validation failed for webhook processing");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling checkout.session.completed event");
            return false;
        }
    }



    private async Task<bool> HandleUnknownEvent(StripeWebhookEvent webhookEvent)
    {
        _logger.LogInformation("Received unknown webhook event type: {EventType}", webhookEvent.Type);
        return true; // Don't fail on unknown events
    }

    private async Task SendOrderNotificationAsync(Order order)
    {
        try
        {
            var notificationPayload = new
            {
                orderId = order.Id.Value,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount.Amount,
                currency = order.TotalAmount.Currency,
                customerEmail = order.CustomerEmail.Value,
                timestamp = DateTime.UtcNow
            };

            var success = await _notificationService.SendNotificationAsync(
                order.NotificationUrl!,
                notificationPayload,
                3); // Default max retries - could be configurable

            if (success)
            {
                // Mark order as fulfilled using domain method
                order.MarkAsFulfilled();
                await _orderRepository.UpdateAsync(order);
                _logger.LogInformation("Notification sent successfully for order: {OrderId}", order.Id.Value);
            }
            else
            {
                _logger.LogWarning("Failed to send notification for order: {OrderId}", order.Id.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification for order: {OrderId}", order.Id.Value);
        }
    }
}

// Data transfer objects for webhook processing
public class WebhookRequest
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

public class WebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class StripeWebhookEvent
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public WebhookEventData Data { get; set; } = new();
}

public class WebhookEventData
{
    public JsonElement Object { get; set; }
}

public class CheckoutSessionData
{
    public string Id { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

// Helper methods for extracting data from Stripe session objects - should be in handler, not function
public static class StripeDataExtractor
{
    public static string ExtractPaymentIntentFromSession(JsonElement sessionObject)
    {
        try
        {
            if (sessionObject.TryGetProperty("payment_intent", out var paymentIntent))
            {
                return paymentIntent.GetString() ?? "unknown";
            }
        }
        catch
        {
            // Ignore extraction errors
        }
        return "unknown";
    }

    public static string? ExtractReceiptUrlFromSession(JsonElement sessionObject)
    {
        try
        {
            if (sessionObject.TryGetProperty("receipt_url", out var receiptUrl))
            {
                return receiptUrl.GetString();
            }
        }
        catch
        {
            // Ignore extraction errors
        }
        return null;
    }
}

