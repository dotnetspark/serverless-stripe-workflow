using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Handlers.DependencyInjection;
using StripeWorkflow.Repositories.Extensions;
using StripeWorkflow.Services.Extensions;

namespace AwsLambda;

public static class LambdaStartup
{
    private static readonly Lazy<IServiceProvider> _serviceProvider = new(ConfigureServices);

    public static IServiceProvider ServiceProvider => _serviceProvider.Value;

    public static T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // HTTP Client
        services.AddHttpClient();

        // Stripe Workflow dependencies
        services.AddStripeWorkflowHandlers();
        services.AddStripeWorkflowServices(configuration);
        services.AddStripeWorkflowRepositories(configuration);

        return services.BuildServiceProvider();
    }
}