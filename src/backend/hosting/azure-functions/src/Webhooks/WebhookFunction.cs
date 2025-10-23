using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Events;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Handlers;

namespace WebhooksFn;

public class WebhookFunction
{
    private readonly ILogger<WebhookFunction> _logger;
    private readonly WebhookHandler _webhookHandler;
    private readonly IEventBus _eventBus;

    public WebhookFunction(ILogger<WebhookFunction> logger, WebhookHandler webhookHandler, IEventBus eventBus)
    {
        _logger = logger;
        _webhookHandler = webhookHandler;
        _eventBus = eventBus;
    }

    [Function("ProcessStripeWebhook")]
    public async Task<HttpResponseData> ProcessStripeWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/stripe")] HttpRequestData req)
    {
        _logger.LogInformation("Processing Stripe webhook");

        try
        {
            // Read request body and headers - minimal parsing in function
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var signature = req.Headers.GetValues("Stripe-Signature").FirstOrDefault() ?? "";
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";

            // Create webhook request for handler
            var webhookRequest = new WebhookRequest
            {
                EventType = "stripe_webhook",
                Payload = requestBody,
                Signature = signature,
                Secret = webhookSecret
            };

            // Delegate ALL business logic to the handler
            var result = await _webhookHandler.ProcessWebhookAsync(webhookRequest);

            // Handler is responsible for:
            // 1. Signature validation 
            // 2. Event deserialization
            // 3. Business logic processing
            // 4. Domain event publishing
            // 5. Order status updates

            // Function only returns HTTP response
            var response = req.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { success = result.Success, message = result.Message });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { success = false, message = "Internal server error" });
            return errorResponse;
        }
    }


}