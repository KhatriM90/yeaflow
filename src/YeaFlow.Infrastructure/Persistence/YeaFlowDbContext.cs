using Microsoft.EntityFrameworkCore;
using Npgsql;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Audit;
using YeaFlow.Domain.Common;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Posting;
using YeaFlow.Domain.Tenancy;
using YeaFlow.Infrastructure.Context;

namespace YeaFlow.Infrastructure.Persistence;

/// <summary>
/// App database. Tenant isolation is enforced twice: an EF Core query filter on every <see cref="ITenantScoped"/> entity,
/// and PostgreSQL row-level security keyed on <c>app.tenant_id</c> (see <see cref="TenantConnectionInterceptor"/>).
/// </summary>
public sealed class YeaFlowDbContext : DbContext, IUnitOfWork
{
    private readonly ScopedExecutionContext? _context;

    public YeaFlowDbContext(DbContextOptions<YeaFlowDbContext> options, ScopedExecutionContext? context = null)
        : base(options)
    {
        _context = context;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<UserCompanyAccess> UserCompanyAccess => Set<UserCompanyAccess>();

    public DbSet<PostingIntent> PostingIntents => Set<PostingIntent>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Used by query filters; Guid.Empty matches nothing, so an uninitialised scope sees no tenant data.</summary>
    internal Guid CurrentTenantId => _context is { HasTenant: true } ? _context.TenantId : Guid.Empty;

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);

            // A repository may have opened a transaction to hold a row lock (e.g. seat checks); the unit of work ends it.
            if (Database.CurrentTransaction is { } transaction)
            {
                await transaction.CommitAsync(cancellationToken);
                await transaction.DisposeAsync();
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("concurrency.stale", "The record was changed by someone else. Reload and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("concurrency.duplicate", "A record with the same key already exists.", ex);
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardTenantWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardTenantWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Money and quantities: numeric(19,6), never floating point.
        configurationBuilder.Properties<decimal>().HavePrecision(19, 6);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YeaFlowDbContext).Assembly);

        modelBuilder.Entity<Company>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserCompanyAccess>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<PostingIntent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AuditEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    /// <summary>Rejects writes of tenant data belonging to a tenant other than the current one.</summary>
    private void GuardTenantWrites()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && entry.Entity.TenantId != CurrentTenantId)
            {
                throw new InvalidOperationException($"Cross-tenant write blocked for {entry.Entity.GetType().Name}.");
            }
        }
    }
}
