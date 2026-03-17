# RetailERP vs DMART (Gap Analysis)

Source documents:
- DMART Database Dictionary: [DMART_Database_Dictionary.md](DMART_Database_Dictionary.md)
- DMART Workflow: [DMART_Workflow.md](DMART_Workflow.md)

## Quick summary
DMART describes a **POS billing system** (web + desktop) with **barcode-based billing**, **payments**, **returns/refunds**, **loyalty/coupons**, **multi-store**, **stock transaction ledger**, and **desktop offline sync**.

Your current RetailERP is a **web ERP MVP** focused on:
- master data (items, warehouses, customers, employees)
- inventory stock on hand + transfer + adjust + low-stock list
- sales invoices (draft → post) with stock deduction
- purchase receiving (draft → receive) with stock increase
- audit log + sales report

## Table-by-table comparison (DMART → RetailERP)

### User & access
- **tbl_roles / tbl_users**
  - Present (different implementation): RetailERP uses ASP.NET Identity (roles + users).
  - Differences vs DMART:
    - DMART stores **permissions as JSON** per role; RetailERP uses role names + controller `[Authorize]`.
    - DMART ties a user to a **store** (`store_id`); RetailERP has no “store assignment per user” concept.
    - DMART uses an **is_active** flag and logs **last_login**; RetailERP doesn’t have those fields as part of your custom user model.
- **tbl_stores**
  - Missing.
  - Closest existing concept: **warehouses**, but DMART stores include legal/tax fields (GST/PAN), codes, city/state, etc.

### Product & inventory
- **tbl_units**
  - Missing.
  - RetailERP items don’t have a unit of measure (Kg/L/Pcs).
- **tbl_categories**
  - Missing.
  - RetailERP items don’t have category hierarchy.
- **tbl_products**
  - Partially present as RetailERP **Items**.
  - Missing fields/workflow compared to DMART:
    - barcode (EAN/UPC)
    - brand
    - unit (Kg/Pcs/L)
    - category + parent/child hierarchy
    - HSN code
    - tax model (GST%, CGST/SGST split)
    - MRP / purchase price vs selling price
    - audit trail for price changes
- **tbl_stock**
  - Present (similar) as RetailERP **Stocks** (per warehouse).
- **tbl_stock_transactions**
  - Missing.
  - RetailERP currently tracks some events in **AuditLogs**, but DMART expects a full stock movement ledger:
    - IN / OUT / ADJUSTMENT / RETURN
    - references to purchase/bill IDs

### Customer & loyalty
- **tbl_customers**
  - Partially present as RetailERP **Customers**.
  - Missing fields/workflow compared to DMART:
    - customer_code
    - mandatory mobile lookup behavior
    - loyalty balance and membership fields
- **tbl_loyalty_transactions**
  - Missing.
- **tbl_coupons**
  - Missing.

### Billing & payment
- **tbl_bills / tbl_bill_items**
  - Partially present as RetailERP **Invoices / InvoiceLines**.
  - Differences vs DMART POS billing:
    - item entry is dropdown-based (not barcode scan)
    - no receipt printing
    - no cart-style POS flow
    - missing discounts/coupons at bill or line level
    - missing GST breakup and tax totals
    - DMART uses store-scoped billing; RetailERP is warehouse-scoped
- **tbl_payments**
  - Missing.
  - DMART needs payment modes (cash/card/UPI/wallet) and split payments.

### Supplier & procurement
- **tbl_suppliers**
  - Present (basic) as RetailERP **Suppliers**.
  - DMART may expect additional vendor/legal/tax fields.
- **tbl_purchase_orders**
  - Partially present but different.
  - RetailERP currently implements direct **purchase receipt** (draft → receive). DMART expects:
    - PO creation
    - GRN (goods receipt note)
    - partial receipt tracking (PARTIAL vs RECEIVED)

### System & sync
- **tbl_audit_logs**
  - Present as RetailERP **AuditLogs**.
  - DMART expects broader coverage (login/logout, price changes, etc.).
- **tbl_sync_log**
  - Missing.
  - RetailERP currently has no desktop offline sync workflow.

## Workflow comparison (DMART → RetailERP)

Present (or mostly present):
- User login/roles (not permission-JSON)
- Inventory stock on hand + manual adjustment
- Stock IN (purchase receiving) and Stock OUT (invoice posting)
- Low stock list
- Sales report
- Audit log

Missing or significantly different:
- POS Billing core flow (barcode scan, cart UI, receipt printing)
- Payment processing (multi-mode + split payments)
- Return & refund workflow (stock reversal + refund record)
- Loyalty earning/redemption and coupons
- End-of-day (EOD) closing (cash reconciliation, daily summary)
- Multi-store model (stores separate from warehouses)
- Offline desktop sync (sync log, conflict rules)
- Stock transaction ledger table (IN/OUT/ADJ/RETURN history)

## Recommended build order (to match DMART workflow)
1) POS-ready product model: units, categories, barcode, GST/HSN basics
2) Billing enhancements: barcode scan add-line + bill tax/discount totals
3) Payments module: cash/card/UPI + split payments
4) Return/refund module: item-level returns + stock reversal
5) Stock transaction ledger (tbl_stock_transactions equivalent)
6) Purchase Orders + GRN + partial receipts
7) Loyalty + coupons
8) Stores + user-to-store assignment
9) Offline sync (desktop) + sync logs

## References (Attach extra sheet if required):
> - <https://firebase.google.com/docs/firestore>
> - <https://developers.openai.com/api-reference/overview/>
> - <http://twilio.com/docs/alpha/ai-assistants/quickstart>
> - <https://firebase.google.com/docs/firestore/quickstart>
