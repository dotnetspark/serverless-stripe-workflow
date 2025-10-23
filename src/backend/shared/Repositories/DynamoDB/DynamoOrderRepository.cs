using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Repositories;

namespace StripeWorkflow.Repositories.DynamoDB;

public class DynamoOrderRepository : IOrderRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoOrderRepository> _logger;
    private readonly string _tableName;

    public DynamoOrderRepository(IAmazonDynamoDB dynamoDb, IOptions<DynamoDbSettings> settings, ILogger<DynamoOrderRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
        _tableName = settings.Value.TableName;
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        try
        {
            _logger.LogDebug("Getting order by ID: {OrderId}", id);

            var request = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"ORDER#{id}" },
                    ["SK"] = new AttributeValue { S = $"ORDER#{id}" }
                }
            };

            var response = await _dynamoDb.GetItemAsync(request);

            if (response.Item.Count == 0)
            {
                _logger.LogDebug("Order not found: {OrderId}", id);
                return null;
            }

            return DynamoOrderDocument.FromDynamoItem(response.Item).ToOrder();
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

            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI1",
                KeyConditionExpression = "GSI1PK = :sessionId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":sessionId"] = new AttributeValue { S = $"SESSION#{stripeSessionId}" }
                }
            };

            var response = await _dynamoDb.QueryAsync(request);

            if (response.Items.Count == 0)
            {
                _logger.LogDebug("Order not found for Stripe session: {SessionId}", stripeSessionId);
                return null;
            }

            return DynamoOrderDocument.FromDynamoItem(response.Items[0]).ToOrder();
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

            var document = DynamoOrderDocument.FromOrder(order);
            var item = document.ToDynamoItem();

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                ConditionExpression = "attribute_not_exists(PK)"
            };

            await _dynamoDb.PutItemAsync(request);

            _logger.LogInformation("Created order: {OrderId}", order.Id);
            return order;
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

            var document = DynamoOrderDocument.FromOrder(order);
            var item = document.ToDynamoItem();

            var request = new PutItemRequest
            {
                TableName = _tableName,
                Item = item,
                ConditionExpression = "attribute_exists(PK) AND version = :expectedVersion",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":expectedVersion"] = new AttributeValue { N = (order.Version - 1).ToString() }
                }
            };

            await _dynamoDb.PutItemAsync(request);

            _logger.LogInformation("Updated order: {OrderId}", order.Id);
            return order;
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogWarning("Optimistic concurrency conflict updating order: {OrderId}", order.Id);
            throw new InvalidOperationException($"Optimistic concurrency conflict for order {order.Id}");
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

            var request = new UpdateItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = $"ORDER#{id}" },
                    ["SK"] = new AttributeValue { S = $"ORDER#{id}" }
                },
                UpdateExpression = "SET #status = :status, updatedAt = :updatedAt, version = version + :inc",
                ConditionExpression = "version = :expectedVersion",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = status.ToString() },
                    [":updatedAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") },
                    [":expectedVersion"] = new AttributeValue { N = expectedVersion.ToString() },
                    [":inc"] = new AttributeValue { N = "1" }
                }
            };

            await _dynamoDb.UpdateItemAsync(request);

            _logger.LogInformation("Updated order status: {OrderId} to {Status}", id, status);
            return true;
        }
        catch (ConditionalCheckFailedException)
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

            var request = new QueryRequest
            {
                TableName = _tableName,
                IndexName = "GSI2",
                KeyConditionExpression = "GSI2PK = :status",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":status"] = new AttributeValue { S = $"STATUS#{status}" }
                }
            };

            var orders = new List<Order>();
            QueryResponse response;

            do
            {
                response = await _dynamoDb.QueryAsync(request);
                orders.AddRange(response.Items.Select(item => DynamoOrderDocument.FromDynamoItem(item).ToOrder()));
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            } while (response.LastEvaluatedKey?.Count > 0);

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

            var request = new ScanRequest
            {
                TableName = _tableName,
                FilterExpression = "#status = :failedStatus AND retryCount < maxRetries",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":failedStatus"] = new AttributeValue { S = OrderStatus.Failed.ToString() }
                }
            };

            var orders = new List<Order>();
            ScanResponse response;

            do
            {
                response = await _dynamoDb.ScanAsync(request);
                orders.AddRange(response.Items.Select(item => DynamoOrderDocument.FromDynamoItem(item).ToOrder()));
                request.ExclusiveStartKey = response.LastEvaluatedKey;
            } while (response.LastEvaluatedKey?.Count > 0);

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

public class DynamoDbSettings
{
    public string TableName { get; set; } = "stripe-workflow-orders";
    public string Region { get; set; } = "us-east-1";
}