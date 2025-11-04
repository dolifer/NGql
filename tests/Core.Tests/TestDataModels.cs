using System;

namespace NGql.Core.Tests;

/// <summary>
/// Test data models for preservation and expression field extraction tests.
/// </summary>
public static class TestDataModels
{
    /// <summary>
    /// Model with navigation property "Field" that maps to "SettingA"
    /// </summary>
    public class SettingGroupA
    {
        public string? SettingA { get; set; }
        public string? Field => SettingA;
    }

    /// <summary>
    /// Model with navigation property "Field" that maps to "SettingB"
    /// </summary>
    public class SettingGroupB
    {
        public string? SettingB { get; set; }
        public string? Field => SettingB;
    }

    /// <summary>
    /// Model with navigation property "Field" that maps to "SettingC"
    /// </summary>
    public class SettingGroupC
    {
        public string? SettingC { get; set; }
        public string? Field => SettingC;
    }

    /// <summary>
    /// Simple user model for basic preservation tests
    /// </summary>
    public class SimpleUser
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>
    /// User model with nested profile object
    /// </summary>
    public class UserWithProfile
    {
        public string? UserId { get; set; }
        public UserProfile? Profile { get; set; }
        public UserSettings? Settings { get; set; }
    }

    /// <summary>
    /// Nested profile object
    /// </summary>
    public class UserProfile
    {
        public string? Name { get; set; }
        public string? Bio { get; set; }
        public string? Avatar { get; set; }
        public UserAddress? Address { get; set; }
    }

    /// <summary>
    /// Nested address object
    /// </summary>
    public class UserAddress
    {
        public string? City { get; set; }
        public string? Street { get; set; }
    }

    /// <summary>
    /// Nested settings object
    /// </summary>
    public class UserSettings
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
    }

    /// <summary>
    /// Deposit info with navigation property "Date"
    /// </summary>
    public class DepositInfo
    {
        public DateTime? FirstDepositTime { get; set; }
        public DateTime? SecondDepositTime { get; set; }

        // Navigation property - should expand to FirstDepositTime and SecondDepositTime
        public DateTime? Date => FirstDepositTime ?? SecondDepositTime;
    }

    /// <summary>
    /// User model with address information (deeply nested)
    /// </summary>
    public class UserWithAddress
    {
        public string? UserId { get; set; }
        public UserProfileWithAddress? Profile { get; set; }
    }

    /// <summary>
    /// User profile with address
    /// </summary>
    public class UserProfileWithAddress
    {
        public string? Name { get; set; }
        public UserAddress? Address { get; set; }
    }

    /// <summary>
    /// User model with array fields
    /// </summary>
    public class UserWithArrays
    {
        public string? UserId { get; set; }
        public string[]? Tags { get; set; }
        public int[]? Scores { get; set; }
    }

    /// <summary>
    /// Product model for multi-parameter tests
    /// </summary>
    public class Product
    {
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
    }

    /// <summary>
    /// Model with nested navigation properties for testing deep computed property resolution
    /// </summary>
    public class UserWithComputedProfile
    {
        public string? UserId { get; set; }
        public ComputedProfile? Profile { get; set; }
    }

    /// <summary>
    /// Profile with nested computed properties
    /// </summary>
    public class ComputedProfile
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public ComputedContact? Contact { get; set; }

        // Navigation property - should expand to FirstName and LastName
        public string? Name => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Contact info with computed property
    /// </summary>
    public class ComputedContact
    {
        public string? PrimaryEmail { get; set; }
        public string? SecondaryEmail { get; set; }

        // Navigation property - should expand to PrimaryEmail and SecondaryEmail
        public string? Email => PrimaryEmail ?? SecondaryEmail;
    }
}
