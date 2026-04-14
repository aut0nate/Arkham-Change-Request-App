# AGENTS.md

## Project overview

Arkham Change Request is an ASP.NET Core 8 MVC application for submitting and managing IT change requests. The core workflow is:

1. Sign in with Microsoft Entra ID
2. Create a change request
3. Upload supporting attachments
4. Review, approve and update request status
5. Inspect the audit trail

## Tools, languages and frameworks

- C#
- .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server for production-oriented setups
- SQLite for lightweight local development
- Azure Blob Storage for production
- Local filesystem storage for lightweight development
- Microsoft Entra ID OpenID Connect
- Application Insights

## Build and test commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --launch-profile https
docker compose up --build
docker compose down
docker compose -f docker-compose.prod.yml up --build -d
docker compose -f docker-compose.prod.yml down
```

Local HTTPS development runs on `https://localhost:7015`.

## Configuration rules

- Non-secret defaults live in [`appsettings.json`](/Users/nathan/Dev/Arkham-Change-Request-App/appsettings.json)
- Local secrets belong in `appsettings.Development.json`, which is ignored by Git
- The example local configuration lives in [`appsettings.Development.example.json`](/Users/nathan/Dev/Arkham-Change-Request-App/appsettings.Development.example.json)
- Docker environment variables belong in `.env.docker`, which is ignored by Git
- Production Docker environment variables belong in `.env.production`, which is ignored by Git
- Production secrets must come from environment variables or a secret store

Important keys:

- `AzureAd:TenantId`
- `AzureAd:ClientId`
- `AzureAd:ClientSecret`
- `SqlConnectionString`
- `ConnectionStrings:Sqlite`
- `Database:Provider`
- `App:DisableHttpsRedirection`
- `DataProtection:Path`
- `Storage:Provider`
- `Storage:LocalPath`
- `AzureStorageConnectionString`
- `ApplicationInsightsConnectionString`

## Authentication and authorisation

- Authentication is direct Microsoft Entra ID, not Auth0
- Use the standard OIDC callback paths:
  - `/signin-oidc`
  - `/signout-callback-oidc`
- Development and production should use separate Entra app registrations
- Current first-pass authorisation is simple: authenticated users can access the app, including approval screens
- Group-based approver restrictions are intentionally deferred to a later change

## Storage and data

- Application data is stored through `ApplicationDbContext` using SQL Server or SQLite, depending on configuration
- Attachments are stored through `IBlobStorageService`
- The blob abstraction can target Azure Blob Storage for production or local filesystem storage for development
- `EnsureCreated()` is used on startup; there are no EF migrations in the repository yet
- Local Docker uses SQLite plus local filesystem uploads under the named Docker volume mounted at `/data`
- Local Docker persists application data in a named Docker volume mounted at `/data`
- Production Docker also persists application data in a named Docker volume mounted at `/data`

## Code style guidelines

- Follow the existing ASP.NET Core MVC structure and naming
- Keep changes small and local to the relevant feature
- Prefer simple configuration-driven behaviour over hard-coded environment branches
- Use British English in user-facing text, comments and documentation

## Testing instructions

- Run `dotnet test` after code changes
- Run `dotnet build` if you changed startup, configuration or dependency wiring
- For authentication changes, also test the full browser sign-in flow locally
- For workflow changes, manually verify create request, view request and sign-out

## Security considerations

- Never commit client secrets, storage keys or SQL credentials
- Keep `appsettings.Development.json` out of source control
- Prefer environment variables for production secrets
- Production must run behind HTTPS with a reverse proxy that forwards `X-Forwarded-Proto`

## Deployment notes

- Production is expected to run behind Nginx or Caddy on a VPS
- Use a real HTTPS domain for the production Entra redirect URI
- Keep dev and prod app registrations separate
- The app already enables forwarded headers to support reverse-proxy deployment
- Local Docker runs on `http://localhost:8080` with HTTPS redirection disabled by configuration
- Docker Compose is the intended local container entrypoint, with Entra settings supplied by `.env.docker`
- `docker-compose.prod.yml` is the intended VPS entrypoint, with Entra settings supplied by `.env.production`

## Constraints and project rules

- Preserve the change request creation flow above all else
- Do not reintroduce Auth0-specific configuration
- Do not assume approver group logic exists unless you are explicitly adding it
- Do not commit local environment files or secrets
