# Testing strategy

Domain tests exercise rules without frameworks. Application tests cover validation and orchestration independently of HTTP and persistence. API unit tests target controllers and middleware with focused test doubles; Infrastructure unit tests cover isolated technical behavior when introduced. Empty test projects indicate no applicable behavior yet. Integration tests use WebApplicationFactory for HTTP boundaries and Testcontainers with a real PostgreSQL instance for connectivity. Docker is required; a database mock cannot replace that test.

For middleware and examples, use red-green-refactor: write and run a failing behavior test, implement the minimum behavior, rerun, then refactor and rerun. Record actual results; a compilation failure from unrelated setup is not evidence of the intended red state.

Run `dotnet test BallastLane.sln`. Run a single project with `dotnet test tests/Unit/BallastLane.Domain.UnitTests`. Run `npm ci` and `npm run build` in the frontend directory. Validate Compose with `docker compose --env-file .env.example config --quiet`.

## Recorded initial execution

The first Domain example test run failed to compile because the example namespace did not yet exist; after the rule was added, all seven domain cases passed. The first middleware test attempt failed because the API entry point was missing, which was unrelated setup. After an entry point was added, the next attempt failed because the middleware namespace did not exist; after implementation and correction of a JSON response overload, the initial two middleware tests passed. Additional middleware, application, controller, and integration tests were added after the corresponding implementations, so a separate red-green cycle was not recorded for every behavior. This falls short of the requested per-behavior TDD evidence and must not be presented as a complete TDD audit.

Customer integration tests use Testcontainers and apply the same versioned migration used in development. They do not use the development database. The first customer domain test run failed to compile because the new entity and enum were absent; after implementation, the domain project passed. This is not a complete per-behavior TDD record.

## ListCustomers verification

The domain, application, and controller tests were added before their respective implementations. The first builds failed on the missing reconstitution method, use case, and MVC action or response type. These compile failures establish that the tests referenced features not yet present. The HTTP integration run executed three tests before the GET action was added. Both HTTP requests returned 405, and Swagger lacked the GET operation. The final run passed all 53 unit tests and 15 integration tests.

The listing integration tests exercise the real PostgreSQL query through the API, including an empty table, a single customer created by POST, and multiple rows inserted out of order. HTTP assertions cover identity and field preservation, nullable trade name, string customer types, ordering, public access, Swagger metadata, and the shared trace identifier in the 500 response and request log. A test-specific repository substitution triggers the unexpected failure.

## GetCustomerById verification

The first Application test build failed on the missing use case. The first controller test build failed on the missing action, response type, and constructor argument. A second Application build exposed an ambiguous validator call in the test, which was fixed before the passing run. Those compilation failures are distinct from behavioral failures.

Before implementing the HTTP action and middleware mapping, all five new integration cases failed. Following the POST Location returned 404, both invalid identifiers and the substituted repository failure returned 404, and the OpenAPI operation was absent. The middleware test returned 500 instead of 404. After implementation, the focused run passed six Application cases, one middleware case, and five integration cases. The full solution run after refactoring passed 61 unit tests and 20 integration tests, including the controller mapping test and the POST and list regressions.

The integration tests apply the versioned migrations in filename order on disposable PostgreSQL 17. They verify direct lookup by ID, the null result for a missing row, anonymous HTTP retrieval, POST Location round-trip, validation errors, safe 404 and 500 responses, trace IDs in request logs, and the OpenAPI contract. The migration test seeds the original schema before applying the version migration and verifies that existing customer data survives and PostgreSQL rejects null and empty versions. The HTTP round-trip also compares the same token across POST, GET by ID, GET collection, a repeated read, and the value stored in PostgreSQL.

## UpdateCustomer verification

The baseline passed 64 unit tests and 21 integration tests. Domain and Application tests were written before the corresponding behavior and initially failed to compile because the new method and use case were missing. The first solution run after adding the repository interface method also failed to compile because the concrete repository did not implement it. The two repository integration cases then passed with the conditional write implemented. Before the PUT action was added, all 14 new HTTP cases ran and failed with 405. These runs establish compile-time and HTTP red states, rather than per-behavior red-green evidence for every case.

Domain and Application tests cover validation, state preservation, token rotation, expected-version forwarding, cancellation, and classification after a zero-row write. Repository integration tests apply the existing migrations to disposable PostgreSQL, exercise stale and duplicate writes, and run competing writes from independent connections. The HTTP race uses a test-only repository wrapper to hold both initial reads until they have independently observed the same version. It verifies two write attempts, one 200, one 409, and a final state matching the winning response. A sequential HTTP test verifies that a stale write leaves the stored state unchanged, that a fresh version permits another update, and that both GET responses expose the stored version.

The final solution run with `dotnet test BallastLane.sln --no-restore -m:1` passed 88 unit tests and 37 integration tests, including the Swagger UI and OpenAPI checks and the existing POST, GET, and migration regressions.

## DeleteCustomer verification

The pre-change baseline passed 88 unit tests and 37 integration tests. The new Domain and Application tests were written before the corresponding code. Their first builds failed on missing domain members and a missing use case namespace. These are compile-time red states. The HTTP, repository and migration tests were verified after implementation.

Integration tests apply all migrations in order to fresh PostgreSQL 17 containers. A separate migration test applies `003` to a schema with existing rows and checks preservation of identifiers, business data and versions, plus the default and null constraint. The HTTP lifecycle verifies the original row remains retrievable and retains its tax ID after deactivation. Tests also verify repeated DELETE, PUT on inactive customers, traceable errors, request logs and OpenAPI. At repository level, two independent data sources compete for one version. At HTTP level, a test-only gate makes both requests read the same version before DELETE competes with another DELETE or PUT.

The full solution passed 103 unit tests and 49 integration tests with `dotnet test BallastLane.sln --no-restore -m:1`. Migration verification used isolated databases.

## User domain and persistence verification

The pre-change baseline passed 103 unit tests and 49 integration tests. The new Domain tests first failed to compile because the User entity was absent. The full solution then passed 113 unit tests and 53 integration tests with `dotnet test BallastLane.sln --no-restore -m:1`, including the existing Customer suite.

Repository integration tests apply all scripts in order in disposable PostgreSQL 17. They verify dependency injection, exact synthetic hash persistence, normalized lookup, inactive user retrieval and safe error translation. Two independent data sources insert different users with the same normalized email and verify one success, one duplicate-user exception and one persisted row. The migration compatibility test applies `004` to existing Customer data and verifies complete row preservation, required fields, blank-text checks and the active default.

## Authentication and authorization verification

The baseline passed 113 unit tests and 53 integration tests. The updated suite passed 131 unit tests and 67 integration tests with `dotnet test BallastLane.sln --no-restore -m:1`. The first full integration run exposed an older error fixture that received 401 before reaching its 500 path. The fixture now sends a real test JWT and preserves the original error assertions. The new authentication tests were run against the implementation and do not establish a complete pre-implementation TDD record.

Tests verify exact password handling, salted hashes, malformed and legacy hash handling, JWT claims and configuration, safe login failures, Bearer challenges and OpenAPI security. Customer fixtures use isolated test keys with real JWT validation. A cross-host test provisions the demonstration user in disposable PostgreSQL, repeats the seed, logs in through IdentityApi and uses the issued token against WebApi for Customer creation, listing, retrieval, update and soft deletion, including conflict responses.

Local development verification applied the seed twice against the existing schema with one insert and zero inserts on repeat. The account and all three existing Customer rows were verified unchanged on repeat. With temporary loopback hosts and a shared ephemeral JWT key, login and the authenticated list returned 200, the unauthenticated list returned a Bearer 401, and both OpenAPI documents reflected endpoint access.

## Customer authorship verification

The authorship integration test creates a Customer before applying migration `005`, then logs two isolated users into IdentityApi against disposable PostgreSQL. It verifies the database row after WebApi creation, edits and soft deletion, including null creator on the legacy row, ignored caller-supplied authorship and preservation on a stale version or repeated inactive DELETE. It also checks that missing, malformed and empty JWT subjects are challenged without changing the database. A repository race uses different authors and verifies the winner's data and authorship survive the losing write.

The solution build passed with no warnings. The full suite passed 131 unit tests and 68 integration tests, including the Customer regression suite.

## Angular authentication verification

Run `npm test -- --watch=false --browsers=ChromeHeadless` from `src/Presentation/BallastLane.Web`. The Karma/Jasmine suite covers HTTP login and errors, memory-only expiration and timer replacement, scoped Bearer headers, stale 401 handling, route redirects, form validation, submission and dashboard sign-out. Use Chrome with access to the local Karma port.

For a browser smoke check, use the existing development demo account and both APIs. Start at `/dashboard` while signed out, check the login error with an incorrect password, then sign in, test the drawer and sign out. Confirm that a full reload requires a new login. Check login and dashboard at approximately 375, 768 and 1440 pixels and exercise keyboard navigation. Verify Customer listing with and without an issued token using GET only. Record statuses without logging credentials, tokens or Customer data.

For the Compose smoke check, run `docker compose --env-file .env.example config --quiet`, then start the configured local stack with `docker compose up -d --build --wait`. Use `docker compose ps web` to confirm that Nginx serves the Angular entry point and both API health endpoints through the configured proxy paths. Repeat the browser smoke check at `http://localhost:4200` after verifying the schema and demo account.

The Angular production build and TypeScript check passed, and all 17 Karma tests passed in Chrome Headless. With both APIs already running, `docker compose up -d --build --no-deps --wait web` rebuilt the frontend and reported it healthy. An automated Chrome smoke test against Nginx verified demo login, invalid credentials, the protected route redirect, keyboard navigation, the responsive drawer, logout, reload and idle expiration using an advanced browser clock. Login and dashboard fit at 375, 768 and 1440 pixels. Customer listing through the proxy returned 200 for an issued token and 401 anonymously.
