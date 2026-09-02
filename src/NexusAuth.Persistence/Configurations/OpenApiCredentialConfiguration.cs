using NexusAuth.Domain.Entities;

namespace NexusAuth.Persistence.Configurations;

public sealed class OpenApiCredentialConfiguration : IEntityTypeConfiguration<OpenApiCredential>
{
    public void Configure(EntityTypeBuilder<OpenApiCredential> builder)
    {
        var converter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>());
        var comparer = new ValueComparer<List<string>>(
            (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null).GetHashCode(),
            value => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(value, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new List<string>());

        builder.ToTable("open_api_credentials");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(item => item.TokenHash).HasColumnName("token_hash").HasMaxLength(OpenApiCredential.TokenHashLength).IsRequired();
        builder.Property(item => item.TargetType).HasColumnName("target_type").HasMaxLength(32).IsRequired();
        builder.Property(item => item.Scopes).HasColumnName("scopes").HasColumnType("jsonb").HasConversion(converter).Metadata.SetValueComparer(comparer);
        builder.Property(item => item.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(item => item.ExpiresAt).HasColumnName("expires_at");
        builder.Property(item => item.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.RevokedAt).HasColumnName("revoked_at");
        builder.HasIndex(item => item.Name).IsUnique().HasDatabaseName("ix_open_api_credentials_name");
        builder.HasIndex(item => item.TokenHash).IsUnique().HasDatabaseName("ix_open_api_credentials_token_hash");
        builder.ToTable(table => table.HasCheckConstraint("ck_open_api_credentials_target_type", "target_type IN ('application', 'service_resource')"));
        builder.ToTable(table => table.HasCheckConstraint("ck_open_api_credentials_token_hash_base64url", "token_hash ~ '^[A-Za-z0-9_-]{43}$'"));
    }
}
