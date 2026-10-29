using YeaFlow.Domain.Common;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.UnitTests.Domain;

public class SeatAndRoleTests
{
    [Theory]
    [InlineData(Package.Starter, 5)]
    [InlineData(Package.Business, 10)]
    [InlineData(Package.Enterprise, 20)]
    public void SeatLimitsHoldAtPackageBoundary(Package package, int limit)
    {
        SeatPolicy.EnsureCanActivate(package, limit - 1);
        var ex = Assert.Throws<DomainException>(() => SeatPolicy.EnsureCanActivate(package, limit));
        Assert.Equal("seats.limit_reached", ex.Code);
    }

    [Fact]
    public void ActivatingAlreadyActiveUser_DoesNotNeedASeat()
    {
        var user = new AppUser(Guid.NewGuid(), Guid.NewGuid(), "a@x.co.ke", "A");
        user.Activate(Package.Starter, 0);
        user.Activate(Package.Starter, 5);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void DisabledUser_FreesSeat()
    {
        var user = new AppUser(Guid.NewGuid(), Guid.NewGuid(), "a@x.co.ke", "A");
        user.Activate(Package.Starter, 4);
        user.Disable();
        Assert.False(user.IsActive);
    }

    [Fact]
    public void Downgrade_RequiresActiveUsersToFit()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", "acme", Package.Business, DateTimeOffset.UnixEpoch);
        Assert.Throws<DomainException>(() => tenant.ChangePackage(Package.Starter, 6));
        tenant.ChangePackage(Package.Starter, 5);
        Assert.Equal(Package.Starter, tenant.Package);
        tenant.ChangePackage(Package.Enterprise, 5);
        Assert.Equal(Package.Enterprise, tenant.Package);
    }

    [Fact]
    public void GoLive_RequiresCompletedChecklist()
    {
        var tenant = new Tenant(Guid.NewGuid(), "Acme", "acme", Package.Starter, DateTimeOffset.UnixEpoch);
        Assert.Throws<DomainException>(() => tenant.GoLive(onboardingChecklistComplete: false));
        tenant.GoLive(onboardingChecklistComplete: true);
        Assert.Equal(TenantStatus.Live, tenant.Status);
    }

    [Fact]
    public void Approver_CanApprove_ButNotCreate()
    {
        var permissions = RolePermissions.For(Role.Approver);
        Assert.Contains("Sales.Order.Approve", permissions);
        Assert.Contains("Purchasing.Order.Approve", permissions);
        Assert.DoesNotContain("Sales.Order.Create", permissions);
    }

    [Fact]
    public void Sales_CannotSeeMarginOrApprove()
    {
        var permissions = RolePermissions.For(Role.Sales);
        Assert.Contains("Sales.Order.Create", permissions);
        Assert.DoesNotContain(Permissions.ViewCostAndMargin, permissions);
        Assert.DoesNotContain("Sales.Order.Approve", permissions);
    }

    [Fact]
    public void PrivilegedRoles_RequireMfa()
    {
        var user = new AppUser(Guid.NewGuid(), Guid.NewGuid(), "a@x.co.ke", "A");
        user.AssignRole(Role.Sales);
        Assert.False(user.RequiresMfa);
        user.AssignRole(Role.AppAdministrator);
        Assert.True(user.RequiresMfa);
    }

    [Fact]
    public void CompanyAccess_IsExplicit()
    {
        var company = Guid.NewGuid();
        var user = new AppUser(Guid.NewGuid(), Guid.NewGuid(), "a@x.co.ke", "A");
        Assert.False(user.CanAccessCompany(company));
        user.GrantCompany(company);
        Assert.True(user.CanAccessCompany(company));
    }
}
