using System;

namespace NGql.Core.Tests;

public static class TestDataModels
{
    public class SettingGroupA
    {
        public string? SettingA { get; set; }
        public string? Field => SettingA;
    }

    public class SettingGroupB
    {
        public string? SettingB { get; set; }
        public string? Field => SettingB;
    }

    public class SettingGroupC
    {
        public string? SettingC { get; set; }
        public string? Field => SettingC;
    }

    public class SimpleUser
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? Status { get; set; }
    }

    public class UserWithProfile
    {
        public string? UserId { get; set; }
        public UserProfile? Profile { get; set; }
        public UserSettings? Settings { get; set; }
    }

    public class UserProfile
    {
        public string? Name { get; set; }
        public string? Bio { get; set; }
        public string? Avatar { get; set; }
        public UserAddress? Address { get; set; }
    }

    public class UserAddress
    {
        public string? City { get; set; }
        public string? Street { get; set; }
    }

    public class UserSettings
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
    }

    public class DepositInfo
    {
        public DateTime? FirstDepositTime { get; set; }
        public DateTime? SecondDepositTime { get; set; }

        // Navigation property - should expand to FirstDepositTime and SecondDepositTime
        public DateTime? Date => FirstDepositTime ?? SecondDepositTime;
    }

    public class UserWithAddress
    {
        public string? UserId { get; set; }
        public UserProfileWithAddress? Profile { get; set; }
    }

    public class UserProfileWithAddress
    {
        public string? Name { get; set; }
        public UserAddress? Address { get; set; }
    }

    public class UserWithArrays
    {
        public string? UserId { get; set; }
        public string[]? Tags { get; set; }
        public int[]? Scores { get; set; }
    }

    public class Product
    {
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal? Price { get; set; }
    }

    public class UserWithComputedProfile
    {
        public string? UserId { get; set; }
        public ComputedProfile? Profile { get; set; }
    }

    public class ComputedProfile
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public ComputedContact? Contact { get; set; }

        // Navigation property - should expand to FirstName and LastName
        public string? Name => $"{FirstName} {LastName}";
    }

    public class ComputedContact
    {
        public string? PrimaryEmail { get; set; }
        public string? SecondaryEmail { get; set; }

        // Navigation property - should expand to PrimaryEmail and SecondaryEmail
        public string? Email => PrimaryEmail ?? SecondaryEmail;
    }
}
