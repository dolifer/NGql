using System;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using GraphQL.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NGql.Client.Tests.Fixtures;
using Xunit;

namespace NGql.Client.Tests;

public class StartupTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public StartupTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Configure_UnhandledExceptionDelegate_LogsAndCompletes()
    {
        var options = _fixture.Services.GetRequiredService<IOptions<ExecutionOptions>>().Value;
        var context = new UnhandledExceptionContext(new GraphQL.Execution.ExecutionContext(), null, new InvalidOperationException("boom"));

        var handling = () => options.UnhandledExceptionDelegate(context);

        await handling.Should().NotThrowAsync();
    }
}
