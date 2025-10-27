using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Common environment variables for Azure Functions
var functionEnvironment = new Dictionary<string, string>
{
    ["PATH"] = $"C:\\nvm4w\\nodejs\\node_modules\\azure-functions-core-tools\\bin;{Environment.GetEnvironmentVariable("PATH")}"
};

var cosmosDb = builder.AddAzureCosmosDB("cosmosdb")
    .RunAsEmulator(emulator =>
    {
        emulator.WithDataVolume(); // Persist data between runs
        emulator.WithLifetime(ContainerLifetime.Persistent); // Keep container running
    });

// Add the database and container using Aspire's built-in approach
var stripeWorkflowDb = cosmosDb.AddCosmosDatabase("StripeWorkflow");
var ordersContainer = stripeWorkflowDb.AddContainer("orders", "/id");

var serviceBus = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator();

var paymentEvents = serviceBus.AddServiceBusTopic("payment-events");
var webhookEvents = serviceBus.AddServiceBusTopic("webhook-events");

var orderSubmittedQueue = serviceBus.AddServiceBusQueue("order-submitted");

paymentEvents.AddServiceBusSubscription("email-notifications");
paymentEvents.AddServiceBusSubscription("sms-notifications");
webhookEvents.AddServiceBusSubscription("order-fulfillment");

// Add storage for Azure Functions (required by Azure Functions runtime)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var checkoutFn = builder.AddAzureFunctionsProject<Projects.CheckoutFn>("checkout-function")
    .WithExternalHttpEndpoints()
    .WithReference(cosmosDb)
    .WithReference(orderSubmittedQueue)
    .WithReference(serviceBus)
    .WithHostStorage(storage)
    .WithEnvironment("PATH", $"C:\\nvm4w\\nodejs\\node_modules\\azure-functions-core-tools\\bin;{Environment.GetEnvironmentVariable("PATH")}")
    .WithEnvironment("ServiceBusConnection", serviceBus.Resource.ConnectionStringExpression)
    .WaitFor(storage)
    .WaitFor(cosmosDb)
    .WaitFor(serviceBus);

var webhooksFn = builder.AddAzureFunctionsProject<Projects.WebhooksFn>("webhooks-function")
    .WithExternalHttpEndpoints()
    .WithReference(cosmosDb)
    .WithHostStorage(storage)
    .WithEnvironment("PATH", $"C:\\nvm4w\\nodejs\\node_modules\\azure-functions-core-tools\\bin;{Environment.GetEnvironmentVariable("PATH")}")
    .WaitFor(storage)
    .WaitFor(cosmosDb);

var notifyFn = builder.AddAzureFunctionsProject<Projects.NotifyFn>("notify-function")
    .WithExternalHttpEndpoints()
    .WithReference(cosmosDb)
    .WithReference(serviceBus)
    .WithHostStorage(storage)
    .WithEnvironment("PATH", $"C:\\nvm4w\\nodejs\\node_modules\\azure-functions-core-tools\\bin;{Environment.GetEnvironmentVariable("PATH")}")
    .WithEnvironment("ServiceBusConnection", serviceBus.Resource.ConnectionStringExpression)
    .WaitFor(storage)
    .WaitFor(cosmosDb)
    .WaitFor(serviceBus);

var webApi = builder.AddProject<Projects.WebApi>("web-api")
    .WithExternalHttpEndpoints()
    .WithReference(serviceBus);

builder.Build().Run();