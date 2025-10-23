using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using SendGrid;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Services;

namespace StripeWorkflow.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStripeWorkflowServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add memory cache for FakeStore API caching
        services.AddMemoryCache();

        // Configure settings
        services.Configure<StripeSettings>(configuration.GetSection("Stripe"));
        services.Configure<SendGridSettings>(configuration.GetSection("SendGrid"));
        services.Configure<TwilioSettings>(configuration.GetSection("Twilio"));

        // Register payment service
        services.AddSingleton<IPaymentService, StripePaymentService>();

        // Register SendGrid client and notification service (for email)
        services.AddSingleton<ISendGridClient>(provider =>
        {
            var sendGridSettings = configuration.GetSection("SendGrid").Get<SendGridSettings>() ?? new SendGridSettings();
            return new SendGridClient(sendGridSettings.ApiKey);
        });
        services.AddSingleton<INotificationService, SendGridNotificationService>();

        // Register Twilio SMS service (for SMS)
        services.AddSingleton<TwilioSmsService>();

        // Register product catalog service with resilient HttpClient
        services.AddHttpClient<IProductCatalogService, FakeStoreProductCatalogService>(client =>
        {
            client.BaseAddress = new Uri("https://fakestoreapi.com");
            client.DefaultRequestHeaders.Add("User-Agent", "StripeWorkflow/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler(options =>
        {
            // Configure retry policy - leveraging Aspire's built-in resilience
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;

            // Configure circuit breaker
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;

            // Configure timeout
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}