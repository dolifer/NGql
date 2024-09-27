using GraphQL.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Schema;

namespace Server;

public class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services
            .AddSingleton<IUsersRepository, UsersRepository>()
            .AddMediatR(c => c.RegisterServicesFromAssemblyContaining<Startup>());

        services
            .AddSingleton<DemoSchema>()
            .AddGraphQL((options, provider) =>
            {
                options.EnableMetrics = true;
                var logger = provider.GetRequiredService<ILogger<Startup>>();
                options.UnhandledExceptionDelegate = ctx => logger.LogError("{Error} occurred", ctx.OriginalException.Message);
            })
            // It is required when both GraphQL HTTP and GraphQL WebSockets middlewares are mapped to the same endpoint (by default 'graphql').
            .AddDefaultEndpointSelectorPolicy()
            // Add required services for GraphQL request/response de/serialization
            .AddSystemTextJson() // For .NET Core 3+
            .AddErrorInfoProvider(opt => opt.ExposeExceptionStackTrace = true)
            .AddGraphTypes(typeof(DemoSchema)); // Add all IGraphType implementors in assembly which ChatSchema exists
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            // map HTTP middleware for schema at default path /graphql
            endpoints.MapGraphQL<DemoSchema>();

            // map playground middleware at default path /ui/playground with default options
            endpoints.MapGraphQLPlayground("/");
        });
    }
}
