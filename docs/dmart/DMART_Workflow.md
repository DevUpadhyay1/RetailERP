DMART

BASE BILLING SOFTWARE

Complete Workflow Document

Web Application  |  Desktop Application

Version 1.0   |   19 February 2026

1. System Overview

DMART Base Billing Software supports both Web and Desktop modes with offline sync capability.

2. User Management Workflow

Covers user registration, role assignment, login authentication and session management.

3. Product Management Workflow

Full lifecycle of product creation, categorization, pricing and barcode assignment.

4. Inventory & Stock Management Workflow

Stock IN, OUT, adjustment and low-stock alert management per store.

4.1  Stock IN (Purchase Receipt)

4.2  Stock OUT (Billing / Sale)

4.3  Stock Adjustment & Alerts

5. Customer Management Workflow

Customer registration, loyalty points earning/redemption and membership management.

6. Billing Workflow (POS — Core Flow)

The primary sales billing process from cart creation to receipt printing.

This is the most critical workflow in the system, used by Cashiers for every sale.

7. Return & Refund Workflow

Handling customer returns, item-level returns and stock reversal.

8. Payment Processing Workflow

Multi-mode payment handling including cash, card, UPI, wallet and split payments.

9. End of Day (EOD) Workflow

Daily closing process for cash reconciliation, reports and system backup.

10. Reports & Analytics Workflow

Available reports, access levels and export options.

11. Desktop Offline Sync Workflow

How the desktop app handles offline billing and syncs to the web server when connected.

12. Security & Access Control Workflow

DMART Base Billing Software — Complete Workflow Document v1.0   |   Confidential   |   All rights reserved.


## Tables



### Table 1


| Admin Full system access, settings & reports | Manager Billing, stock, reports, approvals | Cashier POS billing & customer lookup | Supervisor Stock & purchase oversight |

| --- | --- | --- | --- |


### Table 2


| WEB Web App Cloud-based, multi-store, real-time | ► | SYNC Sync Engine Auto sync on connect | ► | DSK Desktop App Offline POS, hardware integration |

| --- | --- | --- | --- | --- |


### Table 3


| 1 | Admin Creates User Account Admin fills in full name, username, email, mobile, assigns Role (Admin/Manager/Cashier/Supervisor) and Store. |

| --- | --- |

| ▼ | ▼ |

| 2 | Role & Permissions Assigned System maps role to permission set from tbl_roles. Permissions stored as JSON — controls module access (Billing, Stock, Reports, Settings). |

| ▼ | ▼ |

| 3 | User Receives Credentials System generates temporary password. Email/SMS notification sent to user with login link. |

| ▼ | ▼ |

| 4 | First Login & Password Change User logs in with temporary credentials. System forces password reset on first login. New password is bcrypt-hashed before storage. |

| ▼ | ▼ |

| 5 | Login Authentication User enters username + password → system validates hash → checks is_active flag → creates session token (JWT) → logs login time in tbl_users.last_login. |

| ▼ | ▼ |

| 6 | Session Management Web: JWT token stored in browser (15-min expiry with refresh). Desktop: Local session token valid until logout or system restart. |

| ▼ | ▼ |

| 7 | Logout / Session Expiry User logs out or session expires → token invalidated → audit log entry created in tbl_audit_logs. |


### Table 4


| 1 | Create / Select Category Admin/Manager selects or creates category in tbl_categories (supports parent→child hierarchy). Set default GST rate for category. |

| --- | --- |

| ▼ | ▼ |

| 2 | Add New Product Fill product details: Name, SKU/Product Code, Barcode (EAN/UPC), Brand, Unit (Kg/Pcs/L), Category, HSN Code. |

| ▼ | ▼ |

| 3 | Set Pricing & Tax Enter Purchase Price, Selling Price, MRP. Set GST Rate (0/5/12/18/28%). System auto-calculates CGST + SGST splits. |

| ▼ | ▼ |

| 4 | Barcode Assignment Scan existing barcode OR system auto-generates barcode. Barcode stored as unique value in tbl_products.barcode. |

| ▼ | ▼ |

| 5 | Set Stock Threshold Set minimum stock level for low-stock alerts. Configure reorder quantity for automated PO suggestions. |

| ▼ | ▼ |

| 6 | Product Activation Product set to is_active = 1. Product now available in POS billing screen, stock module and reports. |

| ▼ | ▼ |

| 7 | Product Edit / Deactivate Price changes logged with old/new values in tbl_audit_logs. Deactivation hides product from POS without deleting sales history. |


### Table 5


| 1 | Create Purchase Order (PO) Manager creates PO against supplier in tbl_purchase_orders. Items, quantities and expected prices added to PO lines. |

| --- | --- |

| ▼ | ▼ |

| 2 | Supplier Delivers Goods Goods received at store. Cashier/Manager scans or selects products. Actual received qty entered (may differ from PO). |

| ▼ | ▼ |

| 3 | GRN Created (Goods Receipt Note) System creates GRN record. Stock quantity updated in tbl_stock (quantity += received_qty). Stock transaction logged in tbl_stock_transactions with type=IN. |

| ▼ | ▼ |

| 4 | PO Status Updated If full qty received → PO status = RECEIVED. If partial → status = PARTIAL. System flags any shortages for follow-up. |


### Table 6


| 1 | Item Added to Bill Cashier scans barcode or searches product. System checks available stock in tbl_stock for the current store. |

| --- | --- |

| ▼ | ▼ |

| 2 | Stock Validation If stock available → item added to bill. If stock insufficient → system shows warning (can be overridden by Manager). |

| ▼ | ▼ |

| 3 | Bill Completed & Paid On bill completion, system deducts quantities from tbl_stock. Stock transaction logged with type=OUT and reference to bill_id. |


### Table 7


| 1 | Manual Adjustment Admin/Manager can add stock adjustments (damage, theft, recount). Type = ADJUSTMENT / DAMAGE. All adjustments require remarks and are logged in audit trail. |

| --- | --- |

| ▼ | ▼ |

| 2 | Low Stock Alert System continuously checks if tbl_stock.quantity < tbl_products.min_stock_level. Alert shown on dashboard and optionally sent via email/SMS to Manager. |

| ▼ | ▼ |

| 3 | Auto PO Suggestion When stock falls below threshold, system suggests creating a new PO for that product to the default supplier. |


### Table 8


| 1 | Customer Registration Cashier collects customer mobile number (mandatory), name, email, DOB, address. System auto-generates unique customer_code. Customer record created in tbl_customers. |

| --- | --- |

| ▼ | ▼ |

| 2 | Customer Lookup at POS Cashier searches by mobile number or customer code. If found, customer linked to bill. If not found, option to register or proceed as walk-in. |

| ▼ | ▼ |

| 3 | Loyalty Points Earned on Purchase On bill completion, system calculates loyalty points based on grand_total (e.g., 1 point per ₹10). Points credited to customer balance. Transaction logged in tbl_loyalty_transactions with type=EARN. |

| ▼ | ▼ |

| 4 | Loyalty Points Redemption Customer requests to redeem points. Cashier enters points to redeem. System validates balance, calculates discount (e.g., 1 point = ₹0.50). Discount applied to bill. Transaction logged with type=REDEEM. |

| ▼ | ▼ |

| 5 | Membership Upgrade System auto-upgrades membership tier based on total_purchases: Silver (>₹5K), Gold (>₹20K), Platinum (>₹50K). Members receive additional discounts on specific categories. |

| ▼ | ▼ |

| 6 | Birthday / Anniversary Offers System checks customer DOB daily. Auto-generates special discount coupon and sends via SMS/email on birthday. |


### Table 9


| 1 | Open New Bill / Session Start Cashier logs in → system opens POS screen. Cashier optionally enters opening cash balance for the session (cash drawer). |

| --- | --- |

| ▼ | ▼ |

| 2 | Customer Identification (Optional) Cashier searches customer by mobile/code. If found → customer linked to bill (loyalty points eligible). If not found → walk-in sale proceeds without customer. |

| ▼ | ▼ |

| 3 | Add Items to Cart Method 1: Scan barcode using barcode scanner. Method 2: Type product name or code in search. Method 3: Browse category grid and tap/click product. Each scan adds item to cart with current selling price. |

| ▼ | ▼ |

| 4 | Quantity & Price Adjustment Cashier can modify quantity (e.g., 2 Kg of loose items). Manager can apply item-level discount (requires authorization). System recalculates subtotal, GST, and line total in real-time. |

| ▼ | ▼ |

| 5 | Apply Coupon / Discount Cashier enters coupon code → system validates (expiry, min order, usage limit) → discount applied to bill total. Loyalty points redemption entered if customer is linked. |

| ▼ | ▼ |

| 6 | Bill Summary & Tax Calculation System displays: Subtotal, Total Discount, CGST Amount, SGST Amount (or IGST for inter-state), Round Off, Grand Total. All values calculated per GST rules from HSN codes. |

| ▼ | ▼ |

| 7 | Payment Collection Cashier selects payment mode(s): Cash → enter amount received, system calculates change. Card → enter transaction reference. UPI → enter UPI reference ID. Split payment → multiple modes allowed for one bill. |

| ▼ | ▼ |

| 8 | Bill Confirmation & Stock Deduction On payment confirmation: Bill saved to tbl_bills + tbl_bill_items. Payment saved to tbl_payments. Stock deducted from tbl_stock. Loyalty points credited to customer. Coupon usage count incremented. |

| ▼ | ▼ |

| 9 | Receipt Printing & Sharing System prints receipt on thermal printer (80mm). Option to send digital receipt via SMS or email. Receipt includes bill number, itemized list, GST breakup, payment mode, cashier name. |

| ▼ | ▼ |

| 10 | Hold Bill Feature Cashier can put bill ON HOLD (status=HOLD) to serve another customer. Held bill can be resumed anytime. Maximum holds configurable by Admin. |


### Table 10


| 1 | Retrieve Original Bill Cashier searches by bill number or scans receipt barcode. System loads original bill details for review. |

| --- | --- |

| ▼ | ▼ |

| 2 | Select Items to Return Cashier selects specific items and quantities to return. System validates return eligibility (e.g., within return window, product type restrictions). |

| ▼ | ▼ |

| 3 | Return Reason Entry Cashier selects return reason: Defective / Wrong Item / Customer Change of Mind / Damaged Packaging. Reason stored with return record. |

| ▼ | ▼ |

| 4 | Manager Approval (if required) Returns above a configured value threshold require Manager PIN/approval. Approval logged in audit trail. |

| ▼ | ▼ |

| 5 | Return Bill Created System creates new bill with bill_type=RETURN referencing original bill_id. Negative quantities and amounts recorded in tbl_bill_items. |

| ▼ | ▼ |

| 6 | Stock Reversal Returned items added back to stock. tbl_stock.quantity incremented. Stock transaction logged with type=RETURN. |

| ▼ | ▼ |

| 7 | Refund Processing Cash refund: Amount given to customer and logged. Card/UPI refund: Reversal initiated, reference noted. Loyalty points adjustment if points were earned on original bill. |


### Table 11


| Payment Mode | Process | Validation |

| --- | --- | --- |

| CASH | Cashier enters cash received. System auto-calculates change amount. Change recorded in tbl_bills.change_amount. | Amount >= Grand Total required. Denomination breakdown optional. |

| CARD | Cashier swipes/taps card on POS terminal. Enters last 4 digits + transaction reference from terminal receipt. | Transaction reference mandatory. Status = SUCCESS on manual entry. |

| UPI | Customer scans QR code or pays via app. Cashier enters UPI transaction ID from customer's screen. | 12–20 character UPI ref ID required. Manual confirmation by cashier. |

| WALLET | Customer provides wallet ID (Paytm/PhonePe etc.). Amount deducted from customer's linked wallet balance. | Wallet balance pre-check if integrated. Reference ID recorded. |

| SPLIT | Cashier selects multiple payment modes. Enter amount per mode. System ensures total = Grand Total. | Sum of all split amounts must equal Grand Total exactly. |

| CREDIT | For B2B/account customers. Bill amount recorded as credit. Credit limit checked against customer profile. | Requires customer account & credit limit configured. Manager approval. |


### Table 12


| 1 | Session Closing by Cashier Cashier counts physical cash in drawer. Enters closing cash balance in system. System compares with expected cash (opening balance + cash sales - cash refunds). |

| --- | --- |

| ▼ | ▼ |

| 2 | Cash Reconciliation System shows: Expected Cash vs Actual Cash entered. Any variance (Over/Short) flagged and recorded. Cashier adds remarks for any variance. |

| ▼ | ▼ |

| 3 | Manager Reviews EOD Manager reviews all session summaries. Approves or flags discrepancies. Can unlock session for cashier correction if needed. |

| ▼ | ▼ |

| 4 | EOD Reports Generated System auto-generates: Day Sales Summary, Payment Mode Breakup, Product-wise Sales, GST Summary Report, Stock Movement Report, Cashier Performance Report. |

| ▼ | ▼ |

| 5 | Loyalty Points Expiry Check System checks for expired loyalty points (based on expiry policy). Expired points deducted and logged in tbl_loyalty_transactions with type=EXPIRE. |

| ▼ | ▼ |

| 6 | Database Backup (Desktop) Desktop app triggers local database backup to configured folder/external drive. Backup file named with date-timestamp for easy recovery. |

| ▼ | ▼ |

| 7 | Sync to Web Server (Desktop) Desktop app syncs all pending offline transactions to web server. Sync status updated in tbl_sync_log. Conflicts flagged for Admin review. |


### Table 13


| Report Name | Access Level | Frequency | Export Format |

| --- | --- | --- | --- |

| Daily Sales Summary | Manager, Admin | Daily / On-demand | PDF, Excel, Print |

| GST Summary Report | Admin | Monthly | PDF (GSTR-1 format) |

| Stock / Inventory Report | Manager, Admin | Daily / On-demand | Excel, PDF |

| Product-wise Sales Report | Manager, Admin | Weekly / Monthly | Excel, PDF |

| Cashier Performance Report | Manager, Admin | Daily / Weekly | PDF |

| Customer Purchase History | Manager, Admin | On-demand | PDF, Excel |

| Loyalty Points Report | Admin | Monthly | Excel |

| Supplier & PO Report | Manager, Admin | On-demand | PDF, Excel |

| Return & Refund Report | Manager, Admin | Daily / Monthly | PDF |

| Audit Log Report | Admin only | On-demand | PDF |

| Cash Reconciliation Report | Manager, Admin | Daily | PDF, Print |

| Low Stock Alert Report | Manager, Admin | Real-time | Email, Dashboard |


### Table 14


| OFF Offline Mode Bill locally in SQLite | ► | ► Internet Detected Auto-trigger sync | ► | SYN Sync Engine Push to web server | ► | OK Confirmed Sync log updated |

| --- | --- | --- | --- | --- | --- | --- |


### Table 15


| 1 | Offline Detection Desktop app pings web server every 30 seconds. If no response → switches to OFFLINE mode. All operations continue locally using SQLite database. |

| --- | --- |

| ▼ | ▼ |

| 2 | Local Transaction Recording All bills, payments, stock movements recorded locally. Each record flagged with sync_status = PENDING in tbl_sync_log. |

| ▼ | ▼ |

| 3 | Internet Reconnection App detects internet connectivity restored. Sync engine automatically initiates data push to web server. |

| ▼ | ▼ |

| 4 | Conflict Detection Server checks for conflicts (e.g., same barcode sold in both web and desktop while desktop was offline). Conflicts flagged with sync_status = CONFLICT. |

| ▼ | ▼ |

| 5 | Conflict Resolution Auto-resolution: Timestamp-based (latest record wins for non-financial data). Manual resolution: Financial conflicts (stock, payments) flagged for Admin review in web dashboard. |

| ▼ | ▼ |

| 6 | Sync Completion Successfully synced records updated to sync_status = SUCCESS. Failed records retried up to 3 times, then flagged as FAILED for manual intervention. |


### Table 16


| Security Layer | Details |

| --- | --- |

| Authentication | Username + Password (bcrypt hashed). JWT token with 15-min expiry + refresh token. Failed login attempts tracked — account locked after 5 failures. |

| Role-Based Access | All API endpoints and UI screens protected by role-permission check. Permission matrix stored in tbl_roles.permissions as JSON. |

| Audit Logging | Every critical action (login, bill create/edit, stock adjust, price change, user create) logged in tbl_audit_logs with user, timestamp, IP, old & new values. |

| Data Encryption | Passwords: bcrypt (cost factor 12). Sensitive fields (card refs): AES-256 at rest. HTTPS/TLS 1.3 for all web API communication. |

| Session Security | Web: Secure, HttpOnly JWT cookies. Desktop: Local encrypted session file. Auto-logout after 30 min inactivity. |

| Backup & Recovery | Daily automated database backup (web server). Desktop: Manual trigger + scheduled backup. Backups retained for 90 days. |
