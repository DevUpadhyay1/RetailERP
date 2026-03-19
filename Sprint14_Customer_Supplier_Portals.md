# Sprint 14 - Customer & Supplier Portals

Date: 2026-03-19
Status: Completed

## Delivered Scope

- Secure token-based customer and supplier portal links (hash stored, raw token not stored)
- Customer portal with POS bill history, invoice history, and online return request submission
- Supplier portal with purchase-order list and Accept/Reject response flow
- Portal admin console for link generation/revoke, return request review, and supplier response monitoring
- Database migration for Sprint 14 portal tables

## Main Routes

- Internal admin screen: `/PortalAdmin/Index`
- Customer portal: `/portal/customer/access?token=...`
- Supplier portal: `/portal/supplier/access?token=...`

## Migration

- Added migration: `20260319093454_Sprint14_CustomerSupplierPortals`
- Run: `dotnet ef database update`

## Quick Test Steps

1. Login as Admin/Manager/Inventory.
2. Open `/PortalAdmin/Index`.
3. Generate customer link and open it in private window.
4. Submit return request from customer portal.
5. Review the same request in portal admin and update status.
6. Generate supplier link and open it in private window.
7. Accept/Reject a draft PO in supplier portal.
8. Verify supplier response appears in portal admin.
9. Revoke a link and verify it no longer opens.

## Key Files

- `Services/PortalService.cs`
- `Controllers/PortalAdminController.cs`
- `Controllers/CustomerPortalController.cs`
- `Controllers/SupplierPortalController.cs`
- `Data/Entities/PortalAccessLink.cs`
- `Data/Entities/PortalReturnRequest.cs`
- `Data/Entities/SupplierPoResponse.cs`
- `Migrations/20260319093454_Sprint14_CustomerSupplierPortals.cs`
