# Hybrid Observability Configuration - App Insights + Grafana

## Overview

This configuration uses a cost-effective hybrid approach combining Azure Application Insights (free tier) for basic telemetry with structured logging to Azure Storage and Grafana for advanced dashboards and visualization.

## Architecture

```
Azure Functions → Application Insights (Free Tier)
              ↓
         Azure Storage (Structured Logs)
              ↓
           Grafana Dashboard
              ↓
         Custom Alerts & Metrics
```

## Configuration Files

### docker-compose.yml (Grafana for local development)

```yaml
version: "3.8"
services:
  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin123
      - GF_INSTALL_PLUGINS=grafana-azure-monitor-datasource
    volumes:
      - grafana-storage:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning
    networks:
      - observability

  # Optional: Local Application Insights emulator for development
  app-insights-emulator:
    image: mcr.microsoft.com/applicationinsights/emulator
    container_name: app-insights-emulator
    ports:
      - "9095:9095"
    networks:
      - observability

volumes:
  grafana-storage:
    driver: local

networks:
  observability:
    driver: bridge
```

### Azure Storage Structured Logging Configuration

#### appsettings.json

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-app-insights-key"
  },
  "StructuredLogging": {
    "StorageAccount": "your-storage-account",
    "ContainerName": "application-logs",
    "LogLevel": "Information"
  },
  "Grafana": {
    "WebhookUrl": "http://localhost:3000/api/webhooks/your-webhook-id"
        "message" => "\[%{TIMESTAMP_ISO8601:timestamp}\] %{LOGLEVEL:level}: %{GREEDYDATA:log_message}"
      }
    }

    date {
      match => [ "timestamp", "ISO8601" ]
    }

    # Extract custom fields
    if [customerId] {
      mutate {
        add_tag => [ "customer-activity" ]
      }
    }

    if [traceId] {
      mutate {
        add_tag => [ "traced-request" ]
      }
    }
  }

  # Parse Stripe webhook logs
  if [source] == "stripe-webhooks" {
    json {
      source => "webhook_data"
    }

    mutate {
      add_tag => [ "stripe-event" ]
      add_field => { "event_type" => "%{[webhook_data][type]}" }
    }
  }

  # Performance metrics
  if [duration_ms] {
    if [duration_ms] > 5000 {
      mutate {
        add_tag => [ "slow-request" ]
      }
    }
  }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "serverless-stripe-workflow-%{+YYYY.MM.dd}"

    # Route different log types to different indices
    if "customer-activity" in [tags] {
      index => "customer-activity-%{+YYYY.MM.dd}"
    }

    if "stripe-event" in [tags] {
      index => "stripe-events-%{+YYYY.MM.dd}"
    }

    if "slow-request" in [tags] {
      index => "performance-issues-%{+YYYY.MM.dd}"
    }
  }

  # Debug output for development
  stdout {
    codec => rubydebug
  }
}
```

### kibana/config/kibana.yml

```yaml
server.name: kibana
server.host: "0.0.0.0"
elasticsearch.hosts: ["http://elasticsearch:9200"]
xpack.monitoring.ui.container.elasticsearch.enabled: true
xpack.security.enabled: false

# Custom dashboard configurations
xpack.reporting.enabled: false
xpack.alerting.enabled: true
```

## Azure Function Integration

### Hybrid Structured Logging in C#

```csharp
public class HybridLogger
{
    private readonly ILogger _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public HybridLogger(ILogger logger, TelemetryClient telemetryClient, BlobServiceClient blobServiceClient, IConfiguration config)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _blobServiceClient = blobServiceClient;
        _containerName = config["StructuredLogging:ContainerName"] ?? "application-logs";
    }

    public async Task LogAsync(LogLevel level, string message, object? data = null)
    {
        var logEntry = new
        {
            timestamp = DateTime.UtcNow,
            level = level.ToString(),
            message,
            source = "azure-functions",
            environment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"),
            functionName = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_CONTEXT_FUNCTIONNAME"),
            correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
            data
        };

        // 1. Send to Application Insights (Free Tier)
        var properties = new Dictionary<string, string>
        {
            ["source"] = "azure-functions",
            ["functionName"] = logEntry.functionName ?? "unknown",
            ["correlationId"] = logEntry.correlationId
        };

        var metrics = new Dictionary<string, double>();
        if (data is { } && data.GetType().GetProperty("duration_ms") != null)
        {
            metrics["duration_ms"] = Convert.ToDouble(data.GetType().GetProperty("duration_ms")?.GetValue(data));
        }

        _telemetryClient.TrackTrace(message, ConvertLogLevel(level), properties);
        if (metrics.Any())
        {
            foreach (var metric in metrics)
            {
                _telemetryClient.TrackMetric(metric.Key, metric.Value, properties);
            }
        }

        // 2. Send structured logs to Azure Storage for Grafana
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{DateTime.UtcNow:HH}/{Guid.NewGuid()}.json";
            var blobClient = containerClient.GetBlobClient(blobName);

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions { WriteIndented = false });
            await blobClient.UploadAsync(new BinaryData(json), overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send structured log to storage");
        }

        // 3. Fallback to standard logging
        _logger.Log(level, message);
    }

    private SeverityLevel ConvertLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Critical => SeverityLevel.Critical,
        LogLevel.Error => SeverityLevel.Error,
        LogLevel.Warning => SeverityLevel.Warning,
        LogLevel.Information => SeverityLevel.Information,
        LogLevel.Debug => SeverityLevel.Verbose,
        LogLevel.Trace => SeverityLevel.Verbose,
        _ => SeverityLevel.Information
    };

    public async Task LogCheckoutEventAsync(string customerId, string sessionId, decimal amount, string currency)
    {
        await LogAsync(LogLevel.Information, "Checkout session created", new
        {
            customerId,
            sessionId,
            amount,
            currency,
            traceId = Activity.Current?.Id,
            tags = new[] { "checkout", "payment" }
        });
    }

    public async Task LogStripeWebhookAsync(string eventType, string eventId, object webhookData)
    {
        await LogAsync(LogLevel.Information, $"Stripe webhook received: {eventType}", new
        {
            eventType,
            eventId,
            source = "stripe-webhooks",
            webhook_data = webhookData,
            traceId = Activity.Current?.Id
        });
    }
}
```

### Performance Monitoring Extension

```csharp
public class PerformanceTrackingMiddleware
{
    private readonly StructuredLogger _logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            await _logger.LogAsync(LogLevel.Information, "Request completed", new
            {
                method = context.Request.Method,
                path = context.Request.Path,
                statusCode = context.Response.StatusCode,
                duration_ms = stopwatch.ElapsedMilliseconds,
                traceId,
                userId = context.User?.FindFirst("sub")?.Value,
                tags = new[] { "performance", "http-request" }
            });
        }
    }
}
```

## Grafana Dashboards

### 1. Application Overview Dashboard

```json
{
  "dashboard": {
    "id": null,
    "title": "Stripe Workflow - Application Overview",
    "tags": ["azure-functions", "stripe"],
    "timezone": "browser",
    "panels": [
      {
        "id": 1,
        "title": "Request Rate (per minute)",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "Requests",
            "aggregation": "Total",
            "timeGrain": "PT1M"
          }
        ],
        "gridPos": { "h": 8, "w": 6, "x": 0, "y": 0 }
      },
      {
        "id": 2,
        "title": "Error Rate %",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "Http5xx",
            "aggregation": "Total"
          }
        ],
        "gridPos": { "h": 8, "w": 6, "x": 6, "y": 0 }
      },
      {
        "id": 3,
        "title": "Average Response Time",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "AverageResponseTime",
            "aggregation": "Average"
          }
        ],
        "gridPos": { "h": 8, "w": 6, "x": 12, "y": 0 }
      },
      {
        "id": 4,
        "title": "Active Instances",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "FunctionExecutionCount",
            "aggregation": "Total"
          }
        ],
        "gridPos": { "h": 8, "w": 6, "x": 18, "y": 0 }
      },
      {
        "id": 5,
        "title": "Request Timeline",
        "type": "timeseries",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "Requests",
            "aggregation": "Total",
            "timeGrain": "PT5M"
          }
        ],
        "gridPos": { "h": 8, "w": 12, "x": 0, "y": 8 }
      },
      {
        "id": 6,
        "title": "Top Customer Activity (from Storage Logs)",
        "type": "table",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where data contains 'customerId' | summarize count() by customerId | top 10 by count_"
          }
        ],
        "gridPos": { "h": 8, "w": 12, "x": 12, "y": 8 }
      }
    ],
    "time": { "from": "now-1h", "to": "now" },
    "refresh": "30s"
  }
}
```

### 2. Stripe Events Dashboard

```json
{
  "dashboard": {
    "id": null,
    "title": "Stripe Webhook Events",
    "tags": ["stripe", "webhooks", "payments"],
    "panels": [
      {
        "id": 1,
        "title": "Webhook Events (Last Hour)",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where source == 'stripe-webhooks' and timestamp > ago(1h) | count"
          }
        ],
        "gridPos": { "h": 6, "w": 6, "x": 0, "y": 0 }
      },
      {
        "id": 2,
        "title": "Payment Success Rate",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where data contains 'payment_intent' | summarize success=countif(data contains 'succeeded'), total=count() | extend rate=success*100.0/total"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "color": { "mode": "thresholds" },
            "thresholds": {
              "steps": [
                { "color": "red", "value": 0 },
                { "color": "yellow", "value": 90 },
                { "color": "green", "value": 95 }
              ]
            }
          }
        },
        "gridPos": { "h": 6, "w": 6, "x": 6, "y": 0 }
      },
      {
        "id": 3,
        "title": "Event Types Distribution",
        "type": "piechart",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where source == 'stripe-webhooks' | extend event_type = tostring(data.eventType) | summarize count() by event_type"
          }
        ],
        "gridPos": { "h": 8, "w": 12, "x": 12, "y": 0 }
      },
      {
        "id": 4,
        "title": "Webhook Events Timeline",
        "type": "timeseries",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where source == 'stripe-webhooks' | summarize count() by bin(timestamp, 5m)"
          }
        ],
        "gridPos": { "h": 8, "w": 24, "x": 0, "y": 8 }
      },
      {
        "id": 5,
        "title": "Recent Webhook Events",
        "type": "table",
        "targets": [
          {
            "datasource": "Azure Storage",
            "query": "application-logs | where source == 'stripe-webhooks' | project timestamp, level, message, eventType=tostring(data.eventType), eventId=tostring(data.eventId) | order by timestamp desc | take 50"
          }
        ],
        "gridPos": { "h": 10, "w": 24, "x": 0, "y": 16 }
      }
    ]
  }
}
```

### 3. Cost Monitoring Dashboard

```json
{
  "dashboard": {
    "id": null,
    "title": "Cost & Resource Monitoring",
    "tags": ["cost", "resources", "optimization"],
    "panels": [
      {
        "id": 1,
        "title": "Function Execution Count",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Web/sites",
            "metricName": "FunctionExecutionCount",
            "aggregation": "Total"
          }
        ],
        "gridPos": { "h": 6, "w": 6, "x": 0, "y": 0 }
      },
      {
        "id": 2,
        "title": "Storage Usage (GB)",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Storage/storageAccounts",
            "metricName": "UsedCapacity",
            "aggregation": "Average"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "bytes",
            "custom": { "displayMode": "basic" }
          }
        },
        "gridPos": { "h": 6, "w": 6, "x": 6, "y": 0 }
      },
      {
        "id": 3,
        "title": "App Insights Data Volume",
        "type": "stat",
        "targets": [
          {
            "datasource": "Azure Monitor",
            "metricDefinition": "Microsoft.Insights/components",
            "metricName": "BillingVolumeGB",
            "aggregation": "Total"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "bytes",
            "color": { "mode": "thresholds" },
            "thresholds": {
              "steps": [
                { "color": "green", "value": 0 },
                { "color": "yellow", "value": 4000000000 },
                { "color": "red", "value": 5000000000 }
              ]
            }
          }
        },
        "gridPos": { "h": 6, "w": 6, "x": 12, "y": 0 }
      }
    ]
  }
}
```

## Cost Optimization

### Hybrid Approach Pricing

**Azure Services:**

- **Application Insights**: FREE (5GB/month, 90-day retention)
- **Azure Storage (General Purpose v2)**: ~$1.50/month for 50GB structured logs
- **Network**: Minimal costs for internal traffic

**Grafana Options:**

- **Self-hosted (Docker)**: FREE
- **Grafana Cloud**: FREE tier (10k metrics series, 50GB logs)
- **Azure Container Instance (optional)**: ~$15/month for dedicated instance

**Total Monthly Cost:**

- **Minimal Setup**: ~**$2/month** (App Insights free + Storage)
- **With dedicated Grafana**: ~**$17/month** (+ Container Instance)
- **Comparison**: vs $500+/month for Application Insights Premium or $37/month for full ELK stack

## Deployment Commands

### Local Development

```bash
# Start Grafana locally
docker-compose up -d

# Check Grafana status
curl http://localhost:3000/api/health

# Login to Grafana
# URL: http://localhost:3000
# Username: admin
# Password: admin123

# Test structured logging
dotnet run --project src/backend/hosting/azure-functions/src/Checkout
```

### Azure Deployment

```bash
# Enable Application Insights (Free Tier)
az monitor app-insights component create \
  --app "stripe-workflow-insights" \
  --location "East US" \
  --resource-group "rg-stripe-workflow-prod" \
  --application-type web \
  --retention-time 90

# Create storage account for structured logs
az storage account create \
  --name "stlogs${RANDOM}" \
  --resource-group "rg-stripe-workflow-prod" \
  --location "East US" \
  --sku Standard_LRS \
  --kind StorageV2

# Configure Function App with hybrid observability
az functionapp config appsettings set \
  --resource-group "rg-stripe-workflow-prod" \
  --name "func-stripe-workflow-prod" \
  --settings \
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$(az monitor app-insights component show --app stripe-workflow-insights -g rg-stripe-workflow-prod --query connectionString -o tsv)" \
    "StructuredLogging__StorageAccount=stlogs${RANDOM}" \
    "StructuredLogging__ContainerName=application-logs"

# Optional: Deploy Grafana on Container Instance
az container create \
  --resource-group "rg-stripe-workflow-prod" \
  --name "grafana-instance" \
  --image "grafana/grafana:latest" \
  --ports 3000 \
  --environment-variables \
    "GF_SECURITY_ADMIN_PASSWORD=YourSecurePassword123!" \
    "GF_INSTALL_PLUGINS=grafana-azure-monitor-datasource" \
  --cpu 1 \
  --memory 1
```

## Monitoring & Alerting

### Custom Alerts in Kibana

```json
{
  "trigger": {
    "schedule": {
      "interval": "5m"
    }
  },
  "input": {
    "search": {
      "request": {
        "search_type": "query_then_fetch",
        "indices": ["performance-issues-*"],
        "body": {
          "query": {
            "bool": {
              "filter": [
                {
                  "range": {
                    "@timestamp": {
                      "gte": "now-5m"
                    }
                  }
                }
              ]
            }
          }
        }
      }
    }
  },
  "condition": {
    "compare": {
      "ctx.payload.hits.total": {
        "gt": 10
      }
    }
  },
  "actions": {
    "send_email": {
      "email": {
        "to": ["admin@example.com"],
        "subject": "High number of slow requests detected",
        "body": "{{ctx.payload.hits.total}} slow requests detected in the last 5 minutes"
      }
    }
  }
}
```

This ELK stack configuration provides comprehensive observability at a fraction of the cost of Application Insights while maintaining full control over your data and dashboards.
