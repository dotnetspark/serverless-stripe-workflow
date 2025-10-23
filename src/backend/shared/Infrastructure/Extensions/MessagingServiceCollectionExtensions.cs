using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StripeWorkflow.Domain.Messaging;
using StripeWorkflow.Infrastructure.Messaging;

namespace StripeWorkflow.Infrastructure.Extensions;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        // Register Service Bus components
        services.AddSingleton<ServiceBusClient>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var connectionString = configuration.GetConnectionString("ServiceBus")
                                  ?? "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true";
            return new ServiceBusClient(connectionString);
        });

        // Register messaging abstractions - using ServiceBus for all messaging
        services.AddScoped<IEventBus, ServiceBusEventBus>();

        return services;
    }
}

public static class MessagingHealthCheckExtensions
{
    public static IServiceCollection AddMessagingHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<ServiceBusHealthCheck>("servicebus");

        return services;
    }
}