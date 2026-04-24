namespace NGql.Core.Tests.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Tests.Models;
using Xunit;

public class HelpersComprehensiveTests
{
    // ═══════════════════════════════════════════════════════════════
    // ExtractVariablesFromValue - Variable extraction and recursion
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("null-value")]
    [InlineData("direct-variable")]
    [InlineData("variable-in-dict")]
    [InlineData("variable-in-list")]
    [InlineData("nested-dicts")]
    [InlineData("object-properties")]
    [InlineData("cyclic-refs")]
    public void ExtractVariables_BasicCases(string scenario)
    {
        var scenarios = new TestScenarioBag<SortedSet<Variable>>()
            .Register("null-value",
                arrange: () =>
                {
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(null, variables);
                    return variables;
                },
                assert: result => result.Should().BeEmpty())
            .Register("direct-variable",
                arrange: () =>
                {
                    var variable = new Variable("$id", "Int!");
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(variable, variables);
                    return variables;
                },
                assert: result => result.Should().ContainSingle().Which.Name.Should().Be("$id"))
            .Register("variable-in-dict",
                arrange: () =>
                {
                    var variable = new Variable("$userId", "Int!");
                    var dict = new Dictionary<string, object?> { { "id", variable } };
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(dict, variables);
                    return variables;
                },
                assert: result => result.Should().ContainSingle().Which.Name.Should().Be("$userId"))
            .Register("variable-in-list",
                arrange: () =>
                {
                    var variable = new Variable("$filter", "String!");
                    var list = new List<object?> { "status", variable, "active" };
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(list, variables);
                    return variables;
                },
                assert: result => result.Should().ContainSingle().Which.Name.Should().Be("$filter"))
            .Register("nested-dicts",
                arrange: () =>
                {
                    var var1 = new Variable("$id", "Int!");
                    var var2 = new Variable("$name", "String!");
                    var nested = new Dictionary<string, object?> 
                    { 
                        { "filter", new Dictionary<string, object?> { { "user", var1 }, { "search", var2 } } }
                    };
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(nested, variables);
                    return variables;
                },
                assert: result => result.Should().HaveCount(2))
            .Register("object-properties",
                arrange: () =>
                {
                    var variable = new Variable("$status", "String!");
                    var obj = new TestDataObject { Value = variable };
                    var variables = new SortedSet<Variable>();
                    Helpers.ExtractVariablesFromValue(obj, variables);
                    return variables;
                },
                assert: result => result.Should().ContainSingle())
            .Register("cyclic-refs",
                arrange: () =>
                {
                    var dict = new Dictionary<string, object?>();
                    dict["self"] = dict;
                    var variables = new SortedSet<Variable>();
                    Action act = () => Helpers.ExtractVariablesFromValue(dict, variables);
                    act.Should().NotThrow();
                    return variables;
                },
                assert: result => result.Should().NotBeNull());

        var testScenario = scenarios.Get(scenario);
        var result = testScenario.Arrange();
        testScenario.Assert(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeNullableDictionaries - Dictionary merging with recursion
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("empty-existing")]
    [InlineData("no-overlap")]
    [InlineData("with-overlap")]
    [InlineData("nested-dicts")]
    [InlineData("null-values")]
    public void MergeNullableDictionaries_Variations(string scenario)
    {
        var scenarios = new TestScenarioBag<SortedDictionary<string, object?>>()
            .Register("empty-existing",
                arrange: () =>
                {
                    var existing = new Dictionary<string, object?>();
                    var update = new Dictionary<string, object?> { { "key", "value" } };
                    return Helpers.MergeNullableDictionaries(existing, update);
                },
                assert: result =>
                {
                    result.Should().HaveCount(1);
                    result["key"].Should().Be("value");
                })
            .Register("no-overlap",
                arrange: () =>
                {
                    var existing = new Dictionary<string, object?> { { "a", 1 } };
                    var update = new Dictionary<string, object?> { { "b", 2 } };
                    return Helpers.MergeNullableDictionaries(existing, update);
                },
                assert: result =>
                {
                    result.Should().HaveCount(2);
                    result["a"].Should().Be(1);
                    result["b"].Should().Be(2);
                })
            .Register("with-overlap",
                arrange: () =>
                {
                    var existing = new Dictionary<string, object?> { { "key", "old" } };
                    var update = new Dictionary<string, object?> { { "key", "new" } };
                    return Helpers.MergeNullableDictionaries(existing, update);
                },
                assert: result => result["key"].Should().Be("new"))
            .Register("nested-dicts",
                arrange: () =>
                {
                    var existing = new Dictionary<string, object?> 
                    { 
                        { "nested", new Dictionary<string, object?> { { "a", 1 } } }
                    };
                    var update = new Dictionary<string, object?> 
                    { 
                        { "nested", new Dictionary<string, object?> { { "b", 2 } } }
                    };
                    return Helpers.MergeNullableDictionaries(existing, update);
                },
                assert: result =>
                {
                    result.Should().ContainKey("nested");
                    var nested = result["nested"];
                    nested.Should().NotBeNull();
                    if (nested is System.Collections.IDictionary nestedDict)
                    {
                        nestedDict.Count.Should().Be(2);
                    }
                })
            .Register("null-values",
                arrange: () =>
                {
                    var existing = new Dictionary<string, object?> { { "key", null } };
                    var update = new Dictionary<string, object?> { { "other", "value" } };
                    return Helpers.MergeNullableDictionaries(existing, update);
                },
                assert: result =>
                {
                    result.Should().ContainKey("key");
                    result["key"].Should().BeNull();
                });

        var testScenario = scenarios.Get(scenario);
        var result = testScenario.Arrange();
        testScenario.Assert(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeMetadata - Metadata-specific merging with fast paths
    // ═══════════════════════════════════════════════════════════════

    // These tests are now consolidated into Theory tests below

    // ═══════════════════════════════════════════════════════════════
    // SortArgumentValue Theory Tests - Consolidated from 22 Fact tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Null")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Boolean")]
    [InlineData("Float")]
    [InlineData("EmptyDict")]
    [InlineData("DictWithNullValue")]
    public void SortArgumentValue_PrimitiveValues(string testCase)
    {
        var scenarios = new TestScenarioBag<object?>()
            .Register("Null",
                arrange: () => Helpers.SortArgumentValue(null),
                assert: (result) => result.Should().BeNull())
            .Register("Integer",
                arrange: () => Helpers.SortArgumentValue(123),
                assert: (result) => result.Should().Be(123))
            .Register("String",
                arrange: () => Helpers.SortArgumentValue("test"),
                assert: (result) => result.Should().Be("test"))
            .Register("Boolean",
                arrange: () => Helpers.SortArgumentValue(true),
                assert: (result) => result.Should().Be(true))
            .Register("Float",
                arrange: () => Helpers.SortArgumentValue(3.14159),
                assert: (result) => result.Should().Be(3.14159))
            .Register("EmptyDict",
                arrange: () => Helpers.SortArgumentValue(new Dictionary<string, object?>()),
                assert: (result) =>
                {
                    result.Should().BeOfType<SortedDictionary<string, object?>>();
                    ((SortedDictionary<string, object?>)result).Should().BeEmpty();
                })
            .Register("DictWithNullValue",
                arrange: () => Helpers.SortArgumentValue(new Dictionary<string, object?> { { "key", null } }),
                assert: (result) =>
                {
                    result.Should().BeOfType<SortedDictionary<string, object?>>();
                    ((SortedDictionary<string, object?>)result).Should().ContainKey("key");
                    ((SortedDictionary<string, object?>)result)["key"].Should().BeNull();
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    [Theory]
    [InlineData("UnsortedDict")]
    [InlineData("SingleKeyDict")]
    [InlineData("KeyValueDict")]
    public void SortArgumentValue_Dictionaries(string testCase)
    {
        var scenarios = new TestScenarioBag<object?>()
            .Register("UnsortedDict",
                arrange: () => Helpers.SortArgumentValue(
                    new Dictionary<string, object?> { { "z", 1 }, { "a", 2 }, { "m", 3 } }),
                assert: (result) =>
                {
                    result.Should().BeOfType<SortedDictionary<string, object?>>();
                    var sorted = (SortedDictionary<string, object?>)result;
                    sorted.Keys.Should().Equal("a", "m", "z");
                })
            .Register("SingleKeyDict",
                arrange: () => Helpers.SortArgumentValue(
                    new Dictionary<string, object?> { { "key", "value" } }),
                assert: (result) => result.Should().BeOfType<SortedDictionary<string, object?>>())
            .Register("KeyValueDict",
                arrange: () => Helpers.SortArgumentValue(
                    new Dictionary<string, object?> { { "key", "value" } }),
                assert: (result) => result.Should().BeOfType<SortedDictionary<string, object?>>());

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    [Theory]
    [InlineData("ListOfPrimitives")]
    [InlineData("ArrayOfPrimitives")]
    [InlineData("ListWithNestedObjects")]
    [InlineData("EmptyList")]
    [InlineData("EmptyArray")]
    public void SortArgumentValue_ListsAndArrays(string testCase)
    {
        var scenarios = new TestScenarioBag<object?>()
            .Register("ListOfPrimitives",
                arrange: () => Helpers.SortArgumentValue(new List<object> { 1, 2, 3 }),
                assert: (result) =>
                {
                    result.Should().BeOfType<List<object>>();
                    ((List<object>)result).Should().Equal(1, 2, 3);
                })
            .Register("ArrayOfPrimitives",
                arrange: () => Helpers.SortArgumentValue(new object[] { 1, 2, 3 }),
                assert: (result) =>
                {
                    result.Should().BeOfType<object[]>();
                    ((object[])result).Should().HaveCount(3);
                })
            .Register("ListWithNestedObjects",
                arrange: () => Helpers.SortArgumentValue(
                    new List<object> { 1, new Dictionary<string, object?> { { "key", "value" } } }),
                assert: (result) =>
                {
                    result.Should().BeOfType<List<object>>();
                    var list = (List<object>)result;
                    list.Should().HaveCount(2);
                    list[1].Should().BeOfType<SortedDictionary<string, object?>>();
                })
            .Register("EmptyList",
                arrange: () => Helpers.SortArgumentValue(new List<object>()),
                assert: (result) =>
                {
                    result.Should().BeOfType<List<object>>();
                    ((List<object>)result).Should().BeEmpty();
                })
            .Register("EmptyArray",
                arrange: () => Helpers.SortArgumentValue(new object[] { }),
                assert: (result) =>
                {
                    result.Should().BeOfType<object[]>();
                    ((object[])result).Should().BeEmpty();
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    [Theory]
    [InlineData("NestedDictionaries")]
    [InlineData("CustomObject")]
    [InlineData("ComplexNestedStructure")]
    [InlineData("NestedListsWithMixedTypes")]
    public void SortArgumentValue_ComplexNested(string testCase)
    {
        var scenarios = new TestScenarioBag<object?>()
            .Register("NestedDictionaries",
                arrange: () => Helpers.SortArgumentValue(
                    new Dictionary<string, object?>
                    {
                        { "z", new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } } },
                        { "a", new List<object> { 3, 1, 2 } }
                    }),
                assert: (result) => result.Should().BeOfType<SortedDictionary<string, object?>>())
            .Register("CustomObject",
                arrange: () => Helpers.SortArgumentValue(
                    new { zField = "z", aField = "a", mField = "m" }),
                assert: (result) =>
                {
                    result.Should().BeOfType<SortedDictionary<string, object?>>();
                    var sorted = (SortedDictionary<string, object?>)result;
                    sorted.Keys.ToList().Should().Equal("aField", "mField", "zField");
                })
            .Register("ComplexNestedStructure",
                arrange: () => Helpers.SortArgumentValue(
                    new Dictionary<string, object?>
                    {
                        { "z", 26 },
                        { "a", 1 },
                        { "m", new Dictionary<string, object?> { { "z", 2 }, { "b", 1 } } }
                    }),
                assert: (result) =>
                {
                    var dict = result as SortedDictionary<string, object?>;
                    dict.Keys.Should().Equal("a", "m", "z");
                    var nested = dict["m"] as SortedDictionary<string, object?>;
                    nested.Keys.Should().Equal("b", "z");
                })
            .Register("NestedListsWithMixedTypes",
                arrange: () => Helpers.SortArgumentValue(
                    new List<object>
                    {
                        new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } },
                        new Dictionary<string, object?> { { "y", 3 }, { "b", 4 } }
                    }),
                assert: (result) =>
                {
                    var resultList = result as List<object>;
                    var dict1 = resultList[0] as SortedDictionary<string, object?>;
                    dict1.Keys.Should().Equal("a", "z");
                    var dict2 = resultList[1] as SortedDictionary<string, object?>;
                    dict2.Keys.Should().Equal("b", "y");
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeNullableMetadata Theory Tests - Consolidated from 11+ Fact tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BothNull")]
    [InlineData("EmptyUpdateDict")]
    [InlineData("NullExistingWithUpdate")]
    [InlineData("EmptyExistingWithUpdate")]
    [InlineData("OverrideNonDictValue")]
    [InlineData("MixedDictAndNonDictValues")]
    public void MergeNullableMetadata_NullAndEmptyHandling(string testCase)
    {
        var scenarios = new TestScenarioBag<Dictionary<string, object?>>()
            .Register("BothNull",
                arrange: () => Helpers.MergeNullableMetadata(null, null),
                assert: (result) => result.Should().BeEmpty())
            .Register("EmptyUpdateDict",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key1", "value1" } },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)),
                assert: (result) =>
                {
                    result.Should().ContainKey("key1");
                    result["key1"].Should().Be("value1");
                })
            .Register("NullExistingWithUpdate",
                arrange: () => Helpers.MergeNullableMetadata(
                    null,
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key2", "value2" } }),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["key2"].Should().Be("value2");
                })
            .Register("EmptyExistingWithUpdate",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key2", "value2" } }),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["key2"].Should().Be("value2");
                })
            .Register("OverrideNonDictValue",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "oldValue" } },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "newValue" } }),
                assert: (result) => result["key"].Should().Be("newValue"))
            .Register("MixedDictAndNonDictValues",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "key", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "plain" } }),
                assert: (result) => result["key"].Should().Be("plain"));

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    [Theory]
    [InlineData("NestedDictMerge")]
    [InlineData("NestedNullValuesPreserved")]
    [InlineData("DeeplyNestedDictionaries")]
    [InlineData("MultipleKeysWithMixedUpdates")]
    [InlineData("CaseInsensitiveKeys")]
    public void MergeNullableMetadata_NestedAndComplex(string testCase)
    {
        var scenarios = new TestScenarioBag<Dictionary<string, object?>>()
            .Register("NestedDictMerge",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "b", 2 } } }
                    }),
                assert: (result) =>
                {
                    result.Should().ContainKey("nested");
                    var nested = result["nested"] as Dictionary<string, object?>;
                    nested.Should().NotBeNull();
                    nested.Should().HaveCount(2);
                    nested["a"].Should().Be(1);
                    nested["b"].Should().Be(2);
                })
            .Register("NestedNullValuesPreserved",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "nullKey", null } } }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "otherKey", "value" } } }
                    }),
                assert: (result) =>
                {
                    var nestedDict = result["nested"] as Dictionary<string, object?>;
                    nestedDict.Should().ContainKey("nullKey");
                    nestedDict["nullKey"].Should().BeNull();
                    nestedDict.Should().ContainKey("otherKey");
                })
            .Register("DeeplyNestedDictionaries",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        {
                            "level1", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                            {
                                {
                                    "level2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        { "a", 1 }
                                    }
                                }
                            }
                        }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        {
                            "level1", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                            {
                                {
                                    "level2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                                    {
                                        { "b", 2 }
                                    }
                                }
                            }
                        }
                    }),
                assert: (result) =>
                {
                    result.Should().ContainKey("level1");
                    var level1 = result["level1"] as Dictionary<string, object?>;
                    level1.Should().ContainKey("level2");
                    var level2 = level1["level2"] as Dictionary<string, object?>;
                    level2.Should().HaveCount(2);
                })
            .Register("MultipleKeysWithMixedUpdates",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "key1", "value1" },
                        { "key2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } },
                        { "key3", null }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "key2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "b", 2 } } },
                        { "key4", "value4" }
                    }),
                assert: (result) =>
                {
                    result.Should().HaveCount(4);
                    result["key1"].Should().Be("value1");
                    result["key3"].Should().BeNull();
                    result["key4"].Should().Be("value4");
                    var key2Dict = result["key2"] as Dictionary<string, object?>;
                    key2Dict.Should().HaveCount(2);
                })
            .Register("CaseInsensitiveKeys",
                arrange: () => Helpers.MergeNullableMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Key", "value1" }
                    },
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "key", "value2" }
                    }),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["KEY"].Should().Be("value2");
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeMetadata Theory Tests - Consolidated from 10 Fact tests
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NoExisting")]
    [InlineData("NoUpdate")]
    [InlineData("BothPresent")]
    [InlineData("NoExistingEmptyUpdate")]
    [InlineData("ExistingEmptyUpdate")]
    public void MergeMetadata_FastPaths(string testCase)
    {
        var scenarios = new TestScenarioBag<Dictionary<string, object?>>()
            .Register("NoExisting",
                arrange: () => Helpers.MergeMetadata(
                    null,
                    new Dictionary<string, object> { { "key", "value" } }),
                assert: (result) => result.Should().HaveCount(1))
            .Register("NoUpdate",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?> { { "key", "value" } },
                    new Dictionary<string, object>()),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["key"].Should().Be("value");
                })
            .Register("BothPresent",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?> { { "a", 1 } },
                    new Dictionary<string, object> { { "b", 2 } }),
                assert: (result) => result.Should().HaveCount(2))
            .Register("NoExistingEmptyUpdate",
                arrange: () => Helpers.MergeMetadata(
                    null,
                    new Dictionary<string, object>()),
                assert: (result) => result.Should().HaveCount(0))
            .Register("ExistingEmptyUpdate",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "value" } },
                    new Dictionary<string, object>()),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["key"].Should().Be("value");
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    [Theory]
    [InlineData("NestedDictionaries")]
    [InlineData("NonDictOverridesDict")]
    [InlineData("DeepNestedDictionaries")]
    [InlineData("ThreeLevelNesting")]
    [InlineData("CaseInsensitiveKeys")]
    public void MergeMetadata_ComplexCases(string testCase)
    {
        var scenarios = new TestScenarioBag<Dictionary<string, object?>>()
            .Register("NestedDictionaries",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
                    },
                    new Dictionary<string, object>
                    {
                        { "nested", new Dictionary<string, object> { { "b", 2 } } }
                    }),
                assert: (result) =>
                {
                    var nestedResult = result["nested"] as Dictionary<string, object?>;
                    nestedResult.Should().NotBeNull();
                    nestedResult.Should().HaveCount(2);
                })
            .Register("NonDictOverridesDict",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "key", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
                    },
                    new Dictionary<string, object> { { "key", "plainValue" } }),
                assert: (result) => result["key"].Should().Be("plainValue"))
            .Register("DeepNestedDictionaries",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>
                    {
                        {"cache", new Dictionary<string, object?> { {"ttl", 60}, {"key", "old"} }},
                        {"other", "value"}
                    },
                    new Dictionary<string, object>
                    {
                        {"cache", new Dictionary<string, object?> { {"key", "new"} }},
                        {"extra", "added"}
                    }),
                assert: (result) =>
                {
                    result.Should().HaveCount(3);
                    result["other"].Should().Be("value");
                    result["extra"].Should().Be("added");
                    var mergedCache = result["cache"] as Dictionary<string, object?>;
                    mergedCache.Should().HaveCount(2);
                    mergedCache["ttl"].Should().Be(60);
                    mergedCache["key"].Should().Be("new");
                })
            .Register("ThreeLevelNesting",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>
                    {
                        {"a", new Dictionary<string, object?> 
                        { 
                            {"b", new Dictionary<string, object?> { {"c", 1} }}
                        }}
                    },
                    new Dictionary<string, object>
                    {
                        {"a", new Dictionary<string, object?> 
                        { 
                            {"b", new Dictionary<string, object?> { {"d", 2} }}
                        }}
                    }),
                assert: (result) =>
                {
                    var levelA = result["a"] as Dictionary<string, object?>;
                    var levelB = levelA["b"] as Dictionary<string, object?>;
                    levelB.Should().HaveCount(2);
                    levelB["c"].Should().Be(1);
                    levelB["d"].Should().Be(2);
                })
            .Register("CaseInsensitiveKeys",
                arrange: () => Helpers.MergeMetadata(
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Key", "value1" }
                    },
                    new Dictionary<string, object>
                    {
                        { "key", "value2" }
                    }),
                assert: (result) =>
                {
                    result.Should().HaveCount(1);
                    result["KEY"].Should().Be("value2");
                });

        var scenario = scenarios.Get(testCase);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    

    // ═══════════════════════════════════════════════════════════════
    // AreArgumentsEqual - Optimized argument comparison
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BothNull", true)]
    [InlineData("SameReference", true)]
    [InlineData("OneNull", false)]
    [InlineData("DifferentCounts", false)]
    [InlineData("BothEmpty", true)]
    [InlineData("IdenticalArguments", true)]
    [InlineData("DifferentValues", false)]
    [InlineData("NestedDictionaries", true)]
    [InlineData("NestedLists", true)]
    [InlineData("NullInBothValues", true)]
    [InlineData("ListWithNullElements", true)]
    public void AreArgumentsEqual_BasicCases(string scenarioName, bool expectedResult)
    {
        var scenarios = new TestScenarioBag<bool>()
            .Register("BothNull",
                arrange: () => Helpers.AreArgumentsEqual(null, null),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("SameReference",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "key", "value" } },
                    new SortedDictionary<string, object?> { { "key", "value" } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("OneNull",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "key", "value" } },
                    null),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("DifferentCounts",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "a", 1 } },
                    new SortedDictionary<string, object?> { { "a", 1 }, { "b", 2 } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("BothEmpty",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?>(),
                    new SortedDictionary<string, object?>()),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("IdenticalArguments",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "id", 123 }, { "name", "test" } },
                    new SortedDictionary<string, object?> { { "id", 123 }, { "name", "test" } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("DifferentValues",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "id", 123 } },
                    new SortedDictionary<string, object?> { { "id", 456 } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("NestedDictionaries",
                arrange: () =>
                {
                    var nested1 = new Dictionary<string, object?> { { "nested", "value1" } };
                    var nested2 = new Dictionary<string, object?> { { "nested", "value1" } };
                    return Helpers.AreArgumentsEqual(
                        new SortedDictionary<string, object?> { { "data", nested1 } },
                        new SortedDictionary<string, object?> { { "data", nested2 } });
                },
                assert: (result) => result.Should().Be(expectedResult))
            .Register("NestedLists",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } },
                    new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("NullInBothValues",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "key", null } },
                    new SortedDictionary<string, object?> { { "key", null } }),
                assert: (result) => result.Should().Be(expectedResult))
            .Register("ListWithNullElements",
                arrange: () => Helpers.AreArgumentsEqual(
                    new SortedDictionary<string, object?> { { "items", new List<object?> { 1, null, 3 } } },
                    new SortedDictionary<string, object?> { { "items", new List<object?> { 1, null, 3 } } }),
                assert: (result) => result.Should().Be(expectedResult));

        var scenario = scenarios.Get(scenarioName);
        var result = scenario.Arrange();
        scenario.Assert(result);
    }

    // ═══════════════════════════════════════════════════════════════
    // SortArgumentValue - Value sorting for deterministic signatures
    // ═══════════════════════════════════════════════════════════════








    // ═══════════════════════════════════════════════════════════════
    // FindExistingField - Field lookup by various criteria
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("by-name-and-alias")]
    [InlineData("with-complex-name")]
    [InlineData("not-found")]
    public void FindExistingField_Scenarios(string scenario)
    {
        if (scenario == "by-name-and-alias")
        {
            var field = new FieldDefinition("user", "User", "currentUser");
            var fields = new Dictionary<string, FieldDefinition> { { "user:currentUser", field } };
            
            var result = Helpers.FindExistingField(fields, field);
            
            result.Should().Be(field);
        }
        else if (scenario == "with-complex-name")
        {
            var field = new FieldDefinition("profile", "Profile");
            var fields = new Dictionary<string, FieldDefinition> { { "profile", field } };
            
            var result = Helpers.FindExistingField(fields, field);
            
            result.Should().Be(field);
        }
        else if (scenario == "not-found")
        {
            var field = new FieldDefinition("posts", "Post[]");
            var fields = new Dictionary<string, FieldDefinition>();
            
            var result = Helpers.FindExistingField(fields, field);
            
            result.Should().BeNull();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseFieldTypeFromPath - Type annotation parsing
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("user.profile.name", "String", "String")]
    [InlineData("userId", "Int", "Int")]
    [InlineData("field...  ", "String", "field")]
    [InlineData("User.Profile field", "String", "String")]
    [InlineData("123Type field", "String", "String")]
    public void ParseFieldTypeFromPath_SimplePaths_Should_Extract(string pathStr, string defaultStr, string expectedPath)
    {
        var path = pathStr.AsSpan();
        var defaultType = defaultStr.AsSpan();
        
        var result = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual(defaultType).Should().BeTrue();
        if (expectedPath != defaultStr)
        {
            result.SequenceEqual(expectedPath.AsSpan()).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("User user.profile", "String", "User")]
    [InlineData("[User!]! users", "String", "[User!]!")]
    [InlineData("[String!]! users", "Int", "[String!]!")]
    [InlineData("[String!]! path", "Int", "[String!]!")]
    [InlineData("[Int!]! value", "String", "[Int!]!")]
    [InlineData("[User!] items", "Default", "[User!]")]
    [InlineData(".invalid path", "String", "String")]
    public void ParseFieldTypeFromPath_ArrayAndTypePaths_Should_Extract(string pathStr, string defaultStr, string expectedType)
    {
        var path = pathStr.AsSpan();
        var defaultType = defaultStr.AsSpan();
        
        _ = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual(expectedType.AsSpan()).Should().BeTrue();
    }

    [Theory]
    [InlineData("[] items", "String", "[]")]
    [InlineData("[] data", "String", "[]")]
    public void ParseFieldTypeFromPath_BracketOnlyPaths_Should_Extract(string pathStr, string defaultStr, string expectedType)
    {
        var path = pathStr.AsSpan();
        var defaultType = defaultStr.AsSpan();
        
        _ = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual(expectedType.AsSpan()).Should().BeTrue();
    }

    [Theory]
    [InlineData("String     fieldPath", "Int", "String", "    fieldPath")]
    public void ParseFieldTypeFromPath_MultipleSpaces_Should_Preserve(string pathStr, string defaultStr, string expectedType, string expectedPath)
    {
        var path = pathStr.AsSpan();
        var defaultType = defaultStr.AsSpan();
        
        var result = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual(expectedType.AsSpan()).Should().BeTrue();
        result.ToString().Should().Be(expectedPath);
    }

    // ═══════════════════════════════════════════════════════════════
    // ValidateFieldName - Field name validation
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("userId")]
    [InlineData("_private")]
    [InlineData("field123")]
    [InlineData("String fieldName")]
    [InlineData("alias:fieldName")]
    [InlineData("Int! alias:fieldName")]
    [InlineData("a")]
    [InlineData("_")]
    [InlineData("myAlias:myField")]
    [InlineData(":field")]
    public void ValidateFieldName_ValidNames_Should_Pass(string fieldName)
    {
        Action act = () => Helpers.ValidateFieldName(fieldName);
        
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123field")]
    [InlineData("field-name")]
    [InlineData("   ")]
    [InlineData("badAlias-name:validName")]
    [InlineData("validAlias:bad-field")]
    [InlineData("1badAlias:goodName")]
    [InlineData("alias:field@name")]
    [InlineData("validAlias:123field")]
    public void ValidateFieldName_InvalidNames_Should_Throw(string fieldName)
    {
        Action act = () => Helpers.ValidateFieldName(fieldName);
        
        act.Should().Throw<ArgumentException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Test Helper Class
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // TDD: Allocation Profiling Tests
    // ═══════════════════════════════════════════════════════════════




    // ═══════════════════════════════════════════════════════════════
    // MergeNullableMetadata - Comprehensive coverage for edge cases
    // ═══════════════════════════════════════════════════════════════









    // ═══════════════════════════════════════════════════════════════
    // MergeMetadata - Comprehensive coverage for edge cases
    // ═══════════════════════════════════════════════════════════════






    // ═══════════════════════════════════════════════════════════════
    // CreateFieldDefinition - Field creation with various inputs
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("no-args")]
    [InlineData("empty-args")]
    [InlineData("with-args")]
    [InlineData("with-metadata")]
    [InlineData("empty-alias")]
    [InlineData("nested-args")]
    [InlineData("complex-nested-args")]
    [InlineData("large-arg-set")]
    public void CreateFieldDefinition_Variations(string scenario)
    {
        if (scenario == "no-args")
        {
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "String".AsSpan(),
                "alias".AsSpan(),
                null,
                "path".AsSpan()
            );
            
            result.Should().NotBeNull();
            result.Name.Should().Be("field");
            result.Type.Should().Be("String");
            result._alias.Should().Be("alias");
        }
        else if (scenario == "empty-args")
        {
            var args = new Dictionary<string, object?>();
            
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "User".AsSpan(),
                default,
                args,
                "field".AsSpan()
            );
            
            result.Should().NotBeNull();
        }
        else if (scenario == "with-args")
        {
            var args = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 }, { "m", 3 } };
            
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "String".AsSpan(),
                default,
                args,
                "field".AsSpan()
            );
            
            result.Arguments.Should().NotBeNull();
            var keys = result.Arguments.Keys.ToList();
            keys.Should().Equal("a", "m", "z");
        }
        else if (scenario == "with-metadata")
        {
            var metadata = new Dictionary<string, object?> { { "meta", "data" } };
            
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "String".AsSpan(),
                default,
                null,
                "field".AsSpan(),
                metadata
            );
            
            result._metadata.Should().BeSameAs(metadata);
        }
        else if (scenario == "empty-alias")
        {
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "String".AsSpan(),
                ReadOnlySpan<char>.Empty,
                null,
                "field".AsSpan()
            );
            
            result._alias.Should().BeNull();
        }
        else if (scenario == "nested-args")
        {
            var nestedDict = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } };
            var args = new Dictionary<string, object?> { { "nested", nestedDict } };
            
            var result = Helpers.CreateFieldDefinition(
                "field".AsSpan(),
                "String".AsSpan(),
                default,
                args,
                "field".AsSpan()
            );
            
            result.Arguments.Should().NotBeNull();
            var nested = result.Arguments["nested"] as SortedDictionary<string, object?>;
            nested.Should().NotBeNull();
            nested.Keys.Should().Equal("a", "z");
        }
        else if (scenario == "complex-nested-args")
        {
            var args = new Dictionary<string, object?>
            {
                {
                    "filter", new Dictionary<string, object?>
                    {
                        { "z", 1 },
                        { "a", new Dictionary<string, object?> { { "nested", true } } }
                    }
                }
            };
            
            var result = Helpers.CreateFieldDefinition(
                "query".AsSpan(),
                "QueryResponse".AsSpan(),
                default,
                args,
                "query.path".AsSpan()
            );
            
            result.Arguments.Should().NotBeNull();
            result.Arguments.Keys.Should().Contain("filter");
        }
        else if (scenario == "large-arg-set")
        {
            var args = new Dictionary<string, object?>();
            for (int i = 100; i >= 0; i--)
            {
                args[$"field{i:D3}"] = i;
            }
            
            var result = Helpers.CreateFieldDefinition(
                "test".AsSpan(),
                "String".AsSpan(),
                default,
                args,
                "test".AsSpan()
            );
            
            result.Arguments.Should().NotBeNull();
            result.Arguments.Keys.First().Should().Be("field000");
            result.Arguments.Keys.Last().Should().Be("field100");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseFieldTypeFromPath - Edge cases for type parsing
    // ═══════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════
    // ExtractVariablesFromValue - Additional scenarios
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("list-cyclic")]
    [InlineData("complex-nested")]
    [InlineData("nested-lists")]
    [InlineData("deeply-nested-structure")]
    [InlineData("variable-in-object-property")]
    public void ExtractVariables_AdvancedCases(string scenario)
    {
        if (scenario == "list-cyclic")
        {
            var list = new List<object?>();
            list.Add(list); // Circular reference
            var variables = new SortedSet<Variable>();
            
            Action act = () => Helpers.ExtractVariablesFromValue(list, variables);
            act.Should().NotThrow();
        }
        else if (scenario == "complex-nested")
        {
            var var1 = new Variable("$id", "Int!");
            var var2 = new Variable("$status", "String!");
            var var3 = new Variable("$name", "String!");
            
            var complexObj = new Dictionary<string, object?>
            {
                { "filter", new Dictionary<string, object?> { { "id", var1 }, { "status", var2 } } },
                { "list", new List<object?> { var3, "constant" } }
            };
            
            var variables = new SortedSet<Variable>();
            Helpers.ExtractVariablesFromValue(complexObj, variables);
            
            variables.Should().HaveCount(3);
        }
        else if (scenario == "nested-lists")
        {
            var var1 = new Variable("$a", "Int!");
            var var2 = new Variable("$b", "Int!");
            
            var nested = new List<object?> { new List<object?> { var1, var2 }, "text" };
            var variables = new SortedSet<Variable>();
            
            Helpers.ExtractVariablesFromValue(nested, variables);
            
            variables.Should().HaveCount(2);
        }
        else if (scenario == "deeply-nested-structure")
        {
            var var1 = new Variable("$a", "Int!");
            var var2 = new Variable("$b", "String!");
            var var3 = new Variable("$c", "Boolean!");
            
            var obj = new Dictionary<string, object?>
            {
                { "l1", new Dictionary<string, object?> { { "l2", new Dictionary<string, object?> { { "l3", var1 } } } } },
                { "list", new List<object?> { new List<object?> { var2 } } },
                { "direct", var3 }
            };
            
            var variables = new SortedSet<Variable>();
            Helpers.ExtractVariablesFromValue(obj, variables);
            
            variables.Should().HaveCount(3);
        }
        else if (scenario == "variable-in-object-property")
        {
            var variable = new Variable("$id", "Int!");
            var obj = new TestDataObject { Value = variable };
            var variables = new SortedSet<Variable>();
            
            Helpers.ExtractVariablesFromValue(obj, variables);
            
            variables.Should().ContainSingle();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // AreValuesEqual - Additional comparisons
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("list-order")]
    [InlineData("missing-key")]
    [InlineData("nested-diff-values")]
    [InlineData("empty-nested")]
    [InlineData("types-differ")]
    [InlineData("array-vs-list")]
    [InlineData("complex-nested-structures")]
    public void AreArgumentsEqual_DifferencesCases(string scenario)
    {
        if (scenario == "list-order")
        {
            var args1 = new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } };
            var args2 = new SortedDictionary<string, object?> { { "items", new List<object> { 3, 2, 1 } } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeFalse();
        }
        else if (scenario == "missing-key")
        {
            var args1 = new SortedDictionary<string, object?> { { "a", 1 }, { "b", 2 } };
            var args2 = new SortedDictionary<string, object?> { { "a", 1 } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeFalse();
        }
        else if (scenario == "nested-diff-values")
        {
            var nested1 = new Dictionary<string, object?> { { "key", "value1" } };
            var nested2 = new Dictionary<string, object?> { { "key", "value2" } };
            
            var args1 = new SortedDictionary<string, object?> { { "data", nested1 } };
            var args2 = new SortedDictionary<string, object?> { { "data", nested2 } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeFalse();
        }
        else if (scenario == "empty-nested")
        {
            var nested1 = new Dictionary<string, object?>();
            var nested2 = new Dictionary<string, object?>();
            
            var args1 = new SortedDictionary<string, object?> { { "data", nested1 } };
            var args2 = new SortedDictionary<string, object?> { { "data", nested2 } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeTrue();
        }
        else if (scenario == "types-differ")
        {
            var args1 = new SortedDictionary<string, object?> { { "value", 123 } };
            var args2 = new SortedDictionary<string, object?> { { "value", "123" } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeFalse();
        }
        else if (scenario == "array-vs-list")
        {
            var arr = new object[] { 1, 2, 3 };
            var list = new List<object> { 1, 2, 3 };
            
            var args1 = new SortedDictionary<string, object?> { { "data", arr } };
            var args2 = new SortedDictionary<string, object?> { { "data", list } };
            
            var result = Helpers.AreArgumentsEqual(args1, args2);
            
            result.Should().BeFalse();
        }
        else if (scenario == "complex-nested-structures")
        {
            var complex1 = new SortedDictionary<string, object?>
            {
                {
                    "nested", new Dictionary<string, object?>
                    {
                        {
                            "deep", new List<object>
                            {
                                new Dictionary<string, object?> { { "x", 1 } },
                                "text"
                            }
                        }
                    }
                }
            };
            var complex2 = new SortedDictionary<string, object?>
            {
                {
                    "nested", new Dictionary<string, object?>
                    {
                        {
                            "deep", new List<object>
                            {
                                new Dictionary<string, object?> { { "x", 1 } },
                                "text"
                            }
                        }
                    }
                }
            };
            
            var result = Helpers.AreArgumentsEqual(complex1, complex2);
            
            result.Should().BeTrue();
        }
    }



    // ═══════════════════════════════════════════════════════════════
    // Additional Deep Coverage Tests
    // ═══════════════════════════════════════════════════════════════



    // ═══════════════════════════════════════════════════════════════
    // FindExistingField - Comprehensive field lookup scenarios
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("simple-exists")]
    [InlineData("empty-path")]
    [InlineData("whitespace-path")]
    [InlineData("not-found")]
    [InlineData("two-segment-path")]
    [InlineData("two-segment-missing-leaf")]
    [InlineData("single-segment-no-match")]
    public void FindExistingFieldByPath_Scenarios(string scenario)
    {
        if (scenario == "simple-exists")
        {
            var field = new FieldDefinition("user", "User");
            var fields = new Dictionary<string, FieldDefinition> { { "user", field } };
            
            var result = Helpers.FindExistingFieldByPath(fields, "user".AsSpan());
            
            result.Should().Be(field);
        }
        else if (scenario == "empty-path")
        {
            var fields = new Dictionary<string, FieldDefinition>();
            
            var result = Helpers.FindExistingFieldByPath(fields, ReadOnlySpan<char>.Empty);
            
            result.Should().BeNull();
        }
        else if (scenario == "whitespace-path")
        {
            var fields = new Dictionary<string, FieldDefinition>();
            
            var result = Helpers.FindExistingFieldByPath(fields, "   ".AsSpan());
            
            result.Should().BeNull();
        }
        else if (scenario == "not-found")
        {
            var fields = new Dictionary<string, FieldDefinition>();
            
            var result = Helpers.FindExistingFieldByPath(fields, "nonexistent".AsSpan());
            
            result.Should().BeNull();
        }
        else if (scenario == "two-segment-path")
        {
            var address = new FieldDefinition("address", "AddressType");
            
            var profile = new FieldDefinition("profile", "ProfileType");
            profile._children = new FieldChildren();
            profile._children.Append(address);

            var fields = new Dictionary<string, FieldDefinition> { { "profile", profile } };
            
            var result = Helpers.FindExistingFieldByPath(fields, "profile.address".AsSpan());
            
            result.Should().NotBeNull();
            result!.Name.Should().Be("address");
        }
        else if (scenario == "two-segment-missing-leaf")
        {
            var profile = new FieldDefinition("profile", "ProfileType");
            profile._children = new FieldChildren();
            var fields = new Dictionary<string, FieldDefinition> { { "profile", profile } };
            
            var result = Helpers.FindExistingFieldByPath(fields, "profile.address".AsSpan());
            
            result.Should().BeNull();
        }
        else if (scenario == "single-segment-no-match")
        {
            var field = new FieldDefinition("firstName", "String");
            var fields = new Dictionary<string, FieldDefinition> { { "firstName", field } };
            
            var result = Helpers.FindExistingFieldByPath(fields, "lastName".AsSpan());
            
            result.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("by-path")]
    [InlineData("no-name-match")]
    [InlineData("empty-dict")]
    [InlineData("multiple-matches")]
    public void FindExistingField_PathBased(string scenario)
    {
        if (scenario == "by-path")
        {
            var field = new FieldDefinition("profile", "Profile") { Path = "profile" };
            var fields = new Dictionary<string, FieldDefinition> { { "profile", field } };
            var searchField = new FieldDefinition("other", "Other") { Path = "profile" };
            
            var result = Helpers.FindExistingField(fields, searchField);
            
            result.Should().Be(field);
        }
        else if (scenario == "no-name-match")
        {
            var field = new FieldDefinition("name", "String", "name_alias") { Path = "path_key" };
            var fields = new Dictionary<string, FieldDefinition> { { "path_key", field } };
            var searchField = new FieldDefinition("different", "String") { Path = "path_key" };
            
            var result = Helpers.FindExistingField(fields, searchField);
            
            result.Should().Be(field);
        }
        else if (scenario == "empty-dict")
        {
            var fields = new Dictionary<string, FieldDefinition>();
            var searchField = new FieldDefinition("any", "String");
            
            var result = Helpers.FindExistingField(fields, searchField);
            
            result.Should().BeNull();
        }
        else if (scenario == "multiple-matches")
        {
            var field1 = new FieldDefinition("user", "User");
            var field2 = new FieldDefinition("user", "User", "u");
            var field3 = new FieldDefinition("post", "Post");
            var fields = new Dictionary<string, FieldDefinition>
            {
                { "f1", field1 },
                { "f2", field2 },
                { "f3", field3 }
            };
            var searchField = new FieldDefinition("user", "User");
            
            var result = Helpers.FindExistingField(fields, searchField);
            
            result.Should().NotBeNull();
            result.Name.Should().Be("user");
        }
    }

    // Additional edge cases and conditional branches
    // ═══════════════════════════════════════════════════════════════


    // ═══════════════════════════════════════════════════════════════
    // WriteCollection - Collection writing with custom formatting
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("empty-list")]
    [InlineData("single-item")]
    [InlineData("multiple-items")]
    [InlineData("with-nulls")]
    [InlineData("custom-format")]
    public void WriteCollection_Scenarios(string scenario)
    {
        if (scenario == "empty-list")
        {
            var list = new List<string>();
            var builder = new StringBuilder();

            Helpers.WriteCollection('[', ']', list, builder, (sb, obj) => sb.Append(obj ?? "null"));

            builder.ToString().Should().Be("[]");
        }
        else if (scenario == "single-item")
        {
            var list = new List<string> { "item" };
            var builder = new StringBuilder();

            Helpers.WriteCollection('(', ')', list, builder, (sb, obj) => sb.Append(obj));

            builder.ToString().Should().Be("(item)");
        }
        else if (scenario == "multiple-items")
        {
            var list = new List<string> { "a", "b", "c" };
            var builder = new StringBuilder();

            Helpers.WriteCollection('{', '}', list, builder, (sb, obj) => sb.Append(obj));

            builder.ToString().Should().Be("{a, b, c}");
        }
        else if (scenario == "with-nulls")
        {
            var list = new List<string?> { "a", null, "c" };
            var builder = new StringBuilder();

            Helpers.WriteCollection('[', ']', list, builder, (sb, obj) => sb.Append(obj ?? "NULL"));

            builder.ToString().Should().Be("[a, NULL, c]");
        }
        else if (scenario == "custom-format")
        {
            var list = new List<int> { 1, 2, 3 };
            var builder = new StringBuilder();

            Helpers.WriteCollection('<', '>', list, builder, (sb, obj) => sb.Append($"#{obj}"));

            builder.ToString().Should().Be("<#1, #2, #3>");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FindExistingField (FieldChildren variant) - Field lookup in children
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("by-name")]
    [InlineData("not-found")]
    [InlineData("with-alias")]
    [InlineData("empty-children")]
    [InlineData("by-path-not-found")]
    public void FindExistingField_FieldChildrenVariant(string scenario)
    {
        if (scenario == "by-name")
        {
            var child = new FieldDefinition("profile", "Profile");
            var children = new FieldChildren();
            children.Append(child);
            var fieldToFind = new FieldDefinition("profile", "Profile");

            var result = Helpers.FindExistingField(children, fieldToFind);

            result.Should().Be(child);
        }
        else if (scenario == "not-found")
        {
            var child = new FieldDefinition("profile", "Profile");
            var children = new FieldChildren();
            children.Append(child);
            var fieldToFind = new FieldDefinition("other", "Other");

            var result = Helpers.FindExistingField(children, fieldToFind);

            result.Should().BeNull();
        }
        else if (scenario == "with-alias")
        {
            var child = new FieldDefinition("profileData", "Profile", "profile");
            var children = new FieldChildren();
            children.Append(child);
            var fieldToFind = new FieldDefinition("profileData", "Profile", "profile");

            var result = Helpers.FindExistingField(children, fieldToFind);

            result.Should().NotBeNull();
        }
        else if (scenario == "empty-children")
        {
            var children = new FieldChildren();
            var fieldToFind = new FieldDefinition("any", "Any");

            var result = Helpers.FindExistingField(children, fieldToFind);

            result.Should().BeNull();
        }
        else if (scenario == "by-path-not-found")
        {
            // FieldChildren.Find only searches by name, not by dot-separated path
            var child = new FieldDefinition("field", "String") { Path = "parent.field" };
            var children = new FieldChildren();
            children.Append(child);
            var fieldToFind = new FieldDefinition("dummy", "String") { Path = "parent.field" };

            var result = Helpers.FindExistingField(children, fieldToFind);

            // Won't find because "parent.field" doesn't match "field" name
            result.Should().BeNull();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Edge cases and complex scenarios
    // ═══════════════════════════════════════════════════════════════


    [Theory]
    [InlineData("args-nested")]
    [InlineData("args-nested-nulls")]
    [InlineData("merge-deep-nesting")]
    public void ComplexNestedStructures_Scenarios(string scenario)
    {
        if (scenario == "args-nested")
        {
            var nested1 = new Dictionary<string, object?> 
            { 
                { "config", new Dictionary<string, object?> { { "list", new List<object> { 1, 2, 3 } } } } 
            };
            var nested2 = new Dictionary<string, object?> 
            { 
                { "config", new Dictionary<string, object?> { { "list", new List<object> { 1, 2, 3 } } } } 
            };
            var args1 = new SortedDictionary<string, object?> { { "settings", nested1 } };
            var args2 = new SortedDictionary<string, object?> { { "settings", nested2 } };

            var result = Helpers.AreArgumentsEqual(args1, args2);

            result.Should().BeTrue();
        }
        else if (scenario == "args-nested-nulls")
        {
            var args1 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                {"user", new Dictionary<string, object?> { {"id", 1}, {"profile", null} }},
                {"flags", new List<int> { 1, 2, 3 }}
            };
            
            var args2 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                {"user", new Dictionary<string, object?> { {"id", 1}, {"profile", null} }},
                {"flags", new List<int> { 1, 2, 3 }}
            };

            var result = Helpers.AreArgumentsEqual(args1, args2);

            result.Should().BeTrue();
        }
        else if (scenario == "merge-deep-nesting")
        {
            var existing = new Dictionary<string, object?>
            {
                { "level1", new Dictionary<string, object?> { { "level2", new Dictionary<string, object?> { { "key", "old" } } } } }
            };
            var update = new Dictionary<string, object?>
            {
                { "level1", new Dictionary<string, object?> { { "level2", new Dictionary<string, object?> { { "key", "new" } } } } }
            };

            var result = Helpers.MergeNullableDictionaries(existing, update);

            var level1 = result["level1"] as Dictionary<string, object?>;
            var level2 = level1?["level2"] as Dictionary<string, object?>;
            level2?["key"].Should().Be("new");
        }
    }

    [Theory]
    [InlineData("query-block")]
    [InlineData("circular-dict")]
    [InlineData("circular-list")]
    [InlineData("circular-object")]
    [InlineData("mixed-cyclic-linear")]
    [InlineData("string-value")]
    [InlineData("primitive-values")]
    public void ExtractVariables_EdgeCases(string scenario)
    {
        if (scenario == "query-block")
        {
            var queryBlock = new QueryBlock("query", "", null);
            var variables = new SortedSet<Variable>();

            Helpers.ExtractVariablesFromValue(queryBlock, variables);

            variables.Should().BeEmpty();
        }
        else if (scenario == "circular-dict")
        {
            var variables = new SortedSet<Variable>();
            var var1 = new Variable("$id", "ID!");
            var dictA = new Dictionary<string, object?> { {"var", var1} };
            
            // Create circular reference
            dictA["self"] = dictA;

            Helpers.ExtractVariablesFromValue(dictA, variables);

            variables.Should().HaveCount(1);
            variables.First().Name.Should().Be("$id");
        }
        else if (scenario == "circular-list")
        {
            var variables = new SortedSet<Variable>();
            var var1 = new Variable("$name", "String!");
            var list = new List<object?> { var1 };
            
            // Create circular reference
            list.Add(list);

            Helpers.ExtractVariablesFromValue(list, variables);

            variables.Should().HaveCount(1);
            variables.First().Name.Should().Be("$name");
        }
        else if (scenario == "circular-object")
        {
            var variables = new SortedSet<Variable>();
            var var1 = new Variable("$id", "ID!");
            
            var obj1 = new TestContainer { Value = var1 };
            obj1.Self = obj1;

            Helpers.ExtractVariablesFromValue(obj1, variables);

            variables.Should().HaveCount(1);
            variables.First().Name.Should().Be("$id");
        }
        else if (scenario == "mixed-cyclic-linear")
        {
            var dict1 = new Dictionary<string, object?>();
            var dict2 = new Dictionary<string, object?> { { "key", "value" } };
            dict1["self"] = dict1;
            dict1["other"] = dict2;
            var variables = new SortedSet<Variable>();
            
            Action act = () => Helpers.ExtractVariablesFromValue(dict1, variables);
            act.Should().NotThrow();
        }
        else if (scenario == "string-value")
        {
            var variables = new SortedSet<Variable>();
            
            Helpers.ExtractVariablesFromValue("test string", variables);
            
            variables.Should().BeEmpty();
        }
        else if (scenario == "primitive-values")
        {
            var variables = new SortedSet<Variable>();
            
            Helpers.ExtractVariablesFromValue(42, variables);
            Helpers.ExtractVariablesFromValue(true, variables);
            Helpers.ExtractVariablesFromValue(3.14, variables);
            
            variables.Should().BeEmpty();
        }
    }






    [Theory]
    [InlineData("complex-nested", true)]
    [InlineData("different-nested", false)]
    [InlineData("lists-equal", true)]
    [InlineData("different-list-elements", false)]
    [InlineData("mixed-types", true)]
    [InlineData("different-mixed-types", false)]
    [InlineData("deep-nesting", true)]
    [InlineData("first-null", false)]
    [InlineData("plain-value", true)]
    public void AreArgumentsEqual_DataStructures_Scenarios(string scenario, bool expected)
    {
        var scenarios = new TestScenarioBag<bool>()
            .Register("complex-nested",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "filter", new Dictionary<string, object?> { { "status", "active" }, { "type", "user" } } },
                        { "pagination", new Dictionary<string, object?> { { "page", 1 }, { "size", 50 } } }
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "filter", new Dictionary<string, object?> { { "status", "active" }, { "type", "user" } } },
                        { "pagination", new Dictionary<string, object?> { { "page", 1 }, { "size", 50 } } }
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("different-nested",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "filter", new Dictionary<string, object?> { { "status", "active" } } }
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "filter", new Dictionary<string, object?> { { "status", "inactive" } } }
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("lists-equal",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "ids", new List<object> { 1, 2, 3 } }
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "ids", new List<object> { 1, 2, 3 } }
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("different-list-elements",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "ids", new List<object> { 1, 2, 3 } }
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "ids", new List<object> { 1, 2, 4 } }
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("mixed-types",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "string_val", "test" },
                        { "int_val", 42 },
                        { "bool_val", true },
                        { "null_val", null }
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "string_val", "test" },
                        { "int_val", 42 },
                        { "bool_val", true },
                        { "null_val", null }
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("different-mixed-types",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?> { { "value", 42 } };
                    var args2 = new SortedDictionary<string, object?> { { "value", "42" } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("deep-nesting",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?>
                    {
                        { "level1", new Dictionary<string, object?>
                        {
                            { "level2", new Dictionary<string, object?>
                            {
                                { "level3", new List<object> { 1, 2, 3 } }
                            }}
                        }}
                    };
                    var args2 = new SortedDictionary<string, object?>
                    {
                        { "level1", new Dictionary<string, object?>
                        {
                            { "level2", new Dictionary<string, object?>
                            {
                                { "level3", new List<object> { 1, 2, 3 } }
                            }}
                        }}
                    };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("first-null",
                arrange: () =>
                {
                    var args2 = new SortedDictionary<string, object?> { { "key", "value" } };
                    return Helpers.AreArgumentsEqual(null, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("plain-value",
                arrange: () =>
                {
                    var args1 = new SortedDictionary<string, object?> { { "num", 42 }, { "str", "test" } };
                    var args2 = new SortedDictionary<string, object?> { { "num", 42 }, { "str", "test" } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected));

        var testScenario = scenarios.Get(scenario);
        var result = testScenario.Arrange();
        testScenario.Assert(result);
    }

    [Theory]
    [InlineData("structural-equal", true)]
    [InlineData("different-properties", false)]
    [InlineData("nested-class-equal", true)]
    [InlineData("null-property-equal", true)]
    [InlineData("self-reference-equal", true)]
    [InlineData("interface-fallback-equal", true)]
    public void AreArgumentsEqual_ClassTypes_Scenarios(string scenario, bool expected)
    {
        var scenarios = new TestScenarioBag<bool>()
            .Register("structural-equal",
                arrange: () =>
                {
                    var obj1 = new TestContainer { Value = "test", Name = "obj1" };
                    var obj2 = new TestContainer { Value = "test", Name = "obj1" };
                    var args1 = new SortedDictionary<string, object?> { { "data", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "data", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("different-properties",
                arrange: () =>
                {
                    var obj1 = new TestContainer { Value = "test1", Name = "obj1" };
                    var obj2 = new TestContainer { Value = "test2", Name = "obj1" };
                    var args1 = new SortedDictionary<string, object?> { { "data", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "data", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("nested-class-equal",
                arrange: () =>
                {
                    var nested1 = new TestContainer { Value = "nested", Name = "n1" };
                    var nested2 = new TestContainer { Value = "nested", Name = "n1" };
                    var obj1 = new TestContainer { Value = nested1, Name = "parent" };
                    var obj2 = new TestContainer { Value = nested2, Name = "parent" };
                    var args1 = new SortedDictionary<string, object?> { { "data", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "data", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("null-property-equal",
                arrange: () =>
                {
                    var obj1 = new TestContainer { Value = null, Name = "test" };
                    var obj2 = new TestContainer { Value = null, Name = "test" };
                    var args1 = new SortedDictionary<string, object?> { { "data", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "data", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("self-reference-equal",
                arrange: () =>
                {
                    var obj1 = new TestContainer { Value = "test", Name = "obj1", Self = new TestContainer { Value = "nested" } };
                    var obj2 = new TestContainer { Value = "test", Name = "obj1", Self = new TestContainer { Value = "nested" } };
                    var args1 = new SortedDictionary<string, object?> { { "data", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "data", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected))
            .Register("interface-fallback-equal",
                arrange: () =>
                {
                    var obj1 = (ITestInterface)new TestInterface { Value = "test" };
                    var obj2 = (ITestInterface)new TestInterface { Value = "test" };
                    var args1 = new SortedDictionary<string, object?> { { "obj", obj1 } };
                    var args2 = new SortedDictionary<string, object?> { { "obj", obj2 } };
                    return Helpers.AreArgumentsEqual(args1, args2);
                },
                assert: (result) => result.Should().Be(expected));

        var testScenario = scenarios.Get(scenario);
        var result = testScenario.Arrange();
        testScenario.Assert(result);
    }

}
