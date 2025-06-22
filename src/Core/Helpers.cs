using System.Collections;
using System.Collections.Generic;

namespace NGql.Core;

internal static class Helpers
{
    internal static void ExtractVariablesFromValue(object value, SortedSet<Variable> variables)
    {
        switch (value)
        {
            case Variable variable:
                variables.Add(variable);
                break;
            case IDictionary dict:
            {
                foreach (var val in dict.Values)
                {
                    ExtractVariablesFromValue(val, variables);
                }

                break;
            }
            case IList list:
            {
                foreach (var item in list)
                {
                    ExtractVariablesFromValue(item, variables);
                }

                break;
            }
        }
    }
}
