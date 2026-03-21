# RetailERP improvement phases (roadmap)

This tracks the **“path to production quality”** plan: what is done, what is next, and where to learn more.

## Phase 1 — Reliable (tests + CI)

| Item | Status |
|------|--------|
| GitHub Actions `ci.yml` (build + test on push/PR) | Done |
| Unit/integration tests for critical services | In progress (11+ tests; focus: POS, JWT, onboarding) |
| High coverage of whole codebase | Future goal — prioritize **money + stock + auth** |

**Next steps for you:** Add tests when you change billing, stock, or coupons; run `dotnet test` before every push.

---

## Phase 2 — Security

| Item | Status |
|------|--------|
| Identity + lockout + JWT for API | Already in app |
| Security checklist doc | `SECURITY_CHECKLIST.md` |
| Production JWT / connection validation (fail fast) | Done — `ProductionStartupValidation` |
| API `login` / `refresh` rate limited (`Login` policy) | Done |
| Secure auth + antiforgery cookies in Production | Done |
| Forwarded headers behind reverse proxy | Done (non-Dev) |
| HSTS (non-Dev) | Done |
| API authorization audit (every endpoint) | Ongoing — inherit `ApiBaseController` |
| CORS for SPA | Todo if you add a separate front-end origin |

**Next steps:** Walk through `SECURITY_CHECKLIST.md` and [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md) before go-live.

---

## Phase 3 — Observable & operable

| Item | Status |
|------|--------|
| `/health` endpoint | Done |
| SQL + **Redis** health probes (when Redis cache enabled) | Done |
| Serilog file + console logging | Done |
| Runbook | `RUNBOOK.md` |
| Kubernetes-style readiness JSON | Todo (optional) |

---

## Phase 4 — Performance

| Item | Status |
|------|--------|
| Indexes on Item (SKU, Barcode per company) | Done in `ApplicationDbContext` |
| Review hot queries (N+1, AsNoTracking) | Todo — do when data grows |
| Caching strategy doc | Todo |

---

## Phase 5 — Maintainable codebase

| Item | Status |
|------|--------|
| `Program.cs` → `Infrastructure/` (`AddRetailErp`, pipeline) | Done |
| Single `AddControllersWithViews` + localization | Done |
| CONTRIBUTING.md | Done |

**Next steps:** Keep new cross-cutting setup in `Infrastructure/`, not only in `Program.cs`.

---

## Phase 6 — Demo & documentation

| Item | Status |
|------|--------|
| README.md | Done |
| DEMO_SCRIPT.md | Done |
| Architecture diagram (optional) | Todo — draw once for viva |

---

## What we did in the latest “phase work” batch

- **Phase 1:** Added `CancelBillAsync` and `AddLineAsync` (duplicate scan) tests.
- **Phase 2:** Added `SECURITY_CHECKLIST.md`; small null-safety fixes in controllers (sort/dir binding, POS includes).
- **Phase 4:** Confirmed DB indexes already exist — no migration needed for barcode/SKU.
- **Tracking:** This file (`IMPROVEMENT_PHASES.md`) so you always know **what’s next**.

## Production-readiness batch (code + ops)

- **`ProductionStartupValidation`:** In `Production`, requires non-empty DB connection + strong JWT (blocks dev sample keys).
- **Cookies:** Auth + antiforgery use `Secure` in Production; HSTS configured for non-Development.
- **Reverse proxy:** `ForwardedHeaders` (X-Forwarded-For / Proto) when not in Development.
- **Health:** Redis health check registered when Redis cache is actually enabled.
- **API abuse:** `[EnableRateLimiting("Login")]` on JWT `login` and `refresh`.
- **Ship:** `appsettings.Production.json` template, `.dockerignore`, `Docs/PRODUCTION_DEPLOYMENT.md`.

---

## Suggested order for *your* next learning sessions

1. Run **CI** on GitHub after a push; open a green/red log once end-to-end.  
2. Read **SECURITY_CHECKLIST.md** and tick what already applies.  
3. Add **one** new test the next time you fix a bug (regression test).  
4. Optional: draw **one** architecture diagram (Browser → MVC/API → Services → SQL).
