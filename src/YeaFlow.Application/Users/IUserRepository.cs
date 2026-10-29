using YeaFlow.Domain.Identity;

namespace YeaFlow.Application.Users;

public interface IUserRepository
{
    Task<AppUser?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Active people in the current tenant across all its companies; each person counts once.</summary>
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
}
