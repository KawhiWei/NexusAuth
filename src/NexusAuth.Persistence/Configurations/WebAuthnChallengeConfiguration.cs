using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class WebAuthnChallengeConfiguration : IEntityTypeConfiguration<WebAuthnChallenge>
{
    public void Configure(EntityTypeBuilder<WebAuthnChallenge> builder)
    {
        builder.ToTable("webauthn_challenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id).HasColumnName("id");
        builder.Property(challenge => challenge.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(challenge => challenge.Purpose).HasColumnName("purpose").HasMaxLength(32).IsRequired();
        builder.Property(challenge => challenge.UserId).HasColumnName("user_id");
        builder.Property(challenge => challenge.OptionsJson).HasColumnName("options_json").HasColumnType("jsonb").IsRequired();
        builder.Property(challenge => challenge.ReturnUrl).HasColumnName("return_url").HasColumnType("text");
        builder.Property(challenge => challenge.RememberMe).HasColumnName("remember_me").IsRequired();
        builder.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(challenge => challenge.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(challenge => challenge.CreatedAt).HasColumnName("created_at").IsRequired();
        // 使用 PostgreSQL 系统列 xmin 检测并发消费，避免同一 challenge 被重复验证。
        builder.Property<uint>("xmin").IsRowVersion();
        builder.HasIndex(challenge => challenge.TokenHash).IsUnique().HasDatabaseName("ux_webauthn_challenges_token_hash");
        builder.HasIndex(challenge => new { challenge.Purpose, challenge.ExpiresAt }).HasDatabaseName("ix_webauthn_challenges_purpose_expiry");
    }
}
