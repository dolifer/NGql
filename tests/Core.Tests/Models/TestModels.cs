using System;
using System.Collections.Generic;

namespace NGql.Core.Tests.Models;

public class TestModel
{
    public UserData user { get; set; } = null!;
    public MetricsData metrics { get; set; } = null!;
    public string? Name { get; }
    public TestModel? Parent { get; }
}

public class UserData
{
    public ProfileData profile { get; set; } = null!;
    public int age { get; set; }
    public string? name { get; set; }
    public string? email { get; set; }
    public bool isActive { get; set; }
}

public class ProfileData
{
    public string? name { get; set; }
    public string? email { get; set; }
    public int age { get; set; }
}

public class MetricsData
{
    public RealtimeData realtime { get; set; } = null!;
    public OnceADayData onceADay { get; set; } = null!;
}

public class RealtimeData
{
    public DepositsData deposits { get; set; } = null!;
}

public class DepositsData
{
    public decimal firstDepositAmount { get; set; }
}

public class OnceADayData
{
    public SportData sport { get; set; } = null!;
}

public class SportData
{
    public List<PreferenceData> preferences { get; set; } = [];
}

public class PreferenceData
{
    public string? sport { get; set; }
    public int totalBetsCount { get; set; }
}

public class ComplexModel
{
    public string? Name { get; }
    public NavigationProperty? Navigation { get; }
    public string? RegularName { get; set; }
    public string? Title { get; set; }
    public int? Count { get; set; }
}

public class NavigationProperty
{
}

public class TestDataObject
{
    public object Value { get; set; } = null!;
}

public class TestContainer
{
    public object? Value { get; set; }
    public string? Name { get; set; }
    public TestContainer? Self { get; set; }
}

public interface ITestInterface
{
    string? Value { get; set; }
}

public class TestInterface : ITestInterface
{
    public string? Value { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is TestInterface ti && ti.Value == Value;
    }

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}

public struct CustomValueType : IEquatable<CustomValueType>
{
    public int Value { get; set; }

    public bool Equals(CustomValueType other) => Value == other.Value;
    
    public override bool Equals(object? obj) => obj is CustomValueType other && Equals(other);
    
    public override int GetHashCode() => Value.GetHashCode();
}
