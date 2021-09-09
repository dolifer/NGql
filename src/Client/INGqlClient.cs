using System;
using System.Threading.Tasks;
using GraphQL;
using NGql.Core.Abstractions;

namespace NGql.Client
{
    public interface INGqlClient : IDisposable
    {
        /// <summary>
        /// Builds a <see cref="GraphQLRequest"/> by passing <paramref name="query"/> and <paramref name="variables"/> to it.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="variables"></param>
        /// <typeparam name="TResponse">The typed response received from GQL</typeparam>
        /// <returns></returns>
        Task<TResponse> QueryAsync<TResponse>(QueryBase query, object? variables = null);
    }
}
