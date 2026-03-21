# RetailERP — short runbook

## Local developer

1. Install .NET 8 SDK + SQL Server / LocalDB.
2. Clone repo; `dotnet restore` + `dotnet build` on `RetailERP.sln`.
3. Apply EF migrations to your database.
4. Run app; use Swagger at `/swagger` in Development.

## After deployment

| Check | Action |
|-------|--------|
| App listening | Hit site root or `/health` |
| Database | `/health` should report SQL check; verify migrations applied |
| Logs | Inspect `Logs/retailerp-*.log` on server |
| Redis | Optional; if misconfigured, app uses in-memory cache (see startup logs) |

## Rollback

- Redeploy previous build artifact **or** restore DB backup if a bad migration shipped (test migrations in staging first).

## Incident: port bind failure (Windows)

If Kestrel fails with **socket 10013** on a port, change `applicationUrl` in `Properties/launchSettings.json` to free ports (e.g. 6000/7000) or stop the process using that port.
