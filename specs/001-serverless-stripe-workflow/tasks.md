# Tasks: Serverless Stripe Payment Workflow

Feature dir: C:\\Users\\ylrre\\source\\repos\\serverless-stripe-workflow\\specs\\001-serverless-stripe-workflow
Plan: C:\\Users\\ylrre\\source\\repos\\serverless-stripe-workflow\\specs\\001-serverless-stripe-workflow\\plan.md
Spec: C:\\Users\\ylrre\\source\\repos\\serverless-stripe-workflow\\specs\\001-serverless-stripe-workflow\\spec.md
Contracts: C:\\Users\\ylrre\\source\\repos\\serverless-stripe-workflow\\specs\\001-serverless-stripe-workflow\\contracts\\openapi.yaml

Guidance:

- [P] means tasks can run in parallel when they don’t touch the same files.
- Order respects: Setup → Tests → Models → Services → Endpoints → Integration → UI → Polish.
- Use TDD: create failing tests first, then implement.

## Parallel execution examples

- Group A [P]: T020–T026 (entity models)
- Group B [P]: T012–T013 (contract tests per endpoint)
- Group C [P]: T060–T065 (integration scenarios)

Example agent invocation (illustrative):

- Run parallel group: T012 T013
- Then run sequential: T030 → T031 → T032

---

## Setup

T001. [X] Initialize solution structure and tooling

- Create directories:
  - src/frontend/blazor-client
  - src/backend/azure-functions/src/{Checkout,Webhooks,Notify}
  - src/backend/aws-lambda/src/{Checkout,Webhooks,Notify}
  - src/tests/{contract,integration,unit}
- Create a .NET solution and add placeholder projects (empty if needed).
- Output: repo layout matches plan.md Project Structure.
- Dependencies: none

T002. [X] Scaffold Blazor WebAssembly app with Tailwind CSS

- Path: frontend/blazor-client
- Create Blazor WASM project and integrate latest Tailwind (PostCSS config, content globs).
- Add base responsive layout and theme (best-guess branding/colors).
- Output: app builds; Tailwind generates styles.
- Dependencies: T001

T003. [X] Bootstrap Azure Functions hosting adapters

- Paths: src/backend/hosting/azure-functions/src/{Checkout,Webhooks,Notify}
- Convert web projects to Azure Functions (.NET isolated worker):
  - CheckoutFunction: [HttpTrigger(Route = "v1/checkout/session")] → CheckoutHandler
  - WebhookFunction: [HttpTrigger(Route = "v1/webhooks/stripe")] → WebhookHandler
  - NotifyFunction: [ServiceBusTrigger("notifications")] → NotificationHandler
- Output: Functions deployable to Azure, expose REST endpoints, delegate to shared handlers.
- Dependencies: T001, T005a (Handlers)

T004. [X] Bootstrap AWS Lambda hosting adapters

- Path: src/backend/hosting/aws-lambda/
- Create Lambda functions with API Gateway integration:
  - Lambda handlers for checkout, webhook, notification endpoints
  - API Gateway routes: POST /v1/checkout/session, POST /v1/webhooks/stripe
  - SQS trigger for notification processing (equivalent to Service Bus)
  - Transform API Gateway/SQS events to shared handler contracts
- Output: Lambda deployable to AWS, same REST endpoints as Azure, same logic via shared handlers.
- Dependencies: T001, T005a (Handlers)

T005. [X] Create shared domain library for models and contracts

- Path: src/backend/shared/Domain (completed)
- Purpose: host entity models (Product, CartItem, Cart, Order, PaymentEvent, Notification, NotificationPreference) and validation helpers.
- Wire library into Handlers and hosting projects.
- Dependencies: T001

T005a. [X] Create shared handlers library for business logic

- Path: src/backend/shared/Handlers (completed)
- Purpose: Core business logic handlers called by both Azure Functions and Lambda:
  - CheckoutHandler: Stripe integration, price validation, session creation
  - WebhookHandler: Signature verification, idempotent processing, order updates
  - NotificationHandler: SendGrid/Twilio integration with retry logic
- Output: Platform-agnostic handlers testable in isolation.
- Dependencies: T005 (Domain models)

T006. Configure environment management and secrets placeholders

- Add configuration schema for: STRIPE_SECRET_KEY, STRIPE_PUBLISHABLE_KEY, STRIPE_WEBHOOK_SECRET, SENDGRID_API_KEY, TWILIO creds, APP_BASE_URL, ALLOWED_ORIGIN, ENV.
- Output: appsettings and environment variable bindings for Azure and AWS.
- Dependencies: T001, T003, T004

T007. [X] Introduce .NET Aspire orchestration (AppHost + ServiceDefaults)

- Path: src/orchestrator/
- Create Aspire.AppHost (.NET project) and Aspire.ServiceDefaults library.
- Configure OpenTelemetry (tracing + logs), health checks, shared config with Polly resilience.
- Output: `Aspire.AppHost` can launch referenced projects.
- Dependencies: T001

T008. Wire frontend and functions into Aspire AppHost

- Reference projects: src/frontend/blazor-client, src/backend/azure-functions/src/\*
- Configure endpoints, environment variables (STRIPE*\_, SENDGRID*_, TWILIO\*_, APP_BASE_URL, ALLOWED_ORIGIN).
- Output: `dotnet run` AppHost starts dependent projects with correct env.
- Dependencies: T002, T003, T007

T009. Add dev containers/emulators to Aspire (optional)

- If using Azurite/DynamoDB local, define them as Aspire resources.
- Output: running AppHost spins up emulators and sets connection strings.
- Dependencies: T007

---

## Contract tests [P]

T010. Create contract test project

- Path: tests/contract
- Setup .NET test project (xUnit or NUnit), helpers to load OpenAPI: specs/001-serverless-stripe-workflow/contracts/openapi.yaml
- Output: project builds with a failing placeholder test.
- Dependencies: T001

T012 [P]. [X] Test: POST /api/v1/checkout/session contract

- Path: src/tests/contract/CheckoutContractTests.cs
- Assert request schema and 200 response shape {id,url}; include 400/409 cases.
- OpenAPI contract validation passing.
- Dependencies: T010

T013 [P]. [X] Test: POST /api/v1/webhooks/stripe contract

- Path: src/tests/contract/WebhookContractTests.cs
- Assert signature header required and 200/400/500 codes.
- OpenAPI contract validation passing.
- Start failing.
- Dependencies: T010

---

## Data models [P]

T020 [P]. Implement Product model with validation

- Path: src/backend/shared/Domain/Models/Product.cs
- Fields per data-model.md; add basic validation attributes.
- Dependencies: T005

T021 [P]. Implement CartItem model with validation

- Path: src/backend/shared/Domain/Models/CartItem.cs
- Dependencies: T005

T022 [P]. Implement Cart model with derived totals

- Path: src/backend/shared/Domain/Models/Cart.cs
- Include method to compute total from items.
- Dependencies: T005

T023 [P]. Implement Order model + state transitions

- Path: src/backend/shared/Domain/Models/Order.cs
- Include status enum and helper methods; lastEventId.
- Dependencies: T005

T024 [P]. Implement PaymentEvent model

- Path: src/backend/shared/Domain/Models/PaymentEvent.cs
- Dependencies: T005

T025 [P]. Implement Notification model

- Path: src/backend/shared/Domain/Models/Notification.cs
- Dependencies: T005

T026 [P]. Implement NotificationPreference model

- Path: src/backend/shared/Domain/Models/NotificationPreference.cs
- Dependencies: T005

---

## Services and storage

T030. [X] Stripe client integration and price validation

- Add Stripe .NET SDK to Azure Functions Checkout; server-side price validation via FakeStore API
- Fetch current product prices from FakeStore API to validate cart totals
- Idempotency-Key handling; derive from orderId if not provided
- Dependencies: T003, T020–T023

T031. [X] Webhook verification and idempotent handler

- Verify Stripe-Signature using STRIPE_WEBHOOK_SECRET.
- Handle events: checkout.session.completed.
- Persist order status updates; guard with lastEventId.
- Dependencies: T003, T020–T024, T040

T032. [X] Notification service with SendGrid/Twilio

- Send email by default via SendGrid; if smsOptIn, send via Twilio.
- Retry on transient failures; structured logs.
- Dependencies: T003, T025–T026

T032a. Aspire: Centralize notification service config

- Define configuration keys in Aspire ServiceDefaults and propagate to services.
- Ensure secrets loaded from user-secrets in dev; document mapping.
- Dependencies: T007, T032

T033. AWS Lambda: Checkout implementation (fallback)

- Mirror T030 in AWS project; keep contracts identical.
- Dependencies: T004, T020–T023

T034. AWS Lambda: Webhook implementation (fallback)

- Mirror T031; signature verification; persistence via DynamoDB.
- Dependencies: T004, T020–T024, T041

T035. AWS Lambda: Notification implementation (fallback)

- Mirror T032.
- Dependencies: T004, T025–T026

---

## Persistence, config, and observability

T040. [X] Azure Cosmos DB repository

- Path: src/backend/shared/Repositories/Cosmos/CosmosOrderRepository.cs
- Implement order persistence using Azure Cosmos DB (SQL API)
- Support order state transitions: Draft → Pending → Paid → Fulfilled
- Handle states: Failed, Cancelled, Refunded with proper transitions
- Upsert by orderId; store lastEventId for idempotency; optimistic concurrency control
- Schema: Order entity with status, timestamps, Stripe references, line items
- Dependencies: T005a (Handlers), T020–T024

T041. [X] DynamoDB repository

- Path: src/backend/shared/Repositories/DynamoDB/DynamoOrderRepository.cs
- Implement same IOrderRepository interface using DynamoDB
- Mirror Cosmos DB functionality: same state transitions, schema, concurrency control
- Use DynamoDB conditional writes for optimistic concurrency
- Dependencies: T040 (interface), T020–T024

T042. [X] Repository abstraction and DI wiring

- Path: src/backend/shared/Repositories/Extensions/ServiceCollectionExtensions.cs
- Define repository interface for order state management and persistence
- Methods: CreateOrder, UpdateOrderStatus, GetOrder, GetOrdersByStatus
- Include state transition validation and concurrency conflict handling
- Register Cosmos/DynamoDB implementations in respective hosting projects
- Dependencies: T040, T041

T043. Structured logging and correlation IDs

- Add logging middleware/util to include orderId, sessionId, eventId.
- Dependencies: T003, T004

T043a. Aspire OpenTelemetry wiring

- Configure OTLP exporter, resource attributes, tracing for frontend and functions via ServiceDefaults.
- Ensure correlation context flows across services.
- Dependencies: T007, T043

T044. CORS, config, and secrets binding

- Restrict ALLOWED_ORIGIN; map env vars in Azure Functions and Lambda.
- Dependencies: T003, T004, T006

---

## Frontend (Blazor WASM)

T050. Integrate FakeStore API for product catalog

- Path: src/frontend/blazor-client/src/services/ProductService.cs and models
- Integrate with FakeStore API (https://fakestoreapi.com/products) for realistic product data
- Create Product model matching API response (id, title, price, description, category, image, rating)
- Add HTTP client service to fetch products with caching
- Convert prices to Stripe-compatible format (dollars to cents)
- Dependencies: T002

T051. Product list component

- Path: src/frontend/blazor-client/src/components/ProductList.razor
- Responsive grid with Tailwind displaying products from FakeStore API
- Show product image, title, price, category, rating
- Add to cart functionality per product
- Dependencies: T050

T052. Cart state and component

- Path: src/frontend/blazor-client/src/components/Cart.razor
- Add/remove items, quantity adjustment, totals.
- Dependencies: T051

T053. Checkout flow integration

- Path: src/frontend/blazor-client/src/pages/Checkout.razor
- Call POST /api/v1/checkout/session; redirect to Stripe URL.
- Dependencies: T030, T052

T054. Success/Cancel pages and contact capture

- Paths: src/frontend/blazor-client/src/pages/{Success.razor,Cancel.razor}
- Collect email (required) and phone (optional) for SMS opt-in prior to checkout.
- Dependencies: T052

T055. Aspire: Frontend dev profile

- Ensure AppHost sets required env and base URLs for Blazor app.
- Add Tailwind watch integration as a dependent process if feasible.
- Dependencies: T007, T002

---

## Integration tests [P]

T060 [P]. Successful checkout end-to-end

- Validate flow from product selection → checkout → webhook → notification email.
- Dependencies: T053, T031, T032

T061 [P]. Payment failure/cancel

- Ensure status remains unpaid; UI shows retry; no success notification.
- Dependencies: T053, T031

T062 [P]. Webhook-only completion

- Close page before return; webhook marks order paid; notification sent.
- Dependencies: T031, T032

T063 [P]. Mobile responsiveness

- Assert components work at small viewport sizes.
- Dependencies: T051–T054

T064 [P]. Notification preferences

- Email default; SMS when opted in.
- Dependencies: T032, T054

T065 [P]. Multi-cloud failover

- Simulate Azure outage; route to AWS; no user-visible downtime.
- Dependencies: T033–T035

---

## Polish and docs

T070. Accessibility pass (a11y)

- Keyboard navigation, landmarks, contrast checks for storefront and cart.
- Dependencies: T051–T054

T071. Quickstart validation and updates

- Align quickstart.md with actual ports, env var names, and steps.
- Dependencies: T053, T031

T072. Performance SLO validation

- Ensure confirmations within 2 minutes, failover switch target <1 minute (document method).
- Dependencies: T032, T033–T035

T073. Observability and alerts

- Ensure logs include correlation IDs; add basic alerting on webhook failures and 5xx spikes.
- Dependencies: T043

T074. Final constitution compliance checklist

- Verify minimal endpoints, secrets handling, idempotency, event handling, and CORS.
- Dependencies: all core tasks
