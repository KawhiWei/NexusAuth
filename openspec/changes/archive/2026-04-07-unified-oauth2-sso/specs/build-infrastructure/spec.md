## ADDED Requirements

### Requirement: Central NuGet Package Version Management
`Directory.Packages.props` SHALL declare version entries for all NuGet packages used across the solution. Individual `.csproj` files SHALL reference packages without specifying versions. The property `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` SHALL be present in `Directory.Build.props`.

#### Scenario: New OAuth2 packages declared in Directory.Packages.props
- **WHEN** a developer adds a new `<PackageVersion>` entry to `Directory.Packages.props`
- **THEN** all projects referencing that package via `<PackageReference Include="..." />` (without `Version`) SHALL resolve the declared version automatically

#### Scenario: JWT Bearer package declared
- **WHEN** `Directory.Packages.props` is loaded
- **THEN** `Microsoft.AspNetCore.Authentication.JwtBearer` SHALL have a declared `<PackageVersion>` entry

#### Scenario: BCrypt package declared
- **WHEN** `Directory.Packages.props` is loaded
- **THEN** `BCrypt.Net-Next` SHALL have a declared `<PackageVersion>` entry

#### Scenario: System.IdentityModel.Tokens.Jwt package declared
- **WHEN** `Directory.Packages.props` is loaded
- **THEN** `System.IdentityModel.Tokens.Jwt` SHALL have a declared `<PackageVersion>` entry

#### Scenario: EF Design tools declared
- **WHEN** `Directory.Packages.props` is loaded
- **THEN** `Microsoft.EntityFrameworkCore.Design` SHALL have a declared `<PackageVersion>` entry

### Requirement: Unified Target Framework
All projects in the solution SHALL target `net9.0` as defined by `Directory.Build.props`. No individual `.csproj` file SHALL override `<TargetFramework>`.

#### Scenario: All projects build against net9.0
- **WHEN** `dotnet build` is executed at the solution root
- **THEN** all projects SHALL compile targeting `net9.0` without errors
