using Microsoft.Extensions.DependencyInjection;
using StripeWorkflow.Handlers;

namespace StripeWorkflow.Handlers.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all shared handlers with the dependency injection container
    /// </summary>
    public static IServiceCollection AddStripeWorkflowHandlers(this IServiceCollection services)
    {
        services.AddScoped<CheckoutHandler>();
        services.AddScoped<WebhookHandler>();
        services.AddScoped<NotificationHandler>();

        return services;
    }
}