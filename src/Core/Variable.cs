namespace NGql.Core
{
    public readonly struct Variable
    {
        public string Name { get; }

        public Variable(string name)
        {
            Name = name;
        }
    }
}
