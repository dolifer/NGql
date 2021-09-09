using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using NGql.Core.Abstractions;

namespace NGql.Client
{
    public class NGqlClient : INGqlClient
    {
        private readonly GraphQLHttpClient _client;

        public NGqlClient(string url)
            => _client = new GraphQLHttpClient(url, new NewtonsoftJsonSerializer());

        public async Task<TResponse> QueryAsync<TResponse>(QueryBase query, object? variables = null)
        {
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = variables
            };

            var response = await _client.SendQueryAsync<TResponse>(request);

            return response.Data;
        }

        public void Dispose() => _client.Dispose();
    }
}
