# Post-Onboarding Update (Sprint 14+)

**Last updated:** 2026-03-29  
**Purpose:** Clear summary of what was completed *after* the onboarding implementation.

## 1. Onboarding baseline (already delivered)

These are the core migration features that were implemented in Sprint 14:

1. Item CSV onboarding upgraded with:
   - `OpeningStock`
   - `WarehouseName`
   - `BatchNumber`
   - `ExpiryDate`
2. Opening stock import now creates:
   - `Stock` rows
   - matching `StockTransaction` entries (opening-type flow)
3. New bulk CSV services:
   - customer onboarding with `OpeningBalance`
   - supplier onboarding with `OpeningBalance`
4. Item Onboarding UI/controller expanded with:
   - customer import
   - supplier import
   - customer/supplier template download actions

## 2. What was done after onboarding

Post-onboarding work focused on reliability, CI stability, and release confidence.

1. CI hardening:
   - GitHub Actions moved to Node 24 compatible setup.
   - Workflow updated to test the test project directly for stable coverage output.
   - Coverage artifact upload retained.
2. Coverage gate stabilization:
   - Replaced fragile shell parsing with robust XML parsing for Cobertura line-rate.
   - Baseline threshold enforcement is now deterministic in CI.
3. Test stability fix:
   - Integration test host now uses an ephemeral data-protection provider.
   - This removes environment-dependent key-ring permission failures in CI.
4. PDF warning cleanup:
   - Updated QuestPDF image API usage in receipt service to remove obsolete API warnings.

## 3. Current test/CI status snapshot

1. Automated tests: **51 total**
2. Passing: **50**
3. Skipped: **1** (manual scale benchmark by design)
4. Failing: **0**
5. CI pipeline now includes:
   - build
   - tests
   - coverage collection
   - coverage threshold check

## 4. Where to review in code

1. Onboarding UI/controller:
   - `Controllers/ItemOnboardingController.cs`
   - `Views/ItemOnboarding/Index.cshtml`
2. Onboarding services:
   - `Services/ItemOnboardingService.cs`
   - `Services/CustomerOnboardingService.cs`
   - `Services/SupplierOnboardingService.cs`
3. Test coverage for onboarding:
   - `RetailERP.Tests/ItemOnboardingServiceTests.cs`
   - `RetailERP.Tests/CustomerOnboardingServiceTests.cs`
   - `RetailERP.Tests/SupplierOnboardingServiceTests.cs`
4. CI + test host stabilization:
   - `.github/workflows/ci.yml`
   - `RetailERP.Tests/CustomWebApplicationFactory.cs`

## 5. Practical meaning for your team

After onboarding delivery, the project is now much safer to operate day-to-day:

1. CSV migration remains the same behavior for users.
2. Regression confidence is higher due to stronger automated checks.
3. Push-to-main failures from environment-specific test behavior are significantly reduced.
