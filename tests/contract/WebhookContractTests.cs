using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Xunit;

namespace ContractTests;

public class WebhookContractTests
{
    private static OpenApiDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
        using var stream = File.OpenRead(path);
        var reader = new OpenApiStreamReader();
        return reader.Read(stream, out var _);
    }

    [Fact]
    public void HasWebhookEndpointWithSecurity()
    {
        var doc = Load();
        Assert.True(doc.Paths.ContainsKey("/webhooks/stripe"));
        var post = doc.Paths["/webhooks/stripe"].Operations[OperationType.Post];
        Assert.NotNull(post);
        Assert.True(post.Responses.ContainsKey("200"));
        Assert.True(post.Responses.ContainsKey("400"));
        Assert.True(post.Responses.ContainsKey("500"));
        Assert.True(doc.Components.SecuritySchemes.ContainsKey("stripeSignature"));
    }
}
