namespace StripeWorkflow.Services;

public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = "https://localhost:5000/success";
    public string CancelUrl { get; set; } = "https://localhost:5000/cancel";
}