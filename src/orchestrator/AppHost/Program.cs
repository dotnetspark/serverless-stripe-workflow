using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cosmosDb = builder.AddAzureCosmosDB("cosmosdb")
    .RunAsEmulator();

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
    .WaitFor(storage)
    .WaitFor(cosmosDb)
    .WaitFor(serviceBus);

var webhooksFn = builder.AddAzureFunctionsProject<Projects.WebhooksFn>("webhooks-function")
    .WithExternalHttpEndpoints()
    .WithReference(cosmosDb)
    .WithHostStorage(storage)
    .WaitFor(storage)
    .WaitFor(cosmosDb);

var notifyFn = builder.AddAzureFunctionsProject<Projects.NotifyFn>("notify-function")
    .WithExternalHttpEndpoints()
    .WithReference(cosmosDb)
    .WithHostStorage(storage)
    .WaitFor(storage)
    .WaitFor(cosmosDb);

var webApi = builder.AddProject<Projects.WebApi>("web-api")
    .WithExternalHttpEndpoints()
    .WithReference(serviceBus);

builder.Build().Run();