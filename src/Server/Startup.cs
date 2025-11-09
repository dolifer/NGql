using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Schema;

namespace Server;

[SuppressMessage("Style", "S2325:Methods and properties that don't access instance data should be static")]
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
            .AddGraphQL(b => b
                .AddSchema<DemoSchema>()
                .AddSystemTextJson()
                .AddErrorInfoProvider(opt => opt.ExposeExceptionDetailsMode = ExposeExceptionDetailsMode.Message)
                .AddGraphTypes(typeof(DemoSchema).Assembly) // Add all IGraphType implementors in the assembly which DemoSchema exists
                .ConfigureExecutionOptions(options =>
                {
                    options.EnableMetrics = true;
                    // Unhandled exception delegate will be set in Configure method with proper DI
                })
            );
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
    {
        // Configure GraphQL unhandled exception handling with injected logger
        var executionOptions = app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<ExecutionOptions>>();
        executionOptions.Value.UnhandledExceptionDelegate = ctx =>
        {
            logger.LogError("{Error} occurred", ctx.OriginalException.Message);
            return Task.CompletedTask;
        };

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            // map HTTP middleware for schema at the default path /graphql
            endpoints.MapGraphQL<DemoSchema>();

            // map ui
            endpoints.MapGraphQLGraphiQL("/");
        });
    }
}
