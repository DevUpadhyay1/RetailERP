# Security checklist (RetailERP)

Use this before **production** or **public demo with real data**. Not all items may apply to a college lab setup.

## Configuration & secrets

- [ ] `ConnectionStrings:DefaultConnection` is not committed with production passwords (use User Secrets / env / Key Vault).
- [ ] `Jwt:SecretKey` is a **long random** value in production (not the sample from `appsettings.json`).
- [x] With `ASPNETCORE_ENVIRONMENT=Production`, the app **validates** DB + JWT at startup (`ProductionStartupValidation`) — weak dev secrets **fail fast**.
- [ ] `Razorpay`, `Twilio`, `WhatsApp`, SMTP passwords are in secrets, not in git.

## Authentication & authorization

- [ ] Cookie auth: login, lockout, and inactive user sign-out behave as expected (`Program.cs`).
- [x] New MVC actions have correct `[Authorize(Roles = "...")]` (or inherit policy) based on current controller audit.
- [x] New API controllers use `[Authorize]` and JWT where intended; anonymous only where deliberate (`[AllowAnonymous]`) based on current controller audit.
- [x] API controllers now have explicit role-based authorization at controller level (`Controllers/Api/*`).

## Web & API hardening

- [x] HTTPS enabled in production; HSTS on (`Program.cs`/pipeline non-Development branch).
- [x] Rate limiting policies exist and are applied for login/POS/API paths (`WebApplicationBuilderExtensions`, auth and API controllers).
- [ ] CORS: if you add a SPA, restrict `WithOrigins(...)` — do not use `AllowAnyOrigin` with credentials.
- [x] API rate limiting is enforced by default via `ApiBaseController`.
- [x] Anonymous portal endpoints are rate-limited (`CustomerPortalController`, `SupplierPortalController`).
- [x] Anonymous sync queue endpoint is rate-limited and validates key request fields (`SyncController.QueueChange`).
- [x] CORS policy (`ApiCors`) added with explicit production behavior (`Cors:AllowedOrigins`).
- [x] Global MVC anti-forgery validation enabled (`AutoValidateAntiforgeryTokenAttribute`), with API controllers explicitly opting out.

## Data & multi-tenant

- [ ] Confirm global query filters / `CompanyId` cannot be bypassed by crafted requests (review critical services).
- [ ] Admin/SuperAdmin routes cannot be called by lower roles (verify once).
- [x] High-risk controllers now enforce company scoping checks on sensitive actions (payment, e-invoice, portal admin).
- [x] Automated negative tests added for cross-company forbidden access in high-risk controllers (`RetailERP.Tests/SecurityAuthorizationRegressionTests.cs`).
- [x] Additional malformed/replay-style negative tests added (sync invalid payload/action + duplicate refund attempt rejection).

## Dependency & supply chain

- [x] `dotnet list package --vulnerable` run in current audit batch (no vulnerable packages reported on configured sources).

## Logging & privacy

- [x] Logs reviewed and hardened to avoid sensitive response-body/token-like exposure on payment/SMS/WhatsApp paths.
- [ ] Log retention and access controlled on the server.

## Deployment

- [ ] Production `deploy.yml` SSH step only enabled when `deploy_to_server` is checked and secrets exist.
- [ ] Firewall / NSG allows only necessary ports (443, 22 for admin if needed).

---

**Viva one-liner:** *“We use Identity + JWT, rate limits, security headers, secrets outside git, and CI runs tests on every push.”*
