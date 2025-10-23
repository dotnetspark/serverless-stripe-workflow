using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Services;

namespace NotifyFn;

public class SmsNotificationFunction
{
    private readonly ILogger<SmsNotificationFunction> _logger;
    private readonly IEventBus _eventBus;
    private readonly IOrderRepository _orderRepository;
    private readonly TwilioSmsService _twilioSmsService;

    public SmsNotificationFunction(
        ILogger<SmsNotificationFunction> logger,
        IEventBus eventBus,
        IOrderRepository orderRepository,
        TwilioSmsService twilioSmsService)
    {
        _logger = logger;
        _eventBus = eventBus;
        _orderRepository = orderRepository;
        _twilioSmsService = twilioSmsService;
    }
    [Function("ProcessSmsNotification")]
    public async Task ProcessSmsNotification(
        [ServiceBusTrigger("payment-events", "sms-notifications", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing SMS notification: {Message}", message);

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

            _logger.LogInformation("Sending SMS notification for order {OrderId}, CorrelationId: {CorrelationId}",
                paymentEvent.OrderId, correlationId);

            // Get order details for SMS content
            var order = await _orderRepository.GetByIdAsync(paymentEvent.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found for SMS notification: {OrderId}", paymentEvent.OrderId);
                return;
            }

            // Check if customer has opted in for SMS notifications and has a phone number
            if (!order.CustomerSmsOptIn)
            {
                _logger.LogInformation("Customer {CustomerId} has not opted in for SMS notifications, skipping SMS for order {OrderId}",
                    order.CustomerId, paymentEvent.OrderId);
                return;
            }

            if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            {
                _logger.LogWarning("Customer {CustomerId} has SMS opt-in enabled but no phone number available for order {OrderId}",
                    order.CustomerId, paymentEvent.OrderId);
                return;
            }

            var customerPhone = order.CustomerPhone;

            // Create SMS-specific payload (shorter than email)
            var smsPayload = new
            {
                orderId = order.Id.Value,
                totalAmount = order.TotalAmount.Amount,
                currency = order.TotalAmount.Currency,
                customerPhone,
                paymentIntentId = paymentEvent.PaymentIntentId,
                message = $"Payment confirmed for order {order.Id.Value}. Amount: {order.TotalAmount.Currency.ToUpper()} {order.TotalAmount.Amount:F2}",
                timestamp = DateTime.UtcNow,
                correlationId
            };

            // Send SMS using the Twilio SMS service
            var success = await _twilioSmsService.SendNotificationAsync(
                $"sms://{customerPhone}", // Use SMS protocol for routing
                smsPayload,
                3); // Max retries

            // Publish notification sent event
            var notificationSentEvent = new NotificationSentEvent(
                OrderId: paymentEvent.OrderId,
                CustomerId: order.CustomerId.Value,
                NotificationType: "payment_completed",
                Channel: "sms",
                Success: success,
                ErrorMessage: success ? null : "SMS notification failed",
                SentAt: DateTimeOffset.UtcNow
            );

            await _eventBus.PublishAsync(notificationSentEvent);

            if (success)
            {
                _logger.LogInformation("SMS notification sent successfully for order: {OrderId}, CorrelationId: {CorrelationId}",
                    paymentEvent.OrderId, correlationId);
            }
            else
            {
                _logger.LogError("Failed to send SMS notification for order: {OrderId}, CorrelationId: {CorrelationId}",
                    paymentEvent.OrderId, correlationId);
                throw new InvalidOperationException($"SMS notification failed for order {paymentEvent.OrderId}");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize payment event: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SMS notification: {Message}", message);
            throw;
        }
    }
}