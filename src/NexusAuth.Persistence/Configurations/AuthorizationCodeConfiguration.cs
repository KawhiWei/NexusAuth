using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>
{
    public void Configure(EntityTypeBuilder<AuthorizationCode> builder)
    {
        builder.ToTable("authorization_codes");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.Code)
            .HasColumnName("code")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(a => a.RedirectUri)
            .HasColumnName("redirect_uri")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(a => a.Scope)
            .HasColumnName("scope")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(a => a.CodeChallenge)
            .HasColumnName("code_challenge")
            .HasMaxLength(256);

        builder.Property(a => a.CodeChallengeMethod)
            .HasColumnName("code_challenge_method")
            .HasMaxLength(10);

        builder.Property(a => a.IsUsed)
            .HasColumnName("is_used")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(a => a.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(a => a.Code).IsUnique();
    }
}
