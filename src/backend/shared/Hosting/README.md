# Azure Functions Shared Host Configuration

This project provides a shared host builder pattern to eliminate code duplication across multiple Azure Functions projects.

## How It Works

### Shared Host Builder

The `AzureFunctionsHostBuilder` class in the `StripeWorkflow.Hosting` namespace provides a standardized way to configure Azure Functions with:

- **Automatic Environment Detection**: Automatically uses `local.settings.json` in development and Azure Key Vault in production
- **Shared Dependencies**: Configures all necessary services (Handlers, Services, Repositories) in one place
- **Azure Key Vault Integration**: Uses `DefaultAzureCredential` for seamless authentication across environments
- **Application Insights**: Preconfigured telemetry and logging

### Usage

Each Azure Function now has a minimal `Program.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using StripeWorkflow.Hosting;

// Create and run the standardized Azure Functions host
AzureFunctionsHostBuilder.CreateHost().Run();
```

### Environment Configuration

**Development (local.settings.json)**:

- No additional configuration needed
- The host automatically uses `local.settings.json` for configuration

**Production (Azure Key Vault)**:

- Set the `AZURE_KEY_VAULT_NAME` environment variable
- Configure Managed Identity for the Azure Functions App
- The host automatically uses Key Vault for configuration

### Benefits

1. **DRY Principle**: No code duplication across Azure Functions
2. **Centralized Configuration**: All host setup logic in one place
3. **Easy Maintenance**: Changes to hosting configuration only need to be made once
4. **Consistent Behavior**: All Azure Functions behave identically
5. **Environment Agnostic**: Seamless transition between local and production

### Dependencies

The shared host builder handles registration of:

- Application Insights telemetry
- Stripe Workflow handlers
- Services with resilience patterns
- Repository implementations
- Azure Key Vault configuration (when available)

This approach significantly reduces boilerplate code while maintaining flexibility and ensuring consistent configuration across all Azure Functions in the solution.
