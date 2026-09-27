# dlf-voting-website
Website used by DLF to vote on internal stuff.

## Running locally

### Backend

Needs the .NET 10 SDK (`global.json` pins 10.0.x).

```bash
dotnet run --project backend/src/DlfVoting.Api
```

Runs at `http://localhost:5120` and applies database migrations on startup (`appsettings.Development.json`).

On a fresh database there is no administrator yet. Set one up once with user-secrets (never in a file):

```bash
cd backend/src/DlfVoting.Api
dotnet user-secrets set "InitialAdmin:Email" "you@example.com"
dotnet user-secrets set "InitialAdmin:Password" "<20-64 chars, uppercase, digit, special character>"
```

### Frontend

```bash
cd frontend
npm run dev
```

Open `http://localhost:5173`. The site calls the API at `/api` on its own address, as in production; Vite passes
those calls on to the backend (`API_PROXY_TARGET=http://localhost:<port>` if the backend runs elsewhere).

Both need to be running simultaneously for the app to work end-to-end.

## Deploying

See [`docs/deployment.md`](./docs/deployment.md) (Azure App Service + Azure Database for PostgreSQL).

## Testing

Backend integration tests live in `backend/tests/DlfVoting.Api.Tests` and run against a real PostgreSQL test database. See [`docs/testing.md`](./docs/testing.md) for setup steps, known gotchas (schema permissions, EF CLI version pinning), and how to run them:

```bash
dotnet test backend/tests/DlfVoting.Api.Tests
```

Working hours: 39h
