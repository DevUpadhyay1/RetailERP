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
| Readiness (LB / K8s) | `GET /health/ready` returns JSON; includes SQL (+ Redis when enabled) |
| Database | `/health` should report SQL check; verify migrations applied |
| Logs | Inspect `Logs/retailerp-*.log` on server |
| Redis | Optional; if misconfigured, app uses in-memory cache (see startup logs) |

## Rollback

- Redeploy previous build artifact **or** restore DB backup if a bad migration shipped (test migrations in staging first).

## Incident: port bind failure (Windows)

If Kestrel fails with **socket 10013** on a port, change `applicationUrl` in `Properties/launchSettings.json` to free ports (e.g. 6000/7000) or stop the process using that port.

## Logs & Retention

- **Rolling Policy:** Serilog is configured to roll files daily (`retailerp-yyyyMMdd.log`) with a hard cap of 50 MB per file.
- **Retention:** By default, it preserves the last 30 days of logs and automatically deletes older ones to prevent disk-fill.
- **ACLs (Critical):** The `Logs/` directory contains PII (Correlation IDs, failed auth attempts, IP addresses, etc.). Explicitly restrict directory permissions so that **only the Application Pool account** (e.g., `IIS AppPool\RetailERP`) or Docker run-as user has Read/Write access. Deny all other local system users.
