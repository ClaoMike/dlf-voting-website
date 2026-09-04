# dlf-voting-website
Website used by DLF to vote on internal stuff.

## Running locally

### Backend

```bash
dotnet run --project backend/src/DlfVoting.Api
```

Runs at `http://localhost:5120` by default.

### Frontend

```bash
cd frontend
npm run dev
```

Runs at `http://localhost:5173` by default.

Both need to be running simultaneously for the app to work end-to-end.

## Testing

Backend integration tests live in `backend/tests/DlfVoting.Api.Tests` and run against a real PostgreSQL test database. See [`docs/testing.md`](./docs/testing.md) for setup steps, known gotchas (schema permissions, EF CLI version pinning), and how to run them:

```bash
dotnet test backend/tests/DlfVoting.Api.Tests
```

Working hours: 4h
