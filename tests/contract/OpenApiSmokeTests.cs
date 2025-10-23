using System.IO;
using Xunit;

namespace ContractTests;

public class OpenApiSmokeTests
{
    [Fact]
    public void OpenApiExists()
    {
        var baseDir = AppContext.BaseDirectory;
        var openApiPath = Path.Combine(baseDir, "openapi.yaml");
        Assert.True(File.Exists(openApiPath), $"OpenAPI file not found at {openApiPath}");
        var text = File.ReadAllText(openApiPath);
        Assert.Contains("openapi:", text);
    }
}
