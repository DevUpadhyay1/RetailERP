# RetailERP

ASP.NET Core 8 retail / POS / inventory platform with MVC UI, REST APIs (JWT), SignalR, background jobs, multi-tenant company scoping, GST/e-invoice support, and Serilog-based observability.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server or LocalDB
- Optional Redis (`ConnectionStrings:Redis`) for distributed cache and data-protection key storage

## Quick start

```bash
git clone <your-repo>
cd RetailERP
dotnet restore RetailERP.sln
dotnet build RetailERP.sln -c Release
dotnet ef database update
dotnet run --project RetailERP.csproj
```

- Default local URLs are defined in `Properties/launchSettings.json`.
- Swagger is available in Development at `/swagger`.
- Health endpoints: `/health` and `/health/ready`.
- Metrics endpoint: `/metrics`.

> UI assets are loaded from CDN so a fresh clone renders correctly without `libman restore`.

## Configuration and secrets

| Setting | Where |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | `appsettings*.json`, User Secrets, or environment |
| `Jwt:SecretKey` | User Secrets or environment in production |
| `Razorpay`, `Twilio`, `WhatsApp`, `Smtp` | User Secrets or environment only |

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING" --project RetailERP.csproj
dotnet user-secrets set "Jwt:SecretKey" "YOUR_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS" --project RetailERP.csproj
```

## Validation snapshot

Latest repo verification used for the docs refresh on `2026-04-25`:

- Release build: passed
- Automated tests: `88 passed, 1 skipped`
- Coverage collection: enabled in CI, current baseline gate `2%`
- Current dependency warning: `MailKit 4.15.1` shows a moderate vulnerability warning and should be upgraded

## Tests and CI/CD

```bash
dotnet test RetailERP.Tests/RetailERP.Tests.csproj -c Release --no-build
```

Active workflows:

- `.github/workflows/ci.yml`: restore, build, test, Cobertura coverage upload, threshold gate
- `.github/workflows/deploy-staging.yml`: GHCR image build and optional SSH-based staging deploy
- `.github/workflows/deploy.yml`: self-hosted Windows production deploy for `main`

Important production note:

- The self-hosted runner account must be able to access Docker on the host.
- If Actions fails with Docker pipe permission errors, fix runner account permissions first.
- See `Docs/PRODUCTION_DEPLOYMENT.md` and `Docs/RUNBOOK.md` for the exact recovery steps.

## Project layout

| Area | Purpose |
| --- | --- |
| `Controllers/` | MVC controllers plus `Api/` REST endpoints |
| `Services/` | Business logic for POS, stock, invoicing, loyalty, sync, reports, and integrations |
| `Data/` | EF Core context, entities, migrations, seeders |
| `Infrastructure/` | Middleware, startup helpers, production validation |
| `RetailERP.Tests/` | xUnit tests for services, integration, regression, and startup validation |

## Operations

1. Publish the app or deploy Docker image with `ASPNETCORE_ENVIRONMENT=Production`.
2. Configure DB, JWT, and external-provider secrets outside git.
3. Run EF migrations in a controlled step.
4. Verify `/health`, `/health/ready`, logs, and metrics after deploy.

## Current maturity

The project is now beyond the "college demo only" stage and is moving toward controlled production readiness. A realistic current maturity snapshot is about `82%` toward a fully polished professional rollout, with the biggest remaining gaps around operator workflows, coverage depth, and a few deployment/security cleanups.

See:

- [Docs/OVERALL_PROGRESS_TRACKER.md](Docs/OVERALL_PROGRESS_TRACKER.md)
- [Docs/IMPROVEMENT_PHASES.md](Docs/IMPROVEMENT_PHASES.md)
- [Docs/PROJECT_REVIEW_2026-04-25.md](Docs/PROJECT_REVIEW_2026-04-25.md)
- [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md)
- [Docs/REAL_WORLD_MAPPING.md](Docs/REAL_WORLD_MAPPING.md)
- [Docs/SECURITY_CHECKLIST.md](Docs/SECURITY_CHECKLIST.md)
- [Docs/CI_CD_Workflow_Guide.md](Docs/CI_CD_Workflow_Guide.md)
- [Docs/PRODUCTION_DEPLOYMENT.md](Docs/PRODUCTION_DEPLOYMENT.md)
- [Docs/RUNBOOK.md](Docs/RUNBOOK.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

Set according to your institution or distribution model.
