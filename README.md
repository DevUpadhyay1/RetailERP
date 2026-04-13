# RetailERP

ASP.NET Core 8 retail / POS / inventory system with MVC UI, REST API (JWT), SignalR, background jobs, GST/e-invoice features, and Serilog logging.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server or **LocalDB** (default connection string uses LocalDB)
- Optional: **Redis** (`ConnectionStrings:Redis`) for distributed cache; app falls back to memory cache if Redis is missing or unreachable

## Quick start

```bash
git clone <your-repo>
cd RetailERP
dotnet restore RetailERP.sln
dotnet build RetailERP.sln
dotnet ef database update   # from project folder, if you use EF CLI
dotnet run --project RetailERP.csproj
```

- **HTTP profile:** see `Properties/launchSettings.json` (runs on `http://localhost:5820` or `https://localhost:7240` by default).
- **Swagger (Development):** `/swagger`
- **Health:** `GET /health` (includes SQL Server check)
- **Metrics:** `GET /metrics` (Prometheus text format)

> **Note:** UI assets (Bootstrap CSS/JS) are loaded via CDN (jsDelivr) to ensure the UI renders correctly on a fresh clone without requiring `libman restore`.

## Configuration & secrets

| Setting                                  | Where                                                                   |
| ---------------------------------------- | ----------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection`    | `appsettings.json`, User Secrets, or environment                        |
| `Jwt:SecretKey`                          | **Use User Secrets or env in production** - change from default in repo |
| `Razorpay`, `Twilio`, `WhatsApp`, `Smtp` | User Secrets / environment - do not commit real keys                    |

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING" --project RetailERP.csproj
dotnet user-secrets set "Jwt:SecretKey" "YOUR_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS" --project RetailERP.csproj
```

## Tests & CI

```bash
dotnet test RetailERP.sln -c Release
```

GitHub Actions workflow: `.github/workflows/ci.yml` (build + test on push/PR).
CI also collects Cobertura coverage and enforces a baseline threshold (`COVERAGE_THRESHOLD` in workflow).
Staging deployment workflow: `.github/workflows/deploy-staging.yml` (build image + optional SSH deploy).
Production deployment workflow: `.github/workflows/deploy.yml` (self-hosted Windows runner).

Important for production workflow:

- The runner service account must have access to Docker engine on the host.
- If Actions fails with `permission denied while trying to connect to the docker API at npipe:////./pipe/docker_engine`, fix runner account permissions first.
- See `Docs/PRODUCTION_DEPLOYMENT.md` for exact remediation steps.

## Project layout (high level)

| Area               | Purpose                                                           |
| ------------------ | ----------------------------------------------------------------- |
| `Controllers/`     | MVC + `Api/` REST controllers                                     |
| `Services/`        | Business logic (POS, invoices, loyalty, etc.)                     |
| `Data/`            | EF Core context, entities, migrations                             |
| `Infrastructure/`  | Startup composition (`AddRetailErp`, `UseRetailErpPipelineAsync`) |
| `RetailERP.Tests/` | xUnit + in-memory EF integration and unit tests                   |

## Operations (short runbook)

1. **Deploy:** publish `RetailERP.csproj`, set `ASPNETCORE_ENVIRONMENT=Production`, configure connection string and secrets on the host.
2. **Database:** run migrations (`dotnet ef database update`) against production DB from a controlled pipeline or maintenance window.
3. **Logs:** file logs under `Logs/retailerp-*.log` (rolling daily); console in Development.
4. **Smoke checks:** `GET /health` after deploy. See [Docs/RUNBOOK.md](Docs/RUNBOOK.md) for more.

If you deploy through GitHub Actions self-hosted runner, ensure the runner account can run `docker version` and `docker build` without permission errors.

## Roadmap / improvement phases (~80% complete)

See [Docs/IMPROVEMENT_PHASES.md](Docs/IMPROVEMENT_PHASES.md) for what's done next (tests, security, performance).
Overall tracker: [Docs/OVERALL_PROGRESS_TRACKER.md](Docs/OVERALL_PROGRESS_TRACKER.md).
Viva / Real-World Demo guide: [Docs/REAL_WORLD_MAPPING.md](Docs/REAL_WORLD_MAPPING.md).
Architecture overview: [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) - Caching notes: [Docs/CACHING_STRATEGY.md](Docs/CACHING_STRATEGY.md).
Operational runbook: [Docs/RUNBOOK.md](Docs/RUNBOOK.md).
Security before production: [Docs/SECURITY_CHECKLIST.md](Docs/SECURITY_CHECKLIST.md).
**Production deploy:** [Docs/PRODUCTION_DEPLOYMENT.md](Docs/PRODUCTION_DEPLOYMENT.md) (env vars, Docker, health, proxy).
**Post-onboarding summary:** [Docs/POST_ONBOARDING_UPDATE.md](Docs/POST_ONBOARDING_UPDATE.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

Your project / institution - set as appropriate.
