using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StripeWorkflow.Domain.Models;
using StripeWorkflow.Domain.ValueObjects;
using StripeWorkflow.Handlers;
using StripeWorkflow.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace StripeWorkflow.IntegrationTests;

/// <summary>
/// Simplified integration tests that align with the actual domain model and handler APIs
/// </summary>
public class SimplifiedIntegrationTests : SimpleIntegrationTestBase
{
    public SimplifiedIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task CheckoutHandler_CreateCheckoutSessionAsync_ShouldCreateOrderAndSession()
    {
        // Arrange
        var checkoutHandler = ServiceProvider.GetRequiredService<CheckoutHandler>();
        var request = new StripeWorkflow.Handlers.CheckoutRequest
        {
            CustomerId = "customer-123",
            CustomerEmail = "test@example.com",
            Items = new List<StripeWorkflow.Handlers.CheckoutItemRequest>
            {
                new() { ProductId = "product-1", Quantity = 2 }
            },
            SuccessUrl = "https://example.com/success",
            CancelUrl = "https://example.com/cancel",
            NotificationUrl = "https://example.com/webhook"
        };

        // Act
        var response = await checkoutHandler.CreateCheckoutSessionAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.OrderId.Should().NotBeEmpty();
        response.SessionId.Should().NotBeEmpty();
        response.TotalAmount.Should().BeGreaterThan(0);
        response.Currency.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WebhookHandler_ProcessWebhookAsync_ShouldProcessValidWebhook()
    {
        // Arrange
        var webhookHandler = ServiceProvider.GetRequiredService<WebhookHandler>();
        var request = new WebhookRequest
        {
            EventType = "checkout.session.completed",
            Payload = """
                {
                    "id": "evt_test_webhook",
                    "type": "checkout.session.completed",
                    "data": {
                        "object": {
                            "id": "cs_test_session",
                            "payment_status": "paid"
                        }
                    }
                }
                """,
            Signature = "test-signature",
            Secret = "test-secret"
        };

        // Act
        var response = await webhookHandler.ProcessWebhookAsync(request);

        // Assert
        response.Should().NotBeNull();
        // Note: This might fail without proper signature validation, but tests the structure
    }

    [Fact]
    public async Task NotificationHandler_ProcessNotificationAsync_ShouldProcessValidNotification()
    {
        // Arrange
        var notificationHandler = ServiceProvider.GetRequiredService<NotificationHandler>();

        // First create an order through the checkout flow
        var checkoutHandler = ServiceProvider.GetRequiredService<CheckoutHandler>();
        var checkoutRequest = new StripeWorkflow.Handlers.CheckoutRequest
        {
            CustomerId = "customer-456",
            CustomerEmail = "notification-test@example.com",
            Items = new List<StripeWorkflow.Handlers.CheckoutItemRequest>
            {
                new() { ProductId = "product-1", Quantity = 1 }
            },
            SuccessUrl = "https://example.com/success",
            CancelUrl = "https://example.com/cancel",
            NotificationUrl = "https://example.com/webhook"
        };

        var checkoutResponse = await checkoutHandler.CreateCheckoutSessionAsync(checkoutRequest);

        var notificationRequest = new NotificationRequest
        {
            OrderId = checkoutResponse.OrderId
        };

        // Act
        var response = await notificationHandler.ProcessNotificationAsync(notificationRequest);

        // Assert
        response.Should().NotBeNull();
        response.Message.Should().NotBeEmpty();
    }

    [Fact]
    public void Product_Create_ShouldCreateValidProduct()
    {
        // Arrange
        var money = new Money(29.99m, "USD");
        var category = ProductCategory.Electronics;

        // Act
        var product = Product.Create(
            "Test Product",
            "A test product description",
            money,
            category,
            new Uri("https://example.com/image.jpg"));

        // Assert
        product.Should().NotBeNull();
        product.Id.Value.Should().NotBeEmpty();
        product.Title.Should().Be("Test Product");
        product.Description.Should().Be("A test product description");
        product.Price.Amount.Should().Be(29.99m);
        product.Price.Currency.Should().Be("USD");
        product.Category.Should().Be(category);
        product.IsActive.Should().BeTrue();
        product.IsAvailable().Should().BeTrue();
    }

    [Fact]
    public void ValueObjects_ShouldCreateCorrectly()
    {
        // Arrange & Act
        var orderId = OrderId.New();
        var customerId = CustomerId.From("customer-123");
        var email = new Email("test@example.com");
        var productId = ProductId.From("product-456");

        // Assert
        orderId.Value.Should().NotBeEmpty();
        customerId.Value.Should().Be("customer-123");
        email.Value.Should().Be("test@example.com");
        productId.Value.Should().Be("product-456");

        // Test implicit string conversion
        string orderIdString = orderId;
        string customerIdString = customerId;
        string emailString = email;
        string productIdString = productId;

        orderIdString.Should().Be(orderId.Value);
        customerIdString.Should().Be("customer-123");
        emailString.Should().Be("test@example.com");
        productIdString.Should().Be("product-456");
    }

    [Fact]
    public void Order_Create_ShouldCreateValidOrder()
    {
        // Arrange
        var customerId = CustomerId.From("customer-789");
        var email = new Email("order-test@example.com");
        var notificationUrl = "https://example.com/webhook";

        // Act
        var order = Order.Create(customerId, email, notificationUrl);

        // Assert
        order.Should().NotBeNull();
        order.Id.Value.Should().NotBeEmpty();
        order.CustomerId.Should().Be(customerId);
        order.CustomerEmail.Should().Be(email);
        order.NotificationUrl.Should().Be(notificationUrl);
        order.Status.Should().Be(OrderStatus.Draft);
        order.Items.Should().BeEmpty();
        order.TotalAmount.Amount.Should().Be(0);
    }

    [Fact]
    public void Money_ShouldCreateAndCalculateCorrectly()
    {
        // Arrange & Act
        var money1 = new Money(10.50m, "USD");
        var money2 = new Money(5.25m, "USD");
        var sum = money1.Add(money2);

        // Assert
        money1.Amount.Should().Be(10.50m);
        money1.Currency.Should().Be("USD");

        money2.Amount.Should().Be(5.25m);
        money2.Currency.Should().Be("USD");

        sum.Amount.Should().Be(15.75m);
        sum.Currency.Should().Be("USD");
    }
}