using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Services;

namespace StripeWorkflow.Services;

public class StripePaymentService : IPaymentService
{
    private readonly ILogger<StripePaymentService> _logger;
    private readonly StripeSettings _settings;

    public StripePaymentService(ILogger<StripePaymentService> logger, IOptions<StripeSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;

        // Configure Stripe API key
        StripeConfiguration.ApiKey = _settings.SecretKey;
    }

    public async Task<string> CreateCheckoutSessionAsync(string orderId, IEnumerable<OrderItem> items, string customerEmail, string successUrl, string cancelUrl)
    {
        try
        {
            _logger.LogInformation("Creating Stripe checkout session for order {OrderId}", orderId);

            var lineItems = items.Select(item => new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(item.UnitPrice.Amount * 100), // Convert to cents
                    Currency = item.UnitPrice.Currency.ToLowerInvariant(),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.ProductName,
                    },
                },
                Quantity = item.Quantity,
            }).ToList();

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = lineItems,
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                CustomerEmail = customerEmail,
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", orderId }
                },
                ExpiresAt = DateTime.UtcNow.AddHours(1) // Session expires in 1 hour
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            _logger.LogInformation("Successfully created Stripe checkout session {SessionId} for order {OrderId}",
                session.Id, orderId);

            return session.Id;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error while creating checkout session for order {OrderId}: {Error}",
                orderId, ex.Message);
            throw new InvalidOperationException($"Payment service error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating checkout session for order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<bool> ValidateWebhookSignatureAsync(string payload, string signature, string secret)
    {
        try
        {
            _logger.LogDebug("Validating Stripe webhook signature");

            // Use provided secret or fall back to configured webhook secret
            var webhookSecret = !string.IsNullOrWhiteSpace(secret) ? secret : _settings.WebhookSecret;

            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                _logger.LogError("Webhook secret is not configured");
                return false;
            }

            // Stripe's webhook signature validation
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);

            _logger.LogDebug("Successfully validated Stripe webhook signature for event {EventType}",
                stripeEvent.Type);

            return true;
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature: {Error}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while validating webhook signature");
            return false;
        }
    }

    public async Task<Event> GetWebhookEventAsync(string payload, string signature, string secret)
    {
        try
        {
            _logger.LogDebug("Constructing Stripe webhook event");

            // Use provided secret or fall back to configured webhook secret
            var webhookSecret = !string.IsNullOrWhiteSpace(secret) ? secret : _settings.WebhookSecret;

            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                throw new InvalidOperationException("Webhook secret is not configured");
            }

            // Stripe's webhook event construction
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);

            _logger.LogInformation("Successfully constructed Stripe webhook event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            return stripeEvent;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe error while constructing webhook event: {Error}", ex.Message);
            throw new InvalidOperationException($"Invalid webhook: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while constructing webhook event");
            throw;
        }
    }
}