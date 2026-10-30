using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YeaFlow.Domain.Connectors;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Infrastructure.Persistence.Configurations;

internal sealed class ConnectorConfiguration : IEntityTypeConfiguration<Connector>
{
    public void Configure(EntityTypeBuilder<Connector> builder)
    {
        builder.ToTable("connectors");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.SecretHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(c => c.Version).HasMaxLength(50);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConnectorCommandConfiguration : IEntityTypeConfiguration<ConnectorCommand>
{
    public void Configure(EntityTypeBuilder<ConnectorCommand> builder)
    {
        builder.ToTable("connector_commands");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.SapCompanyDb).HasMaxLength(100).IsRequired();
        builder.Property(c => c.SapObject).HasMaxLength(64).IsRequired();
        builder.Property(c => c.TransactionId).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Payload).HasColumnType("jsonb");
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.ErrorCode).HasMaxLength(100);
        builder.Property(c => c.ErrorMessage).HasMaxLength(1000);
        builder.Property(c => c.Version).IsRowVersion();
        builder.HasIndex(c => new { c.ConnectorId, c.State, c.CreatedAt });
        builder.HasOne<Connector>().WithMany().HasForeignKey(c => c.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
