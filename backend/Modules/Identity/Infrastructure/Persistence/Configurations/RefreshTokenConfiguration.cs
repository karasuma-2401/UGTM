using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_token", "identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Selector).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ValidatorHash).HasColumnType("bytea").IsRequired();
        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(x => x.Selector).IsUnique();
        builder.HasIndex(x => x.SessionId);
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(x => x.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}