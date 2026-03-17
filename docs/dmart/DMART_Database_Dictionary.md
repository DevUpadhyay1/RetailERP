DMART BASE BILLING SOFTWARE

Database Dictionary

Web & Desktop Application

Version 1.0  |  19 February 2026

# 1. Overview

This document describes the complete database schema for the DMART Base Billing Software, covering both the Web and Desktop applications. The database is designed for MySQL 8.0+ (Web Server) and SQLite/MySQL Local (Desktop). All tables use UTF-8 character encoding.

Total Tables: 18  |  Modules: User Mgmt, Product, Inventory, Billing, Customer, Supplier, Audit, Sync

# 2. Legend & Conventions

# 3. Table Definitions

## 3.1  User & Access Management

### Table: tbl_roles

User role definitions for access control

### Table: tbl_users

Stores all system users (Admin, Cashier, Manager, Supervisor)

### Table: tbl_stores

Stores/branch information for multi-store support

## 3.2  Product & Inventory Management

### Table: tbl_units

Units of measurement for products (Kg, Litre, Piece, etc.)

### Table: tbl_categories

Product category hierarchy (supports parent-child categories)

### Table: tbl_products

Master product catalog with pricing, tax and unit info

### Table: tbl_stock

Current stock levels per product per store

### Table: tbl_stock_transactions

All stock movement records (IN/OUT/ADJUSTMENT/RETURN)

## 3.3  Customer & Loyalty Management

### Table: tbl_customers

Customer master with loyalty and membership details

### Table: tbl_loyalty_transactions

Loyalty points earned and redeemed per customer

### Table: tbl_coupons

Discount coupons and promotional codes

## 3.4  Billing & Payment

### Table: tbl_bills

Main billing/invoice header table for all sales transactions

### Table: tbl_bill_items

Line items for each bill - one row per product per bill

### Table: tbl_payments

Payment transactions linked to bills (supports split payments)

## 3.5  Supplier & Procurement

### Table: tbl_suppliers

Supplier/vendor master data

### Table: tbl_purchase_orders

Purchase orders raised to suppliers

## 3.6  System & Sync Management

### Table: tbl_audit_logs

System-wide audit trail for all critical actions

### Table: tbl_sync_log

Desktop-to-web sync tracking for offline transactions

# 4. Key Relationships Summary

DMART Base Billing Software – Database Dictionary v1.0  |  Confidential


## Tables



### Table 1


| Symbol / Type | Meaning |

| --- | --- |

| PK | Primary Key – unique identifier for the table |

| AI | Auto Increment – automatically assigned by database |

| FK | Foreign Key – references another table's primary key |

| UQ | Unique – value must be unique across all rows |

| NO | NOT NULL – field is mandatory |

| YES | NULL allowed – field is optional |

| ENUM | Enumerated type – only specified values allowed |

| JSON | JSON data type – stores structured JSON object |

| DATETIME | YYYY-MM-DD HH:MM:SS format |

| DECIMAL(x,y) | x = total digits, y = decimal places |


### Table 2


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| role_id | INT | 11 | NO | PK/AI | NULL | Unique role identifier |

| role_name | VARCHAR | 50 | NO | UQ | NULL | Role name (Admin, Cashier, etc.) |

| description | TEXT | - | YES |  | NULL | Role description |

| permissions | JSON | - | YES |  | NULL | JSON object of module permissions |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 3


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| user_id | INT | 11 | NO | PK/AI | NULL | Unique user identifier |

| username | VARCHAR | 50 | NO | UQ | NULL | Login username |

| password_hash | VARCHAR | 255 | NO |  | NULL | Bcrypt hashed password |

| full_name | VARCHAR | 100 | NO |  | NULL | Full name of user |

| email | VARCHAR | 100 | YES | UQ | NULL | Email address |

| mobile | VARCHAR | 15 | YES |  | NULL | Mobile number |

| role_id | INT | 11 | NO | FK | NULL | References tbl_roles.role_id |

| store_id | INT | 11 | YES | FK | NULL | References tbl_stores.store_id |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| last_login | DATETIME | - | YES |  | NULL | Last login timestamp |

| created_at | DATETIME | - | NO |  | NOW() | Record creation time |

| updated_at | DATETIME | - | YES |  | NULL | Last update time |


### Table 4


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| store_id | INT | 11 | NO | PK/AI | NULL | Unique store identifier |

| store_code | VARCHAR | 20 | NO | UQ | NULL | Unique store code |

| store_name | VARCHAR | 100 | NO |  | NULL | Store display name |

| address | TEXT | - | YES |  | NULL | Full address |

| city | VARCHAR | 50 | YES |  | NULL | City |

| state | VARCHAR | 50 | YES |  | NULL | State |

| pincode | VARCHAR | 10 | YES |  | NULL | PIN/ZIP code |

| phone | VARCHAR | 15 | YES |  | NULL | Store phone number |

| gst_number | VARCHAR | 20 | YES |  | NULL | GST registration number |

| pan_number | VARCHAR | 15 | YES |  | NULL | PAN number |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 5


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| unit_id | INT | 11 | NO | PK/AI | NULL | Unique unit ID |

| unit_name | VARCHAR | 50 | NO |  | NULL | Full unit name (Kilogram) |

| unit_symbol | VARCHAR | 10 | NO | UQ | NULL | Symbol (Kg, L, Pcs) |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |


### Table 6


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| category_id | INT | 11 | NO | PK/AI | NULL | Unique category ID |

| category_name | VARCHAR | 100 | NO |  | NULL | Category name |

| parent_id | INT | 11 | YES | FK | NULL | Self-ref: tbl_categories.category_id |

| category_code | VARCHAR | 20 | YES | UQ | NULL | Unique category code |

| gst_rate | DECIMAL | 5,2 | YES |  | 0.00 | Default GST % for category |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| sort_order | INT | 5 | YES |  | 0 | Display sort order |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 7


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| product_id | INT | 11 | NO | PK/AI | NULL | Unique product identifier |

| product_code | VARCHAR | 50 | NO | UQ | NULL | Internal product code/SKU |

| barcode | VARCHAR | 100 | YES | UQ | NULL | EAN/UPC barcode |

| product_name | VARCHAR | 200 | NO |  | NULL | Product display name |

| category_id | INT | 11 | NO | FK | NULL | References tbl_categories.category_id |

| brand | VARCHAR | 100 | YES |  | NULL | Brand name |

| unit_id | INT | 11 | NO | FK | NULL | References tbl_units.unit_id |

| purchase_price | DECIMAL | 10,2 | NO |  | 0.00 | Purchase/cost price |

| selling_price | DECIMAL | 10,2 | NO |  | 0.00 | MRP / selling price |

| mrp | DECIMAL | 10,2 | YES |  | NULL | Maximum retail price |

| gst_rate | DECIMAL | 5,2 | NO |  | 0.00 | GST percentage (e.g. 18.00) |

| hsn_code | VARCHAR | 20 | YES |  | NULL | HSN/SAC code for GST |

| min_stock_level | INT | 11 | YES |  | 0 | Reorder alert threshold |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| image_path | VARCHAR | 255 | YES |  | NULL | Product image path/URL |

| description | TEXT | - | YES |  | NULL | Product description |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |

| updated_at | DATETIME | - | YES |  | NULL | Last update time |


### Table 8


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| stock_id | INT | 11 | NO | PK/AI | NULL | Unique stock record ID |

| product_id | INT | 11 | NO | FK | NULL | References tbl_products.product_id |

| store_id | INT | 11 | NO | FK | NULL | References tbl_stores.store_id |

| quantity | DECIMAL | 10,3 | NO |  | 0.000 | Current available quantity |

| reserved_qty | DECIMAL | 10,3 | YES |  | 0.000 | Reserved/on-hold quantity |

| expiry_date | DATE | - | YES |  | NULL | Expiry date (perishables) |

| batch_number | VARCHAR | 50 | YES |  | NULL | Batch/lot number |

| updated_at | DATETIME | - | YES |  | NULL | Last stock update time |


### Table 9


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| txn_id | INT | 11 | NO | PK/AI | NULL | Unique transaction ID |

| product_id | INT | 11 | NO | FK | NULL | References tbl_products.product_id |

| store_id | INT | 11 | NO | FK | NULL | References tbl_stores.store_id |

| txn_type | ENUM | - | NO |  | NULL | IN / OUT / ADJUSTMENT / RETURN / DAMAGE |

| quantity | DECIMAL | 10,3 | NO |  | NULL | Quantity moved (+/-) |

| reference_id | INT | 11 | YES |  | NULL | Bill/PO/Transfer reference ID |

| reference_type | VARCHAR | 30 | YES |  | NULL | BILL / PURCHASE_ORDER / TRANSFER |

| remarks | TEXT | - | YES |  | NULL | Additional notes |

| created_by | INT | 11 | NO | FK | NULL | References tbl_users.user_id |

| created_at | DATETIME | - | NO |  | NOW() | Transaction timestamp |


### Table 10


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| customer_id | INT | 11 | NO | PK/AI | NULL | Unique customer ID |

| customer_code | VARCHAR | 20 | NO | UQ | NULL | Auto-generated customer code |

| full_name | VARCHAR | 100 | NO |  | NULL | Customer full name |

| mobile | VARCHAR | 15 | NO | UQ | NULL | Mobile number (primary key for lookup) |

| email | VARCHAR | 100 | YES |  | NULL | Email address |

| address | TEXT | - | YES |  | NULL | Delivery/home address |

| city | VARCHAR | 50 | YES |  | NULL | City |

| dob | DATE | - | YES |  | NULL | Date of birth (for offers) |

| gender | ENUM | - | YES |  | NULL | MALE / FEMALE / OTHER |

| loyalty_points | INT | 11 | NO |  | 0 | Current loyalty points balance |

| membership_type | VARCHAR | 30 | YES |  | NULL | SILVER/GOLD/PLATINUM |

| membership_expiry | DATE | - | YES |  | NULL | Membership expiry date |

| total_purchases | DECIMAL | 12,2 | NO |  | 0.00 | Cumulative purchase amount |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Blocked |

| created_at | DATETIME | - | NO |  | NOW() | Registration timestamp |


### Table 11


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| loyalty_txn_id | INT | 11 | NO | PK/AI | NULL | Unique transaction ID |

| customer_id | INT | 11 | NO | FK | NULL | References tbl_customers.customer_id |

| bill_id | INT | 11 | YES | FK | NULL | References tbl_bills.bill_id |

| txn_type | ENUM | - | NO |  | NULL | EARN / REDEEM / EXPIRE / ADJUST |

| points | INT | 11 | NO |  | NULL | Points earned (+) or redeemed (-) |

| balance_after | INT | 11 | NO |  | NULL | Points balance after transaction |

| remarks | VARCHAR | 255 | YES |  | NULL | Notes |

| created_at | DATETIME | - | NO |  | NOW() | Timestamp |


### Table 12


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| coupon_id | INT | 11 | NO | PK/AI | NULL | Unique coupon ID |

| coupon_code | VARCHAR | 30 | NO | UQ | NULL | Unique coupon code string |

| coupon_name | VARCHAR | 100 | NO |  | NULL | Coupon display name |

| discount_type | ENUM | - | NO |  | PERCENT | PERCENT / FLAT |

| discount_value | DECIMAL | 10,2 | NO |  | NULL | Discount % or flat amount |

| max_discount | DECIMAL | 10,2 | YES |  | NULL | Max discount cap (for PERCENT) |

| min_order_value | DECIMAL | 10,2 | YES |  | 0.00 | Minimum bill value to apply |

| usage_limit | INT | 11 | YES |  | NULL | Max total uses (NULL=unlimited) |

| used_count | INT | 11 | NO |  | 0 | Number of times used |

| valid_from | DATE | - | NO |  | NULL | Coupon validity start |

| valid_to | DATE | - | NO |  | NULL | Coupon validity end |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 13


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| bill_id | INT | 11 | NO | PK/AI | NULL | Unique bill ID |

| bill_number | VARCHAR | 30 | NO | UQ | NULL | Formatted invoice number (e.g. INV-2025-00001) |

| bill_date | DATETIME | - | NO |  | NOW() | Bill date and time |

| store_id | INT | 11 | NO | FK | NULL | References tbl_stores.store_id |

| cashier_id | INT | 11 | NO | FK | NULL | References tbl_users.user_id |

| customer_id | INT | 11 | YES | FK | NULL | References tbl_customers.customer_id (walk-in=NULL) |

| bill_type | ENUM | - | NO |  | SALE | SALE / RETURN / EXCHANGE |

| subtotal | DECIMAL | 12,2 | NO |  | 0.00 | Total before tax and discount |

| total_discount | DECIMAL | 12,2 | NO |  | 0.00 | Total discount applied |

| total_gst | DECIMAL | 12,2 | NO |  | 0.00 | Total GST amount |

| cgst_amount | DECIMAL | 12,2 | NO |  | 0.00 | CGST portion |

| sgst_amount | DECIMAL | 12,2 | NO |  | 0.00 | SGST portion |

| igst_amount | DECIMAL | 12,2 | NO |  | 0.00 | IGST (inter-state) portion |

| round_off | DECIMAL | 5,2 | YES |  | 0.00 | Rounding adjustment |

| grand_total | DECIMAL | 12,2 | NO |  | 0.00 | Final payable amount |

| paid_amount | DECIMAL | 12,2 | NO |  | 0.00 | Amount paid by customer |

| change_amount | DECIMAL | 12,2 | NO |  | 0.00 | Change returned |

| loyalty_points_used | INT | 11 | YES |  | 0 | Loyalty points redeemed |

| loyalty_points_earned | INT | 11 | YES |  | 0 | Points earned on this bill |

| coupon_code | VARCHAR | 30 | YES |  | NULL | Coupon code applied |

| coupon_discount | DECIMAL | 10,2 | YES |  | 0.00 | Discount from coupon |

| bill_status | ENUM | - | NO |  | COMPLETED | COMPLETED / HOLD / CANCELLED / RETURNED |

| notes | TEXT | - | YES |  | NULL | Additional notes |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 14


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| item_id | INT | 11 | NO | PK/AI | NULL | Unique line item ID |

| bill_id | INT | 11 | NO | FK | NULL | References tbl_bills.bill_id |

| product_id | INT | 11 | NO | FK | NULL | References tbl_products.product_id |

| product_name | VARCHAR | 200 | NO |  | NULL | Snapshot of product name at time of sale |

| barcode | VARCHAR | 100 | YES |  | NULL | Product barcode at time of sale |

| quantity | DECIMAL | 10,3 | NO |  | NULL | Qty sold |

| unit_price | DECIMAL | 10,2 | NO |  | NULL | Price per unit before discount |

| discount_pct | DECIMAL | 5,2 | YES |  | 0.00 | Discount percentage on item |

| discount_amount | DECIMAL | 10,2 | YES |  | 0.00 | Discount amount on item |

| taxable_amount | DECIMAL | 10,2 | NO |  | NULL | Amount after discount, before tax |

| gst_rate | DECIMAL | 5,2 | NO |  | 0.00 | GST % applied |

| gst_amount | DECIMAL | 10,2 | NO |  | 0.00 | Total GST on item |

| cgst_rate | DECIMAL | 5,2 | YES |  | 0.00 | CGST rate |

| sgst_rate | DECIMAL | 5,2 | YES |  | 0.00 | SGST rate |

| line_total | DECIMAL | 10,2 | NO |  | NULL | Final amount for this line (incl. tax) |


### Table 15


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| payment_id | INT | 11 | NO | PK/AI | NULL | Unique payment ID |

| bill_id | INT | 11 | NO | FK | NULL | References tbl_bills.bill_id |

| payment_mode | ENUM | - | NO |  | NULL | CASH / CARD / UPI / WALLET / CREDIT / CHEQUE |

| amount | DECIMAL | 12,2 | NO |  | NULL | Amount paid via this mode |

| transaction_ref | VARCHAR | 100 | YES |  | NULL | UPI/Card transaction reference |

| payment_status | ENUM | - | NO |  | SUCCESS | SUCCESS / FAILED / PENDING / REFUNDED |

| payment_time | DATETIME | - | NO |  | NOW() | Payment timestamp |

| remarks | VARCHAR | 255 | YES |  | NULL | Additional notes |


### Table 16


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| supplier_id | INT | 11 | NO | PK/AI | NULL | Unique supplier ID |

| supplier_code | VARCHAR | 20 | NO | UQ | NULL | Supplier code |

| supplier_name | VARCHAR | 100 | NO |  | NULL | Supplier company name |

| contact_person | VARCHAR | 100 | YES |  | NULL | Primary contact name |

| mobile | VARCHAR | 15 | YES |  | NULL | Contact mobile |

| email | VARCHAR | 100 | YES |  | NULL | Email address |

| address | TEXT | - | YES |  | NULL | Business address |

| gst_number | VARCHAR | 20 | YES |  | NULL | Supplier GST number |

| pan_number | VARCHAR | 15 | YES |  | NULL | PAN number |

| payment_terms | VARCHAR | 50 | YES |  | NULL | e.g. Net 30, Immediate |

| is_active | TINYINT | 1 | NO |  | 1 | 1=Active, 0=Inactive |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 17


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| po_id | INT | 11 | NO | PK/AI | NULL | Unique PO ID |

| po_number | VARCHAR | 30 | NO | UQ | NULL | PO number (e.g. PO-2025-0001) |

| supplier_id | INT | 11 | NO | FK | NULL | References tbl_suppliers.supplier_id |

| store_id | INT | 11 | NO | FK | NULL | References tbl_stores.store_id |

| po_date | DATETIME | - | NO |  | NOW() | PO creation date |

| expected_date | DATE | - | YES |  | NULL | Expected delivery date |

| total_amount | DECIMAL | 12,2 | NO |  | 0.00 | Total PO value |

| po_status | ENUM | - | NO |  | PENDING | PENDING / APPROVED / RECEIVED / PARTIAL / CANCELLED |

| remarks | TEXT | - | YES |  | NULL | Notes |

| created_by | INT | 11 | NO | FK | NULL | References tbl_users.user_id |

| created_at | DATETIME | - | NO |  | NOW() | Creation timestamp |


### Table 18


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| log_id | BIGINT | 20 | NO | PK/AI | NULL | Unique log ID |

| user_id | INT | 11 | YES | FK | NULL | References tbl_users.user_id |

| store_id | INT | 11 | YES | FK | NULL | References tbl_stores.store_id |

| action | VARCHAR | 100 | NO |  | NULL | Action name (LOGIN, BILL_CREATE, etc.) |

| module | VARCHAR | 50 | YES |  | NULL | Module name (BILLING, STOCK, USER, etc.) |

| reference_id | VARCHAR | 50 | YES |  | NULL | ID of affected record |

| old_value | JSON | - | YES |  | NULL | Previous value (for updates) |

| new_value | JSON | - | YES |  | NULL | New value (for updates) |

| ip_address | VARCHAR | 45 | YES |  | NULL | Client IP address |

| device_type | ENUM | - | YES |  | NULL | WEB / DESKTOP / API |

| created_at | DATETIME | - | NO |  | NOW() | Log timestamp |


### Table 19


| Column Name | Data Type | Size | Null | Key | Default | Description |

| --- | --- | --- | --- | --- | --- | --- |

| sync_id | INT | 11 | NO | PK/AI | NULL | Unique sync record ID |

| device_id | VARCHAR | 100 | NO |  | NULL | Unique desktop device identifier |

| store_id | INT | 11 | NO | FK | NULL | References tbl_stores.store_id |

| table_name | VARCHAR | 50 | NO |  | NULL | Table being synced |

| record_id | INT | 11 | NO |  | NULL | Record ID in the synced table |

| sync_type | ENUM | - | NO |  | NULL | INSERT / UPDATE / DELETE |

| sync_status | ENUM | - | NO |  | PENDING | PENDING / SUCCESS / FAILED / CONFLICT |

| conflict_data | JSON | - | YES |  | NULL | Conflict details if any |

| synced_at | DATETIME | - | YES |  | NULL | Timestamp of successful sync |

| created_at | DATETIME | - | NO |  | NOW() | Record creation time |


### Table 20


| From (FK) |  | To (PK) | Description |

| --- | --- | --- | --- |

| tbl_users.role_id | → | tbl_roles.role_id | Each user has one role |

| tbl_users.store_id | → | tbl_stores.store_id | User belongs to a store |

| tbl_products.category_id | → | tbl_categories.category_id | Product belongs to a category |

| tbl_products.unit_id | → | tbl_units.unit_id | Product has a unit of measure |

| tbl_stock.product_id | → | tbl_products.product_id | Stock tracks a product |

| tbl_stock.store_id | → | tbl_stores.store_id | Stock belongs to a store |

| tbl_bills.store_id | → | tbl_stores.store_id | Bill raised at a store |

| tbl_bills.cashier_id | → | tbl_users.user_id | Bill created by a cashier |

| tbl_bills.customer_id | → | tbl_customers.customer_id | Bill linked to a customer (optional) |

| tbl_bill_items.bill_id | → | tbl_bills.bill_id | Line items belong to a bill |

| tbl_bill_items.product_id | → | tbl_products.product_id | Line item references a product |

| tbl_payments.bill_id | → | tbl_bills.bill_id | Payment linked to a bill |

| tbl_loyalty_transactions.customer_id | → | tbl_customers.customer_id | Loyalty points for customer |

| tbl_purchase_orders.supplier_id | → | tbl_suppliers.supplier_id | PO raised to a supplier |

| tbl_sync_log.store_id | → | tbl_stores.store_id | Sync record for a store |

| tbl_categories.parent_id | → | tbl_categories.category_id | Self-referential category hierarchy |
