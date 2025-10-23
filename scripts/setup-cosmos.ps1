# Setup Cosmos DB Emulator Database and Container
# This script creates the required database and container for local development

$cosmosEndpoint = "https://localhost:8081"
$cosmosKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
$databaseName = "StripeWorkflow"
$containerName = "orders"

Write-Host "🚀 Setting up Cosmos DB for Stripe Workflow..." -ForegroundColor Green

# Install Azure CLI if not present (optional - we can use REST API)
try {
    $headers = @{
        "Authorization" = "type%3dmaster%26ver%3d1.0%26sig%3d$cosmosKey"
        "Content-Type"  = "application/json"
        "x-ms-version"  = "2018-12-31"
    }

    # Create database
    Write-Host "📚 Creating database: $databaseName"
    $createDbUrl = "$cosmosEndpoint/dbs"
    $dbBody = @{
        id = $databaseName
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri $createDbUrl -Method POST -Headers $headers -Body $dbBody -SkipCertificateCheck
        Write-Host "✅ Database created successfully" -ForegroundColor Green
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            Write-Host "ℹ️  Database already exists" -ForegroundColor Yellow
        }
        else {
            Write-Warning "Failed to create database: $($_.Exception.Message)"
        }
    }

    # Create container
    Write-Host "📦 Creating container: $containerName"
    $createContainerUrl = "$cosmosEndpoint/dbs/$databaseName/colls"
    $containerBody = @{
        id             = $containerName
        partitionKey   = @{
            paths = @("/orderId")
            kind  = "Hash"
        }
        indexingPolicy = @{
            indexingMode  = "consistent"
            automatic     = $true
            includedPaths = @(
                @{ path = "/*" }
            )
        }
    } | ConvertTo-Json -Depth 10

    try {
        Invoke-RestMethod -Uri $createContainerUrl -Method POST -Headers $headers -Body $containerBody -SkipCertificateCheck
        Write-Host "✅ Container created successfully" -ForegroundColor Green
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            Write-Host "ℹ️  Container already exists" -ForegroundColor Yellow
        }
        else {
            Write-Warning "Failed to create container: $($_.Exception.Message)"
        }
    }

    Write-Host "🎉 Cosmos DB setup complete!" -ForegroundColor Green
    Write-Host "📍 Database: $databaseName" -ForegroundColor Cyan
    Write-Host "📍 Container: $containerName" -ForegroundColor Cyan
    Write-Host "🔗 Explorer: https://localhost:8081/_explorer/index.html" -ForegroundColor Cyan

}
catch {
    Write-Error "Failed to setup Cosmos DB: $($_.Exception.Message)"
    Write-Host "💡 Make sure Cosmos DB Emulator is running" -ForegroundColor Yellow
}