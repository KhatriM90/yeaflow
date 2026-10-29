using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Tenancy;

public enum TenantStatus
{
    Onboarding = 0,
    Live = 1,
    Suspended = 2,
    Offboarded = 3,
}

/// <summary>A customer of Yeasoft. Seat limits apply across all of the tenant's company databases.</summary>
public sealed class Tenant
{
    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
        TimeZone = string.Empty;
    }

    public Tenant(Guid id, string name, string slug, Package package, DateTimeOffset createdAt, string timeZone = "Africa/Nairobi")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Package = package;
        Status = TenantStatus.Onboarding;
        TimeZone = timeZone;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public Package Package { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>IANA time zone used for display; timestamps are stored in UTC. EAT by default.</summary>
    public string TimeZone { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Changes package. Upgrades always succeed; a downgrade requires the active user count to fit the new limit.
    /// </summary>
    public void ChangePackage(Package newPackage, int activeUserCount)
    {
        if (activeUserCount > PackageSeats.MaxActiveUsers(newPackage))
        {
            throw new DomainException(
                "seats.downgrade_exceeds_limit",
                $"{activeUserCount} active users exceed the {newPackage} limit of {PackageSeats.MaxActiveUsers(newPackage)}. Disable users first.");
        }

        Package = newPackage;
    }

    public void GoLive(bool onboardingChecklistComplete)
    {
        if (!onboardingChecklistComplete)
        {
            throw new DomainException("tenant.onboarding_incomplete", "The onboarding checklist must be complete before the tenant can go live.");
        }

        Status = TenantStatus.Live;
    }

    public void Suspend() => Status = TenantStatus.Suspended;
}
