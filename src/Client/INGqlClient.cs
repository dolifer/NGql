using System;
using System.Threading.Tasks;
using GraphQL;
using NGql.Core;

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
        Task<TResponse> QueryAsync<TResponse>(Query query, object? variables = null);

        /// <summary>
        /// Builds a <see cref="GraphQLRequest"/> by passing <paramref name="mutation"/> and <paramref name="variables"/> to it.
        /// </summary>
        /// <param name="mutation"></param>
        /// <param name="variables"></param>
        /// <typeparam name="TResponse">The typed response received from GQL</typeparam>
        /// <returns></returns>
        Task<TResponse> QueryAsync<TResponse>(Mutation mutation, object? variables = null);
    }
}
