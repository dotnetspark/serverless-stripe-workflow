# Azure Deployment Guide - Serverless Stripe Workflow

## Complete Infrastructure Setup with Azure B2C

### Prerequisites

Before starting the deployment, ensure you have:

- **Azure Subscription** with appropriate permissions
- **Azure CLI** installed and authenticated (`az login`)
- **.NET 8 SDK** installed
- **Visual Studio Code** with Azure Functions extension
- **Docker Desktop** for local testing
- **Git** for version control

### Quick Start Deployment

For immediate deployment, run:

```powershell
# Clone and deploy in one command
git clone https://github.com/your-org/serverless-stripe-workflow.git
cd serverless-stripe-workflow
.\scripts\Deploy-Azure.ps1 -Environment prod -Location "East US" -SubscriptionId "your-subscription-id"
```

## Step 1: Azure B2C Setup

### 1.1 Create Azure B2C Tenant

```powershell
# Create resource group for B2C
az group create --name "rg-stripe-workflow-b2c" --location "East US"

# Create B2C tenant (Note: This creates a separate tenant)
az ad b2c tenant create \
  --country-code "US" \
  --display-name "StripeWorkflow B2C" \
  --domain-name "stripeworkflow" \
  --resource-group "rg-stripe-workflow-b2c"
```

### 1.2 Configure B2C User Flows

Navigate to your B2C tenant in Azure Portal and create user flows:

#### Sign Up/Sign In Flow

```json
{
  "name": "B2C_1_signup_signin",
  "userFlowType": "signUpOrSignIn",
  "userAttributes": ["Email Address", "Given Name", "Surname"],
  "applicationClaims": [
    "Email addresses",
    "Given name",
    "Surname",
    "User's object ID"
  ],
  "identityProviders": ["Email signup", "Microsoft Account", "Google"]
}
```

#### Custom Attributes

Create custom attributes for business logic:

```json
{
  "extension_PreferredCurrency": {
    "dataType": "string",
    "description": "User's preferred currency",
    "defaultValue": "USD"
  },
  "extension_Timezone": {
    "dataType": "string",
    "description": "User's timezone",
    "defaultValue": "UTC"
  },
  "extension_Roles": {
    "dataType": "stringCollection",
    "description": "User authorization roles",
    "defaultValue": ["customer"]
  }
}
```

### 1.3 Register Applications

#### Web Application Registration

```powershell
# Register the web application
az ad app create \
  --display-name "Stripe Workflow Web App" \
  --web-redirect-uris "https://stripe-workflow-frontend.azurewebsites.net/auth/callback" \
  --required-resource-accesses @app-permissions.json
```

#### API Application Registration

```powershell
# Register the API application
az ad app create \
  --display-name "Stripe Workflow API" \
  --identifier-uris "https://stripeworkflow.onmicrosoft.com/api" \
  --app-roles @api-roles.json
```

**app-permissions.json:**

```json
[
  {
    "resourceAppId": "00000003-0000-0000-c000-000000000000",
    "resourceAccess": [
      {
        "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
        "type": "Scope"
      }
    ]
  }
]
```

**api-roles.json:**

```json
[
  {
    "allowedMemberTypes": ["User"],
    "description": "Regular customer access",
    "displayName": "Customer",
    "id": "12345678-1234-1234-1234-123456789012",
    "isEnabled": true,
    "value": "customer"
  },
  {
    "allowedMemberTypes": ["User"],
    "description": "Administrative access",
    "displayName": "Administrator",
    "id": "87654321-4321-4321-4321-210987654321",
    "isEnabled": true,
    "value": "admin"
  }
]
```

## Step 2: Infrastructure Deployment

### 2.1 Create ARM Template

**main.bicep:**

```bicep
@description('Environment name (dev, staging, prod)')
param environmentName string = 'dev'

@description('Azure region for resources')
param location string = resourceGroup().location

@description('B2C tenant name')
param b2cTenantName string

@secure()
@description('Stripe publishable key')
param stripePublishableKey string

@secure()
@description('Stripe webhook secret')
param stripeWebhookSecret string

// Variables
var functionAppName = 'func-stripe-workflow-${environmentName}'
var storageAccountName = 'ststripe${environmentName}${uniqueString(resourceGroup().id)}'
var logsStorageAccountName = 'stlogs${environmentName}${uniqueString(resourceGroup().id)}'
var cosmosAccountName = 'cosmos-stripe-workflow-${environmentName}'
var keyVaultName = 'kv-stripe-${environmentName}-${uniqueString(resourceGroup().id)}'
var staticWebAppName = 'swa-stripe-workflow-${environmentName}'
var frontDoorName = 'fd-stripe-workflow-${environmentName}'
var appInsightsName = 'ai-stripe-workflow-${environmentName}'

// Storage Account for Functions
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// Function App (Consumption Plan - Cost Effective)
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}deployments'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'StructuredLogging__StorageAccount'
          value: logsStorageAccount.name
        }
        {
          name: 'StructuredLogging__ContainerName'
          value: 'application-logs'
        }
        {
          name: 'StructuredLogging__ConnectionString'
          value: 'DefaultEndpointsProtocol=https;AccountName=${logsStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${logsStorageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'CosmosDB_ConnectionString'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'B2C_TenantName'
          value: b2cTenantName
        }
        {
          name: 'B2C_TenantId'
          value: subscription().tenantId
        }
        {
          name: 'Stripe_PublishableKey'
          value: stripePublishableKey
        }
        {
          name: 'Stripe_WebhookSecret'
          value: stripeWebhookSecret
        }
      ]
      cors: {
        allowedOrigins: [
          'https://stripe-workflow-frontend.azurewebsites.net'
          'http://localhost:3000'
        ]
        supportCredentials: false
      }
    }
  }
}

// Cosmos DB Account
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
}

// Cosmos DB Database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosAccount
  name: 'StripeWorkflow'
  properties: {
    resource: {
      id: 'StripeWorkflow'
    }
  }
}

// Orders Container
resource ordersContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'Orders'
  properties: {
    resource: {
      id: 'Orders'
      partitionKey: {
        paths: ['/customerId']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
    }
  }
}

// Static Web App for Blazor WASM (Free Tier)
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: 'East US 2' // Static Web Apps available regions
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    repositoryUrl: 'https://github.com/your-org/serverless-stripe-workflow'
    branch: 'main'
    buildProperties: {
      appLocation: 'src/frontend/blazor-client'
      apiLocation: 'src/backend/hosting/azure-functions'
      outputLocation: 'wwwroot'
    }
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

// Application Insights (Free Tier - Up to 5GB/month)
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    RetentionInDays: 90
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    SamplingPercentage: 100
    DisableIpMasking: false
  }
}

// Storage Account for Structured Logs (for Grafana)
resource logsStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: logsStorageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    accessTier: 'Cool' // Cost optimization for logs
    encryption: {
      services: {
        file: {
          keyType: 'Account'
          enabled: true
        }
        blob: {
          keyType: 'Account'
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

// Blob Container for Application Logs
resource logsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${logsStorageAccount.name}/default/application-logs'
  properties: {
    publicAccess: 'None'
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: functionApp.identity.principalId
        permissions: {
          secrets: ['get', 'list']
        }
      }
    ]
    enableRbacAuthorization: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  }
}

// Azure Front Door for Multi-Cloud Failover (Zero Downtime)
resource frontDoor 'Microsoft.Cdn/profiles@2023-05-01' = {
  name: frontDoorName
  location: 'Global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
  properties: {
    originResponseTimeoutSeconds: 60
  }
}

// Front Door Endpoint
resource frontDoorEndpoint 'Microsoft.Cdn/profiles/afdEndpoints@2023-05-01' = {
  parent: frontDoor
  name: 'stripe-workflow-api'
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
}

// Origin Group for Load Balancing
resource originGroup 'Microsoft.Cdn/profiles/originGroups@2023-05-01' = {
  parent: frontDoor
  name: 'api-backends'
  properties: {
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 50
    }
    healthProbeSettings: {
      probePath: '/api/health'
      probeRequestType: 'GET'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 100
    }
    sessionAffinityState: 'Disabled'
  }
}

// Primary Origin - Azure Functions (East US)
resource azurePrimaryOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: originGroup
  name: 'azure-primary'
  properties: {
    hostName: '${functionAppName}.azurewebsites.net'
    httpPort: 80
    httpsPort: 443
    originHostHeader: '${functionAppName}.azurewebsites.net'
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

// Secondary Origin - Azure Functions (West US)
resource azureSecondaryOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: originGroup
  name: 'azure-secondary'
  properties: {
    hostName: '${functionAppName}-west.azurewebsites.net' // Deploy secondary region
    httpPort: 80
    httpsPort: 443
    originHostHeader: '${functionAppName}-west.azurewebsites.net'
    priority: 2
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

// Fallback Origin - AWS Lambda (API Gateway)
resource awsFallbackOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: originGroup
  name: 'aws-fallback'
  properties: {
    hostName: 'your-api-id.execute-api.us-east-1.amazonaws.com' // Replace with actual API Gateway
    httpPort: 80
    httpsPort: 443
    originHostHeader: 'your-api-id.execute-api.us-east-1.amazonaws.com'
    priority: 3
    weight: 1000
    enabledState: 'Enabled'
    enforceCertificateNameCheck: true
  }
}

// Front Door Route
resource frontDoorRoute 'Microsoft.Cdn/profiles/afdEndpoints/routes@2023-05-01' = {
  parent: frontDoorEndpoint
  name: 'api-route'
  properties: {
    originGroup: {
      id: originGroup.id
    }
    ruleSets: []
    supportedProtocols: ['Http', 'Https']
    patternsToMatch: ['/api/*']
    forwardingProtocol: 'HttpsOnly'
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
output frontDoorEndpoint string = 'https://${frontDoorEndpoint.properties.hostName}'
output appInsightsInstrumentationKey string = applicationInsights.properties.InstrumentationKey
output appInsightsConnectionString string = applicationInsights.properties.ConnectionString
output logsStorageAccountName string = logsStorageAccount.name
output logsStorageEndpoint string = logsStorageAccount.properties.primaryEndpoints.blob
```

### 2.2 Deploy Infrastructure

```powershell
# Create resource group
az group create --name "rg-stripe-workflow-prod" --location "East US"

# Deploy infrastructure
az deployment group create \
  --resource-group "rg-stripe-workflow-prod" \
  --template-file "./infrastructure/main.bicep" \
  --parameters environmentName=prod \
               b2cTenantName=stripeworkflow \
               stripePublishableKey=$env:STRIPE_PUBLISHABLE_KEY \
               stripeWebhookSecret=$env:STRIPE_WEBHOOK_SECRET
```

## Step 3: Function App Deployment

### 3.1 Build and Package Functions

```powershell
# Navigate to functions directory
cd src/functions

# Restore dependencies
dotnet restore

# Build for release
dotnet build --configuration Release

# Publish to output directory
dotnet publish --configuration Release --output ./publish
```

### 3.2 Deploy Functions

```powershell
# Deploy using Azure CLI
az functionapp deployment source config-zip \
  --resource-group "rg-stripe-workflow-prod" \
  --name "func-stripe-workflow-prod" \
  --src "./publish.zip"

# Or deploy using Azure Functions Core Tools
func azure functionapp publish func-stripe-workflow-prod --csharp
```

### 3.3 Configure Application Settings

```powershell
# Set additional configuration
az functionapp config appsettings set \
  --resource-group "rg-stripe-workflow-prod" \
  --name "func-stripe-workflow-prod" \
  --settings \
    "FakeStoreApi_BaseUrl=https://fakestoreapi.com" \
    "SendGrid_ApiKey=$env:SENDGRID_API_KEY" \
    "B2C_ClientId=$env:B2C_CLIENT_ID" \
    "B2C_ClientSecret=$env:B2C_CLIENT_SECRET"
```

## Step 4: CI/CD Pipeline Setup

### 4.1 GitHub Actions Workflow

**.github/workflows/deploy-azure.yml:**

```yaml
name: Deploy to Azure Functions

on:
  push:
    branches: [main]
    paths: ["src/functions/**", "infrastructure/**"]
  workflow_dispatch:
    inputs:
      environment:
        description: "Deployment environment"
        required: true
        default: "staging"
        type: choice
        options:
          - staging
          - prod

env:
  AZURE_FUNCTIONAPP_NAME: func-stripe-workflow-${{ github.event.inputs.environment || 'staging' }}
  AZURE_FUNCTIONAPP_PACKAGE_PATH: "./src/functions"
  DOTNET_VERSION: "8.0"

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    environment: ${{ github.event.inputs.environment || 'staging' }}

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET Core
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore ${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}

      - name: Build application
        run: dotnet build ${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }} --configuration Release --no-restore

      - name: Test application
        run: dotnet test ${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }} --configuration Release --no-build --verbosity normal

      - name: Publish application
        run: dotnet publish ${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }} --configuration Release --no-build --output ./output

      - name: Login to Azure
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Infrastructure
        uses: azure/arm-deploy@v1
        with:
          subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          resourceGroupName: rg-stripe-workflow-${{ github.event.inputs.environment || 'staging' }}
          template: ./infrastructure/main.bicep
          parameters: >
            environmentName=${{ github.event.inputs.environment || 'staging' }}
            b2cTenantName=${{ secrets.B2C_TENANT_NAME }}
            stripePublishableKey=${{ secrets.STRIPE_PUBLISHABLE_KEY }}
            stripeWebhookSecret=${{ secrets.STRIPE_WEBHOOK_SECRET }}

      - name: Deploy Functions
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ env.AZURE_FUNCTIONAPP_NAME }}
          package: "./output"

      - name: Run Health Check
        run: |
          sleep 30
          curl -f https://${{ env.AZURE_FUNCTIONAPP_NAME }}.azurewebsites.net/api/health || exit 1

      - name: Run Integration Tests
        run: |
          dotnet test src/backend/tests/IntegrationTests/ \
            --configuration Release \
            --logger "trx;LogFileName=integration-results.trx" \
            --environment AZURE_FUNCTION_URL=https://${{ env.AZURE_FUNCTIONAPP_NAME }}.azurewebsites.net
        env:
          B2C_TEST_TOKEN: ${{ secrets.B2C_TEST_TOKEN }}

      - name: Publish Test Results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: Integration Tests
          path: "**/*integration-results.trx"
          reporter: dotnet-trx
```

### 4.2 GitHub Secrets Configuration

Add these secrets to your GitHub repository:

```bash
# Azure authentication
AZURE_CREDENTIALS='{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}'
AZURE_SUBSCRIPTION_ID="your-subscription-id"

# B2C configuration
B2C_TENANT_NAME="stripeworkflow"
B2C_CLIENT_ID="your-b2c-client-id"
B2C_CLIENT_SECRET="your-b2c-client-secret"
B2C_TEST_TOKEN="your-test-jwt-token"

# External service keys
STRIPE_PUBLISHABLE_KEY="pk_test_..."
STRIPE_WEBHOOK_SECRET="whsec_..."
SENDGRID_API_KEY="SG...."
```

## Step 5: Testing and Validation

### 5.1 Local Testing Setup

```powershell
# Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Start Cosmos DB Emulator (Windows)
Start-CosmosDbEmulator

# Or use Docker for cross-platform
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  microsoft/azure-cosmosdb-emulator

# Configure local settings
cp local.settings.json.template local.settings.json
# Edit local.settings.json with your development keys

# Start functions locally
cd src/functions
func start --port 7071
```

**local.settings.json:**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDB_ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "B2C_TenantName": "stripeworkflow",
    "B2C_TenantId": "your-tenant-id",
    "B2C_ClientId": "your-client-id",
    "Stripe_PublishableKey": "pk_test_your_key",
    "Stripe_WebhookSecret": "whsec_your_secret",
    "FakeStoreApi_BaseUrl": "https://fakestoreapi.com",
    "SendGrid_ApiKey": "SG.your_key"
  }
}
```

### 5.2 Integration Testing

```powershell
# Run integration tests against local environment
dotnet test src/backend/tests/IntegrationTests/ \
  --environment ASPNETCORE_ENVIRONMENT=Development \
  --environment AZURE_FUNCTION_URL=http://localhost:7071

# Run against staging environment
dotnet test src/backend/tests/IntegrationTests/ \
  --environment ASPNETCORE_ENVIRONMENT=Staging \
  --environment AZURE_FUNCTION_URL=https://func-stripe-workflow-staging.azurewebsites.net
```

### 5.3 Performance Testing

```powershell
# Install Artillery for load testing
npm install -g artillery

# Run load tests
artillery run performance/load-test.yml --target https://func-stripe-workflow-prod.azurewebsites.net
```

**performance/load-test.yml:**

```yaml
config:
  target: "https://func-stripe-workflow-prod.azurewebsites.net"
  phases:
    - duration: 60
      arrivalRate: 10
    - duration: 120
      arrivalRate: 50
  processor: "./auth-processor.js"

scenarios:
  - name: "Checkout Flow"
    weight: 70
    flow:
      - function: "getB2CToken"
      - post:
          url: "/api/checkout"
          headers:
            Authorization: "Bearer {{ token }}"
          json:
            customerId: "test_{{ $randomString() }}"
            items:
              - productId: "1"
                quantity: 2
            currency: "USD"
            successUrl: "http://example.com/success"
            cancelUrl: "http://example.com/cancel"

  - name: "Product Catalog"
    weight: 30
    flow:
      - function: "getB2CToken"
      - get:
          url: "/api/products"
          headers:
            Authorization: "Bearer {{ token }}"
```

## Step 6: Monitoring and Observability

### 6.1 Application Insights Configuration

```csharp
// Program.cs - Configure telemetry
public class Program
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                // Custom telemetry initializer
                services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
            })
            .Build();

        host.Run();
    }
}

public class CustomTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        telemetry.Context.Cloud.RoleName = "StripeWorkflowAPI";
        telemetry.Context.ComponentVersion = "2.0.0";
    }
}
```

### 6.2 Custom Dashboards

Create Application Insights dashboard with these KPIs:

```kusto
// Request success rate
requests
| where timestamp > ago(24h)
| summarize
    Total = count(),
    Success = countif(success == true),
    SuccessRate = (countif(success == true) * 100.0) / count()
by bin(timestamp, 5m)

// Order conversion rate
customEvents
| where name == "OrderCreated" or name == "OrderPaid"
| where timestamp > ago(24h)
| summarize
    Created = countif(name == "OrderCreated"),
    Paid = countif(name == "OrderPaid")
by bin(timestamp, 1h)
| extend ConversionRate = (Paid * 100.0) / Created

// Performance metrics
requests
| where timestamp > ago(24h)
| where name contains "api"
| summarize
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95),
    P99Duration = percentile(duration, 99)
by name, bin(timestamp, 5m)
```

### 6.3 Alerts Configuration

```powershell
# Create alert for high error rate
az monitor metrics alert create \
  --name "High Error Rate" \
  --resource-group "rg-stripe-workflow-prod" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/rg-stripe-workflow-prod/providers/Microsoft.Web/sites/func-stripe-workflow-prod" \
  --condition "avg requests/failed > 5" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action-group "/subscriptions/{subscription-id}/resourceGroups/rg-stripe-workflow-prod/providers/microsoft.insights/actionGroups/AlertActionGroup"

# Create alert for low success rate
az monitor metrics alert create \
  --name "Low Success Rate" \
  --resource-group "rg-stripe-workflow-prod" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/rg-stripe-workflow-prod/providers/Microsoft.Web/sites/func-stripe-workflow-prod" \
  --condition "avg requests/success < 95" \
  --window-size 10m \
  --evaluation-frequency 5m \
  --action-group "/subscriptions/{subscription-id}/resourceGroups/rg-stripe-workflow-prod/providers/microsoft.insights/actionGroups/AlertActionGroup"
```

## Step 7: Security Hardening

### 7.1 Network Security

```bicep
// Add to main.bicep - VNet integration
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-04-01' = {
  name: 'vnet-stripe-workflow-${environmentName}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.0.0.0/16']
    }
    subnets: [
      {
        name: 'subnet-functions'
        properties: {
          addressPrefix: '10.0.1.0/24'
          delegations: [
            {
              name: 'Microsoft.Web.serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

// Update Function App with VNet integration
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  properties: {
    virtualNetworkSubnetId: virtualNetwork.properties.subnets[0].id
    vnetRouteAllEnabled: true
    // ... other properties
  }
}
```

### 7.2 Key Vault Integration

```csharp
// Update Function configuration to use Key Vault
public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly SecretClient _secretClient;

    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
        var keyVaultUrl = _configuration["KeyVault:Url"];
        _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Fallback to app settings for development
            return _configuration[secretName];
        }
    }
}
```

## Step 8: Disaster Recovery

### 8.1 Multi-Region Setup

```bicep
// Deploy to secondary region
param secondaryLocation string = 'West US 2'

resource cosmosAccountSecondary 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: '${cosmosAccountName}-secondary'
  location: secondaryLocation
  properties: {
    // Enable geo-replication
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
      {
        locationName: secondaryLocation
        failoverPriority: 1
      }
    ]
    enableAutomaticFailover: true
    enableMultipleWriteLocations: false
  }
}
```

### 8.2 Backup Strategy

```powershell
# Configure Cosmos DB backup
az cosmosdb sql database create \
  --account-name "cosmos-stripe-workflow-prod" \
  --resource-group "rg-stripe-workflow-prod" \
  --name "StripeWorkflow" \
  --backup-interval 240 \
  --backup-retention 720
```

## Troubleshooting Guide

### Common Issues

#### Function App Won't Start

```powershell
# Check function app logs
az functionapp log tail --name "func-stripe-workflow-prod" --resource-group "rg-stripe-workflow-prod"

# Check application settings
az functionapp config appsettings list --name "func-stripe-workflow-prod" --resource-group "rg-stripe-workflow-prod"
```

#### B2C Authentication Failing

```powershell
# Validate B2C configuration
az ad app show --id "your-b2c-app-id"

# Test token validation endpoint
curl -X GET "https://stripeworkflow.b2clogin.com/stripeworkflow.onmicrosoft.com/B2C_1_signup_signin/discovery"
```

#### Cosmos DB Connection Issues

```powershell
# Test Cosmos DB connectivity
az cosmosdb check-name-exists --name "cosmos-stripe-workflow-prod"

# Check firewall rules
az cosmosdb firewall-rule list --account-name "cosmos-stripe-workflow-prod" --resource-group "rg-stripe-workflow-prod"
```

### Support and Maintenance

#### Log Analysis

```kusto
// Function execution errors
traces
| where timestamp > ago(1h)
| where severityLevel >= 3
| order by timestamp desc

// B2C authentication failures
customEvents
| where name == "AuthenticationFailed"
| where timestamp > ago(1h)
| summarize count() by tostring(customDimensions.Reason)
```

#### Performance Monitoring

```kusto
// Slow requests
requests
| where duration > 5000
| where timestamp > ago(1h)
| order by duration desc
| take 20
```

---

This comprehensive deployment guide provides everything needed to deploy the Serverless Stripe Workflow with Azure B2C authentication to Azure Functions. The infrastructure-as-code approach ensures consistent, reproducible deployments across environments.
