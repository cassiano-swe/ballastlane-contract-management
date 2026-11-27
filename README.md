# BallastLane

BallastLane is a contract-management interview exercise. The Angular workspace provides sign-in, an authenticated dashboard and Customer management. The business API supports customer creation, listing, retrieval, update, and deactivation. The Identity API authenticates users and issues the access tokens required by Customer operations. Contract CRUD is TODO. User management and the separate Generative AI Tools assignment are out of scope. The temporary percentage example demonstrates the API architecture.

Informal user story: **As an authenticated customer administrator, I want to create, view, edit and deactivate customers so that I can maintain customer records without losing their history.**

The solution uses .NET 10, ASP.NET Core MVC API controllers, FluentValidation, Npgsql/ADO.NET, JWT bearer validation, Swagger, Angular standalone components and routing, PostgreSQL, and Docker Compose. See [customer business rules](docs/customer-business-rules.md), [architecture](docs/architecture.md), [development guide](docs/development-guide.md), and [testing strategy](docs/testing-strategy.md).

## Layout

`src/BallastLane.Domain` and `src/BallastLane.Application` contain the inner layers; `src/Infrastructure` provides technical adapters; `src/Presentation` contains both APIs and Angular. `tests/Unit` and `tests/Integration` separate test boundaries. `database` reserves migration and seed directories.

## Prerequisites and configuration

Install .NET 10 SDK, Node.js 22 and npm, Docker and Docker Compose. Copy `.env.example` to `.env` and supply local secrets. The signing key is Base64 text representing at least 32 random bytes. Compose reads `.env` from the project directory. Host-run .NET processes require environment variables or .NET user secrets configured separately. For example, configure both host-run APIs from the same shell:

```sh
export Jwt__Issuer='local-issuer'
export Jwt__Audience='local-audience'
export Jwt__SigningKey="$(openssl rand -base64 32)"
export Jwt__AccessTokenLifetimeSeconds=1800
export ConnectionStrings__Postgres='Host=localhost;Port=5432;Database=ballastlane;Username=ballastlane;Password=<your-local-password>'
```

Both APIs require the same issuer, audience and key at startup, including in Development. Replace any older plain-text key in both hosts together. For Compose, assign the output of `openssl rand -base64 32` to `JWT_SIGNING_KEY` in the ignored `.env` file. Never commit a real key. Explicit local CORS origins can be configured with `Cors__Origins__0=http://localhost:4200`.

## Run with Docker Compose

```sh
docker compose up -d --build --wait
docker compose ps
```

Compose builds Angular from the npm lockfile and starts its Nginx container as the `web` service. Open `http://localhost:4200/login`. No host-side npm process is needed. Nginx forwards `/business-api/` and `/identity-api/` to the `api` and `identity-api` services on port 8080. With `--wait`, Compose waits for `web` to serve the SPA and reach both API health endpoints through Nginx.

The business and Identity APIs are also available directly at `http://localhost:5101` and `http://localhost:5102`. Both use `postgres:5432` inside the Compose network and share the JWT settings from `.env`. Compose defaults to Development so Swagger is available. Set `ASPNETCORE_ENVIRONMENT` to override it. All published ports bind to localhost. Before the first login, apply the pending migrations and provision the demo account using the procedures below.

For the connection string assembled by Compose, avoid semicolons in database values or use a suitably escaped Npgsql connection string configuration.

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

Run long-lived commands in separate terminals. The business API is at `http://localhost:5101` with Swagger at `http://localhost:5101/swagger`. The Identity API is at `http://localhost:5102` with Swagger at `http://localhost:5102/swagger`. Each exposes `/health`. Open the frontend at `http://localhost:4200`. The Angular development proxy forwards `/business-api/` and `/identity-api/` to the corresponding loopback hosts, removing the prefix. The browser uses the same origin for the SPA and its API calls, so this setup requires no CORS changes. Use the existing explicit origin configuration for separate cross-origin clients. The local APIs listen over HTTP. The example `POST /api/examples/calculate-percentage` accepts `{"value":200,"percentage":25}` and returns `{"amount":50}` in Development only.

## Frontend Login Instructions

This account is public and intended only for local development and demonstrations. Never use these credentials in production.

1. Start PostgreSQL, apply migrations `001` through `005`, apply `database/seeds/001_create_demo_user.sql`, and start IdentityApi and WebApi as described above.
2. Start Angular with `cd src/Presentation/BallastLane.Web && npm ci && npm start`, then open `http://localhost:4200/login`.
3. Enter Email: `demo@ballastlane.example` and Password: `BallastLane@2026!`.
4. Click **Sign in**. Verify navigation to the authenticated dashboard, then select **Customers**.
5. Use **Logout** to end the local session. A full page reload also requires login again because the access token is held only in memory.

With Docker Compose, start the stack using `docker compose up -d --build --wait`, run the migration and seed commands below, and use the same URL and login steps. The password is documented only for this public demo account and is not embedded in Angular source or configuration.

## Operations and limitations

Built-in logging is configured in each API's appsettings files and can be overridden with environment variables, e.g. `Logging__LogLevel__Default`. Request completion logs contain a route template, status, elapsed time and trace identifier, but no bodies, query strings or authorization data. Handled errors return `application/problem+json` with a trace identifier and generic unexpected-error details. Request cancellation is propagated.

Migrations run manually. The health checks indicate API process health, not database readiness. The containerized frontend uses Nginx to route its API paths to the Compose services. Docker/Testcontainers requires a working Docker daemon. Future migration scripts must be reviewed and executed through a separately chosen deployment process.

## Customer creation

`POST /api/customers` creates an Individual or Company. It returns `201 Created` with a `Location: /api/customers/{id}` header that can be followed to retrieve the new customer.

```json
{"name":"Acme Corporation","tradeName":"Acme","customerType":"Company","taxId":"00123456789","email":"contact@acme.example"}
```

The response adds the generated `id` and `version` to the customer fields. New customers are active by default. Retrieve the customer through its Location to read `active`. Text is trimmed. Name is required (maximum 200 characters); trade name is optional (maximum 200, Company only); tax ID is required (maximum 50, preserved as a string); email is required and valid (maximum 254). Tax IDs are unique across active and inactive customers, with exact database comparison and no country-specific normalization. Invalid requests return 400, a duplicate tax ID returns 409, and unexpected failures return 500. Errors are ProblemDetails with a trace ID. Validation failures explain their cause in `detail` and provide field-level messages in `errors`.

```json
{"id":"6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17","name":"Acme Corporation","tradeName":"Acme","customerType":"Company","taxId":"00123456789","email":"contact@acme.example","version":"c53db246-a877-43e1-952d-90fdedfb6781"}
```

## Database migrations

Inspect the healthy local Compose PostgreSQL service before applying migrations. On a fresh database, apply all five scripts in order. On an existing database, inspect the schema and apply only pending scripts.

```sh
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/migrations/001_create_customers.sql
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/migrations/002_add_customer_version.sql
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/migrations/003_add_customer_active.sql
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/migrations/004_create_users.sql
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/migrations/005_add_customer_authorship.sql
```

After all migrations and the demo user seed, optionally load the 15 fictional Customer records:

```sh
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/seeds/002_create_demo_customers.sql
```

The Customer seed contains 11 active and four inactive records and attributes their creation to the demo user. Rerunning it neither duplicates nor updates its fixed IDs. A tax ID conflict owned by a different row fails and rolls back instead of overwriting data. Apply seeds only after identifying the target as a local/demo database.

The second migration adds the seventh customer column and backfills a distinct UUID per existing customer inside one transaction. Verify with `\d customers` in psql that `version` is `uuid NOT NULL` with the `ck_customers_version` check. Compare the row count and the six original columns before and after, and verify that every row has a non-empty version. Coordinate the migration with the API deployment because the old API writes without the column and the new API reads and writes it.

The third migration adds `active BOOLEAN NOT NULL DEFAULT TRUE` in one transaction. Coordinate a pause in customer writes while verifying the migration. Compare row counts and a deterministic snapshot of the original columns, including `version`, before and after without publishing customer data. Immediately after the first application, verify that all existing rows are active and that the tax ID constraint remains in place. Apply the migration before starting the API that reads `active`. If the column already exists, verify its definition and investigate incompatible schema before proceeding.

The fourth migration creates the `users` table in one transaction. If the table exists, check its definition before deciding whether the migration is pending. On first application, verify all five columns, their types and nullability, the active default, and the primary key, unique email and text check constraints. Compare the existing customer data before and after using a private snapshot.

The fifth migration adds nullable `created_by_user_id` and `updated_by_user_id` to `customers`, with foreign keys to `users.id` that restrict deletion of referenced users. Apply it before deploying the updated WebApi. Existing customers retain null authorship. The API takes the author from the authenticated JWT and writes it with the customer change. The audit fields are available in PostgreSQL and remain outside the HTTP payloads.

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
    "email": "alex@example.com",
    "version": "c53db246-a877-43e1-952d-90fdedfb6781",
    "active": true
  }
]
```

Unexpected failures return `500 Internal Server Error` with generic ProblemDetails and a request trace ID. Use fictional customer data in local demonstrations.

The repository reads all eight columns in one ordered PostgreSQL query. Listing returns active and inactive customers without filtering or pagination. As the table grows, database sorting, API memory usage, response size, and network transfer can increase.

In Development, use `/swagger` for Swagger UI and `/swagger/v1/swagger.json` for the OpenAPI document. Run `dotnet test BallastLane.sln` for unit and integration tests. The PostgreSQL integration tests require Docker and use disposable databases.

## Customer retrieval by ID

`GET /api/customers/{id}` requires a non-empty UUID in the path. A match returns `200 OK` for active and inactive customers. The response includes the stored ID, version, nullable trade name, tax ID and boolean `active`. The customer type is the string `Individual` or `Company`.

```json
{"id":"6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17","name":"Alex Taylor","tradeName":null,"customerType":"Individual","taxId":"00123456789","email":"alex@example.com","version":"a7280e43-7c4d-4d18-a09e-8c3a214e6732","active":false}
```

| Status | Outcome |
| --- | --- |
| 200 | Customer found |
| 400 | Malformed or empty UUID |
| 404 | No customer with that UUID |
| 500 | Unexpected failure |

Errors use `application/problem+json` and include a trace ID. The repository reads all eight columns in one parameterized PostgreSQL query using the primary key. It returns null for a missing row. The handler raises a customer-not-found exception that the Web API middleware maps to 404.

The application generates a `Guid` when creating a customer and persists it as a native PostgreSQL `uuid`. POST and both GET responses expose that persisted token as a JSON string. Repeated reads preserve it, and successful updates replace it.

Test this operation in Development at `/swagger` or inspect its schema at `/swagger/v1/swagger.json`. Run `dotnet test BallastLane.sln` with Docker available to exercise both HTTP and PostgreSQL behavior.

## Customer update

`PUT /api/customers/{id}` replaces all editable customer fields. Use a non-empty UUID in the path and send the `version` from a previous customer response in the body. The creation validation rules also apply to updates. Omitting `tradeName`, sending null, or sending a blank value clears the stored trade name.

```json
{"name":"Acme International","tradeName":"Acme","customerType":"Company","taxId":"00123456789","email":"contact@acme.example","version":"c53db246-a877-43e1-952d-90fdedfb6781"}
```

A successful request returns `200 OK` with the existing seven-field response and a fresh version, even when the editable values are unchanged. PUT preserves the activation status of active and inactive customers.

```json
{"id":"6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17","name":"Acme International","tradeName":"Acme","customerType":"Company","taxId":"00123456789","email":"contact@acme.example","version":"a7280e43-7c4d-4d18-a09e-8c3a214e6732"}
```

| Status | Outcome |
| --- | --- |
| 200 | The replacement and its new version were persisted |
| 400 | Invalid path UUID, version, or replacement fields |
| 404 | Customer absent on the initial lookup or on the lookup after a zero-row write |
| 409 | Stale version (`urn:ballastlane:customer-concurrency-conflict`) or duplicate tax ID (`urn:ballastlane:customer-duplicate-tax-id`) |
| 500 | Unexpected failure with a generic error message |

For example, two people can retrieve the same customer at version A and each submit an edit. Both requests can pass the in-memory check. The database compares the identifier and expected version in the same statement that writes the replacement and new version. Once the first write commits, the second cannot match version A, so it receives a conflict and the first edit remains stored. The server cannot know how to reconcile their edits and performs no automatic retry. On a concurrency conflict, retrieve the current state before deciding whether to send another update.

Errors use `application/problem+json` with a `traceId`. The duplicate tax ID type also applies to POST. In Development, Swagger UI at `/swagger` and the OpenAPI document at `/swagger/v1/swagger.json` show both conflict examples. Run `dotnet test BallastLane.sln` with Docker to exercise the endpoint and concurrent writes against disposable PostgreSQL.

## Customer deactivation

`DELETE /api/customers/{id}?version={version}` accepts a non-empty path UUID and a required non-empty query UUID. Get the current version from a customer response. The request has no body.

```http
DELETE /api/customers/6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17?version=c53db246-a877-43e1-952d-90fdedfb6781

HTTP/1.1 204 No Content
```

A successful transition sets `active` to false and renews the version while retaining the row and its business data. Repeating DELETE with the inactive customer's current version returns 204 with no write. Reusing a stale token returns 409. Both GET endpoints still return the customer, and its tax ID remains reserved.

| Status | Outcome |
| --- | --- |
| 204 | Deactivated or already inactive with the current version |
| 400 | Invalid identifier or missing or invalid version |
| 404 | Customer absent |
| 409 | Stale version (`urn:ballastlane:customer-concurrency-conflict`) |
| 500 | Unexpected failure with a generic error message |

Concurrent PUT and DELETE writes that read the same active version compete for that version. The database accepts one write and the other receives 409. GET returns the winning state. Reactivation is outside this feature. The endpoint is documented in the Development Swagger UI.

## User persistence

The User domain holds a generated UUID, a trimmed name of up to 200 characters, a normalized email of up to 254 characters, a previously generated password hash, and an activation flag that defaults to true. Email is trimmed and lowercased using invariant rules for both creation and lookup. Reconstitution preserves stored values, including activation and the exact hash.

PostgreSQL stores these fields as `id UUID`, `name VARCHAR(200)`, `email VARCHAR(254)`, `password_hash TEXT`, and `active BOOLEAN`. The scoped repository inserts through the existing Npgsql data source and reads by normalized email. The database's `uq_users_email` constraint allows only one insert when concurrent writes use the same normalized email. The application exception for a duplicate contains no submitted values.

Treat password hashes as sensitive. Keep them out of logs, diagnostics and API responses. Public registration remains out of scope.

## Backend authentication

After applying migrations, provision the demonstration account **only in an isolated local development database**. The password is publicly known. The seed stores its generated hash and preserves existing accounts with the same email.

```sh
docker compose exec -T postgres sh -c 'psql -X -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/seeds/001_create_demo_user.sql
```

The demonstration credentials are `demo@ballastlane.example` and `BallastLane@2026!`. Post the email and password to the Identity API, then use its access token on the business API. This example captures the token without printing it. Avoid shell tracing when running it.

For the browser demo, open `http://localhost:4200/login` and enter the same credentials after provisioning the development account.

```sh
token="$(curl --silent --show-error --fail http://localhost:5102/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@ballastlane.example","password":"BallastLane@2026!"}' | jq -er '.accessToken')"
printf 'header = "Authorization: Bearer %s"\n' "$token" |
  curl --silent --show-error --fail --config - http://localhost:5101/api/customers
unset token
```

| Endpoint | Access |
| --- | --- |
| Identity API `POST /api/auth/login` | Anonymous |
| Both APIs `GET /health` | Anonymous |
| Business API `POST`, `GET`, `PUT`, and `DELETE` Customer operations | Valid Bearer token |
| Swagger UI and the temporary percentage example | Development only |

The login response returns `accessToken`, `tokenType`, and `expiresIn` with `Cache-Control: no-store`. Invalid input returns 400. Incorrect credentials and inactive or missing accounts return the same 401. Customer requests with missing or invalid tokens return 401 with the Bearer challenge. A valid token can authorize all Customer operations. No role-specific policy is configured.

In Development Swagger, obtain the token from the Identity API at `http://localhost:5102/swagger` and paste just the JWT into **Authorize** on the business API at `http://localhost:5101/swagger`. Use HTTPS outside isolated local development. The two hosts share a symmetric key, so both can technically issue tokens. Tokens expire after the configured lifetime and are not revoked immediately if an account becomes inactive. Login rate limiting is outside this delivery.

Public registration is excluded by the project owner. This is an agreed scope decision but remains a compliance gap against the exercise's user-creation requirement. IdentityApi exposes anonymous login and health only; it has no protected endpoint of its own. Login rate limiting is not implemented and is required before an internet-facing deployment.

## Angular authentication

`/login` is public and `/dashboard` is private. The root and unknown paths lead to the dashboard for an active session or to sign-in otherwise. The frontend keeps the access token in memory, so a full reload requires a new login. An expired session returns to sign-in even when idle. Sign-out clears the local session. An issued JWT remains usable until server-side expiration.

The interceptor attaches Bearer credentials only to the configured business Customer collection and item URLs. A 401 for the current Bearer session returns to sign-in. Login errors stay in the form. The frontend can honor a `Retry-After` header on HTTP 429 if an upstream component sends it. WebApi continues to enforce authorization on Customer operations.

## Angular Customer management

Sign in with the documented local demo account and open **Customers**. The authenticated layout lazy-loads the feature under `src/app/features/customers`. Its HTTP service uses the configured Business API, proxied at `/business-api` in both host development and Compose.

| Route | Screen |
| --- | --- |
| `/customers` | Unpaginated list in server order |
| `/customers/new` | Create a customer |
| `/customers/:id` | Customer details |
| `/customers/:id/edit` | Edit a customer |

The forms validate trimmed input, keep tax IDs as text and allow a trade name only for companies. The API remains authoritative, including for tax ID uniqueness. Deactivation requires confirmation and leaves the record visible and editable as **Inactive**. Reactivation is outside this delivery.

The UI sends the version from the displayed record on edits and deactivation. If another operation changes the record, the UI keeps the draft and offers an explicit reload. Confirming the reload replaces the draft with the latest server values. Any further write requires a new submission. The UI refreshes from the API after successful writes.

For host development, start PostgreSQL and both APIs as described above and run `npm start` from the frontend workspace. For Compose, use the same stack and migration/seed preparation described above. Public registration remains outside this delivery.

Run the following from `src/Presentation/BallastLane.Web` with Node.js 22 and Chrome available:

```sh
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

## Tests, decisions and known limitations

Backend tests use disposable PostgreSQL 17 Testcontainers databases and the actual migrations; they never use the development database. They cover migration behavior, both seed scripts, real cross-host JWT validation, invalid/expired JWTs, authenticated Customer CRUD, authorship, uniqueness races and optimistic-concurrency races. Unit projects cover Domain, Application, Infrastructure and both MVC hosts. Angular specs cover auth state, guards, scoped interception, HTTP contracts, forms, stable error handling and stale-conflict behavior. Current execution results belong in the delivery report, not as a permanent pass-count claim.

Dependencies point inward, HTTP DTO mapping is explicit, and persistence uses parameterized Npgsql/ADO.NET through pooled `NpgsqlDataSource`; Entity Framework, Dapper and Mediator are absent. Customer deletion is a soft deactivation, inactive tax IDs remain reserved, and successful writes renew the UUID version through an atomic `id + version` predicate. JWTs are HS256 tokens with issuer, audience, signature, lifetime and algorithm validation. Angular stores the token only in memory; logout is local and issued tokens have no server-side revocation.

HTTPS termination and trusted-forwarded-header configuration are deployment responsibilities. Local endpoints use HTTP and the APIs intentionally do not trust proxy headers by default. Swagger is Development-only. Contract CRUD, registration, refresh tokens, RBAC, pagination/filtering and reactivation remain unimplemented. The IdentityApi-specific user-creation/protected-endpoint requirements and login rate limiting remain known gaps.

The original exercise PDF was unavailable during final review. See [requirements compliance](docs/requirements-compliance.md) for the evidence matrix and source limitation, and [technical presentation](docs/technical-presentation.md) for the interview walkthrough.
