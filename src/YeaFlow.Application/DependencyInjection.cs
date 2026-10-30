using Microsoft.Extensions.DependencyInjection;
using YeaFlow.Application.Connectors;
using YeaFlow.Application.Postings;
using YeaFlow.Application.Tenancy;
using YeaFlow.Application.Users;
using YeaFlow.Domain.Posting;

namespace YeaFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddYeaFlowApplication(this IServiceCollection services)
    {
        services.AddSingleton(RetryPolicy.Default);
        services.AddScoped<PostingService>();
        services.AddScoped<PostingJobs>();
        services.AddScoped<SeatService>();
        services.AddScoped<TenantOnboardingService>();
        services.AddScoped<ConnectorAdminService>();
        return services;
    }
}
