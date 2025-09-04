namespace NGql.Core.Builders;

/// <summary>
/// Internal fluent configuration for field creation
/// </summary>
internal sealed class FieldConfiguration
{
    internal string? Type { get; set; }
    internal SortedDictionary<string, object?>? Arguments { get; set; }
    internal string[]? SubFields { get; set; }
    internal Dictionary<string, object?>? Metadata { get; set; }
    internal Action<FieldBuilder>? Action { get; set; }

    internal FieldConfiguration WithType(string type)
    {
        Type = type;
        return this;
    }

    internal FieldConfiguration WithArguments(SortedDictionary<string, object?> arguments)
    {
        Arguments = arguments;
        return this;
    }

    internal FieldConfiguration WithSubFields(params string[] subFields)
    {
        SubFields = subFields;
        return this;
    }

    internal FieldConfiguration WithMetadata(Dictionary<string, object?> metadata)
    {
        Metadata = metadata;
        return this;
    }

    internal FieldConfiguration WithAction(Action<FieldBuilder> action)
    {
        Action = action;
        return this;
    }

    /// <summary>
    /// Creates a FieldConfiguration from method parameters
    /// </summary>
    internal static FieldConfiguration From(string? type = null, SortedDictionary<string, object?>? arguments = null, 
        string[]? subFields = null, Dictionary<string, object?>? metadata = null, Action<FieldBuilder>? action = null)
    {
        var config = new FieldConfiguration();
        if (type != null) config.Type = type;
        if (arguments != null) config.Arguments = arguments;
        if (subFields != null) config.SubFields = subFields;
        if (metadata != null) config.Metadata = metadata;
        if (action != null) config.Action = action;
        return config;
    }
}
