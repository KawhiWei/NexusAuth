# Client Management Specification

## Purpose
Define OAuth2 client registration, validation, and repository requirements.

## Requirements

### Requirement: OAuth2 Client Aggregate Root
The system SHALL define an `OAuthClient` aggregate root in `NexusAuth.Domain` representing a registered OAuth2 application. `OAuthClient` SHALL contain: `Id` (GUID), `ClientId` (string, unique), `ClientSecretHash` (string, BCrypt-hashed), `ClientName` (string), `RedirectUris` (list of strings), `AllowedScopes` (list of strings), `AllowedGrantTypes` (list of strings), `IsActive` (bool), `CreatedAt` (DateTimeOffset).

#### Scenario: OAuth2 Client created
- **WHEN** a new `OAuthClient` is instantiated with a ClientId and raw ClientSecret
- **THEN** `ClientSecretHash` SHALL store the BCrypt hash of the raw ClientSecret
- **THEN** `IsActive` SHALL default to `true`

#### Scenario: OAuth2 Client with multiple redirect URIs
- **WHEN** an `OAuthClient` is created with multiple redirect URIs
- **THEN** all redirect URIs SHALL be stored and retrievable

### Requirement: OAuth2 Client Secret Verification
The system SHALL provide a method on `OAuthClient` to verify a raw client secret against the stored BCrypt hash.

#### Scenario: Valid client secret accepted
- **WHEN** `OAuthClient.VerifyClientSecret(rawSecret)` is called with the correct secret
- **THEN** it SHALL return `true`

#### Scenario: Invalid client secret rejected
- **WHEN** `OAuthClient.VerifyClientSecret(rawSecret)` is called with an incorrect secret
- **THEN** it SHALL return `false`

### Requirement: Redirect URI Validation
`OAuthClient` SHALL provide a method to validate whether a given redirect URI is in its allowed list.

#### Scenario: Valid redirect URI accepted
- **WHEN** `OAuthClient.IsValidRedirectUri(uri)` is called with a URI present in `RedirectUris`
- **THEN** it SHALL return `true`

#### Scenario: Invalid redirect URI rejected
- **WHEN** `OAuthClient.IsValidRedirectUri(uri)` is called with a URI not in `RedirectUris`
- **THEN** it SHALL return `false`

### Requirement: Grant Type Validation
`OAuthClient` SHALL provide a method to check whether a given grant type is permitted.

#### Scenario: Allowed grant type accepted
- **WHEN** `OAuthClient.IsGrantTypeAllowed(grantType)` is called with a grant type in `AllowedGrantTypes`
- **THEN** it SHALL return `true`

#### Scenario: Disallowed grant type rejected
- **WHEN** `OAuthClient.IsGrantTypeAllowed(grantType)` is called with a grant type not in `AllowedGrantTypes`
- **THEN** it SHALL return `false`

### Requirement: OAuth2 Client Repository Interface
The system SHALL define `IOAuthClientRepository` in `NexusAuth.Domain` with:
- `Task<OAuthClient?> FindByClientIdAsync(string clientId, CancellationToken ct)`
- `Task AddAsync(OAuthClient client, CancellationToken ct)`

#### Scenario: Find client by ClientId
- **WHEN** `FindByClientIdAsync` is called with an existing ClientId
- **THEN** it SHALL return the matching `OAuthClient`

#### Scenario: Find non-existent client
- **WHEN** `FindByClientIdAsync` is called with a non-existent ClientId
- **THEN** it SHALL return `null`

### Requirement: Client Management Application Service
The system SHALL provide a `ClientService` in `NexusAuth.Application` to register and validate OAuth2 clients.

#### Scenario: Register new OAuth2 client
- **WHEN** `ClientService.RegisterClientAsync` is called with valid parameters
- **THEN** a new `OAuthClient` SHALL be created and persisted

#### Scenario: Validate client credentials
- **WHEN** `ClientService.ValidateClientAsync` is called with a valid ClientId and correct ClientSecret
- **THEN** it SHALL return the corresponding `OAuthClient`

#### Scenario: Invalid client credentials rejected
- **WHEN** `ClientService.ValidateClientAsync` is called with invalid credentials
- **THEN** it SHALL return `null`
