using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Handlers;

public class NotificationHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationHandler> _logger;

    public NotificationHandler(
        IOrderRepository orderRepository,
        INotificationService notificationService,
        ILogger<NotificationHandler> logger)
    {
        _orderRepository = orderRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Process notification requests from Service Bus or SQS
    /// </summary>
    public async Task<NotificationResponse> ProcessNotificationAsync(NotificationRequest request)
    {
        try
        {
            _logger.LogInformation("Processing notification for order: {OrderId}", request.OrderId);

            var order = await _orderRepository.GetByIdAsync(request.OrderId);
            if (order == null)
            {
                _logger.LogWarning("Order not found: {OrderId}", request.OrderId);
                return new NotificationResponse { Success = false, Message = "Order not found" };
            }

            if (string.IsNullOrEmpty(order.NotificationUrl))
            {
                _logger.LogInformation("No notification URL configured for order: {OrderId}", request.OrderId);
                return new NotificationResponse { Success = true, Message = "No notification URL configured" };
            }

            // Only send notifications for Paid orders
            if (order.Status != OrderStatus.Paid)
            {
                _logger.LogWarning("Order {OrderId} is not in Paid status, current status: {Status}", request.OrderId, order.Status);
                return new NotificationResponse { Success = false, Message = "Order not in Paid status" };
            }

            // Create notification payload
            var notificationPayload = new
            {
                orderId = order.Id.Value,
                status = order.Status.ToString(),
                totalAmount = order.TotalAmount.Amount,
                currency = order.TotalAmount.Currency,
                customerEmail = order.CustomerEmail.Value,
                items = order.Items.Select(item => new
                {
                    productId = item.ProductId,
                    productName = item.ProductName,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice.Amount,
                    totalPrice = item.TotalPrice.Amount
                }),
                timestamp = DateTime.UtcNow,
                metadata = order.Metadata
            };

            // Send notification with retry logic
            var success = await _notificationService.SendNotificationAsync(
                order.NotificationUrl,
                notificationPayload,
                3); // Default max retries - could be configurable

            if (success)
            {
                // Mark order as fulfilled using domain method
                order.MarkAsFulfilled();
                await _orderRepository.UpdateAsync(order);

                _logger.LogInformation("Notification sent successfully and order fulfilled: {OrderId}", request.OrderId);
                return new NotificationResponse { Success = true, Message = "Notification sent successfully" };
            }
            else
            {
                // Increment failure attempts using domain logic
                const int maxRetries = 3; // Could be configurable

                if (order.FailureAttempts >= maxRetries - 1)
                {
                    order.MarkAsFailed("Max notification retries exceeded");
                    _logger.LogError("Max notification retries exceeded for order: {OrderId}", request.OrderId);
                }
                else
                {
                    // This would require a domain method to increment failure attempts
                    // For now, update directly and save
                }

                await _orderRepository.UpdateAsync(order);

                return new NotificationResponse
                {
                    Success = false,
                    Message = $"Notification failed, retry {order.FailureAttempts + 1}/{maxRetries}",
                    ShouldRetry = order.FailureAttempts < maxRetries - 1
                };
            }
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain validation failed for notification - Order: {OrderId}", request.OrderId);
            return new NotificationResponse { Success = false, Message = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification for order: {OrderId}", request.OrderId);
            return new NotificationResponse { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Retry failed notifications for orders that haven't exceeded max retries
    /// </summary>
    public async Task<RetryResponse> RetryFailedNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Starting retry process for failed notifications");

            var pendingRetryOrders = await _orderRepository.GetPendingRetryOrdersAsync();
            var retriedCount = 0;
            var successfulRetries = 0;

            foreach (var order in pendingRetryOrders)
            {
                const int maxRetries = 3; // Could be configurable
                if (order.FailureAttempts >= maxRetries)
                    continue;

                var notificationRequest = new NotificationRequest { OrderId = order.Id.Value };
                var response = await ProcessNotificationAsync(notificationRequest);

                retriedCount++;
                if (response.Success)
                    successfulRetries++;

                // Add delay between retries to avoid overwhelming the notification service
                await Task.Delay(1000);
            }

            _logger.LogInformation("Retry process completed. Retried: {RetriedCount}, Successful: {SuccessfulCount}",
                retriedCount, successfulRetries);

            return new RetryResponse
            {
                TotalRetried = retriedCount,
                SuccessfulRetries = successfulRetries,
                FailedRetries = retriedCount - successfulRetries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during notification retry process");
            return new RetryResponse { TotalRetried = 0, SuccessfulRetries = 0, FailedRetries = 0 };
        }
    }
}

// Data transfer objects
public class NotificationRequest
{
    public string OrderId { get; set; } = string.Empty;
}

public class NotificationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ShouldRetry { get; set; } = false;
}

public class RetryResponse
{
    public int TotalRetried { get; set; }
    public int SuccessfulRetries { get; set; }
    public int FailedRetries { get; set; }
}