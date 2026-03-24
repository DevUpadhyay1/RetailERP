# Production deployment — RetailERP

This app **refuses to start in Production** unless critical settings are safe (see `ProductionStartupValidation`).

## 1. Environment

Set on the host or container:

| Variable | Example | Notes |
|----------|---------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Required for hardened cookies, validation, no dev seed |
| `ASPNETCORE_URLS` | `http://+:8080` | Dockerfile uses 8080 |
| `ConnectionStrings__DefaultConnection` | SQL Server connection string | Required |
| `Jwt__SecretKey` | 32+ random characters | **Never** use dev sample from repo |
| `AllowedHosts` | `shop.yourdomain.com` | Override `*` — validation warns if `*` |
| `ConnectionStrings__Redis` | `redis:6379` | Optional; health check probes Redis when cache uses it |
| `Razorpay__KeyId` | `rzp_live_...` | Required for payments |
| `Razorpay__KeySecret` | `...` | Required for payments |
| `Twilio__AccountSid` | `AC...` | Required for SMS |
| `Twilio__AuthToken` | `...` | Required for SMS |
| `WhatsApp__PhoneNumberId` | `...` | Required for WhatsApp messages |
| `WhatsApp__AccessToken` | `...` | Required for WhatsApp messages |

**Azure / Linux env syntax:** double underscore `__` nests configuration sections.

## 2. Reverse proxy (nginx / Traefik / cloud LB)

- Terminate TLS at the proxy.
- Forward headers: `X-Forwarded-For`, `X-Forwarded-Proto`.
- The app enables **ForwardedHeaders** in non-Development so `UseHttpsRedirection` and links work.

**Security:** `KnownProxies` / `KnownNetworks` are cleared so any proxy is trusted. For strict networks, lock this down in `WebApplicationBuilderExtensions` to your proxy IPs.

## 3. Database

- Run EF migrations against production before first deploy:  
  `dotnet ef database update`
- Prefer running migrations from CI/CD or a jump box, not from the web container on every start.

## 4. Health

- `GET /health` — SQL Server (+ Redis when configured).
- Use for load balancer probes (HTTP 200 = healthy).

## 5. Docker

```bash
docker build -t retailerp:latest .
docker run -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="..." \
  -e Jwt__SecretKey="..." \
  -p 8080:8080 retailerp:latest
```

## 6. Checklist

- [ ] New random `Jwt__SecretKey` (store in vault)
- [ ] Production connection string (no LocalDB)
- [ ] `AllowedHosts` set to real hostname(s)
- [ ] Razorpay / SMTP / Twilio secrets set if those features are used
- [ ] HTTPS end-to-end or TLS at proxy + forwarded proto
- [ ] Backups for SQL Server scheduled
- [ ] `Logs/` directory ACLs restricted to the application service account only

See also: [SECURITY_CHECKLIST.md](SECURITY_CHECKLIST.md)
