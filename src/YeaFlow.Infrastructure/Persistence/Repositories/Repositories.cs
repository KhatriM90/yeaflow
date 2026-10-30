using Microsoft.EntityFrameworkCore;
using YeaFlow.Application.Connectors;
using YeaFlow.Application.Postings;
using YeaFlow.Application.Tenancy;
using YeaFlow.Application.Users;
using YeaFlow.Domain.Connectors;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Posting;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Infrastructure.Persistence.Repositories;

internal sealed class PostingIntentRepository : IPostingIntentRepository
{
    private readonly YeaFlowDbContext _db;

    public PostingIntentRepository(YeaFlowDbContext db) => _db = db;

    public Task<PostingIntent?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        _db.PostingIntents.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PostingIntent?> FindByTransactionIdAsync(string transactionId, CancellationToken cancellationToken) =>
        _db.PostingIntents.FirstOrDefaultAsync(p => p.TransactionId == transactionId, cancellationToken);

    public void Add(PostingIntent intent) => _db.PostingIntents.Add(intent);

    public async Task<IReadOnlyList<PostingIntent>> ListDueAsync(DateTimeOffset now, int take, CancellationToken cancellationToken) =>
        await _db.PostingIntents
            .Where(p => p.State == PostingState.Queued && (p.NextAttemptAt == null || p.NextAttemptAt <= now))
            .OrderBy(p => p.NextAttemptAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PostingIntent>> ListOutcomeUnknownAsync(int take, CancellationToken cancellationToken) =>
        await _db.PostingIntents
            .Where(p => p.State == PostingState.OutcomeUnknown && !p.RequiresReview)
            .OrderBy(p => p.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PostingIntent>> ListSubmittingSinceAsync(DateTimeOffset submittedBefore, int take, CancellationToken cancellationToken) =>
        await _db.PostingIntents
            .Where(p => p.State == PostingState.Submitting && p.LastSubmittedAt <= submittedBefore)
            .OrderBy(p => p.LastSubmittedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}

internal sealed class TenantRepository : ITenantRepository
{
    private readonly YeaFlowDbContext _db;

    public TenantRepository(YeaFlowDbContext db) => _db = db;

    public Task<Tenant?> GetAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public async Task<Tenant?> GetForUpdateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
        {
            // The lock is released when the unit of work commits the transaction.
            await _db.Database.BeginTransactionAsync(cancellationToken);
        }

        return await _db.Tenants
            .FromSql($"SELECT * FROM tenants WHERE \"Id\" = {tenantId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken);

    public void Add(Tenant tenant) => _db.Tenants.Add(tenant);

    public async Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken) =>
        await _db.Tenants
            .Where(t => t.Status == TenantStatus.Live || t.Status == TenantStatus.Onboarding)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
}

internal sealed class UserRepository : IUserRepository
{
    private readonly YeaFlowDbContext _db;

    public UserRepository(YeaFlowDbContext db) => _db = db;

    public Task<AppUser?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Users.Include(u => u.CompanyAccess).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
        _db.Users.CountAsync(u => u.IsActive, cancellationToken);
}

internal sealed class ConnectorRepository : IConnectorRepository
{
    private readonly YeaFlowDbContext _db;

    public ConnectorRepository(YeaFlowDbContext db) => _db = db;

    public Task<Connector?> GetAsync(Guid connectorId, CancellationToken cancellationToken) =>
        _db.Connectors.FirstOrDefaultAsync(c => c.Id == connectorId, cancellationToken);

    public async Task<IReadOnlyList<Connector>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Connectors.OrderBy(c => c.Name).ToListAsync(cancellationToken);

    public void Add(Connector connector) => _db.Connectors.Add(connector);
}

internal sealed class CompanyRepository : ICompanyRepository
{
    private readonly YeaFlowDbContext _db;

    public CompanyRepository(YeaFlowDbContext db) => _db = db;

    public Task<Company?> GetAsync(Guid companyId, CancellationToken cancellationToken) =>
        _db.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

    public Task<bool> SapCompanyDbExistsAsync(string sapCompanyDb, CancellationToken cancellationToken) =>
        _db.Companies.AnyAsync(c => c.SapCompanyDb == sapCompanyDb, cancellationToken);

    public void Add(Company company) => _db.Companies.Add(company);
}
