namespace NGql.Core.Abstractions;

/// <summary>
/// Defines strategies for merging query definitions
/// </summary>
public enum MergingStrategy
{
    /// <summary>
    /// Uses the default merging behavior, which inherits from parent strategy
    /// </summary>
    MergeByDefault,
    
    /// <summary>
    /// Merges queries based on field path compatibility
    /// </summary>
    MergeByFieldPath,
    
    /// <summary>
    /// Never merge queries, always create separate definitions
    /// </summary>
    NeverMerge
}
