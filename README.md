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

## Deploying to production

The site runs on Azure as one Web App (website and API together) with an Azure PostgreSQL database.
How it was set up is in [`docs/deployment.md`](./docs/deployment.md); this is how to ship a new version from a Mac.

| | |
|---|---|
| Web App | `dlf-koncernvalg` |
| Resource group | `rg-itservicedesk-p-weu-voting-1` |
| Subscription | `sub-itservicedesk-p-1` |
| Address | https://dlf-koncernvalg-aqhsh9cnhac2axaq.denmarkeast-01.azurewebsites.net |

**Before you start:** don't deploy while voting is open. A deploy restarts the app for up to a minute; nobody is
signed out, but a vote sent during the restart fails and has to be sent again.

### One-time setup

```bash
brew install azure-cli
az login                                         # opens the browser; sign in with your DLF account
az account set --subscription sub-itservicedesk-p-1
```

`az login` lasts a while; when a command says you're not signed in, run it again.

### Every release

1. **Deploy what's on `develop`, and nothing else.**

   ```bash
   git switch develop
   git pull
   git status        # must say "nothing to commit"
   ```

2. **Build the release package.** This compiles the API and also builds the website into `publish/wwwroot`
   (the `PublishFrontend` target in `DlfVoting.Api.csproj` runs `npm ci` and `npm run build`).

   ```bash
   rm -rf publish app.zip
   dotnet publish backend/src/DlfVoting.Api -c Release -o publish
   ```

3. **Zip the contents of `publish`** (the files themselves, not the folder).

   ```bash
   (cd publish && zip -qr ../app.zip .)
   ```

4. **Upload it.** Azure unpacks the zip, replaces the running app and restarts it; the command waits until the
   new version has started ("Deployment has completed successfully"). A warning that it doesn't run build
   automation is expected: the package is already built.

   ```bash
   az webapp deploy --resource-group rg-itservicedesk-p-weu-voting-1 --name dlf-koncernvalg \
     --src-path app.zip --type zip
   ```

   Database changes (new migrations) are applied by the app itself when it starts (`Database__MigrateOnStartup`).

5. **Check it.**

   ```bash
   curl https://dlf-koncernvalg-aqhsh9cnhac2axaq.denmarkeast-01.azurewebsites.net/healthz    # → Healthy
   ```

   Then open the site and sign in. If something is wrong, read the app's log:

   ```bash
   az webapp log tail --resource-group rg-itservicedesk-p-weu-voting-1 --name dlf-koncernvalg   # live, Ctrl+C to stop
   ```

6. **Clean up:** `rm -rf publish app.zip` (both are git-ignored).

**Going back to the previous version:** check out the commit that was live before, and repeat steps 2-5.

## Testing

Backend integration tests live in `backend/tests/DlfVoting.Api.Tests` and run against a real PostgreSQL test database. See [`docs/testing.md`](./docs/testing.md) for setup steps, known gotchas (schema permissions, EF CLI version pinning), and how to run them:

```bash
dotnet test backend/tests/DlfVoting.Api.Tests
```

Working hours: 39h
