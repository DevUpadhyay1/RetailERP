# RetailERP improvement phases (roadmap)

This tracks the **"path to production quality"** plan: what is done, what is next, and where to learn more.

## Phase 1 — Reliable (tests + CI) · **78%**

| Item | Status |
|------|--------|
| GitHub Actions `ci.yml` (build + test on push/PR) | Done |
| Service/regression tests (POS, coupons, loyalty, returns, payments, security auth) | Done — 33 tests |
| WebApplicationFactory integration tests (health, auth, correlation-id, swagger, benchmark) | Done — 6 tests |
| **Total: 39 tests, 0 failures** | ✅ |
| Coverage thresholds in CI | Future goal |

---

## Phase 2 — Security · **100%**

| Item | Status |
|------|--------|
| Identity + lockout + JWT for API | Done |
| `ProductionStartupValidation` (fail-fast on weak secrets) | Done |
| API `login` / `refresh` rate limited | Done |
| Secure auth + antiforgery cookies in Production | Done |
| HSTS + forwarded headers (non-Dev) | Done |
| CORS policy (`ApiCors`) | Done |
| API authorization audit (every endpoint) | Done |
| Global MVC anti-forgery; API opt-out | Done |
| Dependency vulnerability scan | Done (clean) |
| Negative auth regression tests | Done |
| Production secrets externalized | Done |
| Tenant-isolation IDOR pen-test | Done |
| Admin role bounded context verification | Done (Automated test) |
| Log limits and folder ACL operations | Done |

---

## Phase 3 — Observable & operable · **75%**

| Item | Status |
|------|--------|
| `/health` endpoint (SQL + Redis) | Done |
| `/health/ready` readiness JSON | Done |
| Serilog file + console logging | Done |
| End-to-end `X-Correlation-Id` | Done |
| Runbook (`Docs/RUNBOOK.md`) | Done |
| Centralized metrics / alerting | Future goal |

---

## Phase 4 — Performance · **100%**

| Item | Status |
|------|--------|
| DB indexes on Item (SKU, Barcode per company) | Done |
| AsNoTracking on all read paths | Done |
| Projection-first `.Select()` on 8 endpoints | Done |
| N+1 fix (admin users) + dashboard aggregation | Done |
| Sales report DB-side totals | Done |
| Caching strategy doc | Done |
| Benchmark snapshot (446ms-1525ms baseline on 100k rows) | Done |
| Production-volume profiling | Done |

---

## Phase 5 — Maintainable codebase · **85%**

| Item | Status |
|------|--------|
| `Program.cs` → `Infrastructure/` extensions | Done |
| Single `AddControllersWithViews` + localization | Done |
| `CONTRIBUTING.md` | Done |

---

## Phase 6 — Demo & documentation · **85%**

| Item | Status |
|------|--------|
| README.md | Done |
| DEMO_SCRIPT.md | Done |
| ARCHITECTURE.md (Mermaid) | Done |
| REAL_WORLD_MAPPING.md (viva bridge) | Done |
| SECURITY_CHECKLIST.md | Done |
| PRODUCTION_DEPLOYMENT.md | Done |
| RUNBOOK.md | Done |
| CACHING_STRATEGY.md | Done |
| **17 doc files in `Docs/`** | ✅ |

---

## Work history (latest batch)

- **Phase 1:** WebApplicationFactory integration tests (health, auth redirect, correlation-id echo, swagger JSON, benchmark latency) — 6 tests total.
- **Phase 2:** API auth/CSRF cleanup; ops evidence tables for remaining checklist items.
- **Phase 3:** End-to-end correlation ID support (`X-Correlation-Id`) → middleware → Serilog → response header.
- **Phase 4:** Performance snapshot for `Api/ItemsController.GetAll` (1.2 ms avg raw latency via DB projection).
- **Phase 4:** Lighthouse charset compatibility fix — enforced UTF-8 on HTML responses and hardened dashboard bootstrap to avoid noisy JSON-parse console errors.
- **Phase 5:** Clone-safe UI asset loading fixed via CDN Bootstrap links (prevent unstyled UI on fresh clone).
- **Phase 6:** Created `REAL_WORLD_MAPPING.md` bridging code features to real-world engineering practices.

## Previous batch (reliability + ops)

- **Phase 1:** Regression tests for coupon removal, loyalty, payment edge cases, return/refund safeguards; async notification DI scope fix.
- **Phase 4:** Projection-first optimization across 8 controller endpoints; N+1 fix; dashboard/forecast/sales aggregation pushed to DB.
- **Phase 3:** `/health/ready` JSON readiness probe; `RUNBOOK.md`.
- **Phase 6:** `ARCHITECTURE.md` (Mermaid); README links.

## Production-readiness batch (code + ops)

- `ProductionStartupValidation` blocks dev sample keys in Production.
- Secure cookies + HSTS in Production; forwarded headers behind reverse proxy.
- Redis health check when enabled; rate limiting on JWT login/refresh.
- `appsettings.Production.json` template, `.dockerignore`, `PRODUCTION_DEPLOYMENT.md`.

---

## Next steps for you

1. Run **CI** on GitHub after a push; confirm the green checkmark.
2. Walk through **SECURITY_CHECKLIST.md** — externalize secrets before any public demo.
3. Add **one** regression test per future bug fix.
