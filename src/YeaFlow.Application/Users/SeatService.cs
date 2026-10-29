using YeaFlow.Application.Common;
using YeaFlow.Application.Tenancy;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Application.Users;

/// <summary>Activates and disables app users under the package seat limit (scope section 3).</summary>
public sealed class SeatService
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLog _audit;
    private readonly IExecutionContext _context;

    public SeatService(ITenantRepository tenants, IUserRepository users, IUnitOfWork unitOfWork, IAuditLog audit, IExecutionContext context)
    {
        _tenants = tenants;
        _users = users;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _context = context;
    }

    public async Task ActivateAsync(Guid userId, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, Permissions.AdminUsers);

        // Lock the tenant row so two concurrent activations cannot both take the last seat.
        var tenant = await _tenants.GetForUpdateAsync(_context.TenantId, cancellationToken) ?? throw new NotFoundException("Tenant not found.");
        var user = await _users.GetAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        var active = await _users.CountActiveAsync(cancellationToken);

        user.Activate(tenant.Package, active);
        _audit.Record("user.activated", "ok", detail: user.Email);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableAsync(Guid userId, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, Permissions.AdminUsers);
        var user = await _users.GetAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");
        user.Disable();
        _audit.Record("user.disabled", "ok", detail: user.Email);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SeatUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        Authorize.Require(_context, Permissions.AdminUsers);
        var tenant = await _tenants.GetAsync(_context.TenantId, cancellationToken) ?? throw new NotFoundException("Tenant not found.");
        return new SeatUsage(tenant.Package, await _users.CountActiveAsync(cancellationToken), PackageSeats.MaxActiveUsers(tenant.Package));
    }
}

public sealed record SeatUsage(Package Package, int Active, int Limit);
