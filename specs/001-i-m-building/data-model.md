# Data Model: Serverless Stripe Payment Workflow

Date: 2025-10-15

## Entities

- Product

  - id: string
  - name: string
  - description: string
  - imageUrl: string
  - unitPrice: integer (minor units)
  - currency: string (ISO 4217, default USD)
  - isActive: boolean

- CartItem

  - productId: string
  - name: string
  - unitPrice: integer
  - quantity: integer >= 1
  - subtotal: integer (derived)

- Cart

  - items: CartItem[]
  - total: integer (derived)
  - currency: string (default USD)

- Order

  - orderId: string (UUID)
  - status: enum [pending, paid, failed, canceled]
  - amount: integer (minor units)
  - currency: string (default USD)
  - lineItems: CartItem[]
  - customerEmail: string (required)
  - customerPhone: string (optional, E.164)
  - paymentReference: string (Stripe sessionId or paymentIntentId)
  - createdAt: datetime (ISO8601)
  - updatedAt: datetime (ISO8601)
  - lastEventId: string (idempotency guard)

- PaymentEvent

  - eventType: string
  - providerReference: string (e.g., Stripe event id)
  - orderId: string
  - receivedAt: datetime (ISO8601)

- Notification

  - channel: enum [email, sms]
  - destination: string (email or phone)
  - orderId: string
  - status: enum [queued, sent, failed]
  - sentAt: datetime (ISO8601)

- NotificationPreference
  - email: string (required)
  - smsOptIn: boolean
  - phone: string (optional, E.164 when smsOptIn is true)
  - preferredChannel: enum [email, sms] (default email)

## Validation Rules

- Price integrity: amount must match sum(cartItem.unitPrice \* quantity) using server-side catalog.
- Email required on checkout initiation; phone required if smsOptIn.
- Idempotency: orderId must be unique; webhook handling must check lastEventId.

## State Transitions (Order)

- pending -> paid (on checkout.session.completed or payment_intent.succeeded)
- pending -> failed (on payment_intent.payment_failed)
- pending -> canceled (on user cancel/timeout)
- Any -> paid is idempotent; repeat events do not duplicate notifications.
