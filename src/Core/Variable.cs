using System;

namespace NGql.Core
{
    /// <summary>
    ///     Represents a variable in GraphQL query.
    /// </summary>
    public readonly struct Variable : IComparable, IComparable<Variable>
    {
        /// <summary>
        /// Name of the variable.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Type of the variable.
        /// </summary>
        public string Type { get; }

        public Variable(string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be null or whitespace.", nameof(name));

            if (!name.StartsWith('$'))
                throw new ArgumentException("Variable name must start with '$'.", nameof(name));

            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Variable type cannot be null or whitespace.", nameof(type));

            Name = name;
            Type = type;
        }

        public override string ToString() => $"{Name}:{Type}";

        public int CompareTo(object? obj)
        {
            return obj switch
            {
                null => 1,
                Variable variable => CompareTo(variable),
                _ => throw new ArgumentException($"Object must be of type {nameof(Variable)}")
            };
        }

        public int CompareTo(Variable other)
        {
            var nameComparison = string.Compare(Name, other.Name, StringComparison.Ordinal);
            return nameComparison != 0 ? nameComparison : string.Compare(Type, other.Type, StringComparison.Ordinal);
        }
        
        public static bool operator ==(Variable left, Variable right) => left.Equals(right);
        public static bool operator !=(Variable left, Variable right) => !left.Equals(right);
        
        public static bool operator <(Variable left, Variable right) => left.CompareTo(right) < 0;
        public static bool operator >(Variable left, Variable right) => left.CompareTo(right) > 0;
        
        public static bool operator <=(Variable left, Variable right) => left.CompareTo(right) <= 0;
        public static bool operator >=(Variable left, Variable right) => left.CompareTo(right) >= 0;
        
        public override bool Equals(object? obj) => CompareTo(obj) == 0;
        
        public override int GetHashCode() => HashCode.Combine(Name, Type);
    }
}
