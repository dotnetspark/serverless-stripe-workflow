// This file is not used by AWS Lambda functions directly.
// Each function has its own entry point in the Functions folder.
// This is kept for compatibility with build tools and local testing.

Console.WriteLine("AWS Lambda functions ready for deployment.");
Console.WriteLine("Available functions:");
Console.WriteLine("- CheckoutFunction::CreateCheckoutSession");
Console.WriteLine("- WebhookFunction::ProcessStripeWebhook");
Console.WriteLine("- NotificationFunction::ProcessNotification");
Console.WriteLine("- NotificationFunction::RetryFailedNotifications");
