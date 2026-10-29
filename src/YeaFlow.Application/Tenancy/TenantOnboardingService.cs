using FluentValidation;
using YeaFlow.Application.Common;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Application.Tenancy;

public sealed record CreateTenantCommand(string Name, string Slug, Package Package);

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(63).Matches("^[a-z0-9]+(-[a-z0-9]+)*$");
        RuleFor(c => c.Package).IsInEnum();
    }
}

/// <summary>Operator console use cases for Yeasoft staff (scope section 8). Not reachable with a tenant role.</summary>
public sealed class TenantOnboardingService
{
    public const string OperatorPermission = "Platform.Tenants.Manage";

    private readonly ITenantRepository _tenants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExecutionContext _context;
    private readonly TimeProvider _time;

    public TenantOnboardingService(ITenantRepository tenants, IUnitOfWork unitOfWork, IExecutionContext context, TimeProvider time)
    {
        _tenants = tenants;
        _unitOfWork = unitOfWork;
        _context = context;
        _time = time;
    }

    public async Task<Tenant> CreateAsync(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        Authorize.Require(_context, OperatorPermission);
        await new CreateTenantCommandValidator().ValidateAndThrowAsync(command, cancellationToken);
        if (await _tenants.SlugExistsAsync(command.Slug, cancellationToken))
        {
            throw new ConflictException("tenant.slug_taken", $"A tenant with slug '{command.Slug}' already exists.");
        }

        var tenant = new Tenant(Guid.NewGuid(), command.Name, command.Slug, command.Package, _time.GetUtcNow());
        _tenants.Add(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return tenant;
    }
}
