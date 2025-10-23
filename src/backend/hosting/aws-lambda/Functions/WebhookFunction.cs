using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Handlers;

namespace AwsLambda.Functions;

public class WebhookFunction
{
    private readonly WebhookHandler _webhookHandler;
    private readonly ILogger<WebhookFunction> _logger;

    public WebhookFunction()
    {
        _webhookHandler = LambdaStartup.GetService<WebhookHandler>();
        _logger = LambdaStartup.GetService<ILogger<WebhookFunction>>();
    }

    public async Task<APIGatewayProxyResponse> ProcessStripeWebhook(APIGatewayProxyRequest request, ILambdaContext context)
    {
        _logger.LogInformation("Processing Stripe webhook");

        try
        {
            // Extract signature from headers
            var signature = request.Headers?.TryGetValue("Stripe-Signature", out var sig) == true ? sig : "";
            var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";

            // Create webhook request
            var webhookRequest = new WebhookRequest
            {
                EventType = "stripe_webhook",
                Payload = request.Body,
                Signature = signature,
                Secret = webhookSecret
            };

            // Call shared handler
            var result = await _webhookHandler.ProcessWebhookAsync(webhookRequest);

            // Return response
            return new APIGatewayProxyResponse
            {
                StatusCode = result.Success ? 200 : 400,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                Body = JsonSerializer.Serialize(new { success = result.Success, message = result.Message })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                Body = JsonSerializer.Serialize(new { success = false, message = "Internal server error" })
            };
        }
    }
}