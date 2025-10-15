<!--
Sync Impact Report
- Version change: 1.0.0 → 1.0.1
- Modified principles: None
- Added sections: None
- Removed sections: None
- Templates requiring updates:
  - .specify/templates/plan-template.md ✅ updated (version reference and path)
- Follow-up TODOs: None
-->

# Serverless Stripe Workflow Constitution

## Core Principles

### I. Minimal Surface Area

- Only implement what is required: one endpoint to create a payment (Checkout Session or Payment Intent) and one webhook endpoint to handle Stripe events.
- Prefer Stripe-hosted UI (Checkout) or Payment Element to minimize PCI scope and custom logic.

### II. Security by Default

- Never expose secret keys client-side; load from environment/secret store server-side only.
- Verify Stripe webhook signatures on every request; reject if verification fails.
- Enforce idempotency for all write operations; deduplicate by a stable key (e.g., orderId) or Idempotency-Key header.
- Lock CORS to the allowed origin(s); validate server-side prices via lookup—never trust client-provided amounts.

### III. Event-Driven Source of Truth

- Treat Stripe events as the source of payment state. Persist updates on webhook receipt, not on client redirects alone.
- Webhook handlers must be idempotent and safe to retry; handle out-of-order delivery.

### IV. Test Parity and Observability

- Full support for Stripe test mode; same code paths for live/test with different keys.
- Emit structured logs for every payment lifecycle transition; include correlation IDs (orderId, sessionId, paymentIntentId).

### V. Simplicity and Portability

- Cloud-agnostic, serverless-first: no stateful servers. Minimal infrastructure-as-code, minimal dependencies.
- Clear versioning for APIs and events; avoid breaking changes without a migration note.

## Minimal Requirements

### A. Endpoints (HTTP)

- POST /api/checkout/session (Checkout path)
  - Input: product/price identifier(s) or server-validated cart reference; optional customer email; metadata {orderId}.
  - Output: { id, url } for Stripe-hosted Checkout.
- OR POST /api/payment-intents (Elements path)
  - Input: server-validated amount/currency and metadata {orderId}.
  - Output: { clientSecret } for Payment Element.
- POST /api/webhooks/stripe
  - Stripe signature verification required using STRIPE_WEBHOOK_SECRET.
  - Handle at minimum: checkout.session.completed, payment_intent.succeeded, payment_intent.payment_failed, charge.refunded.
  - Persist status transitions and emit logs; do not perform user-facing redirects here.

### B. Environment and Secrets

- STRIPE_SECRET_KEY (live/test)
- STRIPE_PUBLISHABLE_KEY (client consumption)
- STRIPE_WEBHOOK_SECRET (per deployed environment)
- APP_BASE_URL (for success/cancel URLs and absolute links)
- ALLOWED_ORIGIN (CORS)
- ENV (development|staging|production)
- Optional: PRICE_LOOKUP_TABLE or STRIPE_PRICE_IDs; LOG_LEVEL

Secrets must be stored in the platform secret manager and never checked into source control. Rotation plan documented.

### C. Security and Compliance

- PCI: Maintain SAQ-A by using Stripe Checkout or Elements; never handle raw card data on our servers.
- Validate all server-calculated prices by lookup; ignore/override client-provided amounts.
- Require/propagate an Idempotency-Key for create endpoints (or derive from orderId) and pass through to Stripe requests.
- Apply least-privilege IAM for serverless functions and restrict network egress as feasible.

### D. Data and Persistence (Minimum)

- Store a minimal payment record keyed by orderId:
  - orderId, status, amount, currency, customerEmail (if available), sessionId or paymentIntentId, timestamps, raw event type(s), lastEventId.
- Storage can be any managed serverless database (e.g., DynamoDB, Firestore) or a durable log. It must support idempotent upserts by orderId.

### E. Reliability and Ops

- Webhook handler must be idempotent and retry-safe; tolerate duplicate and out-of-order events.
- Timeouts: 10s recommended per handler; keep work minimal; offload heavy tasks to queues if needed later.
- Logging: structured JSON logs with correlation IDs; capture request IDs and event IDs.
- Monitoring: alert on webhook verification failures, 5xx rates, and missing events (e.g., no completion within N minutes of creation).
- Rate limiting: apply basic protection on public endpoints.

### F. Local Development

- Use Stripe test keys and stripe-cli for webhook forwarding during local dev.
- Provide sample fixtures for minimal happy-path tests.

## Development Workflow and Quality Gates

- Tests: at least one integration test for creating a session/intent and one for webhook signature verification + idempotency.
- Code review must verify: secrets not exposed, idempotency present, price validation enforced, CORS restricted, and events handled.
- Versioning: prefix endpoints with /api/v1; changes that break contracts require a migration note.
- Deployment: immutable builds; environment-specific keys; staged rollout permitted. Rollback plan documented.

## Governance

- This constitution supersedes ad-hoc practices for payments. Deviations require a short written justification and a rollback plan.
- Amendments must document risk, migration, and monitoring changes; version must be incremented.
- All PRs must include a checklist verifying compliance with Security, Endpoints, Webhooks, and Env/Secrets sections above.

**Version**: 1.0.1 | **Ratified**: 2025-10-01 | **Last Amended**: 2025-10-15
