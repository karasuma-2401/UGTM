using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class TopicRegistrationEmailConfiguration : IEntityTypeConfiguration<TopicRegistrationEmail>
{
    public void Configure(EntityTypeBuilder<TopicRegistrationEmail> builder)
    {
        builder.ToTable("topic_registration_email", "thesis");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProtectionPurpose).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProtectedPayload).HasColumnType("text").IsRequired();
        builder.Property(x => x.LastErrorCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.ProtectionPurpose, x.ProcessedAtUtc, x.NextAttemptAtUtc })
            .HasDatabaseName("ix_topic_registration_email_dispatch");
        builder.HasIndex(x => new { x.MemberId, x.Type, x.ProcessedAtUtc })
            .HasDatabaseName("ix_topic_registration_email_member");
    }
}
