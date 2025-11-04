using System.Linq.Expressions;

namespace NGql.Core.Builders;

/// <summary>
/// Extracts field paths from C# expression trees.
/// Supports both compile-time lambda expressions and runtime-parsed expressions.
/// </summary>
public static class ExpressionFieldExtractor
{
    /// <summary>
    /// Extracts field paths from a typed predicate expression.
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <param name="predicate">The predicate expression (e.g., x => x.user.profile.age > 10)</param>
    /// <returns>Set of field paths referenced in the expression</returns>
    /// <example>
    /// <code>
    /// var paths = ExpressionFieldExtractor.ExtractFieldPaths&lt;MyModel&gt;(x => x.user.profile.age > 10);
    /// // Returns: ["user.profile.age"]
    /// </code>
    /// </example>
    public static HashSet<string> ExtractFieldPaths<T>(Expression<Func<T, bool>> predicate)
    {
        return ExtractFieldPaths((Expression)predicate);
    }

    /// <summary>
    /// Extracts field paths from a typed selector expression.
    /// Useful for selecting specific fields without a predicate.
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <typeparam name="TResult">The result type (can be object for anonymous types)</typeparam>
    /// <param name="selector">The selector expression (e.g., x => x.user.profile)</param>
    /// <returns>Set of field paths referenced in the expression</returns>
    /// <example>
    /// <code>
    /// var paths = ExpressionFieldExtractor.ExtractFieldPaths&lt;MyModel, object&gt;(x => new { x.user.name, x.user.email });
    /// // Returns: ["user.name", "user.email"]
    /// </code>
    /// </example>
    public static HashSet<string> ExtractFieldPaths<T, TResult>(Expression<Func<T, TResult>> selector)
    {
        return ExtractFieldPaths((Expression)selector);
    }

    /// <summary>
    /// Extracts field paths from any expression.
    /// Works with runtime-parsed expressions (e.g., from DynamicExpresso).
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <returns>Set of field paths referenced in the expression</returns>
    /// <example>
    /// <code>
    /// // With DynamicExpresso (in tests or application code):
    /// var interpreter = new Interpreter();
    /// var expr = interpreter.Parse("user.profile.email != null");
    /// var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);
    /// // Returns: ["user.profile.email"]
    /// </code>
    /// </example>
    public static HashSet<string> ExtractFieldPaths(Expression expression)
    {
        var visitor = new FieldPathVisitor();
        visitor.Visit(expression);
        return visitor.FieldPaths;
    }

    /// <summary>
    /// Internal visitor that walks the expression tree and collects field paths.
    /// </summary>
    private sealed class FieldPathVisitor : ExpressionVisitor
    {
        public HashSet<string> FieldPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Expression> _visitedMembers = new(ReferenceEqualityComparer.Instance);
        private readonly Stack<string> _lambdaContextPaths = new();
        private ParameterExpression? _rootParameter;
        private readonly HashSet<Type> _rootParameterTypes = new();

        /// <summary>
        /// Visits member access expressions (e.g., user.profile.age).
        /// Only collects complete leaf paths to avoid collecting intermediate segments.
        /// </summary>
        protected override Expression VisitMember(MemberExpression node)
        {
            // Skip properties that should be excluded (like string.Length)
            if (ShouldExcludeProperty(node))
            {
                // Still visit the expression to extract paths from null coalescing chains
                VisitNullCoalescingChain(node.Expression);
                return node;
            }

            var path = BuildMemberPath(node);

            if (!string.IsNullOrEmpty(path))
            {
                // If we're inside a lambda context (e.g., LINQ predicate), prepend the base path
                if (_lambdaContextPaths.Count > 0)
                {
                    var basePath = _lambdaContextPaths.Peek();
                    path = $"{basePath}.{path}";
                }

                FieldPaths.Add(path);
            }

            // Visit any method calls in the chain (e.g., First(), Where())
            // This ensures lambda contexts are established properly
            VisitMethodCallsInChain(node);

            return node;
        }

        /// <summary>
        /// Visits null coalescing chains to extract all field paths.
        /// This handles cases like (x.user.profile.name ?? x.user.email ?? "default").Length
        /// where we want to extract both user.profile.name and user.email.
        /// </summary>
        private void VisitNullCoalescingChain(Expression? expression)
        {
            if (expression is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.Coalesce)
            {
                // Visit both sides of the null coalescing operator
                Visit(binaryExpr.Left);
                Visit(binaryExpr.Right);
            }
            else if (expression != null)
            {
                // Visit the expression normally
                Visit(expression);
            }
        }

        /// <summary>
        /// Visits any method call expressions in a member expression chain.
        /// This is necessary to establish lambda contexts for LINQ methods.
        /// </summary>
        private void VisitMethodCallsInChain(MemberExpression node)
        {
            Expression? current = node.Expression;

            while (current != null)
            {
                if (current is MethodCallExpression methodCall)
                {
                    // Visit the method call to process its lambda arguments
                    Visit(methodCall);
                    // Continue from the collection the method was called on
                    current = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
                }
                else if (current is MemberExpression memberExpr)
                {
                    current = memberExpr.Expression;
                }
                else
                {
                    // Reached parameter or other expression type
                    break;
                }
            }
        }

        /// <summary>
        /// Marks all member expressions in a chain as visited to prevent duplicate processing.
        /// </summary>
        private void MarkMemberChainAsVisited(MemberExpression node)
        {
            var current = node;
            while (current != null)
            {
                _visitedMembers.Add(current);
                current = current.Expression as MemberExpression;
            }
        }

        /// <summary>
        /// Visits method call expressions (e.g., LINQ methods like First, Where, Any).
        /// Tracks the base path for LINQ methods to properly resolve lambda parameter references.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var isLinqMethod = IsLinqMethod(node.Method.Name);

            // Get the base path BEFORE visiting (so expressions aren't marked as visited yet)
            string? basePath = null;
            if (isLinqMethod)
            {
                basePath = GetMethodCallBasePath(node);
            }

            // Visit the object/collection being called on (for instance methods)
            if (node.Object != null)
            {
                Visit(node.Object);
            }
            else if (node.Arguments.Count > 0)
            {
                // For extension methods, visit the first argument (the collection)
                Visit(node.Arguments[0]);
            }

            // Visit remaining arguments with context if this is a LINQ method
            var startIndex = node.Object != null ? 0 : 1; // Skip first arg if already visited

            for (var i = startIndex; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];

                // Push base path context for lambda arguments in LINQ methods
                if (isLinqMethod && arg is LambdaExpression && !string.IsNullOrEmpty(basePath))
                {
                    _lambdaContextPaths.Push(basePath);
                    try
                    {
                        Visit(arg);
                    }
                    finally
                    {
                        _lambdaContextPaths.Pop();
                    }
                }
                else
                {
                    Visit(arg);
                }
            }

            return node;
        }

        /// <summary>
        /// Determines if a method name is a LINQ method that operates on collections.
        /// </summary>
        private static bool IsLinqMethod(string methodName)
        {
            return methodName is "First" or "FirstOrDefault" or "Last" or "LastOrDefault" or
                   "Single" or "SingleOrDefault" or "Where" or "Any" or "All" or
                   "Select" or "SelectMany" or "Count" or "Sum" or "Average" or "Min" or "Max";
        }

        /// <summary>
        /// Gets the base path from a method call (the collection being operated on).
        /// </summary>
        private string? GetMethodCallBasePath(MethodCallExpression node)
        {
            // For instance methods: obj.Method()
            if (node.Object != null)
            {
                return BuildPathFromExpression(node.Object);
            }

            // For extension methods: Method(obj)
            if (node.Arguments.Count > 0)
            {
                return BuildPathFromExpression(node.Arguments[0]);
            }

            return null;
        }

        /// <summary>
        /// Builds a path from any expression (member or method chain).
        /// This is a lightweight version for determining base paths without marking as visited.
        /// </summary>
        private static string? BuildPathFromExpression(Expression expr)
        {
            if (expr is MemberExpression memberExpr)
            {
                // Build path directly without side effects
                var parts = new List<string>();
                var current = memberExpr;

                while (current != null)
                {
                    parts.Add(current.Member.Name);

                    if (current.Expression is MemberExpression nextMember)
                    {
                        current = nextMember;
                    }
                    else if (current.Expression is ParameterExpression)
                    {
                        break;
                    }
                    else if (current.Expression is MethodCallExpression methodCall)
                    {
                        // Continue from the collection the method was called on
                        var basePath = BuildPathFromExpression(methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null!));
                        if (basePath != null)
                        {
                            parts.Add(basePath);
                        }
                        break;
                    }
                    else
                    {
                        return null; // Invalid chain
                    }
                }

                parts.Reverse();
                return parts.Count > 0 ? string.Join(".", parts) : null;
            }

            if (expr is MethodCallExpression methodCallExpr)
            {
                // For method calls, get the path from what it was called on
                return BuildPathFromExpression(methodCallExpr.Object ?? (methodCallExpr.Arguments.Count > 0 ? methodCallExpr.Arguments[0] : null!));
            }

            return null;
        }

        /// <summary>
        /// Visits lambda expressions (e.g., predicates in First, Where).
        /// </summary>
        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Track the root parameter for the outermost lambda (not nested LINQ lambdas)
            if (_rootParameter == null && _lambdaContextPaths.Count == 0 && node.Parameters.Count > 0)
            {
                _rootParameter = node.Parameters[0];
                // Track ALL parameter types for multi-parameter lambdas
                foreach (var param in node.Parameters)
                {
                    _rootParameterTypes.Add(param.Type);
                }
            }

            // Visit the body to extract paths from nested predicates
            // Example: items.First(p => p.sport == "F")
            Visit(node.Body);
            return node;
        }

        /// <summary>
        /// Visits parameter expressions (e.g., direct parameter references like null checks).
        /// </summary>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            // If this is the root parameter being used directly (not in a nested lambda context),
            // add it as a field path. This handles cases like: playerProfile => playerProfile == null
            if (_rootParameter != null && node == _rootParameter && _lambdaContextPaths.Count == 0)
            {
                // Add the parameter name as a field path
                FieldPaths.Add(node.Name ?? "");
            }

            return node;
        }

        /// <summary>
        /// Visits binary expressions (e.g., comparisons like greater than, equals, and logical operators).
        /// </summary>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // Visit both sides of the binary expression
            Visit(node.Left);
            Visit(node.Right);
            return node;
        }

        /// <summary>
        /// Visits unary expressions (e.g., !, conversions).
        /// </summary>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            Visit(node.Operand);
            return node;
        }

        /// <summary>
        /// Visits new expressions (e.g., anonymous type creation).
        /// </summary>
        protected override Expression VisitNew(NewExpression node)
        {
            // Visit all constructor arguments to extract paths
            // Handles: new { x.user.name, x.user.email }
            foreach (var arg in node.Arguments)
            {
                Visit(arg);
            }

            return node;
        }

        /// <summary>
        /// Visits member initialization expressions (e.g., object initializers).
        /// </summary>
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            Visit(node.NewExpression);

            foreach (var binding in node.Bindings)
            {
                if (binding is MemberAssignment assignment)
                {
                    Visit(assignment.Expression);
                }
            }

            return node;
        }

        /// <summary>
        /// Visits conditional expressions (e.g., ternary operator).
        /// </summary>
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Visit(node.Test);
            Visit(node.IfTrue);
            Visit(node.IfFalse);
            return node;
        }

        /// <summary>
        /// Builds a dot-separated field path from a member expression chain.
        /// Handles LINQ method calls like First(), Where(), etc.
        /// </summary>
        /// <param name="node">The member expression to analyze</param>
        /// <returns>The dot-separated path (e.g., "user.profile.age") or null if not a valid path</returns>
        private string? BuildMemberPath(MemberExpression node)
        {
            var parts = new Stack<string>();
            Expression? currentExpr = node;
            ParameterExpression? parameterExpr = null;

            while (currentExpr != null)
            {
                if (currentExpr is MemberExpression memberExpr)
                {
                    // Skip properties that should be excluded
                    if (ShouldExcludeProperty(memberExpr))
                    {
                        currentExpr = memberExpr.Expression;
                        continue;
                    }

                    // Add the member name to the path
                    parts.Push(memberExpr.Member.Name);
                    currentExpr = memberExpr.Expression;
                }
                else if (currentExpr is MethodCallExpression methodCall)
                {
                    // Handle LINQ methods like First(), Where(), Any()
                    // Continue from the object the method was called on (the collection)
                    currentExpr = methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);
                }
                else if (currentExpr is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.Coalesce)
                {
                    // For null coalescing, continue with the left side (the potentially null expression)
                    currentExpr = binaryExpr.Left;
                }
                else if (currentExpr is ConditionalExpression conditionalExpr)
                {
                    // Handle null-conditional operator (?.) which creates a conditional expression
                    // Try IfTrue first, fallback to IfFalse if IfTrue is null/constant
                    currentExpr = conditionalExpr.IfTrue is ConstantExpression { Value: null } 
                        ? conditionalExpr.IfFalse 
                        : conditionalExpr.IfTrue;
                }
                else if (currentExpr is ParameterExpression paramExpr)
                {
                    // Reached a parameter - save it for multi-parameter lambda handling
                    parameterExpr = paramExpr;
                    break;
                }
                else
                {
                    // Not a valid chain (e.g., constant)
                    return null;
                }
            }

            if (parts.Count == 0) return null;

            // For multi-parameter lambdas (more than 1 root parameter type), include parameter name
            if (_rootParameterTypes.Count > 1 && parameterExpr != null && !string.IsNullOrEmpty(parameterExpr.Name))
            {
                parts.Push(parameterExpr.Name);
            }

            return string.Join(".", parts);
        }

        /// <summary>
        /// Determines if a member expression represents a property that should be excluded.
        /// Only excludes System.* types (like string.Length).
        /// All other properties are included regardless of namespace, assembly, or where they're declared.
        /// This supports IL-generated properties and properties from any source.
        /// </summary>
        private bool ShouldExcludeProperty(MemberExpression memberExpr)
        {
            var declaringType = memberExpr.Member.DeclaringType;
            if (declaringType == null)
                return false;

            // Only exclude properties from system types (like string.Length)
            if (declaringType.Namespace?.StartsWith("System") == true)
                return true;

            // Include everything else - if it's in the expression tree, it's relevant
            return false;
        }
    }
}
