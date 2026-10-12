using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthenticationSessionConfiguration
    : IEntityTypeConfiguration<AuthenticationSession>
{
    public void Configure(EntityTypeBuilder<AuthenticationSession> builder)
    {
        builder.ToTable("authentication_session", "identity");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RevocationReason).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.UserAgentSummary).HasMaxLength(300);
        builder.Property(x => x.IpAddressHash).HasMaxLength(128);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(x => new { x.UserId, x.RevokedAt, x.AbsoluteExpiresAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.RefreshTokens).WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}