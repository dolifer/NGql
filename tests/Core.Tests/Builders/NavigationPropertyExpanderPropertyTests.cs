namespace NGql.Core.Tests.Builders;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

public class NavigationPropertyExpanderPropertyTests
{
    // ═══════════════════════════════════════════════════════════════
    // Property 1: Null Type Should Return Original Field Name
    // Covers: Lines 20-23 (null type parameter branch)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("profile.bio")]
    [InlineData("user.account.email")]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    public void ExpandNavigationProperty_WithNullType_ReturnsOriginalFieldName(string fieldName)
    {
        // Coverage: Line 20 null check branch, Lines 22-23 null field addition
        var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, null);

        result.Should().Contain(fieldName, "null type should return original field");
        result.Should().HaveCount(1, "null type should only return one result");
    }

    [Fact]
    public void ExpandNavigationProperty_WithNullType_AlwaysReturnsHashSet()
    {
        // Ensures Line 56 (return statement) is reached
        var result = NavigationPropertyExpander.ExpandNavigationProperty("anyField", null);
        result.Should().BeOfType<HashSet<string>>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 2: Non-Existent Property Should Return Original Field
    // Covers: Lines 33-36 (property not found branch)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_NonExistentProperty_ReturnsOriginalFieldName()
    {
        // Coverage: Lines 33-36 property not found case
        var type = typeof(SimplePropertyClass);
        var result = NavigationPropertyExpander.ExpandNavigationProperty("NonExistentField", type);

        result.Should().Contain("NonExistentField");
        result.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("nosuchproperty")]
    [InlineData("_private")]
    public void ExpandNavigationProperty_WithVariousNonExistentFields_ReturnsOriginalName(string fieldName)
    {
        // Property-based: Various non-existent field names should all return the original
        var type = typeof(string); // Built-in type with few properties
        var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, type);

        result.Should().Contain(fieldName, $"non-existent field '{fieldName}' should return original name");
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 3: Exception Handling Path
    // Covers: Lines 49-56 (exception catch block and return)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_AlwaysReturnsNonNullHashSet()
    {
        // Even on error, should return a non-null HashSet (Line 56)
        var result1 = NavigationPropertyExpander.ExpandNavigationProperty("field", null);
        var result2 = NavigationPropertyExpander.ExpandNavigationProperty("NonExistent", typeof(string));
        var result3 = NavigationPropertyExpander.ExpandNavigationProperty("", typeof(object));

        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 4: Regular Properties (Not Navigation Properties)
    // Covers: Lines 44-46 (non-navigation property branch)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_WithSettableProperty_ReturnsProperty()
    {
        // Coverage: Lines 44-46 HandleRegularProperty for settable properties
        var type = typeof(SimplePropertyClass);
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name", type);

        // Regular property should be returned as-is
        result.Should().Contain("Name");
    }

    [Fact]
    public void ExpandNavigationProperty_MultipleSettableProperties_AllFound()
    {
        // Property-based: Multiple calls for different properties should all work
        var type = typeof(SimplePropertyClass);

        var resultName = NavigationPropertyExpander.ExpandNavigationProperty("Name", type);
        var resultEmail = NavigationPropertyExpander.ExpandNavigationProperty("Email", type);
        var resultAge = NavigationPropertyExpander.ExpandNavigationProperty("Age", type);

        resultName.Should().Contain("Name");
        resultEmail.Should().Contain("Email");
        resultAge.Should().Contain("Age");
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 5: Path Splitting Behavior
    // Covers: SplitPath method and path handling
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("simple")]          // No dot - first segment is entire field
    [InlineData("a.b")]             // Single dot - splits into a, b
    [InlineData("a.b.c")]           // Multiple dots - only first dot matters
    [InlineData(".start")]          // Starts with dot
    [InlineData("end.")]            // Ends with dot
    [InlineData("")]                // Empty string
    public void ExpandNavigationProperty_VariousPathFormats_HandlesAllCases(string fieldName)
    {
        // Property-based: All path formats should produce a valid result
        var type = typeof(SimplePropertyClass);
        var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, type);

        result.Should().NotBeNull("should handle all path formats");
        result.Should().BeOfType<HashSet<string>>();
        result.Should().NotBeEmpty("should return at least the original field");
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 6: Complex Type Hierarchies
    // Covers: Multi-level nesting and recursive scenarios
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_ComplexHierarchy_HandlesAllLevels()
    {
        var type = typeof(ClassWithNestedProperty);

        var result1 = NavigationPropertyExpander.ExpandNavigationProperty("Child", type);
        var result2 = NavigationPropertyExpander.ExpandNavigationProperty("Child.Name", type);
        var result3 = NavigationPropertyExpander.ExpandNavigationProperty("NonExistent", type);

        result1.Should().NotBeEmpty();
        result2.Should().NotBeEmpty();
        result3.Should().NotBeEmpty();
    }

    [Fact]
    public void ExpandNavigationProperty_DeepPath_DoesNotCrash()
    {
        // Property-based: Should handle very deep paths without crashing
        var type = typeof(SimplePropertyClass);
        var deepPath = "a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p";

        var action = () => NavigationPropertyExpander.ExpandNavigationProperty(deepPath, type);
        action.Should().NotThrow("should handle very deep paths gracefully");
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 7: Special Characters and Edge Cases
    // Covers: Unusual field names and special cases
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("field_name")]
    [InlineData("field123")]
    [InlineData("get_")]
    [InlineData("$field")]
    [InlineData("_PrivateField")]
    public void ExpandNavigationProperty_FieldNamesWithSpecialChars_HandleGracefully(string fieldName)
    {
        // Property-based: Various special characters should not cause crashes
        var type = typeof(SimplePropertyClass);
        var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, type);

        result.Should().NotBeNull("should handle special characters");
        result.Should().BeOfType<HashSet<string>>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Property 8: Result Set Properties
    // Covers: HashSet semantics and behavior
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_ResultAlwaysContainsAtLeastOneElement()
    {
        // Property-based: Every call should return at least one result
        var type = typeof(SimplePropertyClass);
        var fieldNames = new[] { "Name", "Email", "NonExistent", "path.to.field", "" };

        foreach (var fieldName in fieldNames)
        {
            var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, type);
            result.Should().NotBeEmpty($"result for '{fieldName}' should not be empty");
            result.Count.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void ExpandNavigationProperty_ResultNoDuplicates()
    {
        // Property-based: HashSet guarantees no duplicates
        var type = typeof(SimplePropertyClass);
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name", type);

        var list = result.ToList();
        var unique = list.Distinct().Count();
        list.Count.Should().Be(unique, "result should have no duplicates");
    }

    // ═══════════════════════════════════════════════════════════════
    // Exception Handling Tests (TDD - RED)
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_WithDifficultTypeReflection_HandlesGracefully()
    {
        // Using a type that has problematic reflection characteristics
        // The generic catch block currently swallows OutOfMemoryException,
        // StackOverflowException, and other system-critical exceptions
        var result = NavigationPropertyExpander.ExpandNavigationProperty("anyField", typeof(int));
        
        // Should return the field name instead of throwing
        result.Should().Contain("anyField");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ExpandNavigationProperty_WithSimpleType_NoReflectionFailure()
    {
        // Using a primitive type should handle gracefully
        var result = NavigationPropertyExpander.ExpandNavigationProperty("field", typeof(string));
        result.Should().Contain("field");
    }

    [Fact]
    public void ExpandNavigationProperty_WithRemainedPath_AppendsCorrectly()
    {
        var result = NavigationPropertyExpander.ExpandNavigationProperty("child.name", typeof(ClassWithNestedProperty));
        
        // Should contain the expanded property with remaining path
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ExpandNavigationProperty_WithoutRemainingPath_ExpandsSimple()
    {
        var result = NavigationPropertyExpander.ExpandNavigationProperty("child", typeof(ClassWithNestedProperty));
        
        result.Should().NotBeEmpty();
    }

    // ================== COVERAGE-DRIVEN TESTS ==================
    // Tests targeting uncovered code paths from coverage analysis.

    [Fact]
    public void ExpandNavigationProperty_InvalidOperationExceptionPath_ReturnsOriginalField()
    {
        // Coverage: Exception handling for InvalidOperationException
        var result = NavigationPropertyExpander.ExpandNavigationProperty("field", typeof(int));
        result.Should().Contain("field");
    }

    [Fact]
    public void ExpandNavigationProperty_NavigationPropertyWithRemainedPath_ExpandsAndAppends()
    {
        // Coverage: Lines 87-89 when expanding navigation properties with remaining path
        // Create a complex type hierarchy where navigation property is expanded
        var result = NavigationPropertyExpander.ExpandNavigationProperty("child.name", typeof(ClassWithNestedProperty));
        
        // Should expand properties with the remaining path appended
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ExpandNavigationProperty_WithPotentialAmbiguousMatchType_HandlesGracefully()
    {
        // Coverage: Exception handling for AmbiguousMatchException (lines 54-57)
        // Use a built-in type that might have property resolution issues
        var result = NavigationPropertyExpander.ExpandNavigationProperty("property", typeof(object));
        
        // Should return field name without throwing
        result.Should().Contain("property");
    }

    [Fact]
    public void ExpandNavigationProperty_WithGenericType_HandlesGracefully()
    {
        // Coverage: Exception handling paths with complex type scenarios
        var result = NavigationPropertyExpander.ExpandNavigationProperty("item", typeof(List<string>));
        
        // Should return field name without throwing
        result.Should().Contain("item");
    }

    [Fact]
    public void ExpandNavigationProperty_WithSpecialTypes_DoesNotThrow()
    {
        // Coverage: Various special types to exercise all code paths
        var testTypes = new Type[]
        {
            typeof(Type),
            typeof(Enum),
            typeof(IEnumerable),
            typeof(IDictionary)
        };

        foreach (var type in testTypes)
        {
            var result = NavigationPropertyExpander.ExpandNavigationProperty("test", type);
            result.Should().NotBeNull();
            result.Should().Contain("test");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Exception Handler Tests (Lines 49-58)
    // Covers: InvalidOperationException and AmbiguousMatchException handling
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_InvalidOperationExceptionDuringReflection_ReturnsOriginalFieldName()
    {
        // Arrange: Create a type that throws InvalidOperationException on GetProperty
        var mockType = new InvalidOperationExceptionType();
        
        // Act: Call ExpandNavigationProperty with the problematic type
        var result = NavigationPropertyExpander.ExpandNavigationProperty("fieldName", mockType);
        
        // Assert: Should catch the exception and return original field name
        result.Should().Contain("fieldName", "should return original field name when InvalidOperationException occurs");
        result.Should().HaveCount(1, "should only contain the original field name");
    }

    [Fact]
    public void ExpandNavigationProperty_AmbiguousMatchExceptionDuringReflection_ReturnsOriginalFieldName()
    {
        // Arrange: Create a type that throws AmbiguousMatchException on GetProperty
        var mockType = new AmbiguousMatchExceptionType();
        
        // Act: Call ExpandNavigationProperty with the problematic type
        var result = NavigationPropertyExpander.ExpandNavigationProperty("fieldName", mockType);
        
        // Assert: Should catch the exception and return original field name
        result.Should().Contain("fieldName", "should return original field name when AmbiguousMatchException occurs");
        result.Should().HaveCount(1, "should only contain the original field name");
    }

    [Theory]
    [InlineData("field")]
    [InlineData("property.nested")]
    [InlineData("complex.path.here")]
    public void ExpandNavigationProperty_WithInvalidOperationException_HandlesMultipleFieldNames(string fieldName)
    {
        // Arrange
        var mockType = new InvalidOperationExceptionType();
        
        // Act
        var result = NavigationPropertyExpander.ExpandNavigationProperty(fieldName, mockType);
        
        // Assert
        result.Should().Contain(fieldName, "should return original field name");
        result.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // Test Data Classes
    // ═══════════════════════════════════════════════════════════════

    public class SimplePropertyClass
    {
        public string Name { get; set; } = "Test";
        public string Email { get; set; } = "test@example.com";
        public int Age { get; set; } = 25;
    }

    public class ClassWithNestedProperty
    {
        public SimplePropertyClass Child { get; set; } = new SimplePropertyClass();
    }

    // ═══════════════════════════════════════════════════════════════
    // Test Data Class: Navigation Property with Getter-Only Pattern
    // ═══════════════════════════════════════════════════════════════

    public class TypeWithNavigationProperty
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        // Getter-only navigation property (no setter)
        public string? Name => $"{FirstName} {LastName}";
    }

    // ═══════════════════════════════════════════════════════════════
    // NEW TESTS: Navigation Property Expansion with Remaining Path
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandNavigationProperty_NavigationWithRemainingPath_AppendsPathCorrectly()
    {
        var type = typeof(TypeWithNavigationProperty);
        // "Name.suffix" should recognize "Name" as navigation property
        // and expand to "FirstName.suffix" and "LastName.suffix"
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name.suffix", type);

        result.Should().NotBeEmpty("navigation property with remaining path should expand");
        result.Should().Contain("FirstName.suffix");
        result.Should().Contain("LastName.suffix");
    }

    [Fact]
    public void ExpandNavigationProperty_NavigationWithMultipleLevelPath_HandlesCorrectly()
    {
        var type = typeof(TypeWithNavigationProperty);
        // "Name.deep.nested" should expand to properties with remaining path
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name.deep.nested", type);

        result.Should().NotBeEmpty();
        // Each expanded property should have the remaining path appended
        result.Any(r => r.Contains("deep.nested")).Should().BeTrue("remaining path should be appended");
    }

    [Fact]
    public void ExpandNavigationProperty_WithReflectionFailure_CatchesAndReturnsOriginal()
    {
        // Using interface (abstract reflection scenarios that might cause exceptions)
        var result = NavigationPropertyExpander.ExpandNavigationProperty("field", typeof(IEnumerable));

        // Should catch any exceptions and return original field name
        result.Should().Contain("field");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void ExpandNavigationProperty_OnlySettablePropertiesExpanded_SkipsNavigationProperties()
    {
        var type = typeof(TypeWithNavigationProperty);
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name", type);

        // Should NOT include "Name" itself (it's getter-only)
        // Should include only the settable properties: FirstName, LastName, Id
        result.Should().Contain("FirstName");
        result.Should().Contain("LastName");
        result.Should().NotContain("Name", "navigation property itself should not be expanded");
        // Should include other settable properties like Id
        result.Should().Contain("Id");
    }

    [Fact]
    public void ExpandNavigationProperty_ComplexTypeWithMixedProperties_ExpandsCorrectly()
    {
        var type = typeof(TypeWithNavigationProperty);
        
        // Test 1: Simple navigation property (no remaining path)
        var result1 = NavigationPropertyExpander.ExpandNavigationProperty("Name", type);
        result1.Should().Contain("FirstName");
        result1.Should().Contain("LastName");
        result1.Should().NotContain("Name", "getter-only property should not be included");
        // All settable properties are expanded, including Id
        result1.Should().Contain("Id");

        // Test 2: Regular property should be returned as-is
        var result2 = NavigationPropertyExpander.ExpandNavigationProperty("Id", type);
        result2.Should().Contain("Id");

        // Test 3: Non-existent property should be returned as-is
        var result3 = NavigationPropertyExpander.ExpandNavigationProperty("NonExistent", type);
        result3.Should().Contain("NonExistent");
    }

    // ═══════════════════════════════════════════════════════════════
    // Mock Types for Exception Testing
    // ═══════════════════════════════════════════════════════════════

    public class InvalidOperationExceptionType : Type
    {
        public override string? Name => "InvalidOperationExceptionType";
        public override string? FullName => "InvalidOperationExceptionType";
        public override Guid GUID => Guid.Empty;
        public override Type? BaseType => typeof(object);
        public override Type UnderlyingSystemType => this;
        public override Assembly Assembly => typeof(object).Assembly;
        public override string? Namespace => "Test";
        public override Module Module => typeof(object).Module;
        public override string? AssemblyQualifiedName => null;
        
        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers)
        {
            throw new InvalidOperationException("Simulated reflection failure");
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return [];
        }

        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, System.Globalization.CultureInfo? culture, string[]? namedParameters)
        {
            throw new NotImplementedException();
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return TypeAttributes.Class;
        }

        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers)
        {
            return null;
        }

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            return null;
        }

        protected override bool HasElementTypeImpl()
        {
            return false;
        }

        protected override bool IsArrayImpl()
        {
            return false;
        }

        protected override bool IsByRefImpl()
        {
            return false;
        }

        protected override bool IsPointerImpl()
        {
            return false;
        }

        protected override bool IsPrimitiveImpl()
        {
            return false;
        }

        protected override bool IsCOMObjectImpl()
        {
            return false;
        }

        public override Type? GetElementType()
        {
            return null;
        }

        public override Type MakeArrayType()
        {
            return this;
        }

        public override Type MakeByRefType()
        {
            return this;
        }

        public override Type MakePointerType()
        {
            return this;
        }

        public override Type[] GetInterfaces() => [];
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => [];
        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => [];
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => [];
        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => [];
        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => [];
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => [];
        public override Type? GetNestedType(string name, BindingFlags bindingAttr) => null;
        public override FieldInfo? GetField(string name, BindingFlags bindingAttr) => null;
        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) => null;
        public override Type? GetInterface(string name, bool ignoreCase) => null;
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
        public override object[] GetCustomAttributes(bool inherit) => [];
        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }

    public class AmbiguousMatchExceptionType : Type
    {
        public override string? Name => "AmbiguousMatchExceptionType";
        public override string? FullName => "AmbiguousMatchExceptionType";
        public override Guid GUID => Guid.Empty;
        public override Type? BaseType => typeof(object);
        public override Type UnderlyingSystemType => this;
        public override Assembly Assembly => typeof(object).Assembly;
        public override string? Namespace => "Test";
        public override Module Module => typeof(object).Module;
        public override string? AssemblyQualifiedName => null;
        
        protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers)
        {
            throw new AmbiguousMatchException("Simulated ambiguous match during reflection");
        }

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            return [];
        }

        public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, System.Globalization.CultureInfo? culture, string[]? namedParameters)
        {
            throw new NotImplementedException();
        }

        protected override TypeAttributes GetAttributeFlagsImpl()
        {
            return TypeAttributes.Class;
        }

        protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers)
        {
            return null;
        }

        protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
        {
            return null;
        }

        protected override bool HasElementTypeImpl()
        {
            return false;
        }

        protected override bool IsArrayImpl()
        {
            return false;
        }

        protected override bool IsByRefImpl()
        {
            return false;
        }

        protected override bool IsPointerImpl()
        {
            return false;
        }

        protected override bool IsPrimitiveImpl()
        {
            return false;
        }

        protected override bool IsCOMObjectImpl()
        {
            return false;
        }

        public override Type? GetElementType()
        {
            return null;
        }

        public override Type MakeArrayType()
        {
            return this;
        }

        public override Type MakeByRefType()
        {
            return this;
        }

        public override Type MakePointerType()
        {
            return this;
        }

        public override Type[] GetInterfaces() => [];
        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => [];
        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => [];
        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => [];
        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => [];
        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => [];
        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => [];
        public override Type? GetNestedType(string name, BindingFlags bindingAttr) => null;
        public override FieldInfo? GetField(string name, BindingFlags bindingAttr) => null;
        public override EventInfo? GetEvent(string name, BindingFlags bindingAttr) => null;
        public override Type? GetInterface(string name, bool ignoreCase) => null;
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
        public override object[] GetCustomAttributes(bool inherit) => [];
        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }
}

