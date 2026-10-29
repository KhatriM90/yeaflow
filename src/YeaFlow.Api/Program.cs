using System.Globalization;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using YeaFlow.Api.Endpoints;
using YeaFlow.Api.Infrastructure;
using YeaFlow.Application;
using YeaFlow.Infrastructure;
using YeaFlow.Infrastructure.Jobs;
using YeaFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((_, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var connectionString = builder.Configuration.GetConnectionString("YeaFlow")
    ?? throw new InvalidOperationException("ConnectionStrings:YeaFlow is required (set ConnectionStrings__YeaFlow).");

builder.Services.AddYeaFlowApplication();
builder.Services.AddYeaFlowInfrastructure(connectionString);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddScoped<ExecutionContextMiddleware>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        builder.Configuration.GetSection("Auth").Bind(o);
        o.MapInboundClaims = false;
        o.TokenValidationParameters.RoleClaimType = "roles";
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks().AddDbContextCheck<YeaFlowDbContext>("database", tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("yeaflow-api"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
        {
            t.AddOtlpExporter();
        }
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is not null)
        {
            m.AddOtlpExporter();
        }
    });

var hangfireEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true);
if (hangfireEnabled)
{
    builder.Services.AddHangfire(cfg => cfg
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
    builder.Services.AddHangfireServer();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseMiddleware<ExecutionContextMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapPostingEndpoints();
api.MapAdminEndpoints();

if (hangfireEnabled)
{
    var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
    jobs.AddOrUpdate<TenantJobRunner>("postings-submit", r => r.SubmitDuePostingsAsync(CancellationToken.None), Cron.Minutely());
    jobs.AddOrUpdate<TenantJobRunner>("postings-reconcile", r => r.ReconcilePostingsAsync(CancellationToken.None), "*/2 * * * *");
}

await app.RunAsync();

/// <summary>Entry point; public for WebApplicationFactory in integration tests.</summary>
public partial class Program;
