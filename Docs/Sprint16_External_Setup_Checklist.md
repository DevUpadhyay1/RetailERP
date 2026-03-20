# Sprint 16 External Setup Checklist

Date: 2026-03-20
Project: RetailERP
Purpose: Exact external connections/secrets required for CI/CD, testing, and production deployment.

## A) Required GitHub Secrets (for workflows)

1. `GHCR_PAT`
- Personal access token with package read/write for GHCR.
- Used by deploy workflow to pull container on server.

2. `PROD_HOST`
- Production server host/IP.

3. `PROD_USER`
- SSH user for deployment server.

4. `PROD_SSH_KEY`
- Private SSH key (OpenSSH format) for deploy access.

5. `PROD_APP_DIR`
- Deployment directory path on server (example: `/opt/retailerp`).

## B) Required Production Runtime Variables (.env.production)

Use `deploy/.env.production.template` as base.

### Core
- `APP_IMAGE`
- `SA_PASSWORD`
- `JWT_SECRET`

### SMTP (email)
- `SMTP_HOST`
- `SMTP_PORT`
- `SMTP_USE_STARTTLS`
- `SMTP_USER`
- `SMTP_PASSWORD`
- `SMTP_FROM_EMAIL`
- `SMTP_FROM_NAME`

### Razorpay (current gateway)
- `RAZORPAY_KEY_ID`
- `RAZORPAY_KEY_SECRET`

### Twilio (optional)
- `TWILIO_ACCOUNT_SID`
- `TWILIO_AUTH_TOKEN`
- `TWILIO_FROM_NUMBER`
- `TWILIO_IS_ENABLED`

### WhatsApp Cloud API (optional)
- `WHATSAPP_PHONE_NUMBER_ID`
- `WHATSAPP_ACCESS_TOKEN`
- `WHATSAPP_IS_ENABLED`

## C) Infrastructure Connections Needed

1. SQL Server
- Container or managed SQL instance reachable from app.
- Ensure strong SA/admin password.
- Run migrations before go-live.

2. Redis
- Container or managed Redis instance reachable from app.
- Used for distributed caching.

3. HTTPS reverse proxy
- Nginx/Caddy/IIS in front of app container.
- Domain + TLS certificate.

## D) Sprint 16 What Was Added

1. Test project
- `RetailERP.Tests` with xUnit tests for JWT and item onboarding service.

2. CI workflow
- `.github/workflows/ci.yml`
- Restore, build, test, coverage artifact upload, docker build check.

3. Deployment workflow
- `.github/workflows/deploy.yml`
- Manual trigger, build/push image to GHCR, deploy over SSH.

4. Containerization
- `Dockerfile`
- `.dockerignore`
- `docker-compose.yml`
- `docker-compose.prod.yml`

5. Deployment script
- `scripts/deploy_production.ps1`

6. Production env template
- `deploy/.env.production.template`

## E) Runbook (first deploy)

1. Configure GitHub secrets listed in section A.
2. On server, create deploy directory and copy:
- `docker-compose.prod.yml`
- `.env.production` (from template, with real values)
3. Trigger GitHub action: `Deploy Production`.
4. Verify app:
- `/health` endpoint returns healthy.
- Login, POS, purchase receive, stock adjust, payment, notifications.

## F) Important Next Payment Upgrade

For SaaS model, move from single shared Razorpay keys to tenant-specific gateway credentials:
- Merchant-owned gateway for tenant customer billing.
- Platform-owned gateway only for your SaaS subscription billing.
