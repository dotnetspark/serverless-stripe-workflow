using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace ContractTests;

public class CheckoutContractTests
{
    private static OpenApiDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        return reader.Read(stream, out var _);
    }

    [Fact]
    public void HasCheckoutSessionEndpoint()
    {
        var doc = Load();
        Assert.True(doc.Paths.ContainsKey("/checkout/session"));
        var post = doc.Paths["/checkout/session"].Operations[OperationType.Post];
        Assert.NotNull(post);
        Assert.True(post.RequestBody.Required);
        var schema = post.RequestBody.Content["application/json"].Schema;
        Assert.Contains("orderId", schema.Required);
        Assert.Contains("items", schema.Required);
        Assert.Contains("customerEmail", schema.Required);
        Assert.True(post.Responses.ContainsKey("200"));
        Assert.True(post.Responses.ContainsKey("400"));
        Assert.True(post.Responses.ContainsKey("409"));
    }
}
