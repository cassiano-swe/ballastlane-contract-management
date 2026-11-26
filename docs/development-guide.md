# Development guide

Introduce a domain only when its requirements are supplied. Put domain rules, entities and value objects in Domain; put feature-organized inputs, outputs, validation and handlers in Application. Define a focused persistence interface in Application when a use case requires one, then implement it in Infrastructure with Npgsql and parameterized ADO.NET. Register dependencies at the APIs' composition roots. Controllers bind presentation DTOs, map inputs and outputs explicitly, and call handlers directly. Document real endpoints with response metadata and Swagger; add only justified exception mappings to both middleware implementations and tests.

Put versioned SQL in `database/migrations` when schema requirements exist. Keep scripts immutable once applied, review ordering and rollback needs, and select an explicit deployment execution process before use. Seeds belong in `database/seeds`; no execution mechanism is provided.

Add Angular feature folders under `src/app/features`, shared UI under `shared`, cross-cutting services under `core`, and layout components under `layouts`. Configure API URLs for each deployment. Do not embed secrets in frontend files.

To remove the temporary demonstration, delete Domain/Examples/Percentage.cs, Application/UseCases/Examples, ExampleController, its request and response models, the handler and validator registrations in the business API composition root, and the example-specific tests and documentation. Keep the middleware and infrastructure configuration. Check Swagger and rerun the suite afterward.
