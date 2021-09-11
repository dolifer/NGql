using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using NGql.Core;

namespace NGql.Client
{
    public sealed class NGqlClient : INGqlClient
    {
        private readonly GraphQLHttpClient _client;

        public NGqlClient(string url)
            => _client = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());

        public async Task<TResponse> QueryAsync<TResponse>(Query query, object? variables = null)
            => await SendQueryAsync<TResponse>(query, variables);

        public async Task<TResponse> QueryAsync<TResponse>(Mutation mutation, object? variables = null)
            => await SendQueryAsync<TResponse>(mutation, variables);

        public void Dispose() => _client.Dispose();

        private async Task<TResponse> SendQueryAsync<TResponse>(string query, object? variables = null)
        {
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = variables
            };

            var response = await _client.SendQueryAsync<TResponse>(request);
            return response.Data;
        }
    }
}
