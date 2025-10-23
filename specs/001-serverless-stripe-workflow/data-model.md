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

  - orderId: string (UUID, partition key)
  - status: enum [Draft, Pending, Paid, Fulfilled, Failed, Cancelled, Refunded]
  - amount: integer (minor units)
  - currency: string (default USD)
  - lineItems: CartItem[]
  - customerEmail: string (required)
  - customerPhone: string (optional, E.164)
  - paymentReference: string (Stripe sessionId)
  - createdAt: datetime (ISO8601)
  - updatedAt: datetime (ISO8601)
  - lastEventId: string (idempotency guard)
  - version: integer (optimistic concurrency control)
  - failureReason: string (optional, for Failed status)
  - metadata: object (optional, for extensibility)

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

- Draft → Pending (on checkout session creation)
- Pending → Paid (on checkout.session.completed)
- Pending → Cancelled (on session timeout or user cancel)
- Paid → Fulfilled (on successful notification delivery)
- Failed → Pending (on retry attempt, if retry count < max)
- Cancelled → Pending (on new checkout session for same order)
- Paid → Refunded (on refund processing, future feature)
- Invalid transitions are rejected to maintain state consistency
