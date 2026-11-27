# BallastLane frontend

From the repository root, run `docker compose up -d --build --wait` to start Angular, both APIs and PostgreSQL. Open `http://localhost:4200/login`. See the [root README](../../../README.md) for configuration, database preparation and host development commands.

The authenticated Customers feature provides the list, details, create and edit screens and a deactivation dialog. It uses the existing Material theme and calls the Business API through `/business-api`. Inactive customers remain visible and editable. See [Angular Customer management](../../../README.md#angular-customer-management) for routes and conflict recovery.

Run `npm run build` and `npm test -- --watch=false --browsers=ChromeHeadless` from this workspace.
