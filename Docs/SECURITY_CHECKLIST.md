# Security checklist (RetailERP)

Use this before **production** or **public demo with real data**. Not all items may apply to a college lab setup.

## Configuration & secrets

- [ ] `ConnectionStrings:DefaultConnection` is not committed with production passwords (use User Secrets / env / Key Vault).
- [ ] `Jwt:SecretKey` is a **long random** value in production (not the sample from `appsettings.json`).
- [ ] `Razorpay`, `Twilio`, `WhatsApp`, SMTP passwords are in secrets, not in git.

## Authentication & authorization

- [ ] Cookie auth: login, lockout, and inactive user sign-out behave as expected (`Program.cs`).
- [ ] New MVC actions have correct `[Authorize(Roles = "...")]` (or inherit policy).
- [ ] New API controllers use `[Authorize]` and JWT where intended; anonymous only where deliberate (`[AllowAnonymous]`).

## Web & API hardening

- [ ] HTTPS enabled in production; HSTS on (`Program.cs` pipeline for non-Development).
- [ ] Rate limiting policies still appropriate for login/POS/API (`WebApplicationBuilderExtensions`).
- [ ] CORS: if you add a SPA, restrict `WithOrigins(...)` — do not use `AllowAnyOrigin` with credentials.

## Data & multi-tenant

- [ ] Confirm global query filters / `CompanyId` cannot be bypassed by crafted requests (review critical services).
- [ ] Admin/SuperAdmin routes cannot be called by lower roles (verify once).

## Dependency & supply chain

- [ ] Run `dotnet list package --vulnerable` periodically; update packages when feasible.

## Logging & privacy

- [ ] Logs do not write full payment card numbers, passwords, or JWTs.
- [ ] Log retention and access controlled on the server.

## Deployment

- [ ] Production `deploy.yml` SSH step only enabled when `deploy_to_server` is checked and secrets exist.
- [ ] Firewall / NSG allows only necessary ports (443, 22 for admin if needed).

---

**Viva one-liner:** *“We use Identity + JWT, rate limits, security headers, secrets outside git, and CI runs tests on every push.”*
