using YeaFlow.Domain.Common;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Domain.Identity;

/// <summary>An individual app identity (never a shared login). Seat counting uses <see cref="IsActive"/>.</summary>
public sealed class AppUser : ITenantScoped
{
    private readonly List<string> _roles = [];
    private readonly List<UserCompanyAccess> _companyAccess = [];

    private AppUser()
    {
        Email = string.Empty;
        DisplayName = string.Empty;
    }

    public AppUser(Guid id, Guid tenantId, string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        TenantId = tenantId;
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>Subject claim from the identity provider, once the user has signed in.</summary>
    public string? ExternalSubject { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<string> Roles => _roles;

    public IReadOnlyList<UserCompanyAccess> CompanyAccess => _companyAccess;

    /// <summary>Privileged roles must sign in with MFA (scope section 4).</summary>
    public bool RequiresMfa => _roles.Exists(Role.IsPrivileged);

    public void Activate(Package package, int currentActiveUsers)
    {
        if (IsActive)
        {
            return;
        }

        SeatPolicy.EnsureCanActivate(package, currentActiveUsers);
        IsActive = true;
    }

    /// <summary>Disabling frees the seat; history stays.</summary>
    public void Disable() => IsActive = false;

    public void LinkExternalSubject(string subject) => ExternalSubject = subject;

    public void AssignRole(string role)
    {
        if (!Role.All.Contains(role))
        {
            throw new DomainException("roles.unknown", $"Unknown role '{role}'.");
        }

        if (!_roles.Contains(role))
        {
            _roles.Add(role);
        }
    }

    public void RemoveRole(string role) => _roles.Remove(role);

    public void GrantCompany(Guid companyId)
    {
        if (!_companyAccess.Exists(a => a.CompanyId == companyId))
        {
            _companyAccess.Add(new UserCompanyAccess(TenantId, Id, companyId));
        }
    }

    public void RevokeCompany(Guid companyId) => _companyAccess.RemoveAll(a => a.CompanyId == companyId);

    public bool CanAccessCompany(Guid companyId) => _companyAccess.Exists(a => a.CompanyId == companyId);
}

/// <summary>Explicit per-user company access; no access is implied.</summary>
public sealed class UserCompanyAccess : ITenantScoped
{
    public UserCompanyAccess(Guid tenantId, Guid userId, Guid companyId)
    {
        TenantId = tenantId;
        UserId = userId;
        CompanyId = companyId;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }
}
