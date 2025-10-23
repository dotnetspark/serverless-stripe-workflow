using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using StripeWorkflow.Domain.Services;

namespace StripeWorkflow.Services;

public class SendGridNotificationService : INotificationService
{
    private readonly ISendGridClient _sendGridClient;
    private readonly ILogger<SendGridNotificationService> _logger;
    private readonly SendGridSettings _settings;

    public SendGridNotificationService(
        ISendGridClient sendGridClient,
        IOptions<SendGridSettings> settings,
        ILogger<SendGridNotificationService> logger)
    {
        _sendGridClient = sendGridClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendNotificationAsync(string url, object payload, int maxRetries = 3)
    {
        // For this implementation, we'll send email notifications instead of HTTP webhooks
        // This aligns better with SendGrid's purpose as an email service

        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            // Extract order information from payload for email content
            var orderInfo = ExtractOrderInfo(payload);

            var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
            var to = new EmailAddress(orderInfo.CustomerEmail);
            var subject = $"Order Confirmation - {orderInfo.OrderId}";

            var plainTextContent = $@"
Thank you for your order!

Order ID: {orderInfo.OrderId}
Status: {orderInfo.Status}
Total: ${orderInfo.TotalAmount:F2} {orderInfo.Currency?.ToUpper()}

Order Details:
{string.Join("\n", orderInfo.Items.Select(item => $"- {item.ProductName} x{item.Quantity} = ${item.TotalPrice:F2}"))}

We'll send you another email when your order ships.

Best regards,
The Stripe Workflow Team
";

            var htmlContent = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <h2 style='color: #4F46E5;'>Thank you for your order!</h2>
    
    <div style='background-color: #F8FAFC; padding: 20px; border-radius: 8px; margin: 20px 0;'>
        <h3>Order Summary</h3>
        <p><strong>Order ID:</strong> {orderInfo.OrderId}</p>
        <p><strong>Status:</strong> <span style='color: #059669;'>{orderInfo.Status}</span></p>
        <p><strong>Total:</strong> ${orderInfo.TotalAmount:F2} {orderInfo.Currency?.ToUpper()}</p>
    </div>

    <div style='margin: 20px 0;'>
        <h3>Items Ordered</h3>
        <table style='width: 100%; border-collapse: collapse;'>
            <thead>
                <tr style='background-color: #F1F5F9;'>
                    <th style='padding: 10px; text-align: left; border: 1px solid #E2E8F0;'>Product</th>
                    <th style='padding: 10px; text-align: center; border: 1px solid #E2E8F0;'>Qty</th>
                    <th style='padding: 10px; text-align: right; border: 1px solid #E2E8F0;'>Price</th>
                </tr>
            </thead>
            <tbody>
                {string.Join("", orderInfo.Items.Select(item => $@"
                <tr>
                    <td style='padding: 10px; border: 1px solid #E2E8F0;'>{item.ProductName}</td>
                    <td style='padding: 10px; text-align: center; border: 1px solid #E2E8F0;'>{item.Quantity}</td>
                    <td style='padding: 10px; text-align: right; border: 1px solid #E2E8F0;'>${item.TotalPrice:F2}</td>
                </tr>"))}
            </tbody>
        </table>
    </div>

    <p>We'll send you another email when your order ships.</p>
    
    <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #E2E8F0; color: #64748B; font-size: 14px;'>
        <p>Best regards,<br>The Stripe Workflow Team</p>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            // Add order ID to custom args for tracking
            msg.AddCustomArg("order_id", orderInfo.OrderId);
            msg.AddCustomArg("notification_type", "order_confirmation");

            var response = await _sendGridClient.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email notification sent successfully for order {OrderId}", orderInfo.OrderId);
                return true;
            }
            else
            {
                var responseBody = await response.Body.ReadAsStringAsync();
                _logger.LogError("Failed to send email notification for order {OrderId}. StatusCode: {StatusCode}, Response: {Response}",
                    orderInfo.OrderId, response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending notification");
            return false;
        }
    }

    private OrderNotificationInfo ExtractOrderInfo(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var orderInfo = new OrderNotificationInfo
            {
                OrderId = root.TryGetProperty("orderId", out var orderIdProp) ? orderIdProp.GetString() ?? "Unknown" : "Unknown",
                Status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "Unknown" : "Unknown",
                TotalAmount = root.TryGetProperty("totalAmount", out var totalProp) ? totalProp.GetDecimal() : 0,
                Currency = root.TryGetProperty("currency", out var currencyProp) ? currencyProp.GetString() : "usd",
                CustomerEmail = root.TryGetProperty("customerEmail", out var emailProp) ? emailProp.GetString() ?? "" : ""
            };

            if (root.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsProp.EnumerateArray())
                {
                    orderInfo.Items.Add(new OrderItemInfo
                    {
                        ProductName = item.TryGetProperty("productName", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Quantity = item.TryGetProperty("quantity", out var qtyProp) ? qtyProp.GetInt32() : 0,
                        TotalPrice = item.TryGetProperty("totalPrice", out var priceProp) ? priceProp.GetDecimal() : 0
                    });
                }
            }

            return orderInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract order info from payload");
            return new OrderNotificationInfo { OrderId = "Unknown", CustomerEmail = "support@example.com" };
        }
    }
}

public class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@stripelworkflow.com";
    public string FromName { get; set; } = "Stripe Workflow";
}

public class OrderNotificationInfo
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItemInfo> Items { get; set; } = new();
}

public class OrderItemInfo
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}
