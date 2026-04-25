# Security Checklist (RetailERP)

Use this before production or before any public demo with real data.

## Configuration and secrets

- [x] Production DB connection string is externalized
- [x] `Jwt:SecretKey` is validated at startup in Production
- [x] External provider secrets are intended to live outside git
- [ ] Confirm every live external provider uses real secrets and no placeholder values

## Authentication and authorization

- [x] Identity cookie login with lockout is active
- [x] Inactive-user handling is present
- [x] JWT-based API authentication is present
- [x] Role-based authorization is used on sensitive MVC and API paths
- [x] Tenant/company scoping is enforced through claims and EF filters

## Web and API hardening

- [x] HTTPS and HSTS are used in production mode
- [x] Rate limiting exists for login, POS, and API traffic
- [x] MVC antiforgery validation is enabled
- [x] API controllers explicitly opt out where token-based access is expected
- [x] CORS policy exists for API scenarios

## Data and tenant isolation

- [x] Tenant-owned entities carry `CompanyId`
- [x] Global query filters enforce tenant scoping
- [x] High-risk controllers include company checks
- [x] Negative authorization regression tests exist

## Logging and privacy

- [x] Serilog logging is in place
- [x] Correlation IDs are included for tracing
- [x] Operational logs exist for troubleshooting
- [ ] Confirm production log retention, ACLs, and masking policy on the live host

## Dependency and supply-chain status

- [ ] Upgrade `MailKit 4.15.1`

Current note:

- Build output on `2026-04-25` warns about advisory `GHSA-9j88-vvj5-vhgr`
- Recommended fix is to upgrade to `MailKit 4.16.0+`

## Deployment and perimeter checks

- [x] Production startup validation blocks weak config
- [x] Health and metrics endpoints can be protected by config
- [ ] Confirm firewall or gateway only exposes necessary ports
- [ ] Confirm self-hosted runner account has only the permissions it actually needs
- [ ] Confirm backup and restore testing is part of production operations

## Short honest summary

Security is stronger than a typical college project because the app already has Identity, JWT, tenant scoping, antiforgery, HSTS, rate limits, and production validation. The main open work is dependency hygiene, live-environment verification, and one more targeted hardening pass before wider rollout.
