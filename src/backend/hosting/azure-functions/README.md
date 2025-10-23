# Azure Functions Local Configuration

## Setting up local.settings.json

Each Azure Functions project requires a `local.settings.json` file for local development. This file contains environment variables, API keys, and connection strings.

### ⚠️ SECURITY NOTICE

**NEVER commit `local.settings.json` files to git!** They contain sensitive information like API keys and secrets.

### Setup Instructions

1. **Copy the example file:**

   ```bash
   cp local.settings.json.example src/backend/hosting/azure-functions/src/Checkout/local.settings.json
   cp local.settings.json.example src/backend/hosting/azure-functions/src/Webhooks/local.settings.json
   cp local.settings.json.example src/backend/hosting/azure-functions/src/Notify/local.settings.json
   ```

2. **Update the configuration values:**

   - Replace `YOUR_STRIPE_SECRET_KEY_HERE` with your actual Stripe secret key
   - Replace `YOUR_STRIPE_PUBLISHABLE_KEY_HERE` with your actual Stripe publishable key
   - Add other service keys as needed

3. **Required Configuration:**
   - **Stripe Keys:** Get from [Stripe Dashboard](https://dashboard.stripe.com/test/apikeys)
   - **Cosmos DB:** Uses local emulator by default
   - **SendGrid/Twilio:** Optional for notifications

### Configuration Sections

#### Core Azure Functions

```json
"AzureWebJobsStorage": "UseDevelopmentStorage=true",
"FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
```

#### Stripe Integration

```json
"Stripe__SecretKey": "sk_test_...",
"Stripe__PublishableKey": "pk_test_..."
```

#### Database

```json
"CosmosDb__ConnectionString": "AccountEndpoint=https://localhost:8081/...",
"CosmosDb__DatabaseName": "StripeWorkflow",
"RepositoryProvider": "Cosmos"
```

#### Optional Services

```json
"SENDGRID_API_KEY": "SG...",
"TWILIO_ACCOUNT_SID": "AC...",
"TWILIO_AUTH_TOKEN": "..."
```

### Environment Alternatives

Instead of `local.settings.json`, you can use:

1. **System Environment Variables**
2. **User Secrets** (for non-Functions projects)
3. **Azure Key Vault** (for production)
4. **Docker environment files**

### Production Deployment

In production, these settings are configured as:

- **Azure Functions:** App Settings in Azure Portal
- **Containers:** Environment variables
- **Secrets:** Azure Key Vault references

## Troubleshooting

- **Functions not starting:** Check `FUNCTIONS_WORKER_RUNTIME` is set to `dotnet-isolated`
- **Stripe errors:** Verify API keys are correct and from the right environment (test/live)
- **Database errors:** Ensure Cosmos DB Emulator is running on port 8081
