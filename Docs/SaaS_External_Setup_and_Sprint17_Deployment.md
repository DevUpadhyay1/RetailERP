# RetailERP SaaS Guide

Date: 2026-03-20  
Scope: Payment strategy, external setup checklist, and deployment sprint plan

## 1) Payment Strategy for SaaS (What You Should Use)

### Current project status
- Your code currently uses Razorpay with one shared configuration section (`Razorpay`) in app config.
- This is fine for single-merchant usage but not ideal for SaaS multi-merchant billing collection.

### Recommended SaaS model (best for your case)
1. Merchant-owned payment account (for store billing to their customers)
- Each tenant/company uses their own Razorpay/Stripe credentials.
- Customer payments settle to that tenant account, not platform account.

2. Platform-owned payment account (for your SaaS subscription)
- Keep a separate platform Razorpay/Stripe account for charging ERP subscription fees.
- Never mix merchant transaction flow and platform subscription flow.

### Which gateway to use
- Primary recommendation now: Razorpay Multi-Tenant (merchant-owned) because your project is already integrated and India-focused.
- Add Stripe as optional provider later (provider abstraction), especially for global expansion.

## 2) External Setup Points Found in Project

These are directly visible in your codebase/config:

- App config keys in `appsettings.json`:
  - `ConnectionStrings`
  - `Smtp`
  - `Razorpay`
  - `Twilio`
  - `WhatsApp`
  - `Jwt`

- Wiring in `Program.cs`:
  - SQL Server via `UseSqlServer`
  - Health checks
  - Razorpay options binding
  - Twilio options binding
  - WhatsApp options binding
  - SMTP options binding
  - Localization middleware
  - Health endpoint mapping (`/health`)

### Setup checklist by service

1. Database (Required)
- Configure production SQL Server connection string.
- Apply EF migrations on target database.
- Enable backups + restore test.

2. Redis cache (Recommended for production)
- Provide Redis connection string.
- If unavailable, app falls back to in-memory cache (works but not ideal for scale).

3. Razorpay (Required for online payments)
- Configure `Razorpay:KeyId` and `Razorpay:KeySecret`.
- Use test keys in staging, live keys in production.
- For SaaS: move to per-tenant key storage and runtime tenant key resolution.

4. SMTP Email (Required for reliable email features)
- Configure `Smtp:Host`, `Port`, `User`, `Password`, `FromEmail`.
- Code throws if SMTP password is missing.

5. Twilio SMS (Optional but recommended)
- Configure `Twilio:AccountSid`, `AuthToken`, `FromNumber`, `IsEnabled=true`.
- If not configured, service logs simulated behavior.

6. WhatsApp Cloud API (Optional but recommended)
- Configure `WhatsApp:PhoneNumberId`, `AccessToken`, `IsEnabled=true`.
- If not configured, service logs simulated behavior.

7. JWT Secret (Required for API auth)
- Use strong unique production secret (`Jwt:SecretKey`) from secure secret store.

8. Domain + SSL (Required for production)
- Map domain DNS to host.
- Install TLS certificate.
- Force HTTPS.

## 3) Suggested SaaS Improvements (Next)

1. Payment provider abstraction
- Add provider interface (`IPaymentGateway`) with Razorpay and Stripe implementations.
- Tenant-level provider selection (`Razorpay` or `Stripe`) in company settings.

2. Secure tenant credential vault
- Store gateway keys encrypted at rest.
- Mask secrets in UI.
- Rotate/revoke credentials with audit log.

3. Billing split model
- Tenant customer transactions -> tenant gateway.
- SaaS plan invoices -> platform gateway.

4. Operational controls
- Per-tenant gateway health status.
- Failed payment retry and alerting.
- Webhook idempotency checks and replay protection.

## 4) Sprint 17 (Post Sprint 16) - Deployment & Go-Live Sprint

Name: **Sprint 17 - Deployment, Production Hardening & Go-Live**

Duration: 10-14 days

### Goal
Deploy RetailERP to production with secure, observable, recoverable operations and release checklist closure.

### Work Items

1. Infrastructure & environment
- Provision production app host and SQL Server.
- Configure Redis, storage paths, and log retention.
- Setup environment variables/secrets store for all external keys.

2. CI/CD release pipeline
- Build -> test -> migration -> deploy pipeline.
- Add staging and production gates.
- Add rollback strategy (previous build + DB rollback process).

3. Security hardening
- Production secret rotation.
- Strict CORS/CSP review.
- Admin account hardening and mandatory 2FA policy for privileged roles.

4. Data and migration safety
- Pre-deploy DB backup.
- Migration dry-run on staging.
- Post-deploy smoke verification scripts.

5. External integrations go-live
- Razorpay live mode verification.
- SMTP live email send check.
- Twilio/WhatsApp production sender verification.

6. Monitoring and incident readiness
- Uptime check for `/health`.
- Error alerting and log dashboard.
- On-call runbook + incident response checklist.

7. Go-live checklist and handover
- Final UAT sign-off.
- SOP docs for support/admin operations.
- Launch day playbook and hypercare window.

### Definition of Done
- Production URL live with HTTPS.
- All critical workflows verified in production:
  - Login
  - POS bill
  - Purchase receive
  - Stock adjust
  - Online payment
  - Notification send
- Backups tested and rollback documented.
- Monitoring + alerts active.

## 5) Recommended Order from Here

1. Complete Sprint 16 (tests + CI/CD baseline).  
2. Implement Razorpay multi-tenant merchant-owned architecture.  
3. Execute Sprint 17 deployment plan.  
4. Add Stripe as optional provider after production stabilization.
