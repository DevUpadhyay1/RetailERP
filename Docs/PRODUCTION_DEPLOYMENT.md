# Production deployment - RetailERP

This app refuses to start in Production unless critical settings are safe (see `ProductionStartupValidation`).

## 1. Environment

Set on the host or container:

| Variable | Example | Notes |
|----------|---------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Required for hardened cookies, validation, no dev seed |
| `ASPNETCORE_URLS` | `http://+:8080` | Dockerfile uses 8080 |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | Required |
| `Jwt__SecretKey` | 32+ random characters | Never use dev sample from repo |
| `AllowedHosts` | `shop.yourdomain.com` | Override `*` - validation warns if `*` |
| `OperationalEndpoints__AllowAnonymousHealth` | `false` | Keep `false` in production unless gateway-restricted |
| `OperationalEndpoints__AllowAnonymousMetrics` | `false` | Keep `false` in production unless gateway-restricted |
| `ForwardedHeaders__KnownProxies__0` | `10.0.0.10` | Trusted reverse proxy IP(s) |
| `ForwardedHeaders__KnownProxies__1` | `10.0.0.11` | Optional additional trusted proxy IP |
| `ForwardedHeaders__KnownProxies__2` | `10.0.0.12` | Optional additional trusted proxy IP |
| `ForwardedHeaders__KnownNetworks__0` | `10.0.0.0/24` | Trusted proxy CIDR(s) |
| `ForwardedHeaders__KnownNetworks__1` | `fd00:10::/64` | Optional additional trusted proxy CIDR |
| `ForwardedHeaders__KnownNetworks__2` | `172.18.0.0/16` | Optional additional trusted proxy CIDR |
| `ForwardedHeaders__ForwardLimit` | `2` | Max number of forwarding hops to trust |
| `ConnectionStrings__Redis` | `redis:6379` | Optional; health check probes Redis when cache uses it |
| `Razorpay__KeyId` | `rzp_live_...` | Required for payments |
| `Razorpay__KeySecret` | `...` | Required for payments |
| `Twilio__AccountSid` | `AC...` | Required for SMS |
| `Twilio__AuthToken` | `...` | Required for SMS |
| `WhatsApp__PhoneNumberId` | `...` | Required for WhatsApp messages |
| `WhatsApp__AccessToken` | `...` | Required for WhatsApp messages |

Azure/Linux env syntax uses double underscore `__` for nested config.

## 2. Reverse proxy (nginx / Traefik / cloud LB)

- Terminate TLS at the proxy.
- Forward headers: `X-Forwarded-For`, `X-Forwarded-Proto`.
- The app enables `ForwardedHeaders` in non-Development so redirects and URL generation stay correct.
- Configure `ForwardedHeaders:KnownProxies` and/or `ForwardedHeaders:KnownNetworks` so only trusted reverse proxies can set forwarded headers.

### Quick setup command (recommended)

Use the deploy script to write trusted proxy settings and validate them before `docker compose up`:

```powershell
.\scripts\deploy_production.ps1 \
  -AppDir "C:\retailerp" \
  -EnvFile ".env.production" \
  -KnownProxies "10.0.0.10","10.0.0.11" \
  -KnownNetworks "10.0.0.0/24"
```

The script rejects empty/placeholder/invalid proxy and CIDR values unless `-SkipForwardedHeaderValidation` is explicitly set.

Validation-only check (no container changes):

```powershell
.\scripts\deploy_production.ps1 -AppDir "C:\retailerp" -EnvFile ".env.production" -ValidateOnly
```

## 3. Database

- Run EF migrations against production before first deploy: `dotnet ef database update`
- Prefer running migrations from CI/CD or a controlled jump box, not from app startup.

## 4. Health and metrics

- By default in production, `/health`, `/health/ready`, and `/metrics` require authentication.
- If you intentionally expose them anonymously, set `OperationalEndpoints:AllowAnonymousHealth` and/or `OperationalEndpoints:AllowAnonymousMetrics` and enforce network-level restrictions at the gateway/firewall.

## 5. CI/CD pipeline notes

- CI workflow: `.github/workflows/ci.yml`
- Runs restore/build/test and enforces a baseline line-coverage threshold.
- Staging workflow: `.github/workflows/deploy-staging.yml`
- Builds/pushes GHCR image and can deploy over SSH when staging secrets are configured.
- Production workflow: `.github/workflows/deploy.yml`
- Auto deploys on every push to `main` using a self-hosted Windows runner.
- Also supports manual run with `workflow_dispatch`.

### Auto deploy flow (what happens after push)

1. Push code to `main`.
2. Self-hosted runner checks out latest code.
3. Runner builds local Docker image `retailerp:latest`.
4. Runner executes `docker compose --env-file .env.production -f docker-compose.prod.yml up -d`.
5. Updated container serves live traffic.

### Self-hosted runner requirements (production)

- GitHub Actions runner registered for this repository with labels `self-hosted`, `Windows`, `X64`.
- Docker Desktop (or Docker Engine + Compose plugin) installed and running on runner machine.
- Repository files available through checkout (includes `docker-compose.prod.yml` and `.env.production`).
- `cloudflared` process managed locally on the same host (outside Actions workflow).

Required staging secrets:
- `STAGING_HOST`
- `STAGING_USER`
- `STAGING_SSH_KEY`
- `STAGING_APP_DIR`
- `GHCR_PAT`

## 6. Docker

```bash
docker build -t retailerp:latest .
docker run -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Jwt__SecretKey="..." \
  -p 8080:8080 retailerp:latest
```

## 7. Checklist

- [ ] New random `Jwt__SecretKey` stored in vault
- [ ] Production connection string (no LocalDB)
- [ ] `AllowedHosts` set to real hostname(s)
- [ ] Razorpay/SMTP/Twilio secrets configured if used
- [ ] HTTPS end-to-end or TLS at proxy + forwarded proto
- [ ] SQL backups scheduled and restore-tested
- [ ] `Logs/` ACLs restricted to application service account only

See also: [SECURITY_CHECKLIST.md](SECURITY_CHECKLIST.md)

## 8. VPS quickstart

For a copy-paste Ubuntu 22.04 setup (Docker + Nginx + Let's Encrypt) using
`quickbusiness.co.in`, see [VPS_SETUP_UBUNTU.md](VPS_SETUP_UBUNTU.md).
