# FreshMart — One Simple Business, All Feature Examples

**Purpose:** Use this single story to explain or demo **RetailERP** features in exams, presentations, or client walkthroughs.  
**Scenario:** A fictional grocery retailer **FreshMart** with head office, warehouses, POS, and optional franchise partners.

---

## 1. Who is FreshMart?

| Element | Example |
|--------|---------|
| **Business** | Grocery & daily needs (rice, oil, snacks, detergents). |
| **Head company** | *FreshMart India Pvt Ltd* (franchisor / main tenant). |
| **Stores** | e.g. *Koramangala Outlet*, *Whitefield Outlet*. |
| **Warehouses** | One main warehouse per store (stock that POS sells from). |
| **People** | Admin, store Manager, Cashier; optional franchisee operators. |

Everything below uses **the same characters and products** so you only remember one story.

---

## 2. Company, stores & users (multi-company / roles)

**Example**

- Admin creates **Company** *FreshMart India*, then **Stores** and **Warehouses**, and links them.
- Users log in with roles: **Admin** (full setup), **Manager** (reports, approvals), **Cashier** (POS only).

**What RetailERP does**

- **Multi-tenant:** Each company’s data is scoped (stores, bills, stock belong to a company).
- **Identity & roles:** `Admin`, `Manager`, `Cashier`, `SuperAdmin`, etc., control menus and actions.

**Demo line:** *“FreshMart India is one tenant; cashiers only see POS, managers see forecasts and franchise.”*

---

## 3. Categories, units & items (master data)

**Example**

- Category: **Staples** → Item: **Basmati Rice 5 kg**, SKU `RICE-BAS-5`, unit **Bag**, **MRP**, **GST %**, **reorder level** 30 bags.

**What RetailERP does**

- Items drive **POS**, **stock**, **barcode labels**, **forecast**, and **purchase** suggestions.

**Demo line:** *“One rice product record powers pricing, tax, inventory alerts, and scanning.”*

---

## 4. Barcode on the product + printed labels

**Example**

- Item barcode: `8901234567890`.
- Staff opens **Barcode Labels**, searches “Basmati”, selects the item, chooses **thermal 50×30 mm**, prints PDF, sticks label on shelf and bag.

**What RetailERP does**

- **Barcode Labels** (`BarcodeLabels`): PDF with name, SKU, barcode text, optional **QR**, price; thermal or A4 layout (**QuestPDF** + **QRCoder**).

**Demo line:** *“Same barcode on the label is what the cashier scans at POS.”*

---

## 5. POS billing (checkout)

**Example**

- Cashier starts a bill for **Koramangala** store + its warehouse (or uses **saved default** store/warehouse).
- Scans `8901234567890` → line **Basmati Rice 5 kg** appears; adds oil and snacks.
- Applies **UPI / card / cash** payments; **completes** bill → stock reduces in that warehouse.

**What RetailERP does**

- **POS Billing:** scan by **barcode or SKU**, lines, discounts, promotions, loyalty/coupons (if used), payments, complete/cancel/hold.
- **Stock:** completion posts consumption from **warehouse** linked to the bill.

**Demo line:** *“One completed POS bill is real sales data for forecast and franchise royalty.”*

---

## 6. Stock, transfers & low stock

**Example**

- After many sales, **Basmati** on-hand drops to **12 bags**; reorder level was **30** → item appears on **Low Stock** / alerts.

**What RetailERP does**

- **Stocks** per warehouse, **stock transactions**, optional **background alerts** (e.g. SignalR / email patterns in the app).

**Demo line:** *“Low stock connects master data (reorder level) to operations.”*

---

## 7. Suppliers & purchases (replenishment)

**Example**

- Manager creates **Purchase** from supplier **Agro Foods** for 120 bags of Basmati into **Koramangala warehouse**; goods received → stock increases.

**What RetailERP does**

- **Purchases** + **Suppliers**; inbound quantity updates **stock** so POS can sell again.

**Demo line:** *“Purchase closes the loop after forecast says ‘reorder’.”*

---

## 8. Demand forecast & reorder plan

**Example**

- Last **90 days**: Koramangala warehouse sold Basmati at roughly **8–10 bags/day** with a slight upward trend.
- **Forecast** estimates ~**9 bags/day** for next **14 days**; with **7-day** supplier lead time and safety stock, system suggests **~100 bags** reorder and marks **risk** (e.g. Medium if cover is low).

**What RetailERP does**

- **Forecast** dashboard: KPIs, top reorders.
- **Reorder Plan:** filter by warehouse, risk, search SKU/name; uses **POS + invoice history** and **on-hand** (**ForecastService**).

**Demo line:** *“FreshMart orders rice before festival week, using history—not gut feel.”*

---

## 9. Sales anomalies (spikes & drops)

**Example**

- Normal Basmati sales: **~8–12 bags/day**.  
  - **Spike:** One day **45 bags** (festival promo or bulk order) → **Anomalies** shows **Spike**, high severity.  
  - **Drop:** One day **0 bags** while stock exists → **Drop** → manager checks: POS issue, empty shelf, or theft.

**What RetailERP does**

- **Anomalies** page: compares each day to a **14-day baseline**, **z-score** / deviation rules, **Spike** vs **Drop**, severity.

**Demo line:** *“Exceptions surface automatically for investigation.”*

---

## 10. Franchise & royalty

**Example**

- *FreshMart India* signs a **Franchise Agreement** with *FreshMart Whitefield Operators Pvt Ltd* (franchisee): **4%** of gross POS sales + **₹10,000/month** flat, **minimum ₹25,000/month** royalty.
- **March:** Franchisee’s **completed POS bills** total **₹8,00,000** → system calculates royalty from agreement rules and can **record / track** payment.

**What RetailERP does**

- **Franchise → Agreements** + **Royalty Dashboard**; royalty from **franchisee company’s POS** totals by period (**FranchiseService**).

**Demo line:** *“Head office bills franchisees from real till sales, not manual estimates.”*

---

## 11. Invoices & GST (B2B side)

**Example**

- FreshMart sells **20 crates** to a small hotel on **credit** → **Invoice** posted; **GSTR-1 / 3B** style reports use that data.

**What RetailERP does**

- **Invoices**, **GST reports**, optional **E-Invoice / E-Way** flows (where configured).

**Demo line:** *“Shop sales = POS; business sales = invoices—both feed compliance reports.”*

---

## 12. Loyalty, coupons & promotions

**Example**

- Regular customer uses **loyalty card** at POS; **coupon** `FREESHIP` for ₹50 off; weekend **promo** auto-applies on certain categories.

**What RetailERP does**

- **Loyalty**, **Coupons**, **Promotions** tied to **open POS bills**.

**Demo line:** *“FreshMart keeps customers coming back without breaking stock or tax logic.”*

---

## 13. End of day (EOD)

**Example**

- Manager closes **Koramangala** for the day: cash counted vs system, variances noted.

**What RetailERP does**

- **EOD** reports per store/date; optional **background** draft generation.

**Demo line:** *“Daily cash discipline for each store.”*

---

## 14. Dashboard & reports

**Example**

- Admin opens **Dashboard**: sales widgets, low stock, shortcuts to **POS**, **Forecast**, **Franchise**.

**What RetailERP does**

- Customizable widgets, **Reports** / **Audit** / **GST** as per menu.

**Demo line:** *“One login, one picture of the whole business.”*

---

## 15. Optional: API & mobile-ready backend

**Example**

- Future **store supervisor app** loads items and sales via **JWT API** (same FreshMart data).

**What RetailERP does**

- **REST API + Swagger**, **JWT** auth for integrations.

---

## 16. One-page cheat sheet (exam / viva)

| Feature | FreshMart example in one line |
|--------|--------------------------------|
| **Company / tenant** | FreshMart India as one company; data isolated. |
| **Store + warehouse** | Koramangala store sells from its warehouse. |
| **Item + barcode** | Basmati `8901234567890` on master and on printed label. |
| **POS** | Cashier scans barcode, pays, completes → stock down. |
| **Purchase** | Buy 120 bags from supplier → stock up. |
| **Low stock** | Below 30 bags → alert / list. |
| **Forecast** | ~9 bags/day → suggest reorder ~100. |
| **Anomaly** | 45 bags one day → spike; 0 bags → drop. |
| **Franchise** | 4% of franchisee March POS + fees → royalty due. |
| **Invoice / GST** | Hotel B2B invoice → GST reports. |
| **Loyalty / coupon / promo** | Customer saves ₹50 and earns points at POS. |
| **EOD** | Manager reconciles cash vs system. |

---

## 17. Suggested 5-minute demo order

1. **Items** → show Basmati with barcode.  
2. **Barcode Labels** → print PDF.  
3. **POS** → scan, pay, complete.  
4. **Forecast / Reorder** → show suggestion for same item.  
5. **Anomalies** → mention spike/drop story.  
6. **Franchise** → open agreement + royalty idea.

---

*Document version: March 2026 — aligned with RetailERP modules (POS, inventory, forecast, franchise, labels, GST, loyalty, EOD, multi-tenant).*
