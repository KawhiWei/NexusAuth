## ADDED Requirements

### Requirement: Refresh Token Entity
The system SHALL define a `RefreshToken` entity in `NexusAuth.Domain` with: `Id` (GUID), `Token` (string, unique, cryptographically random), `ClientId` (string), `UserId` (GUID), `Scope` (string), `IsRevoked` (bool), `ExpiresAt` (DateTimeOffset), `CreatedAt` (DateTimeOffset).

#### Scenario: Refresh token created
- **WHEN** a new `RefreshToken` is instantiated
- **THEN** `Token` SHALL be a cryptographically random URL-safe string of at least 64 characters
- **THEN** `IsRevoked` SHALL default to `false`
- **THEN** `ExpiresAt` SHALL be set to 30 days from creation time

### Requirement: Refresh Token Repository Interface
The system SHALL define `IRefreshTokenRepository` in `NexusAuth.Domain` with:
- `Task<RefreshToken?> FindByTokenAsync(string token, CancellationToken ct)`
- `Task AddAsync(RefreshToken token, CancellationToken ct)`
- `Task RevokeAsync(Guid id, CancellationToken ct)`
- `Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)`

#### Scenario: Find refresh token
- **WHEN** `FindByTokenAsync` is called with an existing token string
- **THEN** it SHALL return the matching `RefreshToken`

#### Scenario: Revoke all tokens for user
- **WHEN** `RevokeAllForUserAsync` is called with a UserId
- **THEN** all `RefreshToken` records for that user SHALL have `IsRevoked = true`

### Requirement: JWT Access Token Issuance
The system SHALL provide a `TokenService` in `NexusAuth.Application` that issues JWT Access Tokens signed with HS256.

The JWT payload SHALL include: `sub` (user ID or client ID), `client_id`, `scope`, `iat`, `exp`, `jti` (unique token identifier).

#### Scenario: Issue Access Token for user
- **WHEN** `TokenService.IssueAccessTokenAsync` is called with a UserId, ClientId, and Scope
- **THEN** a signed JWT string SHALL be returned
- **THEN** the JWT `exp` claim SHALL be 1 hour from issuance by default

#### Scenario: Issue Access Token for client credentials
- **WHEN** `TokenService.IssueAccessTokenAsync` is called with a ClientId (no UserId) and Scope
- **THEN** a signed JWT string SHALL be returned with `sub` equal to the ClientId

#### Scenario: JWT signature verified
- **WHEN** a JWT issued by `TokenService` is verified with the same signing key
- **THEN** verification SHALL succeed

#### Scenario: JWT with wrong key rejected
- **WHEN** a JWT is verified with a different signing key
- **THEN** verification SHALL fail

### Requirement: Refresh Token Issuance and Rotation
`TokenService` SHALL issue and manage Refresh Tokens. Upon each use, the old Refresh Token SHALL be revoked and a new one issued (token rotation).

#### Scenario: Issue Refresh Token
- **WHEN** `TokenService.IssueRefreshTokenAsync` is called with a UserId, ClientId, and Scope
- **THEN** a new `RefreshToken` SHALL be persisted and its `Token` string returned

#### Scenario: Use Refresh Token to get new tokens
- **WHEN** `TokenService.RefreshAsync` is called with a valid, non-expired, non-revoked refresh token string
- **THEN** the old `RefreshToken` SHALL be revoked
- **THEN** a new `RefreshToken` SHALL be issued and persisted
- **THEN** a new JWT Access Token SHALL be returned

#### Scenario: Expired Refresh Token rejected
- **WHEN** `TokenService.RefreshAsync` is called with a refresh token whose `ExpiresAt` is in the past
- **THEN** it SHALL return a failure result

#### Scenario: Revoked Refresh Token rejected
- **WHEN** `TokenService.RefreshAsync` is called with a refresh token where `IsRevoked == true`
- **THEN** it SHALL return a failure result

### Requirement: Refresh Token Revocation
`TokenService` SHALL support explicit revocation of a Refresh Token.

#### Scenario: Revoke specific refresh token
- **WHEN** `TokenService.RevokeRefreshTokenAsync` is called with a valid token string
- **THEN** the corresponding `RefreshToken.IsRevoked` SHALL be set to `true`

#### Scenario: Revoke all tokens for user (logout all devices)
- **WHEN** `TokenService.RevokeAllUserTokensAsync` is called with a UserId
- **THEN** all refresh tokens for that user SHALL be revoked
