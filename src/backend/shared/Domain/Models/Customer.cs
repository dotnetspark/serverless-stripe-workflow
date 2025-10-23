using StripeWorkflow.Domain.Exceptions;
using StripeWorkflow.Domain.ValueObjects;

namespace StripeWorkflow.Domain.Models;

public class Customer
{
    public CustomerId Id { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public Email Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool SmsOptIn { get; private set; }
    public Address? BillingAddress { get; private set; }
    public Address? ShippingAddress { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Customer()
    {
        Id = null!;
        FirstName = null!;
        LastName = null!;
        Email = null!;
    }

    public static Customer Create(string firstName, string lastName, Email email, string? phoneNumber = null, bool smsOptIn = false)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name cannot be empty");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name cannot be empty");

        if (smsOptIn && string.IsNullOrWhiteSpace(phoneNumber))
            throw new DomainException("Phone number is required when SMS opt-in is enabled");

        return new Customer
        {
            Id = CustomerId.New(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = email,
            PhoneNumber = phoneNumber?.Trim(),
            SmsOptIn = smsOptIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public string FullName => $"{FirstName} {LastName}";

    public void UpdateContactInfo(string firstName, string lastName, Email email, string? phoneNumber = null, bool smsOptIn = false)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name cannot be empty");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name cannot be empty");

        if (smsOptIn && string.IsNullOrWhiteSpace(phoneNumber))
            throw new DomainException("Phone number is required when SMS opt-in is enabled");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email;
        PhoneNumber = phoneNumber?.Trim();
        SmsOptIn = smsOptIn;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateBillingAddress(Address address)
    {
        BillingAddress = address;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateShippingAddress(Address address)
    {
        ShippingAddress = address;
        UpdatedAt = DateTime.UtcNow;
    }
}

public record Address
{
    public string Street { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string Country { get; init; }

    public Address(string street, string city, string state, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street cannot be empty", nameof(street));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be empty", nameof(city));
        if (string.IsNullOrWhiteSpace(state))
            throw new ArgumentException("State cannot be empty", nameof(state));
        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code cannot be empty", nameof(postalCode));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country cannot be empty", nameof(country));

        Street = street.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim().ToUpperInvariant();
    }

    public override string ToString() => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}