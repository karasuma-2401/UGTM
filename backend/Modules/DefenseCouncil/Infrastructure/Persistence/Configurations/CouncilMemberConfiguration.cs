namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class CouncilMemberConfiguration : IEntityTypeConfiguration<CouncilMember>
{
    public void Configure(EntityTypeBuilder<CouncilMember> builder)
    {
        builder.HasIndex(x => new { x.CouncilId, x.UserId })
            .HasDatabaseName("ix_council_member_council_id_user_id")
            .IsUnique();

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_council_member_user_id");

        builder.ToTable("council_member", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleInCouncil)
            .IsRequired();

        builder.HasOne(x => x.Council)
            .WithMany(c => c.Members)
            .HasForeignKey(x => x.CouncilId)
            .HasConstraintName("fk_council_member_council_council_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
