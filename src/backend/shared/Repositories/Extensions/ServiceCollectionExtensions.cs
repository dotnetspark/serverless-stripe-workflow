using Amazon.DynamoDBv2;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StripeWorkflow.Domain.Repositories;
using StripeWorkflow.Repositories.Cosmos;
using StripeWorkflow.Repositories.DynamoDB;

namespace StripeWorkflow.Repositories.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register Azure Cosmos DB repositories
    /// </summary>
    public static IServiceCollection AddCosmosRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Cosmos DB settings
        services.Configure<CosmosDbSettings>(configuration.GetSection("CosmosDb"));

        // Register Cosmos client as singleton
        services.AddSingleton<CosmosClient>(provider =>
        {
            var settings = configuration.GetSection("CosmosDb").Get<CosmosDbSettings>() ?? new CosmosDbSettings();
            return new CosmosClient(settings.ConnectionString);
        });

        // Register repositories
        services.AddScoped<IOrderRepository, CosmosOrderRepository>();

        return services;
    }

    /// <summary>
    /// Register AWS DynamoDB repositories
    /// </summary>
    public static IServiceCollection AddDynamoDbRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure DynamoDB settings
        services.Configure<DynamoDbSettings>(configuration.GetSection("DynamoDb"));

        // Register DynamoDB client as singleton
        services.AddSingleton<IAmazonDynamoDB>(provider =>
        {
            var settings = configuration.GetSection("DynamoDb").Get<DynamoDbSettings>() ?? new DynamoDbSettings();
            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(settings.Region)
            };
            return new AmazonDynamoDBClient(config);
        });

        // Register repositories
        services.AddScoped<IOrderRepository, DynamoOrderRepository>();

        return services;
    }

    /// <summary>
    /// Register repositories based on configuration
    /// </summary>
    public static IServiceCollection AddStripeWorkflowRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        var repositoryProvider = configuration.GetValue<string>("RepositoryProvider") ?? "Cosmos";

        if (repositoryProvider.Equals("cosmos", StringComparison.OrdinalIgnoreCase) ||
            repositoryProvider.Equals("cosmosdb", StringComparison.OrdinalIgnoreCase))
        {
            return services.AddCosmosRepositories(configuration);
        }
        else if (repositoryProvider.Equals("dynamo", StringComparison.OrdinalIgnoreCase) ||
                 repositoryProvider.Equals("dynamodb", StringComparison.OrdinalIgnoreCase))
        {
            return services.AddDynamoDbRepositories(configuration);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported repository provider: {repositoryProvider}");
        }
    }
}