## ADDED Requirements

### Requirement: Authorization Code Entity
The system SHALL define an `AuthorizationCode` entity in `NexusAuth.Domain` with: `Id` (GUID), `Code` (string, unique, cryptographically random), `ClientId` (string), `UserId` (GUID), `RedirectUri` (string), `Scope` (string), `CodeChallenge` (string, nullable), `CodeChallengeMethod` (string, nullable, "S256" or "plain"), `IsUsed` (bool), `ExpiresAt` (DateTimeOffset), `CreatedAt` (DateTimeOffset).

#### Scenario: Authorization code created
- **WHEN** a new `AuthorizationCode` is instantiated
- **THEN** `Code` SHALL be a cryptographically random URL-safe string of at least 32 characters
- **THEN** `IsUsed` SHALL default to `false`
- **THEN** `ExpiresAt` SHALL be set to 10 minutes from creation time

#### Scenario: Authorization code with PKCE
- **WHEN** a new `AuthorizationCode` is instantiated with a code challenge
- **THEN** `CodeChallenge` and `CodeChallengeMethod` SHALL be stored

### Requirement: Authorization Code Repository Interface
The system SHALL define `IAuthorizationCodeRepository` in `NexusAuth.Domain` with:
- `Task<AuthorizationCode?> FindByCodeAsync(string code, CancellationToken ct)`
- `Task AddAsync(AuthorizationCode code, CancellationToken ct)`
- `Task MarkUsedAsync(Guid id, CancellationToken ct)`

#### Scenario: Find authorization code
- **WHEN** `FindByCodeAsync` is called with an existing code string
- **THEN** it SHALL return the matching `AuthorizationCode`

### Requirement: Authorization Code Flow with PKCE
The system SHALL provide an `AuthorizationService` in `NexusAuth.Application` implementing the Authorization Code grant with PKCE (RFC 7636).

#### Scenario: Generate authorization code
- **WHEN** `AuthorizationService.GenerateCodeAsync` is called with a valid UserId, ClientId, RedirectUri, Scope, and optional PKCE challenge
- **THEN** a new `AuthorizationCode` SHALL be persisted and its `Code` string returned

#### Scenario: Exchange authorization code for token params (valid PKCE S256)
- **WHEN** `AuthorizationService.ValidateAndConsumeCodeAsync` is called with a valid code and the matching code_verifier
- **THEN** the service SHALL verify `SHA256(code_verifier) == base64url(CodeChallenge)`
- **THEN** the `AuthorizationCode.IsUsed` SHALL be set to `true`
- **THEN** it SHALL return the associated UserId, ClientId, and Scope

#### Scenario: Expired authorization code rejected
- **WHEN** `AuthorizationService.ValidateAndConsumeCodeAsync` is called with a code whose `ExpiresAt` is in the past
- **THEN** it SHALL return a failure result

#### Scenario: Already-used authorization code rejected
- **WHEN** `AuthorizationService.ValidateAndConsumeCodeAsync` is called with a code where `IsUsed == true`
- **THEN** it SHALL return a failure result

#### Scenario: PKCE verifier mismatch rejected
- **WHEN** `AuthorizationService.ValidateAndConsumeCodeAsync` is called with an incorrect code_verifier
- **THEN** it SHALL return a failure result

### Requirement: Client Credentials Flow
`AuthorizationService` SHALL support the Client Credentials grant for service-to-service authentication.

#### Scenario: Client credentials grant validated
- **WHEN** `AuthorizationService.ValidateClientCredentialsAsync` is called with a valid ClientId, ClientSecret, and an allowed scope
- **THEN** it SHALL return a success result with the granted scope

#### Scenario: Client credentials with invalid secret rejected
- **WHEN** `AuthorizationService.ValidateClientCredentialsAsync` is called with an invalid ClientSecret
- **THEN** it SHALL return a failure result

#### Scenario: Client credentials with disallowed grant type rejected
- **WHEN** `AuthorizationService.ValidateClientCredentialsAsync` is called for a client that does not have `client_credentials` in `AllowedGrantTypes`
- **THEN** it SHALL return a failure result
