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
