using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using StripeWorkflow.Handlers.DependencyInjection;
using StripeWorkflow.Infrastructure.Extensions;
using StripeWorkflow.Repositories.Extensions;
using StripeWorkflow.Services.Extensions;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add shared handlers from the Handlers library
builder.Services.AddStripeWorkflowHandlers();

// Add services with resilience patterns
builder.Services.AddStripeWorkflowServices(builder.Configuration);

// Add repository implementations (defaults to Cosmos DB)
builder.Services.AddStripeWorkflowRepositories(builder.Configuration);

// Add messaging services (Service Bus and Event Grid)
builder.Services.AddMessaging();

builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
