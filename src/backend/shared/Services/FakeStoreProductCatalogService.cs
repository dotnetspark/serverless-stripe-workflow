using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.Services;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Services;

public class FakeStoreProductCatalogService : IProductCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FakeStoreProductCatalogService> _logger;
    private readonly IMemoryCache _cache;
    private const string BaseUrl = "https://fakestoreapi.com";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, ProductCategory> _categoryMappings = new();

    public FakeStoreProductCatalogService(HttpClient httpClient, ILogger<FakeStoreProductCatalogService> logger, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;

        // Configure HttpClient with base address and default headers
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StripeWorkflow/1.0");

        // Initialize category mappings
        InitializeCategoryMappings();
    }

    public async Task<IEnumerable<Product>> GetProductsAsync()
    {
        const string cacheKey = "fakestore_all_products";

        if (_cache.TryGetValue<IEnumerable<Product>>(cacheKey, out var cachedProducts))
        {
            _logger.LogDebug("Returning cached products");
            return cachedProducts!;
        }

        try
        {
            _logger.LogInformation("Fetching all products from FakeStore API");

            var response = await _httpClient.GetAsync("/products");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FakeStore API returned {StatusCode}: {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
                throw new InvalidOperationException($"FakeStore API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Received empty response from FakeStore API");
                return Enumerable.Empty<Product>();
            }

            var fakeStoreProducts = JsonSerializer.Deserialize<FakeStoreProduct[]>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (fakeStoreProducts == null || !fakeStoreProducts.Any())
            {
                _logger.LogWarning("No products found in FakeStore API response");
                return Enumerable.Empty<Product>();
            }

            var products = fakeStoreProducts.Select(MapToProduct).ToList();

            // Cache the results
            _cache.Set(cacheKey, products, CacheExpiration);

            _logger.LogInformation("Successfully fetched and cached {Count} products", products.Count);
            return products;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize products from FakeStore API");
            throw new InvalidOperationException("Invalid response format from FakeStore API", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching products from FakeStore API");
            throw new InvalidOperationException("Network error accessing FakeStore API", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while fetching products from FakeStore API");
            throw new InvalidOperationException("Timeout accessing FakeStore API", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching products from FakeStore API");
            throw;
        }
    }

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Product ID cannot be empty", nameof(id));
        }

        var cacheKey = $"fakestore_product_{id}";

        if (_cache.TryGetValue<Product>(cacheKey, out var cachedProduct))
        {
            _logger.LogDebug("Returning cached product {ProductId}", id);
            return cachedProduct;
        }

        try
        {
            _logger.LogInformation("Fetching product {ProductId} from FakeStore API", id);

            var response = await _httpClient.GetAsync($"/products/{Uri.EscapeDataString(id)}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product {ProductId} not found", id);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FakeStore API returned {StatusCode} for product {ProductId}: {ReasonPhrase}",
                    response.StatusCode, id, response.ReasonPhrase);
                throw new InvalidOperationException($"FakeStore API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Received empty response for product {ProductId}", id);
                return null;
            }

            var fakeStoreProduct = JsonSerializer.Deserialize<FakeStoreProduct>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (fakeStoreProduct == null)
            {
                _logger.LogWarning("Failed to deserialize product {ProductId}", id);
                return null;
            }

            var product = MapToProduct(fakeStoreProduct);

            // Cache the result
            _cache.Set(cacheKey, product, CacheExpiration);

            _logger.LogInformation("Successfully fetched and cached product {ProductId}: {ProductName}", id, product.Title);
            return product;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize product {ProductId} from FakeStore API", id);
            throw new InvalidOperationException($"Invalid response format for product {id}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching product {ProductId} from FakeStore API", id);
            throw new InvalidOperationException($"Network error accessing product {id}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while fetching product {ProductId} from FakeStore API", id);
            throw new InvalidOperationException($"Timeout accessing product {id}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching product {ProductId} from FakeStore API", id);
            throw;
        }
    }

    public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category cannot be empty", nameof(category));
        }

        var cacheKey = $"fakestore_category_{category.ToLowerInvariant()}";

        if (_cache.TryGetValue<IEnumerable<Product>>(cacheKey, out var cachedProducts))
        {
            _logger.LogDebug("Returning cached products for category '{Category}'", category);
            return cachedProducts!;
        }

        try
        {
            _logger.LogInformation("Fetching products in category '{Category}' from FakeStore API", category);

            var response = await _httpClient.GetAsync($"/products/category/{Uri.EscapeDataString(category)}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Category '{Category}' not found", category);
                return Enumerable.Empty<Product>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FakeStore API returned {StatusCode} for category '{Category}': {ReasonPhrase}",
                    response.StatusCode, category, response.ReasonPhrase);
                throw new InvalidOperationException($"FakeStore API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Received empty response for category '{Category}'", category);
                return Enumerable.Empty<Product>();
            }

            var fakeStoreProducts = JsonSerializer.Deserialize<FakeStoreProduct[]>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (fakeStoreProducts == null || !fakeStoreProducts.Any())
            {
                _logger.LogWarning("No products found in category '{Category}'", category);
                return Enumerable.Empty<Product>();
            }

            var products = fakeStoreProducts.Select(MapToProduct).ToList();

            // Cache the results
            _cache.Set(cacheKey, products, CacheExpiration);

            _logger.LogInformation("Successfully fetched and cached {Count} products in category '{Category}'", products.Count, category);
            return products;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize products for category '{Category}' from FakeStore API", category);
            throw new InvalidOperationException($"Invalid response format for category {category}", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching products for category '{Category}' from FakeStore API", category);
            throw new InvalidOperationException($"Network error accessing category {category}", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while fetching products for category '{Category}' from FakeStore API", category);
            throw new InvalidOperationException($"Timeout accessing category {category}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching products for category '{Category}' from FakeStore API", category);
            throw;
        }
    }

    // Add method to get available categories
    public async Task<IEnumerable<string>> GetCategoriesAsync()
    {
        const string cacheKey = "fakestore_categories";

        if (_cache.TryGetValue<IEnumerable<string>>(cacheKey, out var cachedCategories))
        {
            _logger.LogDebug("Returning cached categories");
            return cachedCategories!;
        }

        try
        {
            _logger.LogInformation("Fetching categories from FakeStore API");

            var response = await _httpClient.GetAsync("/products/categories");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("FakeStore API returned {StatusCode}: {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
                throw new InvalidOperationException($"FakeStore API error: {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Received empty response for categories");
                return Enumerable.Empty<string>();
            }

            var categories = JsonSerializer.Deserialize<string[]>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) ?? Array.Empty<string>();

            // Cache the results
            _cache.Set(cacheKey, categories, CacheExpiration);

            _logger.LogInformation("Successfully fetched and cached {Count} categories", categories.Length);
            return categories;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize categories from FakeStore API");
            throw new InvalidOperationException("Invalid response format for categories", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching categories from FakeStore API");
            throw new InvalidOperationException("Network error accessing categories", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout while fetching categories from FakeStore API");
            throw new InvalidOperationException("Timeout accessing categories", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching categories from FakeStore API");
            throw;
        }
    }

    private static void InitializeCategoryMappings()
    {
        _categoryMappings.TryAdd("electronics", ProductCategory.Electronics);
        _categoryMappings.TryAdd("jewelery", ProductCategory.Jewelery);
        _categoryMappings.TryAdd("men's clothing", ProductCategory.MensClothing);
        _categoryMappings.TryAdd("women's clothing", ProductCategory.WomensClothing);
    }

    private static Product MapToProduct(FakeStoreProduct fakeStoreProduct)
    {
        try
        {
            var category = _categoryMappings.GetValueOrDefault(fakeStoreProduct.Category.ToLowerInvariant(),
                ProductCategory.From(fakeStoreProduct.Category));

            var price = new Money(fakeStoreProduct.Price, "USD");

            Uri? imageUrl = null;
            if (!string.IsNullOrWhiteSpace(fakeStoreProduct.Image) && Uri.TryCreate(fakeStoreProduct.Image, UriKind.Absolute, out var parsedUri))
            {
                imageUrl = parsedUri;
            }

            var product = Product.Create(
                fakeStoreProduct.Title,
                fakeStoreProduct.Description,
                price,
                category,
                imageUrl);

            // Update rating if available
            if (fakeStoreProduct.Rating != null)
            {
                product.UpdateRating((decimal)fakeStoreProduct.Rating.Rate, fakeStoreProduct.Rating.Count);
            }

            return product;
        }
        catch (Exception ex)
        {
            throw new DomainException($"Failed to map FakeStore product {fakeStoreProduct.Id}: {ex.Message}", ex);
        }
    }
}

// DTOs for FakeStore API response
public class FakeStoreProduct
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public FakeStoreRating? Rating { get; set; }
}

public class FakeStoreRating
{
    public double Rate { get; set; }
    public int Count { get; set; }
}