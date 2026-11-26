# BallastLane

BallastLane is an architectural scaffold for a contract-management interview exercise. Customer, Contract, and user management are not implemented. The temporary percentage example is not a business requirement. The separate Generative AI Tools assignment is out of scope.

The solution uses .NET 10, ASP.NET Core MVC API controllers, FluentValidation, Npgsql/ADO.NET, JWT bearer validation, Swagger, Angular standalone components and routing, PostgreSQL, and Docker Compose. See [architecture](docs/architecture.md), [development guide](docs/development-guide.md), and [testing strategy](docs/testing-strategy.md).

## Layout

`src/BallastLane.Domain` and `src/BallastLane.Application` contain the inner layers; `src/Infrastructure` provides technical adapters; `src/Presentation` contains both APIs and Angular. `tests/Unit` and `tests/Integration` separate test boundaries. `database` reserves migration and seed directories.

## Prerequisites and configuration

Install .NET 10 SDK, Node.js 22 and npm, Docker and Docker Compose. Copy `.env.example` to `.env` and replace the password. Compose automatically loads `.env` from the project directory; values exported in the shell also work. A host-run .NET process does **not** automatically load this file. Supply its configuration separately through environment variables or .NET user secrets. The business API can run in Development without JWT settings for local testing. Its current endpoints are public; without JWT settings it cannot validate tokens, so protected endpoints still require authentication. The identity API and the business API outside Development require JWT settings. When configuring JWT, provide:

```sh
export Jwt__Issuer='local-issuer'
export Jwt__Audience='local-audience'
export Jwt__SigningKey='<your-own-random-secret-at-least-32-UTF8-bytes>'
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=ballastlane;Username=ballastlane;Password=<your-local-password>'
```

When both APIs validate tokens, they must use the same issuer, audience and signing key. No token issuer is included. Origins can be supplied through configuration such as `Cors__Origins__0=http://localhost:4200`. Never commit real secrets. The business API rejects partial or invalid JWT settings, and outside Development also rejects missing settings. The identity API always requires valid JWT settings. The data source is created lazily on first use.

## Run locally

```sh
docker compose up -d
dotnet restore BallastLane.sln
dotnet build BallastLane.sln
dotnet test BallastLane.sln
dotnet run --project src/Presentation/BallastLane.WebApi --launch-profile http
dotnet run --project src/Presentation/BallastLane.IdentityApi --launch-profile http
cd src/Presentation/BallastLane.Web
npm ci
npm start
```

Run long-lived commands in separate terminals. The business API is at `http://localhost:5101` and its Swagger UI at `http://localhost:5101/swagger`; the identity API is at `http://localhost:5102` and its Swagger UI at `http://localhost:5102/swagger`. Each exposes `/health`. The frontend is at `http://localhost:4200`. The APIs listen over HTTP; TLS termination will be configured later in Nginx. Configure CORS for the local environment as needed. The example `POST /api/examples/calculate-percentage` accepts `{"value":200,"percentage":25}` and returns `{"amount":50}` in Development only.

## Operations and limitations

Built-in logging is configured in each API's appsettings files and can be overridden with environment variables, e.g. `Logging__LogLevel__Default`. Request completion logs contain a route template, status, elapsed time and trace identifier, but no bodies, query strings or authorization data. Handled errors return `application/problem+json` with a trace identifier and generic unexpected-error details. Request cancellation is propagated.

There are no production endpoints, database tables, migration runner, authentication workflows, or issued tokens. The health checks indicate API process health, not database readiness. Production frontend API paths are deployment placeholders. Docker/Testcontainers requires a working Docker daemon. Future migration scripts must be reviewed and executed through a separately chosen deployment process.
