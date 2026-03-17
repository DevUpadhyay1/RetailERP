# RetailERP — Comprehensive Testing Document
## Full CRUD & Feature Testing Guide with Test Data

---

## 🔗 Application URLs

| Environment | URL |
|-------------|-----|
| **HTTPS** | `https://localhost:7240` |
| **HTTP** | `http://localhost:5082` |
| **Swagger API** | `https://localhost:7240/swagger` |

---

## 🔐 SECTION 1: Authentication & Login Testing

### 1.1 — Login Page
**URL:** `https://localhost:7240/` (redirects to Login)

| # | Test | User | Password | Expected |
|---|------|------|----------|----------|
| 1 | SuperAdmin Login | `retailerp.global@gmail.com` | `SuperAdmin@12345` | Redirects to Platform Dashboard |
| 2 | Admin Login | `admin@retailerp.com` | `Admin@12345` | Redirects to Dashboard |
| 3 | Manager Login | `manager@retailerp.com` | `Manager@12345` | Redirects to Dashboard |
| 4 | Cashier Login | `cashier@retailerp.com` | `Cashier@12345` | Redirects to POS Bill screen |
| 5 | Inventory Login | `inventory@retailerp.com` | `Inventory@12345` | Redirects to Dashboard |
| 6 | Wrong Password | `admin@retailerp.com` | `wrongpass` | Shows error "Invalid credentials" |
| 7 | Empty Fields | (leave blank) | (leave blank) | Validation errors shown |
| 8 | Logout | (any logged-in user) | Click Logout | Returns to Login page |

---

## 📦 SECTION 2: Units (Master Data — Test First)

**URL:** `https://localhost:7240/Units`
**Login as:** Admin (`admin@retailerp.com` / `Admin@12345`)

### 2.1 — Create Units
Click **"Create New"** → Fill form → Click **Save**

| # | Name | Symbol | IsActive | Expected |
|---|------|--------|----------|----------|
| 1 | Pieces | PCS | ✅ | Created successfully, appears in list |
| 2 | Kilograms | KG | ✅ | Created successfully |
| 3 | Liters | LTR | ✅ | Created successfully |
| 4 | Box | BOX | ✅ | Created successfully |
| 5 | Dozen | DZN | ✅ | Created successfully |
| 6 | (empty name) | | ✅ | Validation error — Name required |

### 2.2 — Read / Index
| # | Test | Expected |
|---|------|----------|
| 1 | Open `/Units` | All 5 units listed in a table |
| 2 | Search "Kilo" in search box | Only "Kilograms" shown |
| 3 | Click "Details" on any unit | Shows unit details page |

### 2.3 — Edit Unit
Click **Edit** on "Box" → Change name → **Save**

| # | Field | Old Value | New Value | Expected |
|---|-------|-----------|-----------|----------|
| 1 | Name | Box | Carton | Updates successfully |
| 2 | Symbol | BOX | CTN | Updates successfully |

### 2.4 — Delete Unit
Click **Delete** on "Dozen" → Confirm → **Delete**

| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Dozen" | Unit removed from list |
| 2 | Verify count | 4 units remain |

---

## 🏬 SECTION 3: Warehouses

**URL:** `https://localhost:7240/Warehouses`
**Login as:** Admin

### Pre-existing Seeded Data:
- Main Warehouse (Ahmedabad)
- Store Warehouse (Surat)

### 3.1 — Create Warehouse
| # | Name | Address | Expected |
|---|------|---------|----------|
| 1 | Central Godown | 101 Industrial Area, Rajkot | Created, shows in list |
| 2 | Cold Storage Unit | 45 GIDC, Vadodara | Created |
| 3 | (empty name) | Any | Validation error |

### 3.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Open Index | 4 warehouses listed (2 seeded + 2 new) |
| 2 | Click Details on "Main Warehouse" | Shows name, address |

### 3.3 — Edit
| # | Warehouse | Field | New Value | Expected |
|---|-----------|-------|-----------|----------|
| 1 | Central Godown | Address | 102 Industrial Area, Rajkot | Updated |

### 3.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Cold Storage Unit" | Removed from list |

---

## 🏪 SECTION 4: Stores

**URL:** `https://localhost:7240/Stores`
**Login as:** Admin

### 4.1 — Create Store
| # | StoreCode | Name | Address | Phone | City | State | GstNo | PanNo | IsActive | BusinessType |
|---|-----------|------|---------|-------|------|-------|-------|-------|----------|--------------|
| 1 | STR-001 | RetailERP Main Store | 123 MG Road | 9876543210 | Ahmedabad | Gujarat | 24ABCDE1234F1Z5 | ABCDE1234F | ✅ | Retail |
| 2 | STR-002 | RetailERP Outlet 2 | 456 Station Road | 9876543211 | Surat | Gujarat | 24FGHIJ5678K1Z9 | FGHIJ5678K | ✅ | Retail |
| 3 | (empty code) | Test | | | | | | | ✅ | | Validation error |

### 4.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Open `/Stores` | Both stores listed |
| 2 | Details of STR-001 | All fields displayed |

### 4.3 — Edit
| # | Store | Field | New Value | Expected |
|---|-------|-------|-----------|----------|
| 1 | STR-002 | Phone | 9999888877 | Updated |
| 2 | STR-002 | City | Vadodara | Updated |

### 4.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete STR-002 | Removed |

---

## 📂 SECTION 5: Categories

**URL:** `https://localhost:7240/Categories`
**Login as:** Admin

### 5.1 — Create Categories
| # | Name | Description | IsActive | Expected |
|---|------|-------------|----------|----------|
| 1 | Electronics | Laptops, Mobiles, Accessories | ✅ | Created |
| 2 | Grocery | Daily essentials, food items | ✅ | Created |
| 3 | Clothing | Apparel and garments | ✅ | Created |
| 4 | Stationery | Office and school supplies | ✅ | Created |
| 5 | (empty) | | ✅ | Validation error |

### 5.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All 4 categories listed |
| 2 | Search "Elec" | Only Electronics shown |
| 3 | Details on Grocery | Shows name, description |

### 5.3 — Edit
| # | Category | Field | New Value | Expected |
|---|----------|-------|-----------|----------|
| 1 | Stationery | Name | Office Supplies | Updated |
| 2 | Clothing | IsActive | ❌ (uncheck) | Set to inactive |

### 5.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Office Supplies" | Removed from list |

---

## 🧑‍🤝‍🧑 SECTION 6: Suppliers

**URL:** `https://localhost:7240/Suppliers`
**Login as:** Admin

### Pre-existing Seeded Data:
- ABC Distributors
- Local Wholesale

### 6.1 — Create Supplier
| # | Name | Phone | Email | Address | IsActive | Expected |
|---|------|-------|-------|---------|----------|----------|
| 1 | Global Electronics Ltd | 9812345678 | global@electronics.com | 78 GIDC, Ahmedabad | ✅ | Created |
| 2 | FreshFarm Grocers | 9823456789 | info@freshfarm.in | 12 Market Yard, Surat | ✅ | Created |
| 3 | (empty name) | | | | ✅ | Validation error |

### 6.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | 4 suppliers listed (2 seeded + 2 new) |
| 2 | Search "Global" | Only Global Electronics shown |
| 3 | Details on ABC Distributors | Shows all fields |

### 6.3 — Edit
| # | Supplier | Field | New Value | Expected |
|---|----------|-------|-----------|----------|
| 1 | FreshFarm Grocers | Phone | 9999111122 | Updated |
| 2 | FreshFarm Grocers | Email | sales@freshfarm.in | Updated |

### 6.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "FreshFarm Grocers" | Removed from list |

---

## 👥 SECTION 7: Customers

**URL:** `https://localhost:7240/Customers`
**Login as:** Admin

### Pre-existing Seeded Data:
- Walk-in Customer
- ABC Traders

### 7.1 — Create Customer
| # | Name | Phone | Email | GSTIN | Address | City | State | PinCode | IsActive | Expected |
|---|------|-------|-------|-------|---------|------|-------|---------|----------|----------|
| 1 | Rajesh Patel | 9876501234 | rajesh@gmail.com | 24AABCP1234R1ZX | 45 CG Road | Ahmedabad | Gujarat | 380009 | ✅ | Created |
| 2 | Meena Shah | 9876505678 | meena.shah@yahoo.com | | 78 Ring Road | Surat | Gujarat | 395007 | ✅ | Created |
| 3 | Tech Solutions Pvt Ltd | 9876509012 | info@techsol.com | 24AACTS5678P1Z2 | 101 SG Highway | Gandhinagar | Gujarat | 382010 | ✅ | Created |
| 4 | (empty name) | | | | | | | | ✅ | Validation error |

### 7.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | 5 customers (2 seeded + 3 new) |
| 2 | Search "Rajesh" | Only Rajesh Patel shown |
| 3 | Details on ABC Traders | All fields shown |

### 7.3 — Edit
| # | Customer | Field | New Value | Expected |
|---|----------|-------|-----------|----------|
| 1 | Meena Shah | PinCode | 395001 | Updated |
| 2 | Rajesh Patel | Address | 46 CG Road, Near Stadium | Updated |

### 7.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Tech Solutions Pvt Ltd" | Removed from list |

---

## 🛍️ SECTION 8: Items (Products)

**URL:** `https://localhost:7240/Items`
**Login as:** Admin

### Pre-existing Seeded Data:
- Laptop (SKU: LAPTOP-001, ₹55,000)
- Mouse (SKU: MOUSE-001, ₹499)
- Keyboard (SKU: KB-001, ₹1,299)

### 8.1 — Create Items
| # | SKU | Name | UnitPrice | Barcode | Unit | Category | MRP | PurchasePrice | GstPercent | HsnCode | ReorderLevel | IsActive |
|---|-----|------|-----------|---------|------|----------|-----|---------------|------------|---------|--------------|----------|
| 1 | MON-001 | Dell Monitor 24" | 12500.00 | 8901234567890 | Pieces | Electronics | 13999.00 | 10500.00 | 18 | 8528 | 5 | ✅ |
| 2 | PEN-001 | Cello Pen (Blue) | 10.00 | 8901234567891 | Pieces | (none) | 12.00 | 6.00 | 12 | 9608 | 100 | ✅ |
| 3 | RICE-5KG | Basmati Rice 5kg | 450.00 | 8901234567892 | Kilograms | Grocery | 499.00 | 350.00 | 5 | 1006 | 20 | ✅ |
| 4 | TSHIRT-M | Cotton T-Shirt (M) | 399.00 | 8901234567893 | Pieces | Clothing | 499.00 | 250.00 | 12 | 6109 | 30 | ✅ |
| 5 | USB-CABLE | USB-C Cable 1m | 199.00 | 8901234567894 | Pieces | Electronics | 249.00 | 120.00 | 18 | 8544 | 50 | ✅ |
| 6 | (empty SKU) | Test | 100 | | | | | | | | | ✅ | Validation error |

### 8.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | 8 items listed (3 seeded + 5 new) |
| 2 | Search "Dell" | Only Dell Monitor shown |
| 3 | Details on Laptop | All fields including barcode, HSN, GST% |
| 4 | Low Stock page (`/Items/LowStock`) | Items below reorder level listed |

### 8.3 — Edit
| # | Item | Field | New Value | Expected |
|---|------|-------|-----------|----------|
| 1 | Cello Pen (Blue) | UnitPrice | 12.00 | Updated |
| 2 | USB-C Cable 1m | ReorderLevel | 100 | Updated |
| 3 | Cotton T-Shirt (M) | IsActive | ❌ | Set to inactive |

### 8.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Basmati Rice 5kg" | Removed from list |

---

## 👨‍💼 SECTION 9: Employees

**URL:** `https://localhost:7240/Employees`
**Login as:** Admin

### 9.1 — Create Employees
| # | Name | Email | Phone | Department | Designation | JoinDate | Status | Expected |
|---|------|-------|-------|------------|-------------|----------|--------|----------|
| 1 | Amit Sharma | amit@retailerp.com | 9876511111 | Sales | Sales Executive | 2024-01-15 | Active | Created |
| 2 | Priya Desai | priya@retailerp.com | 9876522222 | Inventory | Stock Manager | 2024-03-01 | Active | Created |
| 3 | Ravi Kumar | ravi@retailerp.com | 9876533333 | Finance | Accountant | 2024-06-15 | Active | Created |
| 4 | (empty name) | | | | | | Active | Validation error |

### 9.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All employees listed |
| 2 | Search "Priya" | Only Priya Desai shown |
| 3 | Details on Amit Sharma | All fields displayed |

### 9.3 — Edit
| # | Employee | Field | New Value | Expected |
|---|----------|-------|-----------|----------|
| 1 | Ravi Kumar | Designation | Senior Accountant | Updated |
| 2 | Amit Sharma | Phone | 9876599999 | Updated |

### 9.4 — Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Delete "Ravi Kumar" | Removed from list |

---

## 📦 SECTION 10: Stock Management

**URL:** `https://localhost:7240/Stocks`
**Login as:** Admin or Inventory (`inventory@retailerp.com` / `Inventory@12345`)

### Pre-existing Seeded Stock:
- Laptop × 5 (Main Warehouse)
- Mouse × 50 (Main Warehouse)
- Keyboard × 20 (Main Warehouse)

### 10.1 — Create Stock
| # | Item | Warehouse | Quantity | Expected |
|---|------|-----------|----------|----------|
| 1 | Dell Monitor 24" | Main Warehouse | 15 | Created |
| 2 | Cello Pen (Blue) | Main Warehouse | 500 | Created |
| 3 | USB-C Cable 1m | Store Warehouse | 100 | Created |
| 4 | Cotton T-Shirt (M) | Central Godown | 200 | Created |

### 10.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All stock records shown with Item, Warehouse, Qty |
| 2 | Details on Laptop stock | Shows 5 quantity in Main Warehouse |

### 10.3 — Edit Stock
| # | Stock Record | Field | New Value | Expected |
|---|-------------|-------|-----------|----------|
| 1 | Mouse / Main Warehouse | Quantity | 60 | Updated from 50 to 60 |

### 10.4 — Stock Adjustment
**URL:** `/Stocks/Adjust`

| # | Item | Warehouse | Adjustment Qty | Reason | Expected |
|---|------|-----------|---------------|--------|----------|
| 1 | Laptop | Main Warehouse | -2 | Damaged — removed | Laptop stock → 3 |
| 2 | Mouse | Main Warehouse | +10 | Found miscounted | Mouse stock → 70 |

### 10.5 — Stock Transactions Ledger
**URL:** `https://localhost:7240/StockTransactions`

| # | Test | Expected |
|---|------|----------|
| 1 | Open StockTransactions | All movements listed (adjustments, purchases, sales, returns) |
| 2 | Check latest entries | Should show the 2 adjustments from 10.4 |

### 10.6 — Stock Transfer
**URL:** `https://localhost:7240/StockTransfers/Create`

| # | Item | From Warehouse | To Warehouse | Qty | Expected |
|---|------|---------------|-------------|-----|----------|
| 1 | Mouse | Main Warehouse | Store Warehouse | 20 | Source -20, Target +20 |
| 2 | Keyboard | Main Warehouse | Central Godown | 5 | Source -5, Target +5 |

After transfer, verify:
- Mouse: Main Warehouse = 50 (was 70 - 20), Store Warehouse = 20
- Keyboard: Main Warehouse = 15 (was 20 - 5), Central Godown = 5

---

## 🛒 SECTION 11: Purchases (Inward)

**URL:** `https://localhost:7240/Purchases`
**Login as:** Admin or Inventory

### 11.1 — Create Purchase (Header)
| # | PurchaseNo | Supplier | Warehouse | PurchaseDate | Notes | Expected |
|---|------------|----------|-----------|-------------|-------|----------|
| 1 | PUR-2025-001 | ABC Distributors | Main Warehouse | 2025-01-15 | Monthly electronics restock | Created (Draft) |
| 2 | PUR-2025-002 | Global Electronics Ltd | Store Warehouse | 2025-01-20 | Monitor bulk order | Created (Draft) |

### 11.2 — Add Lines to Purchase PUR-2025-001
Open Edit page for PUR-2025-001 → Add Lines:

| # | Item | Qty | Unit Cost | Expected |
|---|------|-----|-----------|----------|
| 1 | Laptop | 10 | 48000.00 | Line added, total = 4,80,000 |
| 2 | Mouse | 100 | 350.00 | Line added, total = 5,15,000 |
| 3 | Keyboard | 50 | 900.00 | Line added, total = 5,60,000 |

### 11.3 — Add Lines to Purchase PUR-2025-002
| # | Item | Qty | Unit Cost | Expected |
|---|------|-----|-----------|----------|
| 1 | Dell Monitor 24" | 20 | 10500.00 | Line added, total = 2,10,000 |

### 11.4 — Remove Line
| # | Test | Expected |
|---|------|----------|
| 1 | Remove "Mouse" line from PUR-2025-001 | Mouse line removed, total recalculated |
| 2 | Re-add Mouse line (100 × ₹350) | Line added back |

### 11.5 — Receive Purchase (Stock Inward)
| # | Purchase | Action | Expected |
|---|----------|--------|----------|
| 1 | PUR-2025-001 | Click "Receive" | Status → Received, Stock of Laptop/Mouse/Keyboard increases in Main Warehouse |
| 2 | PUR-2025-002 | Click "Receive" | Status → Received, Monitor stock increases in Store Warehouse |

**Verify stock after receiving PUR-2025-001:**
- Laptop: Main Warehouse old (3 after adjustment) + 10 = 13
- Mouse: Main Warehouse old (50 after transfer) + 100 = 150
- Keyboard: Main Warehouse old (15 after transfer) + 50 = 65

### 11.6 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | Both purchases listed with status |
| 2 | Check PUR-2025-001 shows "Received" | Status = Received |

---

## 🧾 SECTION 12: Invoices (Sales Invoices)

**URL:** `https://localhost:7240/Invoices`
**Login as:** Admin or Manager

### 12.1 — Create Invoice (Header)
| # | InvoiceNo | Customer | Warehouse | InvoiceDate | Expected |
|---|-----------|----------|-----------|-------------|----------|
| 1 | INV-2025-001 | Rajesh Patel | Main Warehouse | 2025-01-25 | Created (Draft) |
| 2 | INV-2025-002 | ABC Traders | Main Warehouse | 2025-01-26 | Created (Draft) |

### 12.2 — Add Lines to INV-2025-001
| # | Item | Qty | UnitPrice | Expected |
|---|------|-----|-----------|----------|
| 1 | Laptop | 2 | 55000.00 | Line added, total = 1,10,000 |
| 2 | Mouse | 5 | 499.00 | Line added, total = 1,12,495 |

### 12.3 — Add Lines to INV-2025-002
| # | Item | Qty | UnitPrice | Expected |
|---|------|-----|-----------|----------|
| 1 | Keyboard | 10 | 1299.00 | Line added, total = 12,990 |
| 2 | Dell Monitor 24" | 3 | 12500.00 | Line added, total = 50,490 |

### 12.4 — Remove Line
| # | Test | Expected |
|---|------|----------|
| 1 | Remove "Mouse" from INV-2025-001 | Line removed, total = 1,10,000 |
| 2 | Re-add Mouse (5 × ₹499) | Total = 1,12,495 |

### 12.5 — Post Invoice (Finalize + Deduct Stock)
| # | Invoice | Expected |
|---|---------|----------|
| 1 | Post INV-2025-001 | Status → Posted, Laptop stock -2, Mouse stock -5 |
| 2 | Post INV-2025-002 | Status → Posted, Keyboard stock -10, Monitor stock -3 |

### 12.6 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | Both invoices listed |
| 2 | Check status shows "Posted" | ✅ |

---

## 🎟️ SECTION 13: Coupons

**URL:** `https://localhost:7240/Coupons`
**Login as:** Admin

### 13.1 — Create Coupons
| # | Code | DiscountType | DiscountValue | MinBillAmount | MaxUses | ValidFrom | ValidTo | IsActive | Expected |
|---|------|-------------|---------------|---------------|---------|-----------|---------|----------|----------|
| 1 | FLAT100 | Amount | 100 | 500 | 50 | 2025-01-01 | 2025-12-31 | ✅ | Created |
| 2 | SAVE10 | Percent | 10 | 1000 | 100 | 2025-01-01 | 2025-06-30 | ✅ | Created |
| 3 | WELCOME50 | Amount | 50 | 200 | 1000 | 2025-01-01 | 2025-12-31 | ✅ | Created |
| 4 | EXPIRED01 | Amount | 200 | 100 | 10 | 2024-01-01 | 2024-12-31 | ✅ | Created (expired) |
| 5 | (empty code) | | | | | | | ✅ | Validation error |

### 13.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All 4 coupons listed |
| 2 | Details on FLAT100 | Shows all fields |

### 13.3 — Edit
| # | Coupon | Field | New Value | Expected |
|---|--------|-------|-----------|----------|
| 1 | SAVE10 | MaxUses | 200 | Updated |
| 2 | WELCOME50 | MinBillAmount | 300 | Updated |

### 13.4 — Toggle Active
| # | Coupon | Action | Expected |
|---|--------|--------|----------|
| 1 | EXPIRED01 | Toggle Active → OFF | IsActive = false |
| 2 | EXPIRED01 | Toggle Active → ON | IsActive = true |

---

## 🏷️ SECTION 14: Promotions (Sprint 7)

**URL:** `https://localhost:7240/Promotions`
**Login as:** Admin

### 14.1 — Create Promotions
**Test each PromoType to verify dynamic form sections:**

| # | Name | PromoType | Details | ValidFrom | ValidTo | MinBill | MaxUses | Priority | Expected |
|---|------|-----------|---------|-----------|---------|---------|---------|----------|----------|
| 1 | 10% Off Electronics | FlatPercent | DiscountPercent=10, Item=(none), Category=Electronics | 2025-01-01 | 2025-12-31 | 0 | 500 | 1 | Created — category-level 10% off |
| 2 | ₹200 Off Laptops | FlatAmount | DiscountAmount=200, Item=Laptop, Category=(none) | 2025-01-01 | 2025-06-30 | 5000 | 100 | 2 | Created — item-level flat ₹200 off |
| 3 | Buy 2 Get 1 Mouse | BOGO | BuyQty=2, GetQty=1, Item=Mouse, FreeItem=Mouse | 2025-01-01 | 2025-12-31 | 0 | 200 | 3 | Created — BOGO promotion |
| 4 | Lunch Hour 20% Off | HappyHour | DiscountPercent=20, HappyHourStart=12:00, HappyHourEnd=14:00 | 2025-01-01 | 2025-12-31 | 500 | 0 | 4 | Created — time-based discount |
| 5 | Laptop+Mouse Combo | ComboDiscount | ComboItemIds=(select Laptop & Mouse), ComboPrice=52000 | 2025-01-01 | 2025-12-31 | 0 | 50 | 5 | Created — combo deal |

**Verify form behavior:**
| # | Test | Expected |
|---|------|----------|
| 1 | Select "FlatPercent" | Shows Discount% field, hides BOGO/Combo/HappyHour sections |
| 2 | Select "FlatAmount" | Shows DiscountAmount field |
| 3 | Select "BOGO" | Shows BuyQty, GetQty, FreeItem fields |
| 4 | Select "HappyHour" | Shows HappyHourStart, HappyHourEnd time pickers |
| 5 | Select "ComboDiscount" | Shows ComboItemIds multi-select, ComboPrice |

### 14.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All 5 promotions listed with type badges |
| 2 | Search "Laptop" | Shows ₹200 Off Laptops + Laptop+Mouse Combo |
| 3 | Details on "Buy 2 Get 1 Mouse" | Shows BOGO config (BuyQty=2, GetQty=1) |

### 14.3 — Edit
| # | Promotion | Field | New Value | Expected |
|---|-----------|-------|-----------|----------|
| 1 | 10% Off Electronics | DiscountPercent | 15 | Updated from 10% to 15% |
| 2 | Lunch Hour 20% Off | HappyHourEnd | 15:00 | Extended by 1 hour |

### 14.4 — Toggle Active / Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Toggle "₹200 Off Laptops" → Inactive | Badge changes to Inactive |
| 2 | Toggle again → Active | Badge changes back to Active |
| 3 | Delete "Lunch Hour 20% Off" | Removed from list |

---

## 💳 SECTION 15: Loyalty Cards

**URL:** `https://localhost:7240/Loyalty`
**Login as:** Admin

### 15.1 — Create Loyalty Cards
| # | Customer | CardNumber | Expected |
|---|----------|------------|----------|
| 1 | Rajesh Patel | LOYAL-001 | Created, Tier=Bronze, Points=0 |
| 2 | Meena Shah | LOYAL-002 | Created, Tier=Bronze, Points=0 |
| 3 | ABC Traders | LOYAL-003 | Created |

### 15.2 — Read
| # | Test | Expected |
|---|------|----------|
| 1 | Index page | All 3 cards listed with customer, card no., tier, balance |
| 2 | Details on LOYAL-001 | Shows card info + transaction history (empty initially) |

### 15.3 — Toggle Active
| # | Test | Expected |
|---|------|----------|
| 1 | Toggle LOYAL-003 → Inactive | Card deactivated |
| 2 | Toggle LOYAL-003 → Active | Card reactivated |

### 15.4 — Lookup (AJAX)
| # | Test | Expected |
|---|------|----------|
| 1 | Will be tested during POS (Section 16) | Verified via Attach Loyalty in POS |

---

## 🖥️ SECTION 16: POS Billing (Main Workflow)

**URL:** `https://localhost:7240/Pos`
**Login as:** Cashier (`cashier@retailerp.com` / `Cashier@12345`)

### 16.1 — POS Bill Index
| # | Test | Expected |
|---|------|----------|
| 1 | Open `/Pos` | Lists all bills (may be empty initially) |

### 16.2 — Create New POS Bill
| # | Test | Expected |
|---|------|----------|
| 1 | Click "New Bill" | Redirects to billing screen with new empty bill |
| 2 | Bill should have auto-generated BillNo | e.g., POS-0001 |
| 3 | Status should be "Open" | Open status badge shown |

### 16.3 — Add Items (Scan / Lookup)
On the billing screen, use the scan barcode / SKU input:

| # | Enter in Scan Field | Expected Result |
|---|---------------------|-----------------|
| 1 | LAPTOP-001 | Laptop added, Qty=1, Price=₹55,000 |
| 2 | MOUSE-001 | Mouse added, Qty=1, Price=₹499 |
| 3 | MOUSE-001 (again) | Mouse Qty increments to 2 |
| 4 | KB-001 | Keyboard added, Qty=1, Price=₹1,299 |
| 5 | MON-001 | Dell Monitor added, Qty=1, Price=₹12,500 |
| 6 | INVALID-SKU | Error: "Item not found" |

### 16.4 — Update Line Quantity
| # | Item | Change Qty To | Expected |
|---|------|--------------|----------|
| 1 | Mouse | 5 | Qty updates to 5, LineTotal = ₹2,495 |
| 2 | Keyboard | 3 | Qty updates to 3, LineTotal = ₹3,897 |
| 3 | Any item | 0 or negative | Should show error or remove line |

### 16.5 — Remove Line
| # | Test | Expected |
|---|------|----------|
| 1 | Remove Dell Monitor | Line removed, total recalculated |
| 2 | Lines remaining: Laptop(1), Mouse(5), Keyboard(3) | SubTotal = ₹55,000 + ₹2,495 + ₹3,897 = ₹61,392 |

### 16.6 — Line-Level Discount (Sprint 7)
| # | Item | Enter Disc% | Expected |
|---|------|-------------|----------|
| 1 | Laptop | 5 | Disc=₹2,750, NetRate=₹52,250, LineTotal=₹52,250 |
| 2 | Mouse | 10 | Disc=₹49.90/unit, NetRate=₹449.10, LineTotal=₹2,245.50 |
| 3 | Keyboard | 0 | No discount |

### 16.7 — Bill-Level Additional Discount (Sprint 7)
| # | Field | Value | Expected |
|---|-------|-------|----------|
| 1 | Add Discount % | 2 | AddDiscountAmount calculated on subtotal |
| 2 | Click Apply | Total reduced by 2% additional |

### 16.8 — Bill-Level Additional Charge (Sprint 7)
| # | Field | Value | Expected |
|---|-------|-------|----------|
| 1 | Add Charge % | 1 | AddChargeAmount added to total |
| 2 | Click Apply | Total includes 1% charge |

### 16.9 — Apply Promotions (Sprint 7)
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Apply Schemes" | System auto-applies eligible promotions |
| 2 | If "10% Off Electronics" is active & items qualify | Discount applies, promo icon shown on line |
| 3 | Click "View Schemes" | Shows list of currently active promotions |

### 16.10 — Attach Loyalty Card
| # | Test | Expected |
|---|------|----------|
| 1 | Enter LOYAL-001 in loyalty field | Loyalty card attached, customer = Rajesh Patel |
| 2 | If card has points, Redeem option shows | Points available for redemption |

### 16.11 — Redeem Loyalty Points
| # | Test | Expected |
|---|------|----------|
| 1 | Click Redeem (if points available) | Loyalty discount applied to bill |
| 2 | If no points | Redeem button disabled or shows 0 |

### 16.12 — Apply Coupon
| # | Coupon Code | Expected |
|---|-------------|----------|
| 1 | FLAT100 | ₹100 discount applied (if subtotal > ₹500) |
| 2 | SAVE10 | 10% discount applied (if subtotal > ₹1,000) |
| 3 | INVALIDCODE | Error: "Invalid coupon" |
| 4 | Remove Coupon | Coupon discount removed |

### 16.13 — Add Payment
| # | Method | Amount | Reference | Expected |
|---|--------|--------|-----------|----------|
| 1 | Cash | 50000 | — | Payment added, remaining amount shown |
| 2 | Card | 8000 | TXN-12345 | Payment added |
| 3 | UPI | (remaining) | upi@bank | Fully paid |

### 16.14 — Remove Payment
| # | Test | Expected |
|---|------|----------|
| 1 | Remove UPI payment | Amount goes back to remaining |
| 2 | Re-add UPI payment | Restored |

### 16.15 — Complete Bill
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Complete Bill" | Status → Completed, CompletedAtUtc set |
| 2 | Bill is now read-only | No more edits possible |
| 3 | Stock deducted | Verify in Stocks page |
| 4 | Loyalty points earned (if card attached) | Check Loyalty card details |

### 16.16 — Receipt
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Receipt" | Shows receipt page with bill details |
| 2 | Click "Download PDF" | PDF receipt downloads |

### 16.17 — Cancel Bill
Create a **new bill**, add 1 item, then:

| # | Test | Expected |
|---|------|----------|
| 1 | Click "Cancel Bill" | Status → Cancelled |
| 2 | Stock NOT deducted | Stock remains unchanged |

---

## ⏸️ SECTION 17: POS Hold/Unhold (Sprint 7)

**Login as:** Cashier

### 17.1 — Hold Bill
| # | Test | Expected |
|---|------|----------|
| 1 | Create new bill, add Laptop(1) + Mouse(2) | Bill created with items |
| 2 | Click "Hold Bill" | Status → OnHold (yellow badge), NEW bill auto-created |
| 3 | New bill is now the active bill | Empty bill ready for next customer |

### 17.2 — View Held Bills
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Pop Hold" button | Modal opens showing held bills |
| 2 | List shows held bill with BillNo, items, total | At least 1 held bill visible |

### 17.3 — Unhold Bill
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Resume" on the held bill | Navigates to the held bill |
| 2 | Bill Status → Open again | Items (Laptop + Mouse) still present |
| 3 | Can continue adding items/payments | Normal billing resumes |
| 4 | Complete this bill normally | Status → Completed |

---

## 🔄 SECTION 18: POS Returns

**URL:** `https://localhost:7240/Pos/Returns`
**Login as:** Admin or Manager

### 18.1 — View Returns List
| # | Test | Expected |
|---|------|----------|
| 1 | Open Returns page | Shows list of all returns (empty initially) |

### 18.2 — Create New Return
| # | Test | Expected |
|---|------|----------|
| 1 | Click "New Return" | Shows form to select a completed bill |
| 2 | Select the completed bill from Section 16 | Shows bill lines for return selection |

### 18.3 — Process Return
| # | Item | Return Qty | Reason | Expected |
|---|------|-----------|--------|----------|
| 1 | Mouse | 2 | Defective | RefundAmount = 2 × ₹499 = ₹998 |
| 2 | Click Process | Return created, Status=Pending or Processed |

### 18.4 — Return Details
| # | Test | Expected |
|---|------|----------|
| 1 | View return details | Shows original bill ref, return lines, refund total |
| 2 | Stock should be restored | Mouse stock increases by 2 |

---

## 🧾 SECTION 19: Bill Templates

**URL:** `https://localhost:7240/BillTemplates`
**Login as:** Admin

### 19.1 — Create Template
| # | Name | Expected |
|---|------|----------|
| 1 | Standard Receipt | Created |
| 2 | Detailed Invoice | Created |

### 19.2 — Designer
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Designer" on Standard Receipt | Opens drag-and-drop GridStack designer |
| 2 | Add widgets (Company Name, Bill No, Items Table, Total, Footer) | Widgets appear on canvas |
| 3 | Rearrange widgets by dragging | Positions update |
| 4 | Click "Save Layout" | Layout JSON saved |

### 19.3 — Upload Logo
| # | Test | Expected |
|---|------|----------|
| 1 | Upload a logo image (PNG/JPG) | Logo saved, preview shows logo |

### 19.4 — Preview Receipt
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Preview" | Opens PDF preview of template with sample data |

### 19.5 — Set Default
| # | Test | Expected |
|---|------|----------|
| 1 | Set "Standard Receipt" as default | Used for all POS receipts |

### 19.6 — Edit / Delete
| # | Test | Expected |
|---|------|----------|
| 1 | Edit template name | Updated |
| 2 | Delete "Detailed Invoice" | Removed |

---

## 📊 SECTION 20: EOD Reports (End of Day)

**URL:** `https://localhost:7240/Eod`
**Login as:** Admin or Manager

### 20.1 — Generate EOD Report
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Generate" | Form asks for date |
| 2 | Select today's date | Report generated with today's totals |
| 3 | Report shows: Total Sales, Bill Count, Cash/Card/UPI breakdown | All figures present |

### 20.2 — View Details
| # | Test | Expected |
|---|------|----------|
| 1 | Click Details on generated report | Full breakdown shown |
| 2 | Verify totals match completed POS bills | Consistent |

### 20.3 — Close / Reconcile
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Close" | Report marked as closed/reconciled |

### 20.4 — Print
| # | Test | Expected |
|---|------|----------|
| 1 | Click "Print" | Print-friendly view opens |

---

## 📈 SECTION 21: Sales Reports

**URL:** `https://localhost:7240/Reports/Sales`
**Login as:** Admin or Manager

| # | Test | From Date | To Date | Expected |
|---|------|-----------|---------|----------|
| 1 | Sales report for today | (today) | (today) | Shows today's completed bills |
| 2 | Sales report for this month | 2025-01-01 | 2025-01-31 | Shows all January bills |
| 3 | Sales report for empty range | 2024-06-01 | 2024-06-30 | Shows empty / zero |

---

## 🔍 SECTION 22: Global Search

**URL:** `https://localhost:7240/Search`
**Login as:** Admin

| # | Search Query | Expected Results |
|---|-------------|-----------------|
| 1 | "Laptop" | Shows Item: Laptop, any related invoices/bills |
| 2 | "Rajesh" | Shows Customer: Rajesh Patel |
| 3 | "POS-0001" | Shows POS Bill |
| 4 | "ABC" | Shows ABC Traders, ABC Distributors, etc. |

---

## 📋 SECTION 23: Audit Logs

**URL:** `https://localhost:7240/Audit`
**Login as:** Admin

| # | Test | Expected |
|---|------|----------|
| 1 | Open Audit page | Shows list of audit events |
| 2 | Check for CRUD actions performed in previous tests | Create/Update/Delete entries visible |
| 3 | Verify user, entity, timestamp are logged | All fields present |

---

## 👑 SECTION 24: Admin User Management

**URL:** `https://localhost:7240/AdminUsers`
**Login as:** Admin

### 24.1 — List Users
| # | Test | Expected |
|---|------|----------|
| 1 | Open AdminUsers page | All 5 seeded users listed |
| 2 | Search "cashier" | Only cashier user shown |

### 24.2 — Create User
| # | Email | Password | Role | Expected |
|---|-------|----------|------|----------|
| 1 | newuser@retailerp.com | NewUser@12345 | Cashier | User created |
| 2 | finance@retailerp.com | Finance@12345 | Finance | User created |
| 3 | (duplicate email) | | | Error: email exists |

### 24.3 — Set Role
| # | User | New Role | Expected |
|---|------|----------|----------|
| 1 | newuser@retailerp.com | Manager | Role changed to Manager |

### 24.4 — Toggle Active
| # | User | Action | Expected |
|---|------|--------|----------|
| 1 | newuser@retailerp.com | Toggle → Inactive | User cannot login |
| 2 | Toggle → Active | User can login again |

---

## 🏢 SECTION 25: SuperAdmin — Companies

**URL:** `https://localhost:7240/Companies`
**Login as:** SuperAdmin (`retailerp.global@gmail.com` / `SuperAdmin@12345`)

### 25.1 — List Companies
| # | Test | Expected |
|---|------|----------|
| 1 | Open Companies page | Shows DEFAULT company (seeded) |

### 25.2 — Create Company
| # | CompanyCode | Name | Address | Expected |
|---|------------|------|---------|----------|
| 1 | BRANCH-01 | RetailERP Surat Branch | 123 Surat Road | Created |
| 2 | BRANCH-02 | RetailERP Vadodara Branch | 456 Vadodara Ave | Created |

### 25.3 — Edit Company
| # | Company | Field | New Value | Expected |
|---|---------|-------|-----------|----------|
| 1 | BRANCH-01 | Address | 124 Surat Road | Updated |

### 25.4 — Details
| # | Test | Expected |
|---|------|----------|
| 1 | View details of DEFAULT | Shows all company info |

### 25.5 — Platform Dashboard
| # | Test | Expected |
|---|------|----------|
| 1 | Navigate to Platform Dashboard | Shows aggregate stats across all companies |

---

## 📊 SECTION 26: Dashboard & Widgets

**URL:** `https://localhost:7240/Home/Dashboard`
**Login as:** Admin

| # | Test | Expected |
|---|------|----------|
| 1 | Open Dashboard | Shows default widget layout |
| 2 | Widgets include: Sales Today, Bills Today, Top Items, Charts | Data populated from seeded/test data |
| 3 | Drag a widget to new position | Widget moves |
| 4 | Click "Save Layout" | Layout saved (AJAX) |
| 5 | Refresh page | Layout persists in saved position |
| 6 | Click "Reset Layout" | Layout returns to defaults |

---

## 🔗 SECTION 27: REST API Testing (Swagger)

**URL:** `https://localhost:7240/swagger`

### 27.1 — JWT Authentication
| # | Endpoint | Method | Body | Expected |
|---|----------|--------|------|----------|
| 1 | `/api/auth/login` | POST | `{"email":"admin@retailerp.com","password":"Admin@12345"}` | Returns `{token, refreshToken}` |
| 2 | `/api/auth/me` | GET | Header: `Authorization: Bearer <token>` | Returns user info |
| 3 | `/api/auth/refresh` | POST | `{"refreshToken":"<from step 1>"}` | Returns new token pair |
| 4 | `/api/auth/logout` | POST | `{"refreshToken":"<token>"}` | Token revoked |

**⚠️ After login, click "Authorize" in Swagger and paste: `Bearer <token>`**

### 27.2 — API: Categories
| # | Endpoint | Method | Body | Expected |
|---|----------|--------|------|----------|
| 1 | `/api/categories` | GET | — | Lists all categories |
| 2 | `/api/categories` | POST | `{"name":"Furniture","description":"Tables & chairs"}` | Created, returns ID |
| 3 | `/api/categories/{id}` | GET | — | Returns Furniture details |
| 4 | `/api/categories/{id}` | PUT | `{"name":"Home Furniture","description":"Updated"}` | Updated |
| 5 | `/api/categories/{id}` | DELETE | — | Deleted |

### 27.3 — API: Items
| # | Endpoint | Method | Body | Expected |
|---|----------|--------|------|----------|
| 1 | `/api/items` | GET | — | Lists all items |
| 2 | `/api/items` | POST | `{"sku":"HDMI-001","name":"HDMI Cable 2m","unitPrice":299,"barcode":"8901111111111","gstPercent":18,"hsnCode":"8544","reorderLevel":25,"isActive":true}` | Created |
| 3 | `/api/items/{id}` | GET | — | Returns HDMI Cable details |
| 4 | `/api/items/{id}` | PUT | `{"sku":"HDMI-001","name":"HDMI Cable 3m","unitPrice":399,...}` | Updated |
| 5 | `/api/items/{id}` | DELETE | — | Deleted |
| 6 | `/api/items/low-stock` | GET | — | Low stock items list |

### 27.4 — API: Customers
| # | Endpoint | Method | Body | Expected |
|---|----------|--------|------|----------|
| 1 | `/api/customers` | GET | — | Lists all customers |
| 2 | `/api/customers` | POST | `{"name":"API Customer","phone":"9876500000","email":"api@test.com","isActive":true}` | Created |
| 3 | `/api/customers/{id}` | PUT | `{"name":"API Customer Updated",...}` | Updated |
| 4 | `/api/customers/{id}` | DELETE | — | Deleted |

### 27.5 — API: Stocks
| # | Endpoint | Method | Body | Expected |
|---|----------|--------|------|----------|
| 1 | `/api/stocks` | GET | — | Lists all stock records |
| 2 | `/api/stocks/{id}` | GET | — | Returns specific stock |
| 3 | `/api/stocks/adjust` | POST | `{"itemId":"<guid>","warehouseId":"<guid>","adjustment":5,"reason":"API test"}` | Stock adjusted |

### 27.6 — API: Stores, Suppliers, Units, Warehouses
All follow the same CRUD pattern:

| Endpoint Base | POST Body Example |
|--------------|-------------------|
| `/api/stores` | `{"storeCode":"API-STR","name":"API Store","isActive":true,"businessType":0}` |
| `/api/suppliers` | `{"name":"API Supplier","phone":"9876500001","isActive":true}` |
| `/api/units` | `{"name":"Meters","symbol":"M","isActive":true}` |
| `/api/warehouses` | `{"name":"API Warehouse","address":"123 API Street"}` |

For each: `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `DELETE /{id}`

### 27.7 — API: POS Bills & Reports
| # | Endpoint | Method | Expected |
|---|----------|--------|----------|
| 1 | `/api/pos/bills` | GET | Lists all POS bills |
| 2 | `/api/pos/bills/{id}` | GET | Returns specific bill with lines, payments |
| 3 | `/api/reports/sales?from=2025-01-01&to=2025-01-31` | GET | Sales report for date range |

---

## 🔄 SECTION 28: Sync (Offline-Online)

**URL:** `https://localhost:7240/Sync`
**Login as:** Admin

| # | Test | Expected |
|---|------|----------|
| 1 | Open Sync Index | Shows sync log entries (may be empty) |
| 2 | Queue a test change (AJAX) | Entry appears with Status=Pending |
| 3 | Process All pending | Status changes to Synced |
| 4 | View Details of a sync entry | Shows entity type, action, payload |

---

## 🔒 SECTION 29: Role-Based Access Testing

Test that unauthorized users CANNOT access certain pages:

### 29.1 — As Cashier (`cashier@retailerp.com`)
| # | URL | Expected |
|---|-----|----------|
| 1 | `/AdminUsers` | Access Denied / Redirect |
| 2 | `/Companies` | Access Denied / Redirect |
| 3 | `/Pos` | ✅ Accessible |
| 4 | `/Pos/NewBill` | ✅ Accessible |
| 5 | `/Items` | May be restricted |

### 29.2 — As Inventory (`inventory@retailerp.com`)
| # | URL | Expected |
|---|-----|----------|
| 1 | `/Stocks` | ✅ Accessible |
| 2 | `/Items` | ✅ Accessible |
| 3 | `/AdminUsers` | Access Denied |
| 4 | `/Companies` | Access Denied |

### 29.3 — As Manager (`manager@retailerp.com`)
| # | URL | Expected |
|---|-----|----------|
| 1 | `/Pos` | ✅ Accessible |
| 2 | `/Reports/Sales` | ✅ Accessible |
| 3 | `/AdminUsers` | Access Denied |
| 4 | `/Companies` | Access Denied |

### 29.4 — As SuperAdmin (`retailerp.global@gmail.com`)
| # | URL | Expected |
|---|-----|----------|
| 1 | `/Companies` | ✅ Accessible |
| 2 | `/Home/PlatformDashboard` | ✅ Accessible |
| 3 | All MVC pages | ✅ Full access |

---

## ✅ SECTION 30: Quick Smoke Test Checklist

Run through this checklist after all testing is done:

| # | Area | Test | Pass? |
|---|------|------|-------|
| 1 | Login | Admin can login | ☐ |
| 2 | Login | Cashier can login | ☐ |
| 3 | Login | SuperAdmin can login | ☐ |
| 4 | Units | Create + Edit + Delete | ☐ |
| 5 | Warehouses | Create + Edit + Delete | ☐ |
| 6 | Stores | Create + Edit + Delete | ☐ |
| 7 | Categories | Create + Edit + Delete | ☐ |
| 8 | Suppliers | Create + Edit + Delete | ☐ |
| 9 | Customers | Create + Edit + Delete (incl. GSTIN, Address) | ☐ |
| 10 | Items | Create + Edit + Delete + Low Stock | ☐ |
| 11 | Employees | Create + Edit + Delete | ☐ |
| 12 | Stock | Create + Edit + Adjust + Transfer | ☐ |
| 13 | Purchases | Create + Add Lines + Receive (stock inward) | ☐ |
| 14 | Invoices | Create + Add Lines + Post (stock deduction) | ☐ |
| 15 | Coupons | Create + Edit + Toggle Active | ☐ |
| 16 | Promotions | Create all 5 types + Edit + Toggle + Delete | ☐ |
| 17 | Loyalty | Create card + Attach in POS + Redeem | ☐ |
| 18 | POS: New Bill | Create + Scan items + Qty change + Remove line | ☐ |
| 19 | POS: Discounts | Line Disc% + Bill Add Discount + Add Charge | ☐ |
| 20 | POS: Promotions | Apply Schemes + View Schemes | ☐ |
| 21 | POS: Payment | Cash + Card + UPI + Complete | ☐ |
| 22 | POS: Coupon | Apply + Remove coupon | ☐ |
| 23 | POS: Hold/Unhold | Hold → Pop Hold → Resume → Complete | ☐ |
| 24 | POS: Cancel | Cancel bill → verify no stock change | ☐ |
| 25 | POS: Receipt | View + PDF download | ☐ |
| 26 | POS: Returns | New Return → Process → Verify stock restore | ☐ |
| 27 | Bill Templates | Create + Designer + Preview + Set Default | ☐ |
| 28 | EOD Report | Generate + Details + Close + Print | ☐ |
| 29 | Sales Report | By date range | ☐ |
| 30 | Search | Global search across entities | ☐ |
| 31 | Audit Logs | View audit trail | ☐ |
| 32 | Admin Users | Create + Set Role + Toggle Active | ☐ |
| 33 | Companies | Create + Edit (SuperAdmin only) | ☐ |
| 34 | Dashboard | Widgets load + Drag + Save + Reset layout | ☐ |
| 35 | Swagger API | Login + CRUD (Items, Categories, etc.) | ☐ |
| 36 | Role Access | Cashier restricted from Admin pages | ☐ |
| 37 | Role Access | SuperAdmin full access | ☐ |
| 38 | Sidebar | All links work, Promotions link visible | ☐ |

---

## 📝 SECTION 31: Recommended Testing Order

Follow this sequence for cleanest testing (dependencies satisfied first):

1. **Login** (Section 1) — Verify all 5 users
2. **Units** (Section 2) — Master data, no dependencies
3. **Warehouses** (Section 3) — Master data
4. **Stores** (Section 4) — May reference warehouses
5. **Categories** (Section 5) — No dependencies
6. **Suppliers** (Section 6) — No dependencies
7. **Customers** (Section 7) — No dependencies
8. **Items** (Section 8) — Depends on Units, Categories
9. **Employees** (Section 9) — No dependencies
10. **Stock** (Section 10) — Depends on Items, Warehouses
11. **Purchases** (Section 11) — Depends on Suppliers, Items, Warehouses
12. **Invoices** (Section 12) — Depends on Customers, Items, Warehouses
13. **Coupons** (Section 13) — No dependencies
14. **Promotions** (Section 14) — Depends on Items, Categories
15. **Loyalty Cards** (Section 15) — Depends on Customers
16. **POS Billing** (Section 16) — Depends on Items, Stock, Customers
17. **POS Hold/Unhold** (Section 17) — Depends on POS
18. **POS Returns** (Section 18) — Depends on completed POS bills
19. **Bill Templates** (Section 19) — Independent
20. **EOD Report** (Section 20) — After POS bills exist
21. **Sales Report** (Section 21) — After POS/Invoices
22. **Search** (Section 22) — After data exists
23. **Audit Logs** (Section 23) — After CRUD operations
24. **Admin Users** (Section 24) — Admin role
25. **Companies** (Section 25) — SuperAdmin role
26. **Dashboard** (Section 26) — After data exists
27. **REST API** (Section 27) — Independent (Swagger)
28. **Sync** (Section 28) — Independent
29. **Role-Based Access** (Section 29) — Test restrictions
30. **Smoke Test Checklist** (Section 30) — Final verification

---

## 🐛 Bug Report Template

When you find a bug, report it in this format:

```
**Bug #:** [number]
**Section:** [which section above]
**Page/URL:** [exact URL]
**Logged in as:** [which user]
**Steps to Reproduce:**
1. ...
2. ...
3. ...
**Expected:** [what should happen]
**Actual:** [what actually happened]
**Screenshot:** [if available]
**Error Message:** [exact error text if any]
```

---

*Document generated for RetailERP — Sprints 1-7 Complete*
*Total Entities: 35 | MVC Controllers: 27 | API Controllers: 12 | Roles: 7*
