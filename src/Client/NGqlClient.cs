using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;

namespace NGql.Client
{
    public sealed class NGqlClient : INGqlClient
    {
        private readonly GraphQLHttpClient _client;

        public NGqlClient(string url)
            => _client = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());

        public void Dispose() => _client.Dispose();

        public async Task<TResponse> QueryAsync<TResponse>(string query, object? variables = null, string? operationName = null)
        {
            var request = new GraphQLRequest
            {
                OperationName = operationName,
                Query = query,
                Variables = variables
            };

            var response = await _client.SendQueryAsync<TResponse>(request);
            return response.Data;
        }
    }
}
