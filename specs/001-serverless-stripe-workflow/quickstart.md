# Quickstart: Serverless Stripe Payment Workflow

This guide helps you run the MVP end-to-end in test mode.

## Prerequisites

- Latest .NET SDK
- Stripe account + test keys
- SendGrid API key (email), Twilio credentials (optional SMS)
- Azure subscription (Functions), AWS account (Lambda)
- stripe-cli installed for local webhook testing

## Environment Variables (local)

- STRIPE_SECRET_KEY, STRIPE_PUBLISHABLE_KEY, STRIPE_WEBHOOK_SECRET
- SENDGRID_API_KEY, TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN (optional)
- APP_BASE_URL, ALLOWED_ORIGIN, ENV=development

## Run Locally (outline)

1. Start frontend (Blazor WASM) with Tailwind build/watch.
2. Start Azure Functions locally for checkout and webhook endpoints.
3. In another terminal, run `stripe listen --forward-to localhost:{port}/api/v1/webhooks/stripe` and set STRIPE_WEBHOOK_SECRET from the output.
4. Use the storefront to add products to cart and click Checkout. Complete payment using test cards.
5. Verify order status and receipt email (and SMS if opted in).

## Notes

- All prices are derived server-side from the mocked catalog; client totals are ignored.
- Webhook handling is idempotent; safe for retries and out-of-order events.
- For failover drills, simulate Azure outage and route requests to AWS endpoints.
