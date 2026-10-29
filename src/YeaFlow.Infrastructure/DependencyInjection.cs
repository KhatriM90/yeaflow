using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YeaFlow.Application.Common;
using YeaFlow.Application.Postings;
using YeaFlow.Application.Tenancy;
using YeaFlow.Application.Users;
using YeaFlow.Infrastructure.Context;
using YeaFlow.Infrastructure.Jobs;
using YeaFlow.Infrastructure.Persistence;
using YeaFlow.Infrastructure.Persistence.Repositories;
using YeaFlow.Infrastructure.Sap;

namespace YeaFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddYeaFlowInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ScopedExecutionContext>();
        services.AddScoped<IExecutionContext>(sp => sp.GetRequiredService<ScopedExecutionContext>());
        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<YeaFlowDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<YeaFlowDbContext>());

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IPostingIntentRepository, PostingIntentRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISapGateway, PendingConnectorSapGateway>();
        services.AddTransient<TenantJobRunner>();
        return services;
    }
}
