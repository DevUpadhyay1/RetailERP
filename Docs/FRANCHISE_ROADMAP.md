# Franchise Roadmap (Simple)

## Purpose
This file explains the current franchise process in simple steps for business users and admins.

## Who Does What
- SuperAdmin: Handles company creation and mapping request approval only.
- Main Company Admin (Brand Owner): Raises mapping requests and manages franchise agreement and royalty settings.
- Franchise Admin: Runs day-to-day franchise operations after login credentials are shared.

## End-to-End Flow
1. Brand Owner Admin raises a request in Franchise Mapping Requests.
2. SuperAdmin opens Franchise -> Mapping Requests.
3. If franchise company does not exist:
   - Open Tenants -> Companies -> New Company.
   - Set Business Type = Franchise.
   - Select Main Company (Franchisor).
   - Create franchise admin account (email and password).
4. SuperAdmin returns to Mapping Requests.
5. SuperAdmin selects that franchise company and clicks Approve.
6. System maps franchise company to the main company.
7. Franchise admin credentials are shared with the franchise team.
8. After that, Admin side handles agreement and royalty operations.

## Database Mapping (Tenant Model)
The system uses the same tenant pattern based on CompanyId.

### 1) Mapping Request Table
Table: FranchiseMappingRequests
- RequestingCompanyId = main company that asked for mapping.
- MappedOperatorCompanyId = selected franchise company (filled on approval).
- CompanyId = tenant scope key (same as RequestingCompanyId).
- Status: 1 Pending, 2 Approved, 3 Rejected, 4 Cancelled.

### 2) Company Master Table
Table: Companies
- CompanyId = tenant key.
- ParentCompanyId = main company link.
- BusinessType = Franchise for franchise company records.

When mapping is approved:
- Companies.ParentCompanyId of franchise company is set to the requesting main company.
- Companies.BusinessType is set to Franchise.
- FranchiseMappingRequests.Status becomes Approved.

## Royalty Dashboard: How It Is Mapped
Royalty uses agreement and payment records, and sales from POS bills.

### A) Agreement Base
Table: FranchiseAgreements
- FranchisorCompanyId
- FranchiseeCompanyId
- RoyaltyPercent
- MonthlyFlatFee
- MinMonthlyRoyalty

### B) Monthly Royalty Calculation Source
Source Table: PosBills
Filter used by system:
- PosBills.CompanyId = FranchiseeCompanyId
- PosBills.Status = 2 (Completed)
- BillDate inside selected month

Formula used:
- GrossSales = sum(PosBills.GrandTotal)
- RoyaltyAmount = GrossSales * RoyaltyPercent / 100
- TotalDue = max(RoyaltyAmount + MonthlyFlatFee, MinMonthlyRoyalty)

### C) Payment Records
Table: RoyaltyPayments
- Stores one row per agreement per month.
- Status: 1 Pending, 2 Paid, 3 Overdue, 4 Waived.
- AmountPaid and PaidAtUtc are stored when payment is recorded.

### D) Dashboard Numbers
Royalty dashboard shows:
- Total agreements
- Active agreements
- Total pending royalty = sum(TotalDue where Status = Pending)
- Total collected royalty = sum(AmountPaid where Status = Paid)
- Recent payments list

## Quick Troubleshooting
- If SuperAdmin cannot see requests, open Franchise -> Mapping Requests and refresh session (logout/login once).
- If no selectable franchise company appears in approval dropdown, create it first in Companies with Business Type = Franchise.
- If royalty looks wrong, verify POS bills are completed and posted in the selected month.

## Current Product Decision
For now, SuperAdmin flow is focused on mapping setup.
Agreement and royalty management is handled from the Admin side after mapping is done.
