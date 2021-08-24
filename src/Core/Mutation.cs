using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class Mutation : QueryPart
    {
        public Mutation(string name)
            : base(name)
        {
        }

        protected override string Prefix => "mutation";
    }
}
