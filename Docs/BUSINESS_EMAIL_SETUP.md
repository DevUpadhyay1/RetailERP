# Business Email Setup (Cloudflare DNS)

This project domain now uses Cloudflare nameservers, so all email DNS records must be added in Cloudflare DNS (not GoDaddy DNS).

## 1) Choose provider

- Lowest-cost option: Zoho Mail Lite
- Most common option: Google Workspace
- Microsoft stack option: Microsoft 365 Business Basic

## 2) Create your mailbox first

Create at least one owner mailbox in provider admin panel:

- admin@quickbusiness.co.in

Optional common inboxes:

- support@quickbusiness.co.in
- billing@quickbusiness.co.in
- noreply@quickbusiness.co.in

## 3) Add email DNS records in Cloudflare

Open Cloudflare -> quickbusiness.co.in -> DNS and add records from your provider wizard.
Use provider values exactly as given.

Required record types:

- MX records (mail routing)
- SPF TXT record
- DKIM TXT/CNAME record (provider-specific)
- DMARC TXT record

## 4) Recommended DMARC starter policy

Start with monitoring mode for 3-7 days, then tighten policy.

- Type: TXT
- Name: \_dmarc
- Value: v=DMARC1; p=none; rua=mailto:dmarc@quickbusiness.co.in; fo=1

After verification period, change to:

- p=quarantine

Later, strict mode:

- p=reject

## 5) Cloudflare proxy rule for mail records

- MX/TXT/DKIM/DMARC records should be DNS only (gray cloud).
- Do not proxy mail-related records.

## 6) Verify in provider panel

After DNS propagation, click Verify in your provider admin.

Then test:

- send mail from admin@quickbusiness.co.in to Gmail/Outlook
- reply back to admin@quickbusiness.co.in

## 7) Use business email to log in to RetailERP

When register page is open (no users yet), create first app account with your business email:

- https://quickbusiness.co.in/Identity/Account/Register

Then promote to SuperAdmin using:

- scripts/grant_superadmin.ps1

Example:

- powershell -ExecutionPolicy Bypass -File scripts/grant_superadmin.ps1 -Email "admin@quickbusiness.co.in"
