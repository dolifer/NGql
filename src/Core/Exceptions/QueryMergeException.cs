using System;

namespace NGql.Core.Exceptions;

/// <summary>
/// Exception thrown when query merging fails
/// </summary>
public class QueryMergeException : Exception
{
    public QueryMergeException(string message) : base(message)
    {
    }

    public QueryMergeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
