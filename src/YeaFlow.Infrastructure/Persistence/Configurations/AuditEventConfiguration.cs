using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YeaFlow.Domain.Audit;

namespace YeaFlow.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Result).HasMaxLength(100).IsRequired();
        builder.Property(a => a.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(a => a.SapReference).HasMaxLength(100);
        builder.Property(a => a.Detail).HasMaxLength(2000);
        builder.HasIndex(a => new { a.TenantId, a.OccurredAt });
    }
}
