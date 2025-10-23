using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Handlers;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AwsLambda.Functions;

public class CheckoutFunction
{
    private readonly CheckoutHandler _checkoutHandler;
    private readonly ILogger<CheckoutFunction> _logger;

    public CheckoutFunction()
    {
        _checkoutHandler = LambdaStartup.GetService<CheckoutHandler>();
        _logger = LambdaStartup.GetService<ILogger<CheckoutFunction>>();
    }

    public async Task<APIGatewayProxyResponse> CreateCheckoutSession(APIGatewayProxyRequest request, ILambdaContext context)
    {
        _logger.LogInformation("Processing checkout session creation request");

        try
        {
            // Parse request body
            var checkoutRequest = JsonSerializer.Deserialize<CheckoutRequest>(request.Body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (checkoutRequest == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                    Body = JsonSerializer.Serialize(new { error = "Invalid request body" })
                };
            }

            // Call shared handler
            var result = await _checkoutHandler.CreateCheckoutSessionAsync(checkoutRequest);

            // Return success response
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                },
                Body = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                })
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid checkout request");
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                Body = JsonSerializer.Serialize(new { error = ex.Message })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } },
                Body = JsonSerializer.Serialize(new { error = "Internal server error" })
            };
        }
    }
}