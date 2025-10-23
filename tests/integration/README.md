# Integration Tests

This directory contains comprehensive integration tests for the Serverless Stripe Workflow system.

## Test Categories

### 1. Checkout Flow Integration Tests (`CheckoutFlowIntegrationTests.cs`)

- **Complete end-to-end checkout flow** testing with both Cosmos DB and DynamoDB
- **Order creation and validation** with proper domain model usage
- **Webhook processing simulation** with Stripe session completion events
- **Error handling validation** for invalid inputs and edge cases
- **Cross-repository compatibility** ensuring both storage backends work identically

### 2. Notification Flow Integration Tests (`NotificationFlowIntegrationTests.cs`)

- **Notification delivery testing** with mocked external services
- **Retry mechanism validation** for failed notification attempts
- **Order state transitions** from Paid to Fulfilled upon successful notification
- **Error scenarios** including non-existent orders and invalid states
- **Bulk retry processing** for handling multiple failed notifications

### 3. Performance Integration Tests (`PerformanceIntegrationTests.cs`)

- **Concurrent load testing** with multiple simultaneous checkout requests
- **High-throughput webhook processing** to validate system scalability
- **Bulk repository operations** testing create/read/update performance
- **Memory usage monitoring** to ensure stable resource consumption under load

## Infrastructure

### Test Base Class (`Infrastructure/IntegrationTestBase.cs`)

- **Testcontainers integration** for Cosmos DB and DynamoDB (LocalStack)
- **Dependency injection setup** with proper service configuration
- **Mock service configuration** for external dependencies
- **Automatic resource cleanup** with proper disposal patterns

### Configuration (`appsettings.test.json`)

- **Test-specific settings** with safe default values
- **Container connection strings** for local testing
- **Mock API keys** for external services
- **Database isolation** to prevent test interference

## Prerequisites

Before running the integration tests, ensure you have:

1. **Docker Desktop** installed and running (for Testcontainers)
2. **.NET 8 SDK** installed
3. **Sufficient memory** (at least 4GB available for containers)
4. **Network connectivity** for pulling container images

## Running Tests

### Run All Integration Tests

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test Categories

```bash
# Checkout flow tests only
dotnet test --filter "FullyQualifiedName~CheckoutFlow"

# Notification flow tests only
dotnet test --filter "FullyQualifiedName~NotificationFlow"

# Performance tests only
dotnet test --filter "FullyQualifiedName~Performance"
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Run Against Specific Repository

```bash
# Test only Cosmos DB scenarios
dotnet test --filter "TestName~Cosmos"

# Test only DynamoDB scenarios
dotnet test --filter "TestName~Dynamo"
```

## Test Data Management

### Isolation Strategy

- Each test creates its own container instances
- Database names/table names include test-specific suffixes
- Automatic cleanup ensures no cross-test contamination

### Test Data Patterns

- **Deterministic test data** with predictable customer IDs and emails
- **Realistic product catalogs** using FakeStore API integration
- **Parameterized scenarios** for testing edge cases and validations

## Continuous Integration

### GitHub Actions Integration

```yaml
- name: Run Integration Tests
  run: |
    docker info
    dotnet test src/backend/tests/IntegrationTests/ \
      --logger "trx;LogFileName=integration-results.trx" \
      --collect:"XPlat Code Coverage"
```

### Performance Benchmarks

- **Checkout processing**: < 5 seconds per order under load
- **Webhook handling**: < 1 second per webhook
- **Bulk operations**: < 500ms per order for CRUD operations
- **Memory usage**: < 100MB increase for 100 orders

## Debugging Tests

### Local Development

1. Start Docker Desktop
2. Set `TESTCONTAINERS_RYUK_DISABLED=true` if experiencing cleanup issues
3. Use `ITestOutputHelper` for detailed logging during test execution
4. Check container logs: `docker logs <container-id>`

### Common Issues

- **Container startup failures**: Ensure Docker has sufficient resources
- **Network timeouts**: Check firewall settings and Docker network configuration
- **Port conflicts**: Tests use dynamic port mapping to avoid conflicts
- **Memory issues**: Close other applications to free up system memory

## Test Architecture

### Domain-Driven Design Validation

- Tests validate proper use of **aggregate roots** and **value objects**
- **Domain events** are tested through state transitions
- **Repository patterns** are validated against both storage backends
- **Business rule enforcement** is tested through invalid operation attempts

### Cross-Platform Compatibility

- Tests run on **Windows**, **Linux**, and **macOS**
- **Container orchestration** works across different Docker environments
- **Database schema** compatibility verified for both Cosmos DB and DynamoDB
- **Timezone handling** tested with UTC normalization

## Metrics and Reporting

### Test Coverage Goals

- **Line coverage**: > 80% for critical paths
- **Branch coverage**: > 70% for business logic
- **Integration coverage**: 100% for handler interactions

### Performance Baselines

- **Response times** tracked per test run
- **Memory consumption** monitored for regression detection
- **Throughput metrics** validated against SLA requirements
- **Error rates** kept below 0.1% for critical operations

## Security Testing

### Data Protection

- **Sensitive data masking** in test logs
- **Mock credentials** used for all external services
- **Container isolation** prevents data leakage between tests
- **Cleanup verification** ensures no test data persists

### Authentication Testing

- **Invalid tokens** rejected properly
- **Webhook signature validation** enforced
- **Rate limiting** behavior validated
- **SQL injection** prevention tested via parameterized queries
