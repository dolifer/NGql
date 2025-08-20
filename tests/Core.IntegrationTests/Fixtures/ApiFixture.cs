using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NFixtures.WebApi;
using Server;

namespace NGql.Client.Tests.Fixtures;

public class ApiFixture : StartupFixture<Startup>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseContentRoot(".");
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
    }
}