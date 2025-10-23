using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StripeWorkflow.Handlers.DependencyInjection;
using StripeWorkflow.Infrastructure.Extensions;
using StripeWorkflow.Repositories.Extensions;
using StripeWorkflow.Services.Extensions;

namespace StripeWorkflow.Hosting;

/// <summary>
/// Provides a standardized host builder configuration for Azure Functions
/// with Azure Key Vault integration and all shared dependencies.
/// </summary>
public static class AzureFunctionsHostBuilder
{
    /// <summary>
    /// Creates and configures a standardized host for Azure Functions
    /// with automatic environment-based configuration (local.settings.json vs Azure Key Vault)
    /// and all shared service registrations.
    /// </summary>
    /// <returns>Configured and built host ready to run</returns>
    public static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();

        // Add Aspire service defaults (telemetry, health checks, service discovery, resilience)
        builder.AddServiceDefaults();

        // Configure Azure Functions
        builder.Services.AddFunctionsWorkerDefaults();
        builder.Services.ConfigureFunctionsApplicationInsights();

        // In production, use Azure Key Vault for additional configuration
        var keyVaultName = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_NAME");
        if (!string.IsNullOrEmpty(keyVaultName))
        {
            var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
            builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
        }

        // Add shared handlers from the Handlers library
        builder.Services.AddStripeWorkflowHandlers();

        // Add services with Aspire resilience patterns
        builder.Services.AddStripeWorkflowServices(builder.Configuration);

        // Add repository implementations (defaults to Cosmos DB)
        builder.Services.AddStripeWorkflowRepositories(builder.Configuration);

        // Add messaging services (Service Bus and Event Grid)
        builder.Services.AddMessaging();

        return builder.Build();
    }
}