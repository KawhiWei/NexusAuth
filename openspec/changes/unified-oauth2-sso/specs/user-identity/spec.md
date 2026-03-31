## ADDED Requirements

### Requirement: User Aggregate Root
The system SHALL define a `User` aggregate root in `NexusAuth.Domain` representing a system user. The `User` SHALL contain: `Id` (GUID), `Username` (unique), `PasswordHash` (BCrypt), `Nickname` (string, required), `Gender` (enum: Unknown=0, Male=1, Female=2), `Ethnicity` (string, nullable), `Email` (unique, nullable), `PhoneNumber` (unique, nullable), `IsActive` (bool), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset).

#### Scenario: User created with username and password
- **WHEN** a new `User` is instantiated with a username, raw password, and nickname
- **THEN** the `PasswordHash` SHALL be a BCrypt hash of the raw password
- **THEN** `IsActive` SHALL default to `true`
- **THEN** `Gender` SHALL default to `Unknown` (0)
- **THEN** `CreatedAt` SHALL be set to the current UTC time

#### Scenario: User created with nickname
- **WHEN** a new `User` is instantiated with a non-empty nickname
- **THEN** the `Nickname` property SHALL store the nickname

#### Scenario: User created with gender
- **WHEN** a new `User` is instantiated with a specific gender value
- **THEN** the `Gender` property SHALL store the provided enum value (Unknown, Male, or Female)

#### Scenario: User created with ethnicity
- **WHEN** a new `User` is instantiated with a non-null ethnicity
- **THEN** the `Ethnicity` property SHALL store the ethnicity string

#### Scenario: User created with email
- **WHEN** a new `User` is instantiated with a non-null email
- **THEN** the `Email` property SHALL store the email in lowercase-normalized form

#### Scenario: User created with phone number
- **WHEN** a new `User` is instantiated with a non-null phone number
- **THEN** the `PhoneNumber` property SHALL store the phone number

### Requirement: Password Verification
The system SHALL provide a method on `User` to verify a raw password against the stored BCrypt hash.

#### Scenario: Correct password verification
- **WHEN** `User.VerifyPassword(rawPassword)` is called with the correct password
- **THEN** the method SHALL return `true`

#### Scenario: Incorrect password rejected
- **WHEN** `User.VerifyPassword(rawPassword)` is called with an incorrect password
- **THEN** the method SHALL return `false`

### Requirement: User Repository Interface
The system SHALL define `IUserRepository` in `NexusAuth.Domain` with the following methods:
- `Task<User?> FindByUsernameAsync(string username, CancellationToken ct)`
- `Task<User?> FindByEmailAsync(string email, CancellationToken ct)`
- `Task<User?> FindByPhoneNumberAsync(string phoneNumber, CancellationToken ct)`
- `Task<User?> FindByIdAsync(Guid id, CancellationToken ct)`
- `Task AddAsync(User user, CancellationToken ct)`

#### Scenario: Find user by username
- **WHEN** `FindByUsernameAsync` is called with an existing username
- **THEN** it SHALL return the matching `User`

#### Scenario: Find user by username not found
- **WHEN** `FindByUsernameAsync` is called with a non-existent username
- **THEN** it SHALL return `null`

### Requirement: User Registration Application Service
The system SHALL provide a `UserService` in `NexusAuth.Application` that registers new users and validates login credentials.

#### Scenario: Register user with unique username
- **WHEN** `UserService.RegisterAsync` is called with a unique username, valid password, and nickname
- **THEN** a new `User` SHALL be created, persisted, and its `Id` returned

#### Scenario: Register user with duplicate username fails
- **WHEN** `UserService.RegisterAsync` is called with an already-existing username
- **THEN** the service SHALL throw a domain exception indicating the conflict

#### Scenario: Validate credentials by username and password
- **WHEN** `UserService.ValidateCredentialsAsync` is called with a valid username and correct password
- **THEN** it SHALL return the corresponding `User`

#### Scenario: Validate credentials by email and password
- **WHEN** `UserService.ValidateCredentialsAsync` is called with a valid email and correct password
- **THEN** it SHALL return the corresponding `User`

#### Scenario: Validate credentials by phone number and password
- **WHEN** `UserService.ValidateCredentialsAsync` is called with a valid phone number and correct password
- **THEN** it SHALL return the corresponding `User`

#### Scenario: Invalid credentials rejected
- **WHEN** `UserService.ValidateCredentialsAsync` is called with wrong credentials
- **THEN** it SHALL return `null`
