using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Handlers;

namespace NotifyFn;

public class EmailNotificationFunction
{
    private readonly ILogger<EmailNotificationFunction> _logger;
    private readonly IEventBus _eventBus;
    private readonly IOrderRepository _orderRepository;
    private readonly INotificationService _notificationService;

    public EmailNotificationFunction(
        ILogger<EmailNotificationFunction> logger,
        IEventBus eventBus,
        IOrderRepository orderRepository,
        INotificationService notificationService)
    {
        _logger = logger;
        _eventBus = eventBus;
        _orderRepository = orderRepository;
        _notificationService = notificationService;
    }

    [Function("ProcessEmailNotification")]
    public async Task ProcessEmailNotification(
        [ServiceBusTrigger("payment-events", "email-notifications", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing email notification: {Message}", message);

        try
        {
            // Deserialize the payment event
            var envelope = MessageEnvelopeExtensions.FromJson<PaymentCompletedEvent>(message);

            if (envelope?.Data == null)
            {
                _logger.LogError("Invalid event envelope: {Message}", message);
                return;
            }

            var paymentEvent = envelope.Data;
            var correlationId = envelope.CorrelationId ?? Guid.NewGuid().ToString();

            _logger.LogInformation("Sending email notification for order {OrderId}, CorrelationId: {CorrelationId}",
                paymentEvent.OrderId, correlationId);

            // Get order details for email content
            var order = await _orderRepository.GetByIdAsync(paymentEvent.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for email notification: {OrderId}", paymentEvent.OrderId);
                return;
            }

            // Create email-specific payload
            var emailPayload = new
            {
                orderId = order.Id.Value,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount.Amount,
                currency = order.TotalAmount.Currency,
                customerEmail = order.CustomerEmail.Value,
                paymentIntentId = paymentEvent.PaymentIntentId,
                receiptUrl = paymentEvent.ReceiptUrl,
                items = order.Items.Select(item => new
                {
                    productId = item.ProductId,
                    productName = item.ProductName,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice.Amount,
                    totalPrice = item.TotalPrice.Amount
                }),
                timestamp = DateTime.UtcNow,
                correlationId = correlationId
            };

            // Send email using the notification service
            var success = await _notificationService.SendNotificationAsync(
                $"email://{order.CustomerEmail.Value}", // Use email protocol for routing
                emailPayload,
                3); // Max retries

            // Publish notification sent event
            var notificationSentEvent = new NotificationSentEvent(
                OrderId: paymentEvent.OrderId,
                CustomerId: order.CustomerId.Value,
                NotificationType: "payment_completed",
                Channel: "email",
                Success: success,
                ErrorMessage: success ? null : "Email notification failed",
                SentAt: DateTimeOffset.UtcNow
            );

            await _eventBus.PublishAsync(notificationSentEvent);

            if (success)
            {
                _logger.LogInformation("Email notification sent successfully for order: {OrderId}, CorrelationId: {CorrelationId}",
                    paymentEvent.OrderId, correlationId);
            }
            else
            {
                _logger.LogError("Failed to send email notification for order: {OrderId}, CorrelationId: {CorrelationId}",
                    paymentEvent.OrderId, correlationId);
                throw new InvalidOperationException($"Email notification failed for order {paymentEvent.OrderId}");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize payment event: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email notification: {Message}", message);
            throw;
        }
    }
}