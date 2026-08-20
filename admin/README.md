# NexusAuth Admin

`NexusAuth.Admin.sln` is the development solution for the administration site.
The Admin API and Dashboard are deployed independently from the SSO host, while
the domain, application, persistence, and shared projects have a single source
under the repository-level `src` directory.

## Projects

- `src/NexusAuth.Workbench.Api`: Admin API and BFF.
- `src/NexusAuth.Workbench.Dashboard`: Admin web application.
- `src/NexusAuth.Extension`: Admin-specific OIDC client integration.
- `../src/NexusAuth.Domain`: Shared domain model.
- `../src/NexusAuth.Application`: Shared application services.
- `../src/NexusAuth.Persistence`: Shared persistence implementation.
- `../src/NexusAuth.Shared`: Shared infrastructure utilities.

Do not copy the shared projects into `admin`. Changes to shared entities,
services, or repositories must be made in the repository-level `src` projects.

## Build

```bash
dotnet build admin/NexusAuth.Admin.sln
npm --prefix admin/src/NexusAuth.Workbench.Dashboard run build
```
