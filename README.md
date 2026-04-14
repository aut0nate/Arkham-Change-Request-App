# Arkham Change Request App

Arkham Change Request App is a simple internal change management portal for creating, reviewing and tracking IT change requests.

## Stack

- ASP.NET Core MVC on .NET 8
- C#
- Entity Framework Core
- Microsoft Entra ID for authentication
- SQLite for local and Docker development
- Local filesystem attachment storage
- Docker Compose for local and VPS deployment
- xUnit for tests

## Configuration

### Local Development

1. Create an `appsettings.Development.json` file:

   ```bash
   cp appsettings.Development.example.json appsettings.Development.json
   ```

2. Create a Microsoft Entra app registration for local development:

   Use these callback URLs for local development:

   ```text
   https://localhost:7015/signin-oidc
   https://localhost:7015/signout-callback-oidc
   http://localhost:8080/signin-oidc
   http://localhost:8080/signout-callback-oidc
   ```

3. Update `appsettings.Development.json`:

   - `AzureAd:TenantId`
   - `AzureAd:ClientId`
   - `AzureAd:ClientSecret`
   - `Database:Provider`
   - `ConnectionStrings:Sqlite`
   - `Storage:Provider`
   - `Storage:LocalPath`

Environment notes:

- Keep [`appsettings.json`](/Users/nathan/Dev/Arkham-Change-Request-App/appsettings.json) as the shared base configuration.
- `appsettings.Development.json` is for local non-Docker development only.
- For local development, use `Sqlite` with `LocalFiles`.
- A simple local SQLite path is `Data Source=App_Data/arkham-change.db`.
- A simple local attachment path is `App_Data/uploads`.
- You can supply the Entra secret through `AZUREAD_CLIENT_SECRET` instead of storing it in `appsettings.Development.json`.

### Docker

1. Create a `.env.docker` file:

   ```bash
   cp .env.docker.example .env.docker
   ```

2. Update `.env.docker`:

   - `AzureAd__TenantId`
   - `AzureAd__ClientId`
   - `AzureAd__ClientSecret`
   - `ConnectionStrings__Sqlite`
   - `Storage__LocalPath`

Environment notes:

- Local Docker runs on `http://localhost:8080`.
- Docker uses the same Entra development app registration, but you must include the `http://localhost:8080` callback URLs.
- Docker stores the SQLite database, attachments and data protection keys in a named volume mounted at `/data`.
- The default Docker SQLite path is `/data/arkham-change.db`.
- The default Docker attachment path is `/data/uploads`.

### VPS Production

1. Create a production environment file:

   ```bash
   cp .env.production.example .env.production
   ```

2. Create a Microsoft Entra production app registration:

   Use these callback URLs for production:

   ```text
   https://arkchg.autonate.dev/signin-oidc
   https://arkchg.autonate.dev/signout-callback-oidc
   ```

3. Update `.env.production`:

   - `AzureAd__TenantId`
   - `AzureAd__ClientId`
   - `AzureAd__ClientSecret`
   - `ConnectionStrings__Sqlite`
   - `Storage__LocalPath`
   - `HOST_BIND`
   - `HOST_PORT`

Environment notes:

- Production uses [`docker-compose.prod.yml`](/Users/nathan/Dev/Arkham-Change-Request-App/docker-compose.prod.yml).
- The app listens on `127.0.0.1:8080` by default and should sit behind `Nginx` or `Caddy`.
- HTTPS redirection remains enabled in production.
- Your reverse proxy should forward public HTTPS traffic from `https://arkchg.autonate.dev` to `http://127.0.0.1:8080`.
- Keep development and production Entra app registrations separate.

## Run Locally

1. Restore dependencies:

   ```bash
   dotnet restore
   ```

2. Start the app:

   ```bash
   dotnet run --launch-profile https
   ```

3. Open [https://localhost:7015](https://localhost:7015).

## Run with Docker

### Local Docker

1. Build and start the container:

   ```bash
   docker compose up --build
   ```

2. Then open [http://localhost:8080](http://localhost:8080).

Notes:

- The SQLite database, uploaded attachments and data protection keys are stored in the named Docker volume `arkham-change-request-app_arkham-change-request-data`.
- The main SQLite database file lives at `/data/arkham-change.db` inside the container.
- Attachments live under `/data/uploads/`.
- Data protection keys live under `/data/keys/`.
- Local Docker disables HTTPS redirection so Entra sign-in can work with `http://localhost:8080`.

### VPS Docker

1. Build and start the production container:

   ```bash
   docker compose -f docker-compose.prod.yml up --build -d
   ```

2. Put the app behind your reverse proxy and use `https://arkchg.autonate.dev`.

Notes:

- Production uses the same named Docker volume pattern for persistence.
- The production compose file joins the external `edge-net` network.
- Create the network once on the VPS before starting the stack:

  ```bash
  docker network create edge-net
  ```

- The app remains private on `127.0.0.1:8080` and should not be exposed directly to the internet.

## Access and Workflow

- Any authenticated user in your Entra tenant can currently sign in.
- Signed-in users can create change requests.
- Signed-in users can currently access approval actions as part of the first-pass local workflow.
- The main create request page is the default landing page.
- Attachments are stored locally in development and Docker.

## Backups and Persistence

- Local non-Docker development stores application data in `App_Data/`, which is ignored by Git.
- Docker stores application data in a named volume, not in the repository.
- Rebuilding or recreating the container will not remove your data as long as the named volume remains in place.
- Do not run `docker compose down -v` if you want to keep your data.
- Back up Docker data by copying the SQLite database and uploads from the named volume.

Useful commands:

```bash
docker volume inspect arkham-change-request-app_arkham-change-request-data
docker compose down
docker compose -f docker-compose.prod.yml down
```

## AI-Assisted Development

Arkham Change Request App was built and maintained with **OpenAI Codex** assistance. This repository includes an [`AGENTS.md`](/Users/nathan/Dev/Arkham-Change-Request-App/AGENTS.md) file, which provides structured instructions and context for AI coding agents. It defines expectations, constraints and project-specific guidance to help keep contributions consistent and reliable.

## Contributions

Contributions, ideas and suggestions are welcome.

If you have improvements, feature ideas or bug fixes, feel free to open an issue or submit a pull request. All contributions are appreciated and help improve the project.
