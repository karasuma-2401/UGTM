using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class StudentRegistrationPenaltyConfiguration : IEntityTypeConfiguration<StudentRegistrationPenalty>
{
    public void Configure(EntityTypeBuilder<StudentRegistrationPenalty> builder)
    {
        builder.ToTable("student_registration_penalty", "thesis");

        builder.HasKey(x => x.StudentId);

        builder.Property(x => x.ViolationCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsRegistrationBlocked)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.LastViolationAtUtc);
        builder.Property(x => x.BlockedAtUtc);

        builder.Property(x => x.Version)
            .IsRowVersion();
    }
}
