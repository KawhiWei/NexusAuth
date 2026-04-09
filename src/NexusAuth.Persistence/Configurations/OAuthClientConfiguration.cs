using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexusAuth.Domain.AggregateRoots.OAuthClients;

namespace NexusAuth.Persistence.Configurations;

public class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("oauth_clients");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(128)
            .IsRequired();

        var clientSecretsConverter = new ValueConverter<List<OAuthClientSecret>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<OAuthClientSecret>>(v, (JsonSerializerOptions?)null) ?? new List<OAuthClientSecret>());

        var clientSecretsComparer = new ValueComparer<List<OAuthClientSecret>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<OAuthClientSecret>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        builder.Property(c => c.ClientSecrets)
            .HasColumnName("client_secrets")
            .HasColumnType("jsonb")
            .HasConversion(clientSecretsConverter)
            .IsRequired()
            .HasDefaultValueSql("'[]'::jsonb")
            .Metadata.SetValueComparer(clientSecretsComparer);

        builder.Property(c => c.TokenEndpointAuthMethod)
            .HasColumnName("token_endpoint_auth_method")
            .HasMaxLength(64)
            .IsRequired()
            .HasDefaultValue("client_secret_basic");

        builder.Property(c => c.ClientName)
            .HasColumnName("client_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        // Use explicit ValueConverter for List<string> ↔ jsonb to avoid Npgsql 8+ dynamic JSON requirement
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

        builder.Property(c => c.RedirectUris)
            .HasColumnName("redirect_uris")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter)
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(c => c.PostLogoutRedirectUris)
            .HasColumnName("post_logout_redirect_uris")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter)
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(c => c.AllowedScopes)
            .HasColumnName("allowed_scopes")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter)
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(c => c.AllowedGrantTypes)
            .HasColumnName("allowed_grant_types")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter)
            .Metadata.SetValueComparer(stringListComparer);

        builder.Property(c => c.RequirePkce)
            .HasColumnName("require_pkce")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(c => c.ClientId).IsUnique();
    }
}
