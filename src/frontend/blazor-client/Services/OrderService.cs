using System.Text.Json;

namespace BlazorClient.Services;

public interface IOrderService
{
    Task<OrderSubmissionResult> SubmitOrderAsync(OrderSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<OrderCancellationResult> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
}

public class OrderService : IOrderService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrderService(HttpClient httpClient, ILogger<OrderService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<OrderSubmissionResult> SubmitOrderAsync(OrderSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Submitting order for customer {CustomerId}", request.CustomerId);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/orders/submit", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<OrderSubmissionResult>(responseJson, _jsonOptions);
                _logger.LogInformation("Order submitted successfully: {OrderId}", result?.OrderId);
                return result ?? new OrderSubmissionResult { Success = false, Error = "Invalid response" };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Order submission failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new OrderSubmissionResult { Success = false, Error = $"Server error: {response.StatusCode}" };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during order submission");
            return new OrderSubmissionResult { Success = false, Error = "Network error occurred" };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Order submission timed out");
            return new OrderSubmissionResult { Success = false, Error = "Request timed out" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during order submission");
            return new OrderSubmissionResult { Success = false, Error = "An unexpected error occurred" };
        }
    }

    public async Task<OrderCancellationResult> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Cancelling order {OrderId}", orderId);

            var response = await _httpClient.PostAsync($"api/orders/{orderId}/cancel", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<OrderCancellationResult>(responseJson, _jsonOptions);
                _logger.LogInformation("Order cancellation requested successfully: {OrderId}", orderId);
                return result ?? new OrderCancellationResult { Success = false, Error = "Invalid response" };
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Order cancellation failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new OrderCancellationResult { Success = false, Error = $"Server error: {response.StatusCode}" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during order cancellation");
            return new OrderCancellationResult { Success = false, Error = "An error occurred during cancellation" };
        }
    }
}

// DTOs matching the Web API
public record OrderSubmissionRequest
{
    public required string CustomerId { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required string PaymentMethodId { get; init; }
    public required string PaymentMethodType { get; init; }
    public required string PaymentMethodLastFour { get; init; }
    public required List<OrderItemRequest> Items { get; init; } = [];
    public required ShippingAddressRequest ShippingAddress { get; init; }
}

public record OrderItemRequest
{
    public required string ProductId { get; init; }
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public record ShippingAddressRequest
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string PostalCode { get; init; }
    public required string Country { get; init; }
}

public record OrderSubmissionResult
{
    public bool Success { get; init; } = true;
    public string? OrderId { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
}

public record OrderCancellationResult
{
    public bool Success { get; init; } = true;
    public string? OrderId { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
}