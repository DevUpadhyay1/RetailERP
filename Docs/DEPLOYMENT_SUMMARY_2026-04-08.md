# Deployment Summary - 2026-04-08

## Scope
This update covers post-deployment stabilization for the Cloudflare tunnel setup, access/role correctness, runtime client-side errors, and production readiness checks.

## Completed Work
- Stabilized Cloudflare named tunnel routing and reduced to a single active connector process.
- Corrected role bootstrap behavior so first-user registration assigns both `SuperAdmin` and `Admin`.
- Promoted owner account to `SuperAdmin` and ensured active/confirmed status.
- Added helper scripts:
  - `scripts/grant_superadmin.ps1`
  - `scripts/set_user_email.ps1`
- Added business email setup guide:
  - `Docs/BUSINESS_EMAIL_SETUP.md`

## Runtime Fixes Applied
- Content Security Policy updated to allow Cloudflare Insights assets and beacon endpoint.
  - File: `Infrastructure/WebApplicationExtensions.cs`
- Service worker navigation strategy hardened:
  - Avoids caching redirected navigation responses.
  - Falls back directly to offline page when network fails.
  - Cache version bumped for clean re-cache.
  - File: `wwwroot/sw.js`
- PWA item cache refresh hardened:
  - Runs only on POS routes.
  - Skips redirected/non-JSON responses.
  - Uses same-origin credentials.
  - File: `wwwroot/js/pwa.js`
- Baseline SEO metadata added in shared layout:
  - Description, canonical URL, robots, and Open Graph tags.
  - File: `Views/Shared/_Layout.cshtml`
- Nullability warning fixed in SMTP sender.
  - File: `Services/SmtpEmailSender.cs`

## Verification Results
- Build: `dotnet build RetailERP.sln -c Release` -> success.
- Tests: `dotnet test RetailERP.sln -c Release --no-build` -> 70 passed, 0 failed, 1 skipped.
- CI workflow files reviewed:
  - `.github/workflows/ci.yml`
  - `.github/workflows/deploy.yml`
  - `.github/workflows/deploy-staging.yml`
- Latest runtime log reviewed:
  - `Logs/retailerp-20260408.log`
  - Noted one earlier startup fatal event due missing production JWT secret, followed by successful startup entries.

## Cleanup
- Removed accidental artifact: `..env.production`.

## Notes
- `ALLOW_INSECURE_COOKIES_FOR_LOCAL_HTTP` has been restored to `false` after fixing forwarded-header proxy trust.
- Trusted forwarded proxy was aligned to the current Docker gateway to preserve HTTPS scheme in redirects.
- Cloudflare DNS and tunnel changes may require local DNS cache flush on developer machines during propagation windows.
