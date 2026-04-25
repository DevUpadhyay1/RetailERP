# RetailERP: Real-World Engineering Mapping

This file connects repo features to real operational value so you can explain the system like an engineer, not only like a coder.

## 1. Correlation ID and structured logs

**What we built:**

- Correlation IDs across the request pipeline
- Serilog-based logging
- File logging for server-side diagnostics

**Why it matters in the real world:**

When something breaks in production, teams do not debug by guessing. They trace one request from entry to exit. Correlation IDs make that possible.

**How to prove it:**

1. Run the app.
2. Call `/health`.
3. Inspect the response headers for `X-Correlation-Id`.
4. Open `Logs/retailerp-*.log` and confirm the same ID appears in logs for that request.

## 2. JWT, rate limiting, and tenant-safe APIs

**What we built:**

- JWT-protected APIs
- Rate-limited login and API paths
- Company-aware authorization checks

**Why it matters in the real world:**

Retail SaaS products are attacked by bots, scraped by scripts, and must never leak one tenant's data into another tenant's view.

**How to prove it:**

1. Hit `/api/auth/login` too quickly and observe `429 Too Many Requests`.
2. Try cross-company access with a mismatched user/company and observe forbidden behavior.

## 3. Integration and regression testing

**What we built:**

- Unit and service tests
- Integration-style tests
- Startup validation tests
- Coverage artifact collection in CI

**Why it matters in the real world:**

Professional teams need confidence that routing, middleware, auth, and DB mapping still work after every change.

**How to prove it:**

1. Run `dotnet test RetailERP.Tests\\RetailERP.Tests.csproj --no-build`.
2. Current verified snapshot is `88 passed, 1 skipped`.

## 4. Health, readiness, and metrics

**What we built:**

- `/health`
- `/health/ready`
- `/metrics`

**Why it matters in the real world:**

These endpoints let load balancers, dashboards, and operations teams decide whether the app is alive, ready, and behaving normally.

**How to prove it:**

1. Open `/health/ready`.
2. Confirm SQL and Redis readiness behavior.
3. Open `/metrics` and confirm counters are emitted for monitoring tools.

## 5. Offline sync and PWA support

**What we built:**

- Service worker
- IndexedDB item and offline bill storage
- Sync queue with background processing

**Why it matters in the real world:**

Retail counters cannot stop when internet quality drops. Offline capture and later sync are highly practical features for unstable-network environments.

**How to prove it:**

1. Open the POS/PWA experience.
2. Simulate offline behavior.
3. Confirm queued offline data is synced after reconnect.

## 6. Background jobs and real-time updates

**What we built:**

- Email queue worker
- Sync queue worker
- Stock alert worker
- EOD auto worker
- SignalR notifications

**Why it matters in the real world:**

Not all work should happen inside the user's request. Background jobs keep the app responsive while still processing alerts, reports, and queue retries.

## 7. Real business compliance

**What we built:**

- GST reporting
- E-invoice support
- Invoice templates and numbering rules
- Audit trails and stock ledgers

**Why it matters in the real world:**

Retail software is only useful if it supports operational compliance, traceability, and document accuracy, not just sales entry.
