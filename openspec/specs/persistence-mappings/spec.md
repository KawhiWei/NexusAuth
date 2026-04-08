# Persistence Mappings Specification

## Purpose
Define Entity Framework persistence mappings and DbContext exposure requirements for domain entities.

## Requirements

### Requirement: User Entity Configuration
The system SHALL provide a `UserConfiguration : IEntityTypeConfiguration<User>` class in `NexusAuth.Persistence` mapping `User` to the `users` table.

Mappings SHALL include:
- Table name: `users`
- `Id` -> primary key, column `id`
- `Username` -> column `username`, max length 100, not null, unique index
- `PasswordHash` -> column `password_hash`, max length 256, not null
- `Email` -> column `email`, max length 256, nullable, unique index (when not null)
- `PhoneNumber` -> column `phone_number`, max length 20, nullable, unique index (when not null)
- `IsActive` -> column `is_active`, not null, default `true`
- `CreatedAt` -> column `created_at`, not null
- `UpdatedAt` -> column `updated_at`, not null

#### Scenario: UserConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `users` table configuration SHALL be applied via `ApplyConfigurationsFromAssembly`
- **THEN** `Username` SHALL have a unique index in the database schema

#### Scenario: Duplicate username rejected by database
- **WHEN** two `User` records with the same `Username` are inserted
- **THEN** the database SHALL reject the second insert with a unique constraint violation

### Requirement: OAuthClient Entity Configuration
The system SHALL provide an `OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>` class in `NexusAuth.Persistence` mapping `OAuthClient` to the `oauth_clients` table.

Mappings SHALL include:
- Table name: `oauth_clients`
- `Id` -> primary key, column `id`
- `ClientId` -> column `client_id`, max length 128, not null, unique index
- `ClientSecretHash` -> column `client_secret_hash`, max length 256, not null
- `ClientName` -> column `client_name`, max length 256, not null
- `RedirectUris` -> column `redirect_uris`, stored as JSON array
- `AllowedScopes` -> column `allowed_scopes`, stored as JSON array
- `AllowedGrantTypes` -> column `allowed_grant_types`, stored as JSON array
- `IsActive` -> column `is_active`, not null, default `true`
- `CreatedAt` -> column `created_at`, not null

#### Scenario: OAuthClientConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `oauth_clients` table configuration SHALL be applied
- **THEN** `ClientId` SHALL have a unique index

### Requirement: AuthorizationCode Entity Configuration
The system SHALL provide an `AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCode>` class in `NexusAuth.Persistence` mapping `AuthorizationCode` to the `authorization_codes` table.

Mappings SHALL include:
- Table name: `authorization_codes`
- `Id` -> primary key, column `id`
- `Code` -> column `code`, max length 256, not null, unique index
- `ClientId` -> column `client_id`, max length 128, not null
- `UserId` -> column `user_id`, not null
- `RedirectUri` -> column `redirect_uri`, max length 2048, not null
- `Scope` -> column `scope`, max length 512, not null
- `CodeChallenge` -> column `code_challenge`, max length 256, nullable
- `CodeChallengeMethod` -> column `code_challenge_method`, max length 10, nullable
- `IsUsed` -> column `is_used`, not null, default `false`
- `ExpiresAt` -> column `expires_at`, not null
- `CreatedAt` -> column `created_at`, not null

#### Scenario: AuthorizationCodeConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `authorization_codes` table configuration SHALL be applied
- **THEN** `Code` SHALL have a unique index

### Requirement: RefreshToken Entity Configuration
The system SHALL provide a `RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>` class in `NexusAuth.Persistence` mapping `RefreshToken` to the `refresh_tokens` table.

Mappings SHALL include:
- Table name: `refresh_tokens`
- `Id` -> primary key, column `id`
- `Token` -> column `token`, max length 512, not null, unique index
- `ClientId` -> column `client_id`, max length 128, not null
- `UserId` -> column `user_id`, not null
- `Scope` -> column `scope`, max length 512, not null
- `IsRevoked` -> column `is_revoked`, not null, default `false`
- `ExpiresAt` -> column `expires_at`, not null
- `CreatedAt` -> column `created_at`, not null

#### Scenario: RefreshTokenConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `refresh_tokens` table configuration SHALL be applied
- **THEN** `Token` SHALL have a unique index

### Requirement: DbSet Registration in NexusAuthDbContext
`NexusAuthDbContext` SHALL expose `DbSet<T>` properties for all domain entities: `Users`, `OAuthClients`, `ApiResources`, `ClientApiResources`, `AuthorizationCodes`, `RefreshTokens`.

#### Scenario: DbSet properties accessible on DbContext
- **WHEN** `NexusAuthDbContext` is instantiated
- **THEN** `DbContext.Users`, `DbContext.OAuthClients`, `DbContext.ApiResources`, `DbContext.ClientApiResources`, `DbContext.AuthorizationCodes`, `DbContext.RefreshTokens` SHALL all be accessible as non-null `DbSet<T>` properties

### Requirement: ApiResource Entity Configuration
The system SHALL provide an `ApiResourceConfiguration : IEntityTypeConfiguration<ApiResource>` class in `NexusAuth.Persistence` mapping `ApiResource` to the `api_resources` table.

Mappings SHALL include:
- Table name: `api_resources`
- `Id` -> primary key, column `id`
- `Name` -> column `name`, max length 128, not null, unique index
- `DisplayName` -> column `display_name`, max length 256, not null
- `Description` -> column `description`, text, nullable
- `IsActive` -> column `is_active`, not null, default `true`
- `CreatedAt` -> column `created_at`, not null

#### Scenario: ApiResourceConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `api_resources` table configuration SHALL be applied via `ApplyConfigurationsFromAssembly`
- **THEN** `Name` SHALL have a unique index

### Requirement: ClientApiResource Entity Configuration
The system SHALL provide a `ClientApiResourceConfiguration : IEntityTypeConfiguration<ClientApiResource>` class in `NexusAuth.Persistence` mapping `ClientApiResource` to the `client_api_resources` table.

Mappings SHALL include:
- Table name: `client_api_resources`
- `ClientId` -> column `client_id`, not null
- `ApiResourceId` -> column `api_resource_id`, not null
- Composite primary key: `(ClientId, ApiResourceId)`

#### Scenario: ClientApiResourceConfiguration applied to DbContext
- **WHEN** `NexusAuthDbContext.OnModelCreating` is called
- **THEN** the `client_api_resources` table configuration SHALL be applied
- **THEN** the composite primary key `(client_id, api_resource_id)` SHALL be enforced

### Requirement: No EF Migrations
The system SHALL NOT use EF Core Migrations to manage the database schema. The `Microsoft.EntityFrameworkCore.Design` package SHALL NOT be added to any project. Database tables SHALL be created manually by executing SQL scripts provided separately. The `NexusAuthDbContext` SHALL have migrations disabled (do not call `Database.Migrate()` on startup).

#### Scenario: DbContext starts without auto-migration
- **WHEN** `NexusAuthDbContext` is instantiated and used
- **THEN** it SHALL NOT automatically apply any schema changes to the database
- **THEN** the database schema is assumed to already exist, created via manual SQL execution
