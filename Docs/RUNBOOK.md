# RetailERP - short runbook

## Local developer

1. Install .NET 8 SDK + SQL Server / LocalDB.
2. Clone repo; `dotnet restore` + `dotnet build` on `RetailERP.sln`.
3. Apply EF migrations to your database.
4. Run app; use Swagger at `/swagger` in Development.

## After deployment

| Check | Action |
|-------|--------|
| App listening | Hit site root/login route |
| Readiness (LB / K8s) | Use authenticated `GET /health/ready` by default; if anonymous probes are required, set `OperationalEndpoints` flags and enforce gateway IP restrictions |
| Database | `/health` should report SQL check; verify migrations applied |
| Logs | Inspect `Logs/retailerp-*.log` on server |
| Metrics | Use authenticated `GET /metrics` by default; keep anonymous access off unless restricted at gateway/firewall |
| Redis | Optional; if misconfigured, app uses in-memory cache (see startup logs) |

## Alerting Baseline

Configure at least these alerts in your monitoring tool (Prometheus/Grafana/Datadog/New Relic):

1. **Error spike:** `retailerp_errors_total` rate increases abnormally (5xx burst).
2. **Latency spike:** `retailerp_request_duration_ms_total / retailerp_requests_total` exceeds your SLO.
3. **Health readiness down:** `/health/ready` status not Healthy for > 2 minutes.
4. **No traffic anomaly:** sudden drop in `retailerp_requests_total` during business hours.

## Rollback

- Redeploy previous build artifact **or** restore DB backup if a bad migration shipped (test migrations in staging first).

## Incident: port bind failure (Windows)

If Kestrel fails with **socket 10013** on a port, change `applicationUrl` in `Properties/launchSettings.json` to free ports (e.g. 6000/7000) or stop the process using that port.

## Incident: self-hosted Deploy Production fails at Docker build

Symptom in GitHub Actions:
- `Build application image` fails.
- Error contains: `permission denied while trying to connect to the docker API at npipe:////./pipe/docker_engine`.

Root cause:
- Runner service account cannot access Docker engine pipe on Windows.

Fix:
1. Check runner service identity:
	 `Get-CimInstance Win32_Service | Where-Object { $_.Name -like 'actions.runner*' } | Select-Object Name,StartName,State`
2. If service runs as `NT AUTHORITY\\NETWORK SERVICE`, switch it to a local account with Docker Desktop access.
3. Restart Docker Desktop.
4. Restart runner service.
5. Re-run `Deploy Production` workflow.

Emergency fallback:
- Build and deploy directly on host:
	`docker build -t retailerp:latest .`
	`docker compose --env-file C:\7th_Semester\RetailERP\.env.production -f docker-compose.prod.yml up -d`

## Logs & Retention

- **Rolling Policy:** Serilog is configured to roll files daily (`retailerp-yyyyMMdd.log`) with a hard cap of 50 MB per file.
- **Retention:** By default, it preserves the last 30 days of logs and automatically deletes older ones to prevent disk-fill.
- **ACLs (Critical):** The `Logs/` directory contains PII (Correlation IDs, failed auth attempts, IP addresses, etc.). Explicitly restrict directory permissions so that **only the Application Pool account** (e.g., `IIS AppPool\RetailERP`) or Docker run-as user has Read/Write access. Deny all other local system users.
