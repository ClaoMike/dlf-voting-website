# Backend Testing

## Overview

Integration tests for the ASP.NET Core API, run against a real PostgreSQL database via `WebApplicationFactory`. Tests exercise the actual HTTP pipeline (controllers, auth, cookies) rather than mocking things out, so they catch real wiring issues.

- Test project: `backend/tests/DlfVoting.Api.Tests`
- Framework: xUnit
- Key packages: `Microsoft.AspNetCore.Mvc.Testing`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Respawn` (fast per-test DB reset)

## One-time setup (new machine)

1. Create a **separate** test database — never point tests at your dev database:
```bash
   createdb dlf_voting_test
```

2. **Grant schema permissions.** PostgreSQL 15+ locks down `CREATE` on the `public` schema by default, even for the database owner. This bit us twice — once on `dlf_voting`, once on `dlf_voting_test` — because `$(whoami)` resolves to your **macOS username**, which is a different Postgres role than the one your app's connection string actually uses (e.g. `claomike`). Always grant to the **exact Postgres role name** used in the connection string, not to `$(whoami)`:
```bash
   psql dlf_voting_test -c "GRANT ALL ON SCHEMA public TO claomike;"
```
   Verify with:
```bash
   psql dlf_voting_test -c "\dn+"
```
   You should see that role listed in the access privileges column.

3. Confirm `dotnet-ef` CLI tool matches the project's EF Core major version (currently 9.x):
```bash
   dotnet ef --version
```
   If it reports a different major version (e.g. 10.x), reinstall pinned:
```bash
   dotnet tool uninstall --global dotnet-ef
   dotnet tool install --global dotnet-ef --version 9.0.9
```

## Running tests

```bash
dotnet test backend/tests/DlfVoting.Api.Tests
```

Migrations are applied automatically to the test database on the first test run (via `DatabaseFixture`). Respawn resets table data between each test class run, so tests don't interfere with each other.

## Architecture notes

- `TestWebApplicationFactory` boots the real `Program` (requires `public partial class Program { }` at the bottom of `Program.cs` since minimal hosting APIs make `Program` `internal` by default) with the environment set to `"Testing"`, and swaps the DbContext's connection string to point at `dlf_voting_test`.
- `DatabaseFixture` applies EF Core migrations once and resets data between tests via `Respawn`.
- Cookie auth is **stateless** — logout only tells the browser to expire the cookie (via `Set-Cookie` with a past expiry date); it does not invalidate the cookie server-side. Tests that manually reuse a captured cookie string after logout will still succeed against `/me`, because there's no server-side session store to check. This is expected — test logout by asserting the response's `Set-Cookie` header carries an expiry in the past, not by trying to reuse the cookie afterward.

## Known gaps / TODO

- No test yet for the 5-minute session expiry itself (would need either a configurable expiry injected via test settings, or manipulating the clock) — currently verified manually.
- Frontend is intentionally not under automated test coverage yet (internal tool, tight deadline, UI still evolving). Revisit once the actual voting flow (ballot, submission, results) is built — that's the part where a UI bug would have real impact.
