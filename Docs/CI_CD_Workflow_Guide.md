# RetailERP CI/CD Workflow Guide

This document describes the current GitHub Actions setup in the repository as of `2026-04-25`.

## 1. Active workflows

### CI (`.github/workflows/ci.yml`)

Triggers:

- Push to `main`, `master`, or `develop`
- Pull request targeting `main`, `master`, or `develop`

What it does:

1. Checks out the repository
2. Installs .NET 8
3. Restores the solution
4. Builds in `Release`
5. Runs the test project with Cobertura coverage collection
6. Enforces a minimum line-coverage threshold
7. Uploads the coverage XML as an artifact

Current note:

- The threshold is intentionally low (`2%`) and should be raised over time.

### Staging deploy (`.github/workflows/deploy-staging.yml`)

Triggers:

- Push to `main`
- Manual `workflow_dispatch`

What it does:

1. Builds a Docker image
2. Pushes it to GHCR
3. Optionally deploys over SSH when staging secrets are configured

Important behavior:

- This workflow is image-first.
- If staging secrets are missing, the image build still works and the deploy step is skipped safely.

### Production deploy (`.github/workflows/deploy.yml`)

Triggers:

- Push to `main` or `develop`
- Manual `workflow_dispatch`

What it does:

1. Runs a lightweight smoke-test job on GitHub-hosted Ubuntu
2. Deploys to production only when the branch is `main`
3. Uses a self-hosted Windows runner
4. Builds `retailerp:latest`
5. Runs `docker compose -p retailerp --env-file ... -f docker-compose.prod.yml up -d`
6. Prints compose status and prunes dangling images

Important behavior:

- `develop` triggers the smoke-test job, but not the production deploy job.
- `main` triggers both the smoke-test job and the production deploy path.

## 2. What this gives the project

This setup already moves RetailERP beyond a basic student project in a few key ways:

- Broken code is caught in CI before manual deployment decisions.
- Coverage artifacts are generated automatically.
- Staging supports a registry-based image flow.
- Production is deployable from source control through a repeatable Docker-based process.

## 3. What is still lightweight

The pipeline is useful and real, but not fully enterprise-hardened yet.

Current gaps:

1. CI coverage gate is present, but the threshold is still a starter baseline.
2. Smoke tests in `deploy.yml` are placeholder-level, not full application checks.
3. Production deploy currently uses `retailerp:latest`; versioned image promotion would be safer.
4. Rollback is operationally possible, but not yet formalized as a first-class workflow step.
5. Self-hosted runner permissions still matter; Docker access can block deployment if misconfigured.

## 4. Safe release process for this repo

Recommended release flow:

1. Validate locally with `dotnet build RetailERP.sln -c Release`.
2. Run `dotnet test RetailERP.Tests/RetailERP.Tests.csproj -c Release --no-build`.
3. Push to a branch and let CI go green.
4. If using staging, confirm GHCR image build and optional server deploy.
5. Before production, ensure `.env.production` and secrets are correct on the host.
6. Push the approved change to `main`.
7. Verify `/health`, `/health/ready`, login, dashboard, POS, and invoice flows after deploy.

## 5. How to verify each workflow

### CI

- Open GitHub Actions
- Confirm `CI` ran
- Check build, test, and coverage upload steps

### Staging

- Confirm `Deploy Staging` ran
- Check the produced GHCR image tag
- If staging secrets exist, verify the SSH deploy step

### Production

- Confirm `Deploy Pipeline` ran on `main`
- Check the self-hosted runner logs
- Check Docker compose status on the server
- Verify health endpoints and a basic smoke path on the live URL

## 6. Known operational caution

If production deploy fails with a Docker pipe permission error on Windows, the most likely cause is that the GitHub Actions runner service account cannot access Docker Desktop or Docker Engine. See `PRODUCTION_DEPLOYMENT.md` and `RUNBOOK.md` for the exact fix steps.
