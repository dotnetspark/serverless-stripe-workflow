# Research Findings: Serverless Stripe Payment Workflow

Date: 2025-10-15

## Decisions

- Use Stripe Checkout for MVP to minimize PCI scope and UI work; Payment Intents/Elements can be added later if needed.
- Azure Functions as primary backend; AWS Lambda as failover. Health/routing layer required (e.g., DNS health checks or API Gateway/LB failover).
- Minimal persistence: order/payment records stored in Azure Table Storage (primary) with a migration/sync path to DynamoDB for failover.
- Notifications: SendGrid for email (default), Twilio for SMS (opt-in). Retries with exponential backoff on transient failures.
- Webhook idempotency: Use orderId + eventId key; store last processed eventId to guard against duplicates/out-of-order.

## Rationale

- Checkout provides Stripe-hosted UI, keeping us in SAQ-A and accelerating delivery.
- Multi-cloud fallback aligns with zero-downtime goal; cost-efficient serverless fits demand pattern.
- Minimal persistence ensures idempotency and reconciliation without heavy DB overhead.

## Alternatives Considered

- Single-cloud only (Azure): simpler but contradicts explicit zero-downtime with AWS fallback.
- Full Payment Element UI: more control but increases effort and PCI surface.
- Shared cross-cloud database: reduces sync complexity but increases cost and complexity; overkill for MVP.

## Open Questions (deferred)

- Exact branding/colors for the storefront (will proceed with best guess and iterate).
- Final failover routing mechanism (DNS vs. traffic manager vs. custom health check orchestrator) to be validated in environment.
