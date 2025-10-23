# Azure Key Vault Integration

## Overview

This project uses Azure Key Vault for secure secret management across all environments:

- **Development:** `local.settings.json` (local only, not committed)
- **CI/CD:** GitHub Actions → Azure Key Vault
- **Production:** Azure Functions → Azure Key Vault references

## Setup Guide

### 1. Create Azure Key Vault

```bash
# Create resource group
az group create --name rg-stripe-workflow --location eastus

# Create Key Vault
az keyvault create \
  --name kv-stripe-workflow-prod \
  --resource-group rg-stripe-workflow \
  --location eastus \
  --sku standard
```

### 2. Store Secrets in Key Vault

```bash
# Stripe secrets
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "StripeSecretKey" --value "sk_live_..."
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "StripePublishableKey" --value "pk_live_..."
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "StripeWebhookSecret" --value "whsec_..."

# Database connection
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "CosmosConnectionString" --value "AccountEndpoint=..."

# Notification services
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "SendGridApiKey" --value "SG...."
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "TwilioAccountSid" --value "AC..."
az keyvault secret set --vault-name kv-stripe-workflow-prod --name "TwilioAuthToken" --value "..."
```

### 3. Configure Azure Functions for Key Vault

#### Option A: Key Vault References (Recommended)

```json
// In Azure Portal App Settings:
{
  "Stripe__SecretKey": "@Microsoft.KeyVault(VaultName=kv-stripe-workflow-prod;SecretName=StripeSecretKey)",
  "Stripe__PublishableKey": "@Microsoft.KeyVault(VaultName=kv-stripe-workflow-prod;SecretName=StripePublishableKey)",
  "CosmosDb__ConnectionString": "@Microsoft.KeyVault(VaultName=kv-stripe-workflow-prod;SecretName=CosmosConnectionString)"
}
```

#### Option B: Code-based Access

```csharp
// Add to Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());
```

### 4. GitHub Actions Integration

#### Service Principal Setup:

```bash
# Create service principal for GitHub Actions
az ad sp create-for-rbac \
  --name "gh-stripe-workflow" \
  --role "Key Vault Secrets User" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/rg-stripe-workflow/providers/Microsoft.KeyVault/vaults/kv-stripe-workflow-prod" \
  --sdk-auth
```

#### GitHub Secrets Configuration:

```yaml
# Add these to GitHub repository secrets:
AZURE_CREDENTIALS: # Output from service principal creation
AZURE_KEYVAULT_NAME: kv-stripe-workflow-prod
```

### 5. Multiple Environments

```bash
# Development Key Vault
az keyvault create --name kv-stripe-workflow-dev --resource-group rg-stripe-workflow

# Staging Key Vault
az keyvault create --name kv-stripe-workflow-staging --resource-group rg-stripe-workflow

# Production Key Vault
az keyvault create --name kv-stripe-workflow-prod --resource-group rg-stripe-workflow
```

## GitHub Actions Workflow

### Secure Secret Retrieval:

```yaml
- name: Get secrets from Key Vault
  uses: Azure/get-keyvault-secrets@v1
  with:
    keyvault: ${{ secrets.AZURE_KEYVAULT_NAME }}
    secrets: "StripeSecretKey, StripePublishableKey, CosmosConnectionString"
  id: keyvault-secrets

- name: Deploy with secrets
  run: |
    az functionapp config appsettings set \
      --name stripe-workflow-functions \
      --resource-group rg-stripe-workflow \
      --settings \
        "Stripe__SecretKey=@Microsoft.KeyVault(VaultName=${{ secrets.AZURE_KEYVAULT_NAME }};SecretName=StripeSecretKey)" \
        "Stripe__PublishableKey=@Microsoft.KeyVault(VaultName=${{ secrets.AZURE_KEYVAULT_NAME }};SecretName=StripePublishableKey)"
```

## Security Benefits

### ✅ Advantages:

- **Centralized secret management**
- **Audit logging** of secret access
- **Role-based access control**
- **Secret rotation** capabilities
- **No secrets in code or config files**
- **Compliance** with security standards

### 🔒 Access Control:

- **Developers:** Read access to dev Key Vault only
- **GitHub Actions:** Specific secrets for deployment
- **Production:** Function identity with minimal permissions

## Development Workflow

### Local Development:

1. Use `local.settings.json` for local development
2. Get production secrets from Key Vault when needed:
   ```bash
   az keyvault secret show --vault-name kv-stripe-workflow-dev --name "StripeSecretKey" --query value -o tsv
   ```

### CI/CD Pipeline:

1. GitHub Actions authenticates to Azure
2. Retrieves secrets from Key Vault
3. Configures Azure Functions with Key Vault references
4. Deploys application

## Migration Strategy

### Phase 1: Setup Infrastructure

- [ ] Create Azure Key Vault
- [ ] Set up service principal for GitHub Actions
- [ ] Configure GitHub repository secrets

### Phase 2: Store Production Secrets

- [ ] Move production secrets to Key Vault
- [ ] Configure Azure Functions with Key Vault references
- [ ] Test production deployment

### Phase 3: Development Integration

- [ ] Create dev/staging Key Vaults
- [ ] Update deployment pipeline
- [ ] Document team access procedures

### Phase 4: Enhanced Security

- [ ] Implement secret rotation
- [ ] Set up monitoring and alerting
- [ ] Review and audit access permissions
