namespace YeaFlow.Domain.Tenancy;

/// <summary>Subscription packages from scope section 3. All packages include all seven scoped modules.</summary>
public enum Package
{
    Starter = 1,
    Business = 2,
    Enterprise = 3,
}

public static class PackageSeats
{
    /// <summary>Maximum named active app users per package (scope section 3).</summary>
    public static int MaxActiveUsers(Package package) => package switch
    {
        Package.Starter => 5,
        Package.Business => 10,
        Package.Enterprise => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(package), package, "Unknown package."),
    };
}
