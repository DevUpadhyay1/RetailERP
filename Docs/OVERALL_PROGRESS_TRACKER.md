# RetailERP overall progress tracker

This document is the single source to track what was completed, what is in progress, and what is next.
Update this after every meaningful work batch.

## Current phase completion (estimated)

| Phase | Name | Completion |
|------|------|------------|
| 1 | Reliable (tests + CI) | 93% |
| 2 | Security | 97% |
| 3 | Observable & operable | 93% |
| 4 | Performance | 96% |
| 5 | Maintainable codebase | 90% |
| 6 | Demo & documentation | 93% |

**Overall completion:** **~96%**

---

## Phase-wise status update (completed so far)

### Phase 1 — Reliable (tests + CI)

- CI pipeline is active for build + tests on push/PR.
- Core service test suite is in place and expanded (POS billing, security regressions; coupon remove; loyalty attach/redeem/remove; payment removal + complete shortfall; **return/refund** stock + refund payment; over-return prevention, non-positive/empty-return validation, refund-payment removal block, and payment-removal-on-closed-bill safeguards).
- Regression-first test practice is started but not yet complete.

### Phase 2 — Security

- Identity/JWT security baseline is active.
- Global MVC anti-forgery enforcement is active; API opt-out is explicit where required.
- API role-based authorization and API rate limiting are in place.
- High-risk controller pass completed with tenant scoping checks on sensitive actions.
- Anonymous portal and sync endpoints are now rate-limited.
- Dependency vulnerability scan completed (`dotnet list package --vulnerable`) with no vulnerable packages reported.
- Manual controller/pipeline checklist pass completed for authorization coverage, HSTS/HTTPS, and rate-limit wiring.
- Automated negative authorization regressions added for cross-company access denial in high-risk controllers.
- Malformed/replay-style negative regressions added (invalid sync action/payload rejection, duplicate refund attempt rejection).
- Logging/privacy hardening pass completed for payment + messaging logs (removed sensitive response-body logging, masked phone data).

### Phase 3 — Observable & operable

- Health endpoint is available.
- Redis-aware health checks are wired when Redis is enabled.
- Logging and runbook coverage are available for operations.

### Phase 4 — Performance

- Database indexing baseline for inventory lookup paths is done.
- One concrete N+1 optimization completed on admin users listing (role loading consolidated to set-based query).
- Portal admin dashboard queries were streamlined by removing unnecessary eager-loading where projection already covers required fields.
- SuperAdmin platform dashboard company rows optimized from per-row count queries to set-based grouped counts (users/stores).
- Dashboard widget queries optimized by removing unnecessary eager-loading on projected read paths (recent lists, expiring items, category/top-item charts, EOD summary).
- Forecast snapshot loading optimized by removing unnecessary eager-loading/materialization on stock reads (project-only required fields).
- Sales report endpoint optimized to compute totals/counts in database instead of aggregating from paged in-memory rows.
- Caching strategy note added (`Docs/CACHING_STRATEGY.md`) for dashboard/report/POS hotspots.
- API items listing/details/create-read path optimized to projection-first DTO queries (`Select`) and SQL `LIKE` search (removed unnecessary eager-loading/materialization).
- API low-stock path optimized to projection-first DTO loading (removed eager `Include` materialization for item/unit/category reads).
- POS bill history list optimized to projection-only rows (removed eager graph materialization while keeping list fields unchanged).
- POS returns list optimized to projection-only rows (reduced query/materialization overhead for return history paging).
- Notifications log list optimized to projection-only paging rows (removed unnecessary include/materialization of related entities).
- Stock ledger list optimized to projection-only paging rows (reduced related-entity graph materialization for stock movement history).
- Structured broader performance review (query shape/read-only optimization on more screens) is still pending.

### Phase 5 — Maintainable codebase

- Startup/pipeline structure moved into infrastructure extensions.
- Cross-cutting app wiring is centralized and cleaner than earlier state.

### Phase 6 — Demo & documentation

- Core project docs and demo support docs are available.
- Architecture overview with Mermaid diagram: `Docs/ARCHITECTURE.md`.
- Documentation tracking process is now established with this file.

---

## Current in-progress and pending work

### Phase 1 (Reliable) pending

- Optional: further POS edge cases (multi-line partial returns with discounts/tax, mixed refund methods) as issues appear.
- **Reliability fix:** async notification sends now resolve a **new DI scope** for DB + HTTP clients (avoids `ObjectDisposedException` on scoped `DbContext` after the HTTP request ends).

### Phase 2 (Security) pending

- Apply log retention/ACL guidance on the target server (see expanded bullets in `SECURITY_CHECKLIST.md`).

### Phase 4 (Performance) pending

- Pick one slow screen and run query review:
  - N+1 checks,
  - read-only `AsNoTracking`,
  - projection cleanup (`Select` shape minimization).
- Create short caching strategy note for dashboard/report hotspots.

### Phase 6 (Documentation) pending

- Add one architecture diagram for viva/demo.
- Keep this tracker updated per work batch.

---

## Next recommended execution order

1. Add 2-4 missing regression tests (Phase 1).
2. Perform final manual security checks and logging/privacy pass (Phase 2).
3. Profile one additional heavy screen and optimize queries (Phase 4).
4. Update this tracker and `IMPROVEMENT_PHASES.md` after each batch.

---

## Phase closure summary (done vs remaining)

### Done enough for strong demo/viva

- **Phase 1 (Reliable):** CI active and regression coverage improved significantly.
- **Phase 2 (Security):** core hardening complete (authz, antiforgery policy, rate limits, tenant scoping, negative tests, privacy log hardening).
- **Phase 3 (Operable):** health checks, logging, and runbook are in place.
- **Phase 4 (Performance):** multiple high-impact query optimizations completed on admin/dashboard/report/forecast paths.
- **Phase 5 (Maintainability):** startup/infrastructure refactor and cleaner composition complete.
- **Phase 6 (Documentation):** roadmap/checklist/tracker coverage is established.

### Remaining for production-ready closure

- **Secrets/deploy hygiene:** confirm real production secrets are externalized and never committed.
- **Security operations:** verify server-side log retention/access controls and complete one final pen-style execution run.
- **Performance validation:** run basic profiling on production-like data volume and capture before/after query timings.
- **Testing depth:** continue adding regression tests for newly fixed bugs and critical money/stock edge cases.
- **Architecture artifact:** `Docs/ARCHITECTURE.md` (expand with deployment topology if needed).

### Current status

- **Estimated overall completion:** **~96%**
- **Project state:** demo-ready and strong for viva; final production-hardening tasks remain.

---

## Update template (copy for each new batch)

### Batch date

- Date:
- Focus:
- Files touched:

### Completed in this batch

- Item 1
- Item 2

### Verification

- Build:
- Tests:
- Lints:

### Risks / follow-ups

- Risk 1
- Next action
