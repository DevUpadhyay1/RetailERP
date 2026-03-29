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

Security note: `KnownProxies` and `KnownNetworks` are currently cleared. For strict environments, lock these down to trusted proxy IPs.

## 3. Database

- Run EF migrations against production before first deploy: `dotnet ef database update`
- Prefer running migrations from CI/CD or a controlled jump box, not from app startup.

## 4. Health and metrics

- `GET /health` - SQL Server (+ Redis when configured).
- `GET /health/ready` - readiness JSON payload for orchestrators.
- `GET /metrics` - Prometheus metrics (request/error/latency counters).

## 5. CI/CD pipeline notes

- CI workflow: `.github/workflows/ci.yml`
- Runs restore/build/test and enforces a baseline line-coverage threshold.
- Staging workflow: `.github/workflows/deploy-staging.yml`
- Builds/pushes GHCR image and can deploy over SSH when staging secrets are configured.

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
