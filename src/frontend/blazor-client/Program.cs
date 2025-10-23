using BlazorClient;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configure HttpClient for Web API
builder.Services.AddHttpClient("WebAPI", client =>
{
    var webApiEndpoint = builder.Configuration["WebApiEndpoint"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(webApiEndpoint);
});

// Register OrderService manually
builder.Services.AddScoped<BlazorClient.Services.IOrderService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("WebAPI");
    var logger = sp.GetRequiredService<ILogger<BlazorClient.Services.OrderService>>();
    return new BlazorClient.Services.OrderService(httpClient, logger);
});

await builder.Build().RunAsync();