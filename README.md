# Serverless Stripe Workflow

A cloud-native payment processing system demonstrating modern serverless architecture patterns with .NET 9, Aspire orchestration, and multi-cloud resilience.

## 🏗️ Architecture

This system showcases enterprise-grade patterns while maintaining cost optimization:

- **Serverless Functions**: Azure Functions and AWS Lambda with isolated worker model
- **Event-Driven**: Service Bus messaging with domain events
- **Multi-Cloud Storage**: Cosmos DB with hybrid failover options
- **Observability**: Distributed tracing with OpenTelemetry
- **Orchestration**: .NET Aspire for local development

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)

### Local Development

1. **Clone and start the system**:

   ```bash
   git clone https://github.com/yourusername/serverless-stripe-workflow.git
   cd serverless-stripe-workflow
   dotnet run --project src/orchestrator/AppHost
   ```

2. **Access the dashboard**: Navigate to the URL shown in the terminal (typically `https://localhost:17294`)

3. **View running services**: The Aspire dashboard shows all services, their health, logs, and metrics

### Configuration

The system uses Azure emulators for local development:

- **Cosmos DB**: Azure Cosmos DB Emulator on port 8081
- **Service Bus**: Service Bus Emulator for messaging
- **Storage**: Azurite for Azure Functions runtime storage

## 📋 Project Structure

```
src/
├── orchestrator/
│   ├── AppHost/           # .NET Aspire orchestration
│   └── ServiceDefaults/   # Shared telemetry & resilience
├── backend/
│   ├── hosting/
│   │   ├── web-api/       # REST API endpoints
│   │   ├── azure-functions/ # Azure serverless functions
│   │   └── aws-functions/ # AWS Lambda functions
│   └── shared/
│       ├── Domain/        # Business entities & rules
│       ├── Services/      # Application services
│       ├── Repositories/  # Data access layer
│       └── Infrastructure/ # External integrations
├── frontend/              # (Future: React/Blazor UI)
```

## 🛠️ Development Workflow

### Running Individual Components

```bash
# Start just the web API
dotnet run --project src/backend/hosting/web-api

# Run Azure Functions locally
cd src/backend/hosting/azure-functions/src/Checkout
func start

# Run AWS Lambda functions locally (requires SAM CLI)
cd src/backend/hosting/aws-functions
sam local start-api

# Build entire solution
dotnet build
```

### Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## 🎯 Core Features

- **Payment Processing**: Stripe integration with webhook handling
- **Order Management**: Complete order lifecycle with domain events
- **Notifications**: Email and SMS notifications via SendGrid/Twilio
- **Resilience**: Retry policies, circuit breakers, and graceful degradation
- **Observability**: Comprehensive logging, metrics, and distributed tracing

## 🌐 Deployment

The system supports multiple deployment targets:

- **Azure**: Container Apps with managed services
- **AWS**: Lambda functions with EventBridge
- **Multi-Cloud**: Hybrid architecture for resilience

See [deployment documentation](./docs/deployment.md) for detailed instructions.

## 📚 Documentation

- [Architecture Overview](./docs/architecture.md)
- [Development Guide](./tasks.md)
- [API Documentation](./docs/api.md)
- [Technical Specifications](./specs/)
- [Testing Strategy](./tests/README.md)
- [Troubleshooting](./docs/troubleshooting.md)

## 🤝 Contributing

This is a portfolio project demonstrating professional software architecture practices. See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.

## 🎯 Project Goals

This system serves as a demonstration of:

- Modern .NET cloud-native development
- Cost-conscious enterprise architecture
- DevOps and observability best practices
- Multi-cloud resilience patterns

For detailed principles and governance, see [Project Constitution](./.specify/memory/constitution.md).
