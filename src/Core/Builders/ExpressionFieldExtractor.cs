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
        => ExtractFieldPaths((Expression)predicate);

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
        => ExtractFieldPaths((Expression)selector);

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
        private readonly Stack<string> _lambdaContextPaths = new();
        private ParameterExpression? _rootParameter;
        private int _rootParameterCount;

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
                    // Continue from the collection the method was called on. For instance
                    // methods Object is non-null; for static/extension methods (LINQ) the
                    // first argument is the source. A parameterless static-like call has
                    // neither, so there is nothing further to walk.
                    current = MethodCallSourceExpression(methodCall);
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
        /// Visits method call expressions (e.g., LINQ methods like First, Where, Any).
        /// Tracks the base path for LINQ methods to properly resolve lambda parameter references.
        /// </summary>
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var isLinqMethod = IsLinqMethod(node);
            var basePath = isLinqMethod ? GetMethodCallBasePath(node) : null;

            VisitReceiver(node);
            VisitRemainingArguments(node, isLinqMethod, basePath);

            return node;
        }

        private void VisitReceiver(MethodCallExpression node)
        {
            if (node.Object is not null)
            {
                Visit(node.Object);
            }
            else if (node.Arguments.Count > 0)
            {
                Visit(node.Arguments[0]);
            }
        }

        private void VisitRemainingArguments(MethodCallExpression node, bool isLinqMethod, string? basePath)
        {
            var startIndex = node.Object is not null ? 0 : 1;
            for (var i = startIndex; i < node.Arguments.Count; i++)
            {
                VisitArgument(node.Arguments[i], isLinqMethod, basePath);
            }
        }

        private void VisitArgument(Expression arg, bool isLinqMethod, string? basePath)
        {
            if (isLinqMethod && arg is LambdaExpression && !string.IsNullOrEmpty(basePath))
            {
                VisitWithLambdaContext(arg, basePath);
                return;
            }
            Visit(arg);
        }

        private void VisitWithLambdaContext(Expression arg, string basePath)
        {
            _lambdaContextPaths.Push(basePath);
            try { Visit(arg); }
            finally { _lambdaContextPaths.Pop(); }
        }

        /// <summary>
        /// Determines if a method is a LINQ extension method from System.Linq.
        /// </summary>
        private static bool IsLinqMethod(MethodCallExpression node)
        {
            var declaringType = node.Method.DeclaringType;
            return declaringType is { Namespace: "System.Linq", Name: "Enumerable" or "Queryable" };
        }

        /// <summary>
        /// Gets the base path from a LINQ method call (the collection being operated on).
        /// LINQ on Enumerable/Queryable is always an extension method whose first argument is
        /// the source collection — instance LINQ methods do not exist in the BCL, so
        /// node.Arguments is guaranteed non-empty here.
        /// </summary>
        private static string? GetMethodCallBasePath(MethodCallExpression node)
            => BuildPathFromExpression(node.Arguments[0]);

        /// <summary>
        /// Builds a path from any expression (member or method chain).
        /// Lightweight version for determining base paths without marking as visited.
        /// </summary>
        private static string? BuildPathFromExpression(Expression? expr) => expr switch
        {
            MemberExpression memberExpr => BuildPathFromMember(memberExpr),
            MethodCallExpression methodCallExpr => BuildPathFromExpression(MethodCallSourceExpression(methodCallExpr)),
            _ => null,
        };

        private static string? BuildPathFromMember(MemberExpression start)
        {
            // Loop entry adds at least one part; parts.Count is never 0 on exit.
            var parts = new List<string>();
            for (MemberExpression? current = start; current is not null;)
            {
                parts.Add(current.Member.Name);
                if (!TryStepUp(current, parts, out current)) return null;
            }

            parts.Reverse();
            return string.Join(".", parts);
        }

        // Returns true if the chain continues (next set, possibly null when terminating at a parameter
        // or a successfully-resolved method call); returns false to signal an invalid chain shape.
        private static bool TryStepUp(MemberExpression current, List<string> parts, out MemberExpression? next)
        {
            switch (current.Expression)
            {
                case MemberExpression nextMember:
                    next = nextMember;
                    return true;
                case ParameterExpression:
                    next = null;
                    return true;
                case MethodCallExpression methodCall:
                    var basePath = BuildPathFromExpression(MethodCallSourceExpression(methodCall));
                    if (basePath is not null) parts.Add(basePath);
                    next = null;
                    return true;
                default:
                    next = null;
                    return false;
            }
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
                // Track the outermost lambda's parameter COUNT (not distinct types): two
                // parameters of the same type must still be disambiguated by name, otherwise
                // their fields leak across sibling nodes during downstream preservation.
                _rootParameterCount = node.Parameters.Count;
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
            // Lambdas authored in C# always carry a parameter name; the BCL's
            // Expression.Parameter overloads accept null but no public path produces such
            // parameters here, so we treat the name as non-null.
            if (_rootParameter != null && node == _rootParameter && _lambdaContextPaths.Count == 0)
            {
                FieldPaths.Add(node.Name!);
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
            // The first StepUpChain call always pushes node.Member.Name onto parts before
            // it can fail or terminate, so parts is non-empty by the time we exit the loop.
            var parts = new Stack<string>();
            Expression? currentExpr = node;
            ParameterExpression? parameterExpr = null;

            while (currentExpr != null)
            {
                var step = StepUpChain(currentExpr, parts);
                if (step.IsTerminal)
                {
                    parameterExpr = step.Parameter;
                    if (!step.Valid) return null;
                    break;
                }
                currentExpr = step.Next;
            }

            // For multi-parameter lambdas (more than 1 root parameter), include parameter name
            if (_rootParameterCount > 1 && parameterExpr != null && !string.IsNullOrEmpty(parameterExpr.Name))
            {
                parts.Push(parameterExpr.Name);
            }

            return string.Join(".", parts);
        }

        /// <summary>
        /// One step of <see cref="BuildMemberPath"/>'s chain walk. Returns either the next
        /// expression to walk (<see cref="Next"/>) or a terminal status (<see cref="IsTerminal"/>)
        /// indicating whether the chain ended cleanly at a parameter (<see cref="Valid"/>) or hit
        /// an unsupported node shape.
        /// </summary>
        private readonly ref struct ChainStep
        {
            public Expression? Next { get; init; }
            public ParameterExpression? Parameter { get; init; }
            public bool IsTerminal { get; init; }
            public bool Valid { get; init; }

            public static ChainStep Continue(Expression? next) => new() { Next = next };
            public static ChainStep TerminateAt(ParameterExpression p) => new() { IsTerminal = true, Valid = true, Parameter = p };
            public static ChainStep Invalid() => new() { IsTerminal = true, Valid = false };
        }

        private static ChainStep StepUpChain(Expression currentExpr, Stack<string> parts)
        {
            switch (currentExpr)
            {
                case MemberExpression memberExpr:
                    parts.Push(memberExpr.Member.Name);
                    return ChainStep.Continue(memberExpr.Expression);

                case MethodCallExpression methodCall:
                    var source = MethodCallSourceExpression(methodCall);
                    // A parameterless static-like call (null Object, zero arguments) has no
                    // source to walk toward the lambda parameter, so the chain cannot resolve
                    // to a valid field path.
                    return source is null ? ChainStep.Invalid() : ChainStep.Continue(source);

                case BinaryExpression { NodeType: ExpressionType.Coalesce } binaryExpr:
                    return ChainStep.Continue(binaryExpr.Left);

                case ConditionalExpression conditionalExpr:
                    return ChainStep.Continue(SelectConditionalBranch(conditionalExpr));

                case ParameterExpression paramExpr:
                    return ChainStep.TerminateAt(paramExpr);

                default:
                    return ChainStep.Invalid();
            }
        }

        // The source a method call is chained onto: Object for an instance method, or the first
        // argument for an extension/static method. A parameterless static-like call has neither,
        // so this returns null and the caller treats the sub-expression as contributing no path.
        private static Expression? MethodCallSourceExpression(MethodCallExpression methodCall)
            => methodCall.Object ?? (methodCall.Arguments.Count > 0 ? methodCall.Arguments[0] : null);

        /// <summary>
        /// Picks the branch of a null-conditional-style ternary that carries member access.
        /// IfTrue takes precedence; the IfFalse branch wins only when IfTrue is the constant null.
        /// </summary>
        private static Expression SelectConditionalBranch(ConditionalExpression conditionalExpr)
            => conditionalExpr.IfTrue is ConstantExpression { Value: null }
                ? conditionalExpr.IfFalse
                : conditionalExpr.IfTrue;

        /// <summary>
        /// Determines if a member expression represents a property that should be excluded.
        /// Excludes only System.* types (like string.Length); IL-emitted properties from
        /// QueryBuilderTypeGenerator and user types are kept.
        /// </summary>
        private static bool ShouldExcludeProperty(MemberExpression memberExpr)
            => memberExpr.Member.DeclaringType!.Namespace?.StartsWith("System") == true;
    }
}
