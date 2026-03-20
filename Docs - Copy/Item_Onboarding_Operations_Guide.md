# Item Onboarding Operations Guide

This guide explains how to onboard item masters quickly for any new business tenant in RetailERP.

## 1. Where to open

Go to:
- `Inventory` -> `Item Onboarding`

Direct URL:
- `/ItemOnboarding`

## 2. What is supported now

1. Standard CSV bulk import
2. Supplier catalog style CSV import (alternate column names supported)
3. Auto-create missing Units and Categories during import
4. Update existing items by SKU (optional)
5. Starter packs by business type (Kirana, Supermarket, Hardware, Pharmacy, etc.)
6. Quick-create item from Purchase Edit screen when item is missing

## 3. CSV templates

Use:
- `Download Standard CSV`
- `Download Supplier CSV`

The importer accepts both templates and common alias headers.

## 4. Recommended onboarding sequence for a new client

1. Import supplier catalog CSV first.
2. Review import result and fix failed rows.
3. Apply starter pack only if client is new and needs base items.
4. During procurement, use Purchase -> Quick Create Item for missing SKUs.
5. Final cleanup in Items list (search/filter/edit).

## 5. Import options explained

- `Update existing items when SKU already exists`
  - ON: row updates the existing item
  - OFF: row is skipped if SKU exists

- `Auto-create missing Units/Categories`
  - ON: importer creates new Unit/Category names
  - OFF: row fails if Unit/Category does not exist

## 6. Error handling

After import, the result panel shows:
- total rows
- inserted
- updated
- skipped
- row-level issues (row number, SKU, message)

Fix failed rows in CSV and re-import.

## 7. Quick create from Purchase flow

Open:
- `Purchases` -> open a draft purchase
- Right-side panel: `Quick Create Item (if missing)`

Minimum required:
- SKU
- Name

Optional:
- Unit Cost, Unit Price, GST, HSN, Category, Unit

After create, item is immediately available in the Add Line dropdown.

## 8. Business starter packs

Use when client is starting from zero.

Flow:
1. Select Business Type
2. Choose whether to update existing items
3. Apply pack

Result shows inserted/updated/skipped counts.

## 9. Important data rules

1. SKU must be unique per tenant.
2. Barcode must not collide with another item in same tenant.
3. Name and SKU are mandatory.
4. Import supports tenant isolation automatically (current tenant context).

## 10. Internal implementation map

- Service: `Services/ItemOnboardingService.cs`
- Controller: `Controllers/ItemOnboardingController.cs`
- View: `Views/ItemOnboarding/Index.cshtml`
- Purchase quick create: `Controllers/PurchasesController.cs`, `Views/Purchases/Edit.cshtml`
- Navigation: `Views/Shared/_Layout.cshtml`

