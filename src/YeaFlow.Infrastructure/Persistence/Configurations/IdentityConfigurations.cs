using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YeaFlow.Domain.Identity;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Infrastructure.Persistence.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.ExternalSubject).HasMaxLength(200);
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
        builder.HasIndex(u => new { u.TenantId, u.IsActive });
        builder.PrimitiveCollection(u => u.Roles).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(u => u.RequiresMfa);
        builder.HasMany(u => u.CompanyAccess).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.CompanyAccess).HasField("_companyAccess").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserCompanyAccessConfiguration : IEntityTypeConfiguration<UserCompanyAccess>
{
    public void Configure(EntityTypeBuilder<UserCompanyAccess> builder)
    {
        builder.ToTable("user_company_access");
        builder.HasKey(a => new { a.UserId, a.CompanyId });
        builder.HasOne<Company>().WithMany().HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
