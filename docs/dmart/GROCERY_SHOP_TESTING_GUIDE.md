# RetailERP (DMART Alignment) — Grocery Shop Testing Guide

This document is a **manual testing guide** for the features implemented so far.
Use it like a checklist: enter the sample data, run each flow, and compare the **Expected Results**.

---

## 1) Pre-checks (before testing)

### Database migrations (important)
Make sure your database schema is up-to-date (otherwise Stock Ledger / new fields may not work):

- Run: `dotnet ef database update`

Expected:
- Latest migrations apply successfully.

### Accounts / roles
- **Register is intentionally hidden** (self-registration disabled after bootstrap).
- Login with an **Admin** user.
- Create these staff accounts using **Admin → Create User**:
  - Manager
  - Cashier
  - Inventory
  - Finance

Suggested emails/passwords (change as you like):

| Role | Email | Password |
|---|---|---|
| Admin | (existing) | (existing) |
| Manager | manager@grocery.local | Test@1234 |
| Cashier | cashier@grocery.local | Test@1234 |
| Inventory | inventory@grocery.local | Test@1234 |
| Finance | finance@grocery.local | Test@1234 |

Expected:
- Each user can login.
- Each user sees only the modules allowed by their role (nav menu changes).

### Data safety
- Make sure your DB is the one you want to test (LocalDB / SQL Server connection string).
- If you already had stock from earlier, **Stock Ledger will only show movements from Phase 2 onward** (see “Ledger baseline note” in Section 6).

---

## 2) Grocery Shop sample master data (enter this first)

### 2.1 Store
Create **1 Store** (Modules → Inventory → Stores):

| Field | Value (example) | Your actual entry |
|---|---|---|
| Store Code | GS-001 | |
| Store Name | GreenMart Grocery | |
| Address | Main Road, City Center | |
| Phone | 0300-0000000 | |
| GST/PAN (if fields exist) | (leave blank if not present) | |

Expected:
- Store is created.

### 2.2 Warehouse
Create **1 Warehouse** and link it to the Store (Modules → Inventory → Warehouses):

| Field | Value (example) | Your actual entry |
|---|---|---|
| Name | GS-001 Warehouse | |
| Address | Backside storage | |
| Store | GS-001 (Store) | |

Expected:
- Warehouse exists and shows the Store mapping.

### 2.3 Units
Create these Units (Modules → Inventory → Units):

| Unit Name | Notes |
|---|---|
| pcs | pieces |
| kg | kilograms |
| ltr | liters |

Expected:
- Units list shows these items.

### 2.4 Categories
Create these Categories (Modules → Inventory → Categories):

| Category | Parent (if applicable) |
|---|---|
| Staples | (none) |
| Dairy | (none) |
| Beverages | (none) |
| Snacks | (none) |
| Rice | Staples |

Expected:
- Category hierarchy works (Rice under Staples).

### 2.5 Items (5 items)
Create these 5 items (Modules → Inventory → Items):

| # | SKU | Name | Barcode | Unit | Category | MRP | Purchase Price | Selling Price (UnitPrice) | GST% | HSN | Reorder Level | IsActive |
|---:|---|---|---|---|---|---:|---:|---:|---:|---|---:|---|
| 1 | ITM-001 | Basmati Rice 5kg | 8901000000011 | kg | Rice | 2200 | 2000 | 2100 | 0 | 1006 | 5 | true |
| 2 | ITM-002 | Sugar 1kg | 8901000000012 | kg | Staples | 170 | 150 | 160 | 0 | 1701 | 10 | true |
| 3 | ITM-003 | Milk 1L | 8901000000013 | ltr | Dairy | 220 | 200 | 210 | 5 | 0401 | 20 | true |
| 4 | ITM-004 | Cooking Oil 1L | 8901000000014 | ltr | Staples | 650 | 600 | 630 | 5 | 1512 | 10 | true |
| 5 | ITM-005 | Potato Chips 50g | 8901000000015 | pcs | Snacks | 60 | 45 | 55 | 12 | 1905 | 30 | true |

Expected:
- Item Create/Edit screens accept these fields.
- Barcode uniqueness: no two items can share the same barcode.

### 2.6 Supplier (required for Purchases)
Create **1 Supplier** (Modules → Inventory → Suppliers):

| Field | Value (example) |
|---|---|
| Name | FreshFoods Distributors |
| Phone/Email | optional |

Expected:
- Supplier is created.

### 2.7 Customer (required for Invoices)
Create **1 Customer** (Modules → Sales/CRM → Customers):

| Field | Value (example) |
|---|---|
| Name | Walk-in Customer |
| Phone/Email | optional |

Expected:
- Customer is created.

---

## 3) Stock setup (initial stock on hand)

You have two ways to get opening stock for testing:

### Option A (simple): Purchase Receive to create stock + ledger
This is recommended because it creates:
- Stock On Hand change
- Stock Ledger entries (Type = IN)

Steps:
1) Modules → Inventory → Purchases → Create
2) Create a draft purchase to your warehouse.
3) Add lines for the 5 items with quantities (example below).
4) Click **Receive**.

Suggested quantities for opening stock:

| SKU | Qty | Unit Cost |
|---|---:|---:|
| ITM-001 | 5 | 2000 |
| ITM-002 | 30 | 150 |
| ITM-003 | 40 | 200 |
| ITM-004 | 20 | 600 |
| ITM-005 | 100 | 45 |

Expected:
- Stock On Hand increases for each item.
- Stock Ledger shows **IN** rows for each line.

### Option B (advanced): Manual stock row + Adjust
If you create stock rows manually, you may not get a ledger entry unless you adjust.

---

## 4) Test cases — Inventory & Masters

### TC-INV-01: Units CRUD
Steps:
1) Create a unit (e.g., `box`).
2) Edit it.
3) Delete it (only if safe).

Expected:
- All operations work.
- Uniqueness: unit names should be unique.

### TC-INV-02: Categories hierarchy
Steps:
1) Create parent: Staples.
2) Create child: Rice under Staples.

Expected:
- Parent/child shows correctly.

### TC-INV-03: Items DMART fields
Steps:
1) Open Item Edit for one item.
2) Confirm Barcode, Unit, Category, MRP, PurchasePrice, GST%, HSN are visible.
3) Try setting the same barcode on 2 items.

Expected:
- Duplicate barcode should fail (unique constraint).

Note:
- If an item already has Stock / invoices / purchases, it cannot be deleted (ERP-safe). Set `IsActive = false` instead.

---

## 5) Test cases — Stock correctness flows

### TC-STK-01: Purchase Receive (Stock IN)
Precondition:
- Purchase Draft exists with lines.

Steps:
1) Receive the purchase.
2) Open Stock On Hand for the warehouse.
3) Open Stock Ledger.

Expected:
- Stock On Hand increased.
- Ledger contains rows with:
  - Type = IN
  - Warehouse = your warehouse
  - RefType = Purchase
  - RefId = purchase id

### TC-STK-02: Invoice Post (Stock OUT)
Steps:
1) Modules → Sales/CRM → Invoices → Create
2) Choose customer + warehouse + date + optional employee.
3) Add 2–3 lines (use unit price).
4) Click **Post (Deduct Stock)**.

Expected:
- Stock On Hand decreased for those items.
- Ledger contains rows with:
  - Type = OUT
  - RefType = Invoice
  - RefId = invoice id

### TC-STK-03: Manual Stock Adjust (ADJUSTMENT)
Steps:
1) Modules → Inventory → Stock On Hand
2) Pick a stock row → Adjust
3) Enter DeltaQty (e.g., `-2`) and Reason (e.g., `Damaged pack`).

Expected:
- Stock On Hand changes by DeltaQty.
- Ledger contains:
  - Type = ADJUSTMENT
  - Qty = DeltaQty (signed)
  - Reason = your reason

### TC-STK-04: Stock Transfer (TRANSFER two-entry rule)
Precondition:
- You need **two warehouses**.

Setup:
1) Create a 2nd warehouse (e.g., `GS-001 Front Store`).

Steps:
1) Modules → Inventory → Stock Transfer
2) From = main warehouse
3) To = second warehouse
4) Select an item and Qty
5) Submit

Expected:
- Stock On Hand: source decreases, destination increases.
- Ledger: **two rows** with the same RefId:
  - Type = TRANSFER, Qty negative (source)
  - Type = TRANSFER, Qty positive (destination)

---

## 6) Stock Ledger — “how to read it”

### What the ledger means
- `Stocks` = current balance
- `StockTransactions` (ledger) = history of changes

Rules:
- `Qty > 0` means stock increased
- `Qty < 0` means stock decreased
- `RefType/RefId` tells you what caused it

### Ledger baseline note (important)
If your database already had stock quantities before we started writing ledger entries, then:
- Stock On Hand might not equal the sum of ledger rows.
- This is normal until you decide to create an **Opening Balance** transaction approach.

---

## 7) Test cases — Security / roles / admin tooling

### TC-AUTH-01: Register hidden
Steps:
1) Logout.
2) Open Landing page.

Expected:
- No public registration (unless the system is in “first user bootstrap” mode).

### TC-AUTH-02: Admin creates user + assigns role
Steps:
1) Login as Admin.
2) Admin → Create User.
3) Create a Cashier user.

Expected:
- User is created.
- Role is assigned immediately.
- New user can login.

---

## 8) Test cases — Audit log & reporting

### TC-AUD-01: Audit log entries exist
Steps:
1) Post an invoice.
2) Receive a purchase.
3) Do a stock transfer.
4) Open Insights → Audit Log.

Expected:
- You can see audit log entries like InvoicePosted / PurchaseReceived / StockTransferred.

### TC-RPT-01: Sales report
Steps:
1) Post at least 1 invoice.
2) Open Insights → Sales Report.

Expected:
- Posted invoices contribute to the report totals.

---

## 9) Quick “done” checklist

- [ ] Store created
- [ ] Warehouse created and linked to Store
- [ ] Units created
- [ ] Categories created (parent/child)
- [ ] 5 items created with barcode/unit/category/GST/HSN
- [ ] Purchase received → ledger IN rows
- [ ] Invoice posted → ledger OUT rows
- [ ] Stock adjusted → ledger ADJUSTMENT row
- [ ] Transfer between warehouses → ledger two TRANSFER rows
- [ ] Audit log shows events
- [ ] Sales report shows posted invoices

---

If you want, I can also add a **printable “Expected Stock After Tests” table** (so you can quickly check stock balances after running TC-STK-01/02/03/04).