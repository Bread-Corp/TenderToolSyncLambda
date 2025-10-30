using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.Net.Auth.AwsSigV4;
using TenderToolSyncLambda.Data;

namespace TenderToolSyncLambda;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container
    public void ConfigureServices(IServiceCollection services)
    {
        // 1. CONFIGURE JSON LOGGING
        services.AddLogging(builder =>
        {
            builder.ClearProviders(); // Clear default text loggers
            builder.AddJsonConsole(options =>
            {
                options.IncludeScopes = false;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fffZ";
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
                {
                    Indented = false // Compact logs for CloudWatch
                };
            });
            builder.AddConfiguration(Configuration.GetSection("Logging"));
        });

        // 2. CONFIGURE ENTITY FRAMEWORK (for RDS)
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));

        // 3. CONFIGURE OPENSEARCH CLIENT (for Searching)
        services.AddSingleton<IOpenSearchClient>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var endpointUrl = config["OpenSearch:Endpoint"];
            if (string.IsNullOrEmpty(endpointUrl))
            {
                throw new InvalidOperationException("OpenSearch:Endpoint is not configured in appsettings.json");
            }

            // This connection will automatically find AWS credentials
            // from the Lambda's execution role.
            var httpConnection = new AwsSigV4HttpConnection();

            var settings = new ConnectionSettings(new Uri(endpointUrl), httpConnection)
                // We'll call our main search index "tenders"
                .DefaultIndex("tenders")
                // This is needed to connect to the VPC endpoint
                .ServerCertificateValidationCallback(CertificateValidations.AllowAll);

            return new OpenSearchClient(settings);
        });

        services.AddControllers();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // We don't need HTTPS redirection in Lambda
        // app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Welcome to the Tender Tool Sync Lambda");
            });
        });
    }
}