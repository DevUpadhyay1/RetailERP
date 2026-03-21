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
| API authorization audit (every endpoint) | Todo |
| CORS / production headers review | Todo |

**Next steps:** Walk through `SECURITY_CHECKLIST.md` before any public deployment.

---

## Phase 3 — Observable & operable

| Item | Status |
|------|--------|
| `/health` endpoint | Done |
| Serilog file + console logging | Done |
| Runbook | `RUNBOOK.md` |
| Redis in health check (optional) | Todo |

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

---

## Suggested order for *your* next learning sessions

1. Run **CI** on GitHub after a push; open a green/red log once end-to-end.  
2. Read **SECURITY_CHECKLIST.md** and tick what already applies.  
3. Add **one** new test the next time you fix a bug (regression test).  
4. Optional: draw **one** architecture diagram (Browser → MVC/API → Services → SQL).
