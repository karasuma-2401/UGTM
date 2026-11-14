namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class SubmissionProductLinkConfiguration : IEntityTypeConfiguration<SubmissionProductLink>
{
    public void Configure(EntityTypeBuilder<SubmissionProductLink> builder)
    {
        builder.ToTable("submission_product_link", "thesis", table =>
            table.HasCheckConstraint("ck_submission_product_link_version_no", "version_no > 0"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.VersionNo, x.DisplayOrder })
            .HasDatabaseName("ix_submission_product_link_entity_version");

        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.VersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.Category)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(80);

        builder.Property(x => x.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
