# API Specification v2.0 - Azure B2C Integration

## Serverless Stripe Workflow API

### Overview

This API specification describes the updated Serverless Stripe Workflow API with **Azure B2C authentication integration**. All endpoints require valid Azure B2C JWT tokens except for webhooks (which use Stripe signature validation) and health checks.

## Authentication

### Azure B2C JWT Token Requirements

All protected endpoints require an `Authorization` header with a valid Azure B2C JWT token:

```http
Authorization: Bearer <azure-b2c-jwt-token>
```

#### Token Structure

```json
{
  "iss": "https://stripeWorkflow.b2clogin.com/12345678-1234-1234-1234-123456789012/v2.0/",
  "aud": "abcd1234-5678-90ab-cdef-123456789012",
  "sub": "12345678-1234-1234-1234-123456789012",
  "email": "user@example.com",
  "name": "John Doe",
  "roles": ["customer"],
  "preferred_currency": "USD",
  "timezone": "America/New_York",
  "exp": 1729336800,
  "iat": 1729250400
}
```

#### Custom Claims

- `roles`: User's authorization roles (`["customer", "admin", "support"]`)
- `preferred_currency`: User's preferred currency for pricing display
- `timezone`: User's timezone for date/time formatting

### B2C User Flows

#### Sign Up/Sign In Flow

```
GET https://stripeWorkflow.b2clogin.com/stripeWorkflow.onmicrosoft.com/B2C_1_signup_signin/oauth2/v2.0/authorize
  ?client_id={client-id}
  &response_type=code
  &redirect_uri={redirect-uri}
  &response_mode=query
  &scope=openid profile email
  &state={state}
```

#### Password Reset Flow

```
GET https://stripeWorkflow.b2clogin.com/stripeWorkflow.onmicrosoft.com/B2C_1_password_reset/oauth2/v2.0/authorize
  ?client_id={client-id}
  &response_type=code
  &redirect_uri={redirect-uri}
  &response_mode=query
  &scope=openid
  &state={state}
```

## Base URL

### Azure Functions

```
Production:  https://stripe-workflow-prod.azurewebsites.net/api
Staging:     https://stripe-workflow-staging.azurewebsites.net/api
Development: http://localhost:7071/api
```

## Endpoints

### 1. Checkout Flow

#### Create Checkout Session

Creates a new Stripe checkout session for the authenticated user.

```http
POST /api/checkout
Authorization: Bearer <azure-b2c-jwt-token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "customerId": "customer_12345",
  "items": [
    {
      "productId": "1",
      "quantity": 2
    },
    {
      "productId": "3",
      "quantity": 1
    }
  ],
  "currency": "USD",
  "successUrl": "https://example.com/success",
  "cancelUrl": "https://example.com/cancel"
}
```

**Response (201 Created):**

```json
{
  "orderId": "order_1729250400123",
  "checkoutUrl": "https://checkout.stripe.com/c/pay/cs_test_...",
  "sessionId": "cs_test_1234567890abcdef",
  "expiresAt": "2025-10-18T11:00:00Z",
  "totalAmount": {
    "amount": 89.97,
    "currency": "USD"
  },
  "items": [
    {
      "productId": "1",
      "productName": "Fjallraven - Foldsack No. 1 Backpack",
      "quantity": 2,
      "unitPrice": {
        "amount": 29.99,
        "currency": "USD"
      },
      "totalPrice": {
        "amount": 59.98,
        "currency": "USD"
      }
    },
    {
      "productId": "3",
      "productName": "Mens Cotton Jacket",
      "quantity": 1,
      "unitPrice": {
        "amount": 29.99,
        "currency": "USD"
      },
      "totalPrice": {
        "amount": 29.99,
        "currency": "USD"
      }
    }
  ]
}
```

**Authorization Requirements:**

- User must have `customer` or `admin` role
- `customerId` in request must match token `sub` claim (unless admin role)

**Error Responses:**

```json
// 401 Unauthorized
{
  "error": "unauthorized",
  "message": "Invalid or expired B2C token",
  "details": "Token signature validation failed"
}

// 403 Forbidden
{
  "error": "forbidden",
  "message": "Customer ID mismatch",
  "details": "Token sub claim does not match requested customerId"
}

// 400 Bad Request
{
  "error": "validation_error",
  "message": "Invalid request data",
  "details": {
    "items": "At least one item is required",
    "items[0].quantity": "Quantity must be greater than 0"
  }
}

// 404 Not Found
{
  "error": "product_not_found",
  "message": "One or more products not found",
  "details": {
    "invalidProductIds": ["999"]
  }
}
```

### 2. Webhook Processing

#### Stripe Webhook Handler

Processes Stripe webhook events (no B2C authentication required - uses Stripe signature validation).

```http
POST /api/webhook
Content-Type: application/json
Stripe-Signature: t=1729250400,v1=5257a869e7ecebeda32affa62cdca3fa51cad7e77a0e56ff536d0ce8e108d8bd
```

**Request Body (Stripe Event):**

```json
{
  "id": "evt_1234567890",
  "object": "event",
  "type": "checkout.session.completed",
  "data": {
    "object": {
      "id": "cs_test_1234567890abcdef",
      "object": "checkout.session",
      "payment_status": "paid",
      "customer_email": "user@example.com",
      "metadata": {
        "orderId": "order_1729250400123"
      }
    }
  },
  "created": 1729250400
}
```

**Response (200 OK):**

```json
{
  "received": true,
  "orderId": "order_1729250400123",
  "status": "processed",
  "timestamp": "2025-10-18T10:00:00Z"
}
```

**Supported Event Types:**

- `checkout.session.completed` - Payment successful
- `checkout.session.expired` - Session expired without payment
- `payment_intent.payment_failed` - Payment failed

### 3. Product Catalog

#### Get Products

Retrieves product catalog with user-specific pricing (B2C authentication required for personalized content).

```http
GET /api/products?category=electronics&limit=20&currency=USD
Authorization: Bearer <azure-b2c-jwt-token>
```

**Query Parameters:**

- `category` (optional): Filter by product category
- `limit` (optional): Number of products to return (default: 20, max: 100)
- `currency` (optional): Currency for pricing (defaults to user's preferred currency from B2C token)

**Response (200 OK):**

```json
{
  "products": [
    {
      "id": "1",
      "title": "Fjallraven - Foldsack No. 1 Backpack",
      "price": {
        "amount": 29.99,
        "currency": "USD",
        "displayPrice": "$29.99"
      },
      "description": "Your perfect pack for everyday use...",
      "category": "men's clothing",
      "image": "https://fakestoreapi.com/img/81fPKd-2AYL._AC_SL1500_.jpg",
      "rating": {
        "rate": 3.9,
        "count": 120
      },
      "availability": "in_stock"
    }
  ],
  "pagination": {
    "total": 20,
    "limit": 20,
    "offset": 0,
    "hasMore": false
  },
  "currency": "USD",
  "userPreferences": {
    "preferredCurrency": "USD",
    "timezone": "America/New_York"
  }
}
```

#### Get Single Product

```http
GET /api/products/{productId}?currency=USD
Authorization: Bearer <azure-b2c-jwt-token>
```

**Response (200 OK):**

```json
{
  "id": "1",
  "title": "Fjallraven - Foldsack No. 1 Backpack",
  "price": {
    "amount": 29.99,
    "currency": "USD",
    "displayPrice": "$29.99"
  },
  "description": "Your perfect pack for everyday use and walks in the forest...",
  "category": "men's clothing",
  "image": "https://fakestoreapi.com/img/81fPKd-2AYL._AC_SL1500_.jpg",
  "rating": {
    "rate": 3.9,
    "count": 120
  },
  "availability": "in_stock",
  "relatedProducts": ["2", "15", "18"]
}
```

### 4. Order Management

#### Get User Orders

Retrieves order history for the authenticated user.

```http
GET /api/orders?status=paid&limit=10&offset=0
Authorization: Bearer <azure-b2c-jwt-token>
```

**Query Parameters:**

- `status` (optional): Filter by order status
- `limit` (optional): Number of orders to return (default: 10, max: 50)
- `offset` (optional): Pagination offset

**Response (200 OK):**

```json
{
  "orders": [
    {
      "orderId": "order_1729250400123",
      "customerId": "customer_12345",
      "status": "paid",
      "totalAmount": {
        "amount": 89.97,
        "currency": "USD"
      },
      "items": [
        {
          "productId": "1",
          "productName": "Fjallraven - Foldsack No. 1 Backpack",
          "quantity": 2,
          "unitPrice": {
            "amount": 29.99,
            "currency": "USD"
          }
        }
      ],
      "createdAt": "2025-10-18T10:00:00Z",
      "updatedAt": "2025-10-18T10:05:00Z",
      "stripeSessionId": "cs_test_1234567890abcdef",
      "paymentStatus": "succeeded"
    }
  ],
  "pagination": {
    "total": 15,
    "limit": 10,
    "offset": 0,
    "hasMore": true
  }
}
```

**Authorization Requirements:**

- User can only access their own orders (matching token `sub` claim)
- Admin users can access all orders

#### Get Single Order

```http
GET /api/orders/{orderId}
Authorization: Bearer <azure-b2c-jwt-token>
```

**Response (200 OK):**

```json
{
  "orderId": "order_1729250400123",
  "customerId": "customer_12345",
  "status": "paid",
  "totalAmount": {
    "amount": 89.97,
    "currency": "USD"
  },
  "items": [
    {
      "productId": "1",
      "productName": "Fjallraven - Foldsack No. 1 Backpack",
      "quantity": 2,
      "unitPrice": {
        "amount": 29.99,
        "currency": "USD"
      },
      "totalPrice": {
        "amount": 59.98,
        "currency": "USD"
      }
    }
  ],
  "customerInfo": {
    "email": "user@example.com",
    "name": "John Doe"
  },
  "paymentInfo": {
    "stripeSessionId": "cs_test_1234567890abcdef",
    "paymentStatus": "succeeded",
    "paidAt": "2025-10-18T10:05:00Z"
  },
  "timeline": [
    {
      "status": "draft",
      "timestamp": "2025-10-18T10:00:00Z",
      "description": "Order created"
    },
    {
      "status": "pending",
      "timestamp": "2025-10-18T10:01:00Z",
      "description": "Checkout session created"
    },
    {
      "status": "paid",
      "timestamp": "2025-10-18T10:05:00Z",
      "description": "Payment completed successfully"
    }
  ],
  "createdAt": "2025-10-18T10:00:00Z",
  "updatedAt": "2025-10-18T10:05:00Z"
}
```

### 5. User Profile

#### Get User Profile

Retrieves user profile information from Azure B2C.

```http
GET /api/profile
Authorization: Bearer <azure-b2c-jwt-token>
```

**Response (200 OK):**

```json
{
  "userId": "12345678-1234-1234-1234-123456789012",
  "email": "user@example.com",
  "name": "John Doe",
  "givenName": "John",
  "surname": "Doe",
  "roles": ["customer"],
  "preferences": {
    "preferredCurrency": "USD",
    "timezone": "America/New_York",
    "language": "en-US",
    "emailNotifications": true
  },
  "createdAt": "2025-01-15T08:30:00Z",
  "lastLoginAt": "2025-10-18T09:45:00Z"
}
```

#### Update User Preferences

Updates user preferences (limited subset - full profile editing done through B2C).

```http
PUT /api/profile/preferences
Authorization: Bearer <azure-b2c-jwt-token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "preferredCurrency": "EUR",
  "timezone": "Europe/London",
  "emailNotifications": false
}
```

**Response (200 OK):**

```json
{
  "updated": true,
  "preferences": {
    "preferredCurrency": "EUR",
    "timezone": "Europe/London",
    "language": "en-US",
    "emailNotifications": false
  }
}
```

### 6. Admin Endpoints

#### Get All Orders (Admin Only)

```http
GET /api/admin/orders?status=paid&customer=customer_123&limit=50
Authorization: Bearer <azure-b2c-jwt-token>
```

**Authorization Requirements:**

- User must have `admin` role in B2C token

**Response (200 OK):**

```json
{
  "orders": [
    {
      "orderId": "order_1729250400123",
      "customerId": "customer_12345",
      "customerEmail": "user@example.com",
      "status": "paid",
      "totalAmount": {
        "amount": 89.97,
        "currency": "USD"
      },
      "createdAt": "2025-10-18T10:00:00Z",
      "paymentStatus": "succeeded"
    }
  ],
  "summary": {
    "totalOrders": 156,
    "totalRevenue": {
      "amount": 12750.45,
      "currency": "USD"
    },
    "ordersByStatus": {
      "paid": 142,
      "pending": 8,
      "failed": 4,
      "cancelled": 2
    }
  }
}
```

### 7. Health & Monitoring

#### Health Check

System health status (no authentication required).

```http
GET /api/health
```

**Response (200 OK):**

```json
{
  "status": "healthy",
  "timestamp": "2025-10-18T10:00:00Z",
  "version": "2.0.0",
  "environment": "production",
  "dependencies": {
    "cosmosDb": {
      "status": "healthy",
      "responseTime": "45ms",
      "region": "East US"
    },
    "stripeApi": {
      "status": "healthy",
      "responseTime": "120ms"
    },
    "fakeStoreApi": {
      "status": "healthy",
      "responseTime": "200ms"
    },
    "azureB2C": {
      "status": "healthy",
      "responseTime": "80ms"
    }
  }
}
```

#### Metrics (Admin Only)

```http
GET /api/admin/metrics?timeRange=24h
Authorization: Bearer <azure-b2c-jwt-token>
```

**Response (200 OK):**

```json
{
  "timeRange": "24h",
  "period": {
    "start": "2025-10-17T10:00:00Z",
    "end": "2025-10-18T10:00:00Z"
  },
  "metrics": {
    "totalRequests": 15420,
    "totalOrders": 89,
    "successRate": 99.2,
    "averageResponseTime": "245ms",
    "totalRevenue": {
      "amount": 2450.67,
      "currency": "USD"
    },
    "topProducts": [
      {
        "productId": "1",
        "productName": "Fjallraven - Foldsack No. 1 Backpack",
        "orderCount": 12,
        "revenue": {
          "amount": 359.88,
          "currency": "USD"
        }
      }
    ],
    "errorsByType": {
      "validation_error": 15,
      "product_not_found": 8,
      "payment_failed": 3
    }
  }
}
```

## Error Handling

### Standard HTTP Status Codes

- `200 OK` - Successful request
- `201 Created` - Resource created successfully
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Invalid or missing B2C token
- `403 Forbidden` - Valid token but insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource already exists
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error
- `503 Service Unavailable` - Service temporarily unavailable

### Error Response Format

All error responses follow this format:

```json
{
  "error": "error_code",
  "message": "Human-readable error message",
  "details": "Additional error context or validation details",
  "timestamp": "2025-10-18T10:00:00Z",
  "requestId": "12345678-1234-1234-1234-123456789012"
}
```

### B2C-Specific Errors

```json
// Invalid B2C Token
{
  "error": "invalid_token",
  "message": "Azure B2C token validation failed",
  "details": "Token signature verification failed against B2C JWKS endpoint",
  "timestamp": "2025-10-18T10:00:00Z",
  "requestId": "12345678-1234-1234-1234-123456789012"
}

// Expired Token
{
  "error": "token_expired",
  "message": "B2C token has expired",
  "details": "Token expired at 2025-10-18T09:30:00Z",
  "timestamp": "2025-10-18T10:00:00Z",
  "requestId": "12345678-1234-1234-1234-123456789012"
}

// Insufficient Role
{
  "error": "insufficient_permissions",
  "message": "User role does not have permission for this operation",
  "details": "Required role: admin, User roles: [customer]",
  "timestamp": "2025-10-18T10:00:00Z",
  "requestId": "12345678-1234-1234-1234-123456789012"
}
```

## Rate Limiting

### API Management Policies

- **Authenticated endpoints**: 1000 requests per hour per user
- **Checkout endpoint**: 10 requests per minute per user
- **Webhook endpoint**: 100 requests per minute (Stripe IP-based)
- **Admin endpoints**: 5000 requests per hour per admin user

### Rate Limit Headers

```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 995
X-RateLimit-Reset: 1729254000
X-RateLimit-Policy: 1000-per-hour-per-user
```

## Versioning

### API Versioning Strategy

- **URL versioning**: `/api/v2/endpoint`
- **Header versioning**: `API-Version: 2.0`
- **Backward compatibility**: v1 endpoints maintained for 12 months

### Version Migration

```http
# Current v2 endpoint
GET /api/checkout

# Legacy v1 endpoint (deprecated)
GET /api/v1/checkout
Warning: API version 1.0 is deprecated. Please migrate to v2.0
```

## OpenAPI Specification

A complete OpenAPI 3.0 specification is available at:

- Production: `https://stripe-workflow-prod.azurewebsites.net/api/swagger.json`
- Interactive docs: `https://stripe-workflow-prod.azurewebsites.net/api/docs`

---

This API specification provides comprehensive documentation for the Azure B2C integrated Serverless Stripe Workflow API, ensuring developers can easily implement authentication and utilize all available endpoints.
