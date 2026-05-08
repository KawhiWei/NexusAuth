using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public class OAuthClientSecretConfiguration : IEntityTypeConfiguration<OAuthClientSecret>
{
    public void Configure(EntityTypeBuilder<OAuthClientSecret> builder)
    {
        builder.ToTable("oauth_client_secrets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ClientId)
            .HasColumnName("client_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.PlainValue)
            .HasColumnName("plain_value")
            .HasColumnType("text");

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => new { x.ClientId, x.Type });
    }
}
