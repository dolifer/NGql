namespace NGql.Core
{
    public readonly struct Variable
    {
        public string Name { get; }
        public string Type { get; }

        public Variable(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }
}
