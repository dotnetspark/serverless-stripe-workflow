# SMS Opt-In Disclosure - Serverless Stripe Workflow

## ⚠️ PROOF-OF-CONCEPT NOTICE

**This is a proof-of-concept demonstration system for educational and development purposes only. This system is not intended for production use or real customer communications.**

## SMS Notification Consent

By enabling SMS notifications (`smsOptIn`) in this demonstration system, you acknowledge and agree to the following:

### What You're Consenting To

- **SMS Notifications**: You will receive text messages related to order placement, payment confirmation, and order status updates
- **Message Frequency**: You may receive messages for each order you place through this demo system
- **Message Content**: Messages will contain order confirmation details, payment status, and fulfillment updates

### Your Rights

- **Opt-Out**: You can stop receiving SMS messages at any time by:
  - Replying "STOP" to any SMS message
  - Disabling the `smsOptIn` option in the system
  - Contacting the system administrator
- **Help**: Reply "HELP" to any SMS message for assistance
- **No Cost to You**: This is a demonstration system with no charges for the service itself

### Important Information

- **Message and Data Rates**: Standard message and data rates from your mobile carrier may apply
- **Carrier Support**: SMS delivery depends on your mobile carrier's network and policies
- **Demo Purposes Only**: All messages are generated for testing and demonstration purposes

### Technical Implementation

The SMS opt-in is controlled by the `smsOptIn` parameter in the order submission process:

```json
{
  "customerId": "demo-customer-123",
  "customerEmail": "demo@example.com",
  "items": [...],
  "smsOptIn": true,  // Enable SMS notifications
  "phoneNumber": "+1234567890"  // Required when smsOptIn is true
}
```

### Compliance Notes

This proof-of-concept system implements SMS consent mechanisms to demonstrate best practices for:

- **TCPA Compliance**: Telephone Consumer Protection Act requirements
- **CTIA Guidelines**: Cellular Telecommunications Industry Association messaging guidelines
- **Carrier Requirements**: Major carrier policies for application-to-person messaging

### Data Handling

- **Phone Numbers**: Stored temporarily for demonstration purposes only
- **Message Logs**: SMS delivery logs are maintained for system testing
- **No Commercial Use**: No phone numbers or personal data are used for marketing or commercial purposes

### Contact Information

For questions about this SMS demonstration system:

- **System Purpose**: Educational proof-of-concept for serverless payment workflows
- **Technology Stack**: Azure Functions, Twilio SMS API, Service Bus messaging
- **Repository**: [serverless-stripe-workflow](https://github.com/dotnetspark/serverless-stripe-workflow)

## Developer Notes

### Implementation Details

The SMS opt-in system includes:

1. **Consent Capture**: The `smsOptIn` boolean flag in order requests
2. **Phone Validation**: Required phone number when SMS is enabled
3. **Opt-Out Handling**: STOP/START keyword processing via Twilio webhooks
4. **Message Templates**: Standardized SMS templates for different order states

### Twilio Configuration

Required Twilio webhook endpoints for STOP/START handling:

```
POST /webhooks/twilio/sms-status
POST /webhooks/twilio/sms-opt-out
```

### Testing Guidelines

When testing SMS functionality:

1. Use only phone numbers you own or have explicit permission to message
2. Verify opt-out functionality works correctly
3. Test with different carriers and phone types
4. Monitor delivery rates and failure reasons

---

**Last Updated**: October 27, 2025  
**System Version**: Proof-of-Concept v1.0  
**Compliance Review**: Development/Testing Only
