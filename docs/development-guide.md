# Development guide

Introduce a domain only when its requirements are supplied. Put domain rules, entities and value objects in Domain; put feature-organized inputs, outputs, validation and handlers in Application. Define a focused persistence interface in Application when a use case requires one, then implement it in Infrastructure with Npgsql and parameterized ADO.NET. Register dependencies at the APIs' composition roots. Controllers bind presentation DTOs, map inputs and outputs explicitly, and call handlers directly. Document real endpoints with response metadata and Swagger; add only justified exception mappings to both middleware implementations and tests.

Put versioned SQL in `database/migrations` when schema requirements exist. Keep scripts immutable once applied, review ordering and rollback needs, and select an explicit deployment execution process before use. Seeds belong in `database/seeds`; no execution mechanism is provided.

Apply pending customer migrations in filename order using the local Compose procedure in the README. Before applying `002_add_customer_version.sql`, verify the target schema and check that `gen_random_uuid()` is available. Coordinate application replacement with the migration because earlier builds omit the new required column. After applying it, check the UUID column, constraints, row count, and preserved customer fields before starting the updated API.

Customer update uses the existing version column and tax ID constraint. Verify that migration `002` is present in the target schema before deploying the API. Use disposable databases for mutation testing. In Development, inspect the PUT request schema and both 409 examples in Swagger.

Apply `003_add_customer_active.sql` to the local development database before starting the API that reads `active`. Check whether the column already exists and verify the definition before deciding if the script is pending. On first application, compare counts and a private deterministic snapshot of the seven original columns before and after. Verify the boolean type, NOT NULL constraint, true default, initial active rows, and preserved tax ID uniqueness. Use fictional records in isolated PostgreSQL for mutation testing. In Development Swagger, verify that DELETE requires the version query parameter and that both GET schemas expose a boolean status.

Apply `004_create_users.sql` after verifying the existing local schema and migration sequence. Verify the user column types, length limits, required fields, true active default and named constraints. Compare Customer data before and after the first application without exporting customer records. Use synthetic hashes for persistence tests and keep Npgsql parameter logging disabled for this data source.

Apply `005_add_customer_authorship.sql` after `004` and before deploying the updated WebApi. Verify the nullable UUID columns, both restrictive foreign keys, and null authorship on pre-existing customers. Test writes with a token whose subject corresponds to a user in the target database.

Provision the demo account manually using the seed under `database/seeds` on verified local development PostgreSQL. The credential is public. A repeat preserves the existing account, so investigate a failed demo login without resetting an existing user's hash or activation. Both APIs require the matching JWT settings described in the README. Keep credentials, hashes and tokens out of logs and use isolated keys in tests.

Add Angular feature folders under `src/app/features`, shared UI under `shared`, cross-cutting services under `core`, and layout components under `layouts`. Configure API URLs for each deployment. Do not embed secrets in frontend files.

To remove the temporary demonstration, delete Domain/Examples/Percentage.cs, Application/UseCases/Examples, ExampleController, its request and response models, the handler and validator registrations in the business API composition root, and the example-specific tests and documentation. Keep the middleware and infrastructure configuration. Check Swagger and rerun the suite afterward.
