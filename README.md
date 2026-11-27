# BallastLane

BallastLane is a contract-management interview exercise. The business API supports customer creation, listing, and retrieval by ID. Contract management, user management, and the separate Generative AI Tools assignment are out of scope. The temporary percentage example demonstrates the API architecture.

The solution uses .NET 10, ASP.NET Core MVC API controllers, FluentValidation, Npgsql/ADO.NET, JWT bearer validation, Swagger, Angular standalone components and routing, PostgreSQL, and Docker Compose. See [customer business rules](docs/customer-business-rules.md), [architecture](docs/architecture.md), [development guide](docs/development-guide.md), and [testing strategy](docs/testing-strategy.md).

## Layout

`src/BallastLane.Domain` and `src/BallastLane.Application` contain the inner layers; `src/Infrastructure` provides technical adapters; `src/Presentation` contains both APIs and Angular. `tests/Unit` and `tests/Integration` separate test boundaries. `database` reserves migration and seed directories.

## Prerequisites and configuration

Install .NET 10 SDK, Node.js 22 and npm, Docker and Docker Compose. Copy `.env.example` to `.env` and replace the database password and JWT signing key with your own local secrets (the JWT signing key must contain at least 32 UTF-8 bytes). Compose automatically loads `.env` from the project directory; values exported in the shell also work. A host-run .NET process does **not** automatically load this file. Supply its configuration separately through environment variables or .NET user secrets. The business API can run in Development without JWT settings for local testing. Its current endpoints are public; without JWT settings it cannot validate tokens, so protected endpoints still require authentication. The identity API and the business API outside Development require JWT settings. When configuring JWT, provide:

```sh
export Jwt__Issuer='local-issuer'
export Jwt__Audience='local-audience'
export Jwt__SigningKey='<your-own-random-secret-at-least-32-UTF8-bytes>'
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=ballastlane;Username=ballastlane;Password=<your-local-password>'
```

When both APIs validate tokens, they must use the same issuer, audience and signing key. No token issuer is included. Origins can be supplied through configuration such as `Cors__Origins__0=http://localhost:4200`. Never commit real secrets. The business API rejects partial or invalid JWT settings, and outside Development also rejects missing settings. The identity API always requires valid JWT settings. The data source is created lazily on first use.

## Run with Docker Compose

```sh
docker compose up -d --build
docker compose ps
```

The web application is served at `http://localhost:4200`. Its `/business-api/` and `/identity-api/` paths are proxied to the `api` and `identity-api` services on port 8080. The business and identity APIs are also available directly at `http://localhost:5101` and `http://localhost:5102`, respectively. Both APIs use `postgres:5432` inside the Compose network, and use the same JWT settings from `.env`. Compose defaults to Development so the Swagger UIs are available; `ASPNETCORE_ENVIRONMENT` can override this. PostgreSQL and API ports bind only to localhost. The database migration is a separate manual step described below; starting Compose does not apply it.

For the connection string assembled by Compose, avoid semicolons in database values or use a suitably escaped Npgsql connection string configuration. Compose's JWT signing key is only local configuration; it does not issue tokens.

## Run applications on the host

Start only the database with `docker compose up -d postgres`, then use separate terminals for the following commands:

```sh
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

There is no migration runner, authentication workflow, or token issuer. The health checks indicate API process health, not database readiness. The containerized frontend uses Nginx to route its API paths to the Compose services. Docker/Testcontainers requires a working Docker daemon. Future migration scripts must be reviewed and executed through a separately chosen deployment process.

## Customer creation

`POST /api/customers` creates an Individual or Company. This endpoint is temporarily anonymous and is not intended for production deployment. It returns `201 Created` with a `Location: /api/customers/{id}` header that can be followed to retrieve the new customer.

```json
{"name":"Acme Corporation","tradeName":"Acme","customerType":"Company","taxId":"00123456789","email":"contact@acme.example"}
```

The response contains `id`, `name`, `tradeName`, `customerType`, `taxId`, and `email`. Text is trimmed. Name is required (maximum 200 characters); trade name is optional (maximum 200, Company only); tax ID is required (maximum 50, preserved as a string); email is required and valid (maximum 254). Tax IDs are unique with exact database comparison; no country-specific normalization is applied. Invalid requests return 400; a duplicate tax ID returns 409; unexpected failures return 500. Errors are ProblemDetails with a trace ID; validation failures explain their cause in `detail` and provide field-level messages in `errors`.

The PostgreSQL schema is defined by `database/migrations/001_create_customers.sql`. Configure the local Compose environment as described above, start PostgreSQL, inspect existing tables, then apply once:

```sh
docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" < database/migrations/001_create_customers.sql
```

The shell variables must contain the configured Compose database user and database name. Verify with `\d customers` in psql. The migration creates the six customer columns, a primary key, a unique tax ID constraint, and checks for customer type, trade name and required text. It does not modify an existing table. PostgreSQL resolves concurrent duplicates atomically.

## Customer listing

`GET /api/customers` returns `200 OK` with a JSON array containing every customer, ordered by name and then ID in ascending order. It accepts no request body or query parameters. An empty table returns `[]`. The response preserves persisted IDs and tax IDs, including leading zeros. `customerType` is `Individual` or `Company`, and `tradeName` can be null.

```json
[
  {
    "id": "6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17",
    "name": "Alex Taylor",
    "tradeName": null,
    "customerType": "Individual",
    "taxId": "00123456789",
    "email": "alex@example.com"
  }
]
```

Unexpected failures return `500 Internal Server Error` with generic ProblemDetails and a request trace ID. Anonymous access is temporary and is not intended for production deployment. Use fictional customer data in local demonstrations.

The repository reads the six columns in one ordered PostgreSQL query. Listing retrieves all rows without filtering or pagination. As the table grows, database sorting, API memory usage, response size, and network transfer can increase.

In Development, use `/swagger` for Swagger UI and `/swagger/v1/swagger.json` for the OpenAPI document. Run `dotnet test BallastLane.sln` for unit and integration tests. The PostgreSQL integration tests require Docker and use disposable databases.

## Customer retrieval by ID

`GET /api/customers/{id}` requires a non-empty UUID in the path. A match returns `200 OK` with one JSON object containing `id`, `name`, `tradeName`, `customerType`, `taxId`, and `email`. The response retains the stored ID, a nullable trade name, and leading zeros in the tax ID. The customer type is the string `Individual` or `Company`.

| Status | Outcome |
| --- | --- |
| 200 | Customer found |
| 400 | Malformed or empty UUID |
| 404 | No customer with that UUID |
| 500 | Unexpected failure |

Errors use `application/problem+json` and include a trace ID. The repository reads the six columns in one parameterized PostgreSQL query using the primary key. It returns null for a missing row. The handler raises a customer-not-found exception that the Web API middleware maps to 404.

`ICustomerRepository.GetByIdAsync` can support future update and delete use cases. A successful read does not guarantee the row will still exist when a later write runs. Future mutations must handle concurrent changes and check the affected row count.

Anonymous access is temporary and is not intended for production deployment. Test this operation in Development at `/swagger` or inspect its schema at `/swagger/v1/swagger.json`. Run `dotnet test BallastLane.sln` with Docker available to exercise both HTTP and PostgreSQL behavior.
