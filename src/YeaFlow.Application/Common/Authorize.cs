using YeaFlow.Domain.Identity;

namespace YeaFlow.Application.Common;

/// <summary>Backend permission checks (scope section 4: hiding a menu is insufficient).</summary>
public static class Authorize
{
    public static void Require(IExecutionContext context, string permission)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsSystem || context.Permissions.Contains(permission))
        {
            return;
        }

        throw new ForbiddenException($"Missing permission {permission}.");
    }

    public static void RequireCompany(IExecutionContext context, Guid companyId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.IsSystem || context.CompanyIds.Contains(companyId))
        {
            return;
        }

        throw new ForbiddenException("No access to this company.");
    }

    public static bool CanSeeCostAndMargin(IExecutionContext context) =>
        context.IsSystem || context.Permissions.Contains(Permissions.ViewCostAndMargin);
}
