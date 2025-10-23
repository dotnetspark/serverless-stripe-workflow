using FluentAssertions;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace PerformanceTests;

public class CheckoutPerformanceTests
{
    [Fact]
    public void CheckoutEndpoint_ShouldHandleHighLoad()
    {
        var scenario = Scenario.Create("checkout_load_test", async context =>
        {
            var request = Http.CreateRequest("POST", "https://func-stripe-workflow-prod.azurewebsites.net/api/checkout")
                .WithHeader("Authorization", "Bearer test-token")
                .WithJsonBody(new
                {
                    customerId = $"test_customer_{context.ScenarioInfo.ThreadId}",
                    items = new[]
                    {
                        new { productId = "1", quantity = 2 },
                        new { productId = "2", quantity = 1 }
                    },
                    currency = "USD",
                    successUrl = "https://example.com/success",
                    cancelUrl = "https://example.com/cancel"
                });

            var response = await Http.Send(request, context);

            return response.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail($"Status: {response.StatusCode}");
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 10, during: TimeSpan.FromMinutes(2)),
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromMinutes(3))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("performance-reports")
            .Run();

        // Assertions for performance criteria
        stats.AllScenarios[0].Ok.Request.Mean.Should().BeLessThan(TimeSpan.FromSeconds(2));
        stats.AllScenarios[0].Ok.Request.Count.Should().BeGreaterThan(100);
        stats.AllScenarios[0].Fail.Request.Count.Should().BeLessThan(5);
    }

    [Fact]
    public void MultiCloudFailover_ShouldMaintainPerformance()
    {
        var azureScenario = Scenario.Create("azure_primary", async context =>
        {
            var request = Http.CreateRequest("GET", "https://func-stripe-workflow-prod.azurewebsites.net/api/health");
            var response = await Http.Send(request, context);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        });

        var awsScenario = Scenario.Create("aws_fallback", async context =>
        {
            var request = Http.CreateRequest("GET", "https://your-api-gateway.execute-api.us-east-1.amazonaws.com/api/health");
            var response = await Http.Send(request, context);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        });

        var stats = NBomberRunner
            .RegisterScenarios(azureScenario, awsScenario)
            .WithLoadSimulations(
                Simulation.InjectPerSec(rate: 20, during: TimeSpan.FromMinutes(1))
            )
            .Run();

        // Both endpoints should perform similarly
        var azureStats = stats.AllScenarios.First(s => s.ScenarioName == "azure_primary");
        var awsStats = stats.AllScenarios.First(s => s.ScenarioName == "aws_fallback");

        azureStats.Ok.Request.Mean.Should().BeLessThan(TimeSpan.FromSeconds(1));
        awsStats.Ok.Request.Mean.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }
}

public class ELKStackPerformanceTests
{
    [Fact]
    public void ElasticsearchIngestion_ShouldHandleHighThroughput()
    {
        var scenario = Scenario.Create("log_ingestion", async context =>
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow,
                level = "INFO",
                message = $"Test log entry {context.InvocationNumber}",
                source = "performance-test",
                customerId = $"customer_{context.ScenarioInfo.ThreadId}",
                traceId = Guid.NewGuid().ToString()
            };

            var request = Http.CreateRequest("POST", "http://localhost:9200/application-logs/_doc")
                .WithJsonBody(logEntry);

            var response = await Http.Send(request, context);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromMinutes(1))
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        stats.AllScenarios[0].Ok.Request.Mean.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
        stats.AllScenarios[0].Ok.Request.Count.Should().BeGreaterThan(5000);
    }
}