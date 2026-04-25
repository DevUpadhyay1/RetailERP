# RetailERP Improvement Phases

This file tracks the "path to production quality" for the current codebase.

## Phase 1 - Reliable (tests + CI) - **88%**

| Item | Status |
| --- | --- |
| GitHub Actions CI on push/PR | Done |
| Release build validation | Done |
| Service and regression tests | Done |
| Integration and startup validation tests | Done |
| Current test snapshot | `88 passed, 1 skipped` |
| Coverage artifact upload | Done |
| Coverage threshold gate | Done, but still low at `2%` |
| Missing | More critical end-to-end flows for billing, purchase receive, and stock reconciliation |

## Phase 2 - Security - **84%**

| Item | Status |
| --- | --- |
| Identity lockout and inactive-user handling | Done |
| JWT auth for API | Done |
| MVC antiforgery and API opt-out model | Done |
| Rate limiting | Done |
| CORS policy | Done |
| HSTS and forwarded headers | Done |
| Production startup validation | Done |
| Tenant scoping and authorization regression tests | Done |
| Remaining | Upgrade `MailKit`, final external-service secret audit, targeted pen-test pass |

## Phase 3 - Observable and operable - **80%**

| Item | Status |
| --- | --- |
| `/health` | Done |
| `/health/ready` | Done |
| `/metrics` | Done |
| Serilog logs | Done |
| Correlation ID flow | Done |
| Runbook and monitoring guides | Done |
| Remaining | Live dashboard adoption, alert tuning, centralized log shipping |

## Phase 4 - Performance - **81%**

| Item | Status |
| --- | --- |
| Tenant-safe indexes on hot tables | Done |
| `AsNoTracking()` on read-heavy paths | Done |
| Projection-first query cleanup | Done |
| Dashboard/report aggregation optimization | Done |
| Optional Redis cache | Done |
| Benchmark tooling | Done |
| Remaining | More realistic POS load testing and search/catalog hot-path profiling |

## Phase 5 - Maintainable codebase - **79%**

| Item | Status |
| --- | --- |
| Clear controller/service/data separation | Done |
| Infrastructure helper classes exist | Done |
| Contributing guide | Done |
| Remaining | Reduce startup duplication between `Program.cs` and `Infrastructure/*`, continue cleanup of older patterns and doc drift |

## Phase 6 - Demo and documentation - **89%**

| Item | Status |
| --- | --- |
| README | Done |
| Demo script | Done |
| Architecture guide | Done |
| Real-world mapping | Done |
| Security checklist | Done |
| Production deployment guide | Done |
| Runbook | Done |
| Project review and recommendations | Done |
| Remaining | Business-user training docs and exportable polished manuals |

## Latest verified baseline

- Release build passed on `2026-04-25`
- Test run passed with `88 passed, 1 skipped`
- Coverage run succeeded with current line coverage around `2.19%`
- Build warns about `MailKit 4.15.1` vulnerability and should be upgraded

## Recommended next execution order

1. Upgrade `MailKit` to `4.16.0+`.
2. Add inventory count and cycle count workflows.
3. Add purchase receiving and GRN-style partial receive flow.
4. Add supplier-item mapping with preferred vendor and last purchase cost.
5. Raise CI coverage threshold after each new test batch.
6. Consolidate startup configuration to a single composition path.
