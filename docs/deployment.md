# Deploying to Azure

## How it runs

One **Azure App Service** (Linux, .NET 9) serves both the website and the API from the same address:

```
https://<app>.azurewebsites.net/          → the React site (wwwroot, built by dotnet publish)
https://<app>.azurewebsites.net/api/...   → the API
https://<app>.azurewebsites.net/healthz   → health check (app + database)
```

Same address means the session cookies are first-party and there is no CORS to configure. Data lives in
**Azure Database for PostgreSQL – Flexible Server**. GitHub Actions builds, tests and (when asked) deploys.

## What the app already does for production

| | |
|---|---|
| Website + API in one package | `dotnet publish` runs `npm ci && npm run build` and puts the site in `wwwroot` |
| Page routing | Unknown paths return `index.html` (React routes); unknown `/api/...` paths are 404 |
| Caching | Hashed files in `/assets` are cached for a year; `index.html` is always revalidated |
| HTTPS | HSTS, HTTPS redirect, `Secure` cookies; honours Azure's `X-Forwarded-Proto` |
| Security headers | Content-Security-Policy, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`, `Permissions-Policy` |
| Sessions | 10 minutes without activity; cookie keys stored in the database, so restarts and redeploys don't sign anyone out |
| Sign-in protection | 10 attempts per account per 5 minutes (per account, not per IP: the office shares one IP) |
| First administrator | Created from the `InitialAdmin__*` settings when there are no administrators (no passwords in code) |
| Database schema | Migrations applied at startup when `Database__MigrateOnStartup=true` |

## Azure resources

Pick one EU region close to Denmark for everything (e.g. North Europe, West Europe or Sweden Central).

1. **Resource group**, e.g. `rg-dlf-voting`.
2. **PostgreSQL Flexible Server**, version 16.
   - Burstable B1ms is enough for the data; it allows about 50 connections.
   - Networking: public access with "Allow public access from any Azure service" (simplest), or a private VNet.
   - Create the database `dlf_voting` and an app login that owns it.
3. **App Service plan**, Linux.
   - B1 for trying it out. For voting day use at least P0v3/P1v3: every sign-in deliberately costs ~0.1 s of CPU
     (password hashing), so hundreds of people signing in at once need cores.
   - Start with **one instance** (see "Scaling out" below).
4. **Web App** on that plan, runtime stack **.NET 9 (Linux)**. In its settings:
   - Configuration → General: **HTTPS Only** on, minimum TLS 1.2, **Always On** on.
   - Monitoring → Health check: path `/healthz`.

## App settings (Web App → Settings → Environment variables)

| Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=<server>.postgres.database.azure.com;Database=dlf_voting;Username=<user>;Password=<password>;SSL Mode=Require;Maximum Pool Size=20` |
| `Database__MigrateOnStartup` | `true` |
| `InitialAdmin__Email` | the first administrator's email (**remove after first sign-in**) |
| `InitialAdmin__Password` | 20-64 chars, an uppercase letter, a digit, a special character (**remove after first sign-in**) |
| `InitialAdmin__Username` | optional; defaults to the email |

`ASPNETCORE_ENVIRONMENT` is `Production` by default; leave it.

**Connection pool:** `Maximum Pool Size` × number of instances must stay below the server's connection limit
(B1ms: about 50). Without it, a burst of voters can open more connections than PostgreSQL allows.

## First deployment

1. Deploy (below). On first start the app creates the tables and the first administrator; the log shows
   `Created the initial administrator ...`.
   On a brand-new database EF Core also logs one `Failed executing DbCommand ... __EFMigrationsHistory` error
   before the migrations: that is EF checking for a table that doesn't exist yet, and it is harmless.
2. Open `https://<app>.azurewebsites.net/healthz` → `Healthy`.
3. Sign in at `/login/admin`, then **delete the `InitialAdmin__*` settings**.
4. Import the voters, add the voting options, and try a vote with a test account.

## Deploying from GitHub Actions

`.github/workflows/ci.yml` builds and tests every push and pull request, and publishes the package as an artifact.
Deploying is manual: Actions → CI → Run workflow → tick "Deploy".

One-time setup (sign-in with OpenID Connect, so no password is stored in GitHub):

1. In Microsoft Entra ID, create an app registration, add a **federated credential** for this repository with
   entity type *Environment* = `production`, and give it the **Website Contributor** role on the Web App.
2. In GitHub → Settings → Environments, create `production` (optionally with required reviewers), and add the
   variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `AZURE_WEBAPP_NAME`.

To deploy by hand instead: `dotnet publish backend/src/DlfVoting.Api -c Release -o publish`, zip the contents of
`publish`, and `az webapp deploy --resource-group <rg> --name <app> --src-path publish.zip --type zip`.

## Scaling out (more than one instance)

Works, with two things to know:

- Each instance runs the migrations at startup. Deploy new migrations while running one instance, or set
  `Database__MigrateOnStartup=false` and apply them separately (`dotnet ef migrations bundle`).
- The sign-in limit is counted per instance, so the effective limit is multiplied by the instance count.

Sessions work across instances (the cookie keys are in the database).

## Before going live

- The old default admin password was in `AdminSeeder.cs` (now removed) and is still in the git history: make sure
  it isn't used for anything.
- Administrators can see who voted for what. If the election is meant to be secret, that needs a decision first.
