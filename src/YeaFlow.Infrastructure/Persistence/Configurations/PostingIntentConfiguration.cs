using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YeaFlow.Domain.Posting;
using YeaFlow.Domain.Tenancy;

namespace YeaFlow.Infrastructure.Persistence.Configurations;

internal sealed class PostingIntentConfiguration : IEntityTypeConfiguration<PostingIntent>
{
    public void Configure(EntityTypeBuilder<PostingIntent> builder)
    {
        builder.ToTable("posting_intents");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TransactionId).HasMaxLength(50).IsRequired();
        builder.Property(p => p.SapObject).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(p => p.State).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.LastErrorCode).HasMaxLength(100);
        builder.Property(p => p.LastErrorMessage).HasMaxLength(1000);
        builder.Property(p => p.Version).IsRowVersion();
        builder.Ignore(p => p.IsTerminal);

        // Duplicate clicks and worker retries collapse onto one intent per transaction ID.
        builder.HasIndex(p => new { p.TenantId, p.TransactionId }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.State, p.NextAttemptAt });
        builder.HasOne<Company>().WithMany().HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
