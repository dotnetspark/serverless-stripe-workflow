using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StripeWorkflow.Domain.Services;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace StripeWorkflow.Services;

// Twilio SMS Service for SMS notifications
public class TwilioSmsService : INotificationService
{
    private readonly ILogger<TwilioSmsService> _logger;
    private readonly TwilioSettings _settings;

    public TwilioSmsService(
        IOptions<TwilioSettings> settings,
        ILogger<TwilioSmsService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendNotificationAsync(string destination, object payload, int maxRetries = 3)
    {
        // For SMS, the destination should be in format "sms://+1234567890"
        const string smsPrefix = "sms://";
        if (!destination.AsSpan().StartsWith(smsPrefix.AsSpan()))
        {
            _logger.LogWarning("Invalid SMS destination format: {Destination}", destination);
            return false;
        }

        // Use ReadOnlySpan to avoid string allocation - extract phone number after "sms://" prefix
        var phoneNumber = destination.AsSpan(smsPrefix.Length).ToString();

        try
        {
            // Extract SMS content from payload
            var smsContent = ExtractSmsContent(payload);

            // Initialize Twilio client
            TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);

            // Send SMS using Twilio SDK
            var message = await MessageResource.CreateAsync(
                body: smsContent,
                from: new Twilio.Types.PhoneNumber(_settings.FromPhoneNumber),
                to: new Twilio.Types.PhoneNumber(phoneNumber)
            );

            _logger.LogInformation("SMS sent to {PhoneNumber} with SID: {MessageSid}", phoneNumber, message.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
            return false;
        }
    }

    private string ExtractSmsContent(object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            // Extract message from payload or create default
            if (root.TryGetProperty("message", out var messageProp))
            {
                return messageProp.GetString() ?? "Payment confirmation";
            }

            // Fallback: create simple message from order info
            var orderId = root.TryGetProperty("orderId", out var orderIdProp) ? orderIdProp.GetString() : "unknown";
            var amount = root.TryGetProperty("totalAmount", out var amountProp) ? amountProp.GetDecimal() : 0;
            var currency = root.TryGetProperty("currency", out var currencyProp) ? currencyProp.GetString() : "USD";

            return $"Payment confirmed for order {orderId}. Amount: {currency?.ToUpper() ?? "USD"} {amount:F2}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract SMS content from payload, using default message");
            return "Payment confirmation - your order has been processed successfully.";
        }
    }
}
