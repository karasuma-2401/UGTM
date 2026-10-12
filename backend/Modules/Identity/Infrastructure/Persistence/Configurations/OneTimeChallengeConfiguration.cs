using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class OneTimeChallengeConfiguration
    : IEntityTypeConfiguration<OneTimeChallenge>
{
    public void Configure(EntityTypeBuilder<OneTimeChallenge> builder)
    {
        builder.ToTable("one_time_challenge", "identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Selector).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SecretHash).HasColumnType("bytea").IsRequired();
        builder.Property(x => x.DestinationSnapshot).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Selector).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}