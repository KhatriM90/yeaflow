using YeaFlow.Domain.Common;

namespace YeaFlow.Domain.Tenancy;

/// <summary>
/// Seat rules from scope section 3: each active person counts once across the tenant's companies,
/// disabled users do not occupy a seat, and activation above the package limit is rejected.
/// </summary>
public static class SeatPolicy
{
    public static void EnsureCanActivate(Package package, int currentActiveUsers)
    {
        var max = PackageSeats.MaxActiveUsers(package);
        if (currentActiveUsers >= max)
        {
            throw new DomainException(
                "seats.limit_reached",
                $"The {package} package allows {max} active users and all seats are in use. Disable a user or upgrade the package.");
        }
    }
}
