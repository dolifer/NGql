using System;
using System.Threading.Tasks;
using GraphQL;

namespace NGql.Client
{
    public interface INGqlClient : IDisposable
    {
        /// <summary>
        /// Builds a <see cref="GraphQLRequest"/> by passing <paramref name="query"/> and <paramref name="variables"/> to it.
        /// </summary>
        /// <param name="query">The Query text</param>
        /// <param name="variables">The request Variables</param>
        /// <param name="operationName">The name of the Operation</param>
        /// <typeparam name="TResponse">The typed response received from GQL</typeparam>
        /// <returns></returns>
        Task<TResponse> QueryAsync<TResponse>(string query, object? variables = null, string? operationName = null);
    }
}
