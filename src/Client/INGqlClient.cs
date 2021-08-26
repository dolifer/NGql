using System;
using System.Threading.Tasks;
using NGql.Core.Abstractions;

namespace NGql.Client
{
    public interface INGqlClient : IDisposable
    {
        Task<TResponse> QueryAsync<TResponse>(QueryBase query);
    }
}
