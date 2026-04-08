# API Resource Management Specification

## Purpose
Define API resource registration, client associations, and scope validation requirements.

## Requirements

### Requirement: ApiResource Aggregate Root
The system SHALL define an `ApiResource` aggregate root in `NexusAuth.Domain` representing a registered API resource that can be protected by the authorization server. `ApiResource` SHALL contain: `Id` (GUID), `Name` (string, unique, used as scope identifier), `DisplayName` (string), `Description` (string, nullable), `IsActive` (bool), `CreatedAt` (DateTimeOffset).

#### Scenario: ApiResource created with valid name
- **WHEN** a new `ApiResource` is instantiated with a unique name and display name
- **THEN** `IsActive` SHALL default to `true`
- **THEN** `CreatedAt` SHALL be set to the current UTC time

#### Scenario: ApiResource name used as scope identifier
- **WHEN** an OAuth2 client requests scope `orders.read`
- **THEN** the system SHALL be able to look up an `ApiResource` with `Name == "orders.read"`

### Requirement: ApiResource Repository Interface
The system SHALL define `IApiResourceRepository` in `NexusAuth.Domain` with:
- `Task<ApiResource?> FindByNameAsync(string name, CancellationToken ct)`
- `Task<ApiResource?> FindByIdAsync(Guid id, CancellationToken ct)`
- `Task<IReadOnlyList<ApiResource>> GetAllActiveAsync(CancellationToken ct)`
- `Task AddAsync(ApiResource resource, CancellationToken ct)`

#### Scenario: List all active API resources
- **WHEN** `GetAllActiveAsync` is called
- **THEN** it SHALL return all `ApiResource` records where `IsActive == true`

#### Scenario: Find by name
- **WHEN** `FindByNameAsync` is called with an existing resource name
- **THEN** it SHALL return the matching `ApiResource`

### Requirement: ClientApiResource Association
The system SHALL define a `ClientApiResource` value object or join entity in `NexusAuth.Domain` representing the many-to-many relationship between `OAuthClient` and `ApiResource`. It SHALL contain: `ClientId` (GUID, references `OAuthClient.Id`), `ApiResourceId` (GUID, references `ApiResource.Id`). The composite `(ClientId, ApiResourceId)` SHALL be unique.

#### Scenario: Client associated with API resource
- **WHEN** a `ClientApiResource` record is created for a given client and resource
- **THEN** the association SHALL be persisted to the `client_api_resources` table

### Requirement: OAuthClient API Resource Association Repository
The system SHALL define `IClientApiResourceRepository` in `NexusAuth.Domain` with:
- `Task<IReadOnlyList<ApiResource>> GetResourcesByClientIdAsync(Guid clientId, CancellationToken ct)`
- `Task AddAsync(ClientApiResource association, CancellationToken ct)`
- `Task RemoveAsync(Guid clientId, Guid apiResourceId, CancellationToken ct)`

#### Scenario: Get API resources for a client
- **WHEN** `GetResourcesByClientIdAsync` is called with a valid client id
- **THEN** it SHALL return all `ApiResource` entities associated with that client

#### Scenario: Remove API resource from client
- **WHEN** `RemoveAsync` is called with a valid clientId and apiResourceId
- **THEN** the association SHALL be deleted from `client_api_resources`

### Requirement: ApiResource Management Application Service
The system SHALL provide an `ApiResourceService` in `NexusAuth.Application` for managing API resources and client associations.

#### Scenario: Register new API resource
- **WHEN** `ApiResourceService.RegisterAsync` is called with a unique name and display name
- **THEN** a new `ApiResource` SHALL be created and persisted

#### Scenario: Register duplicate API resource name fails
- **WHEN** `ApiResourceService.RegisterAsync` is called with an already-existing name
- **THEN** the service SHALL throw a domain exception

#### Scenario: Assign API resource to client
- **WHEN** `ApiResourceService.AssignToClientAsync` is called with a valid clientId and apiResourceId
- **THEN** a `ClientApiResource` association SHALL be created and persisted

#### Scenario: Revoke API resource from client
- **WHEN** `ApiResourceService.RevokeFromClientAsync` is called with a valid clientId and apiResourceId
- **THEN** the association SHALL be removed

#### Scenario: List resources available for a client (management view)
- **WHEN** `ApiResourceService.GetClientResourcesAsync` is called with a clientId
- **THEN** it SHALL return the list of `ApiResource` entities currently assigned to that client

#### Scenario: List all resources (for management UI selection)
- **WHEN** `ApiResourceService.GetAllActiveResourcesAsync` is called
- **THEN** it SHALL return all active `ApiResource` entities for display in the management interface

### Requirement: Scope Validation Against ApiResources
When an OAuth2 flow requests a scope, the application layer SHALL validate that each requested scope exists as an active `ApiResource.Name` AND is present in the `OAuthClient.AllowedScopes`.

#### Scenario: Valid scope accepted
- **WHEN** an authorization request includes scope `orders.read`
- **THEN** the system SHALL verify `ApiResource` with `Name == "orders.read"` exists and is active
- **THEN** the system SHALL verify `OAuthClient.AllowedScopes` contains `orders.read`
- **THEN** the scope SHALL be granted

#### Scenario: Unknown scope rejected
- **WHEN** an authorization request includes a scope that has no matching `ApiResource`
- **THEN** the request SHALL be rejected with an `invalid_scope` error
