using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;

namespace StripeWorkflow.Repositories.Cosmos;

public class CosmosOrderRepository : IOrderRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosOrderRepository> _logger;
    private const string ContainerName = "orders";
    private const string DatabaseName = "StripeWorkflow";

    public CosmosOrderRepository(CosmosClient cosmosClient, IOptions<CosmosDbSettings> settings, ILogger<CosmosOrderRepository> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(settings.Value.DatabaseName ?? DatabaseName);
        _container = database.GetContainer(settings.Value.ContainerName ?? ContainerName);
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        try
        {
            _logger.LogDebug("Getting order by ID: {OrderId}", id);
            var response = await _container.ReadItemAsync<CosmosOrderDocument>(id, new PartitionKey(id));
            return response.Resource.ToOrder();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Order not found: {OrderId}", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by ID: {OrderId}", id);
            throw;
        }
    }

    public async Task<Order?> GetByStripeSessionIdAsync(string stripeSessionId)
    {
        try
        {
            _logger.LogDebug("Getting order by Stripe session ID: {SessionId}", stripeSessionId);

            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.stripeSessionId = @stripeSessionId")
                .WithParameter("@stripeSessionId", stripeSessionId);

            var iterator = _container.GetItemQueryIterator<CosmosOrderDocument>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var document = response.FirstOrDefault();
                if (document != null)
                {
                    return document.ToOrder();
                }
            }

            _logger.LogDebug("Order not found for Stripe session: {SessionId}", stripeSessionId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by Stripe session ID: {SessionId}", stripeSessionId);
            throw;
        }
    }

    public async Task<Order> CreateAsync(Order order)
    {
        try
        {
            _logger.LogDebug("Creating order: {OrderId}", order.Id);

            var document = CosmosOrderDocument.FromOrder(order);
            var response = await _container.CreateItemAsync(document, new PartitionKey(order.Id));

            _logger.LogInformation("Created order: {OrderId}", order.Id);
            return response.Resource.ToOrder();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order: {OrderId}", order.Id);
            throw;
        }
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        try
        {
            _logger.LogDebug("Updating order: {OrderId}", order.Id);

            var document = CosmosOrderDocument.FromOrder(order);
            var response = await _container.ReplaceItemAsync(document, order.Id, new PartitionKey(order.Id));

            _logger.LogInformation("Updated order: {OrderId}", order.Id);
            return response.Resource.ToOrder();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order: {OrderId}", order.Id);
            throw;
        }
    }

    public async Task<bool> UpdateStatusAsync(string id, OrderStatus status, int expectedVersion)
    {
        try
        {
            _logger.LogDebug("Updating order status: {OrderId} to {Status}, expected version: {Version}", id, status, expectedVersion);

            // Use optimistic concurrency control with ETag
            var patchOperations = new[]
            {
                PatchOperation.Replace("/status", status.ToString()),
                PatchOperation.Replace("/updatedAt", DateTime.UtcNow),
                PatchOperation.Increment("/version", 1)
            };

            var patchItemRequestOptions = new PatchItemRequestOptions
            {
                FilterPredicate = $"FROM c WHERE c.version = {expectedVersion}"
            };

            var response = await _container.PatchItemAsync<CosmosOrderDocument>(id, new PartitionKey(id), patchOperations, patchItemRequestOptions);

            _logger.LogInformation("Updated order status: {OrderId} to {Status}", id, status);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogWarning("Optimistic concurrency conflict updating order: {OrderId}", id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status: {OrderId} to {Status}", id, status);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        try
        {
            _logger.LogDebug("Getting orders by status: {Status}", status);

            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.status = @status")
                .WithParameter("@status", status.ToString());

            var orders = new List<Order>();
            var iterator = _container.GetItemQueryIterator<CosmosOrderDocument>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                orders.AddRange(response.Select(doc => doc.ToOrder()));
            }

            _logger.LogDebug("Found {Count} orders with status: {Status}", orders.Count, status);
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders by status: {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetPendingRetryOrdersAsync()
    {
        try
        {
            _logger.LogDebug("Getting orders pending retry");

            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.status = @status AND c.retryCount < c.maxRetries")
                .WithParameter("@status", OrderStatus.Failed.ToString());

            var orders = new List<Order>();
            var iterator = _container.GetItemQueryIterator<CosmosOrderDocument>(queryDefinition);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                orders.AddRange(response.Select(doc => doc.ToOrder()));
            }

            _logger.LogDebug("Found {Count} orders pending retry", orders.Count);
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending retry orders");
            throw;
        }
    }
}

public class CosmosDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "StripeWorkflow";
    public string ContainerName { get; set; } = "orders";
}
