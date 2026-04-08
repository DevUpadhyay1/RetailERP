# Cloudflare named tunnel setup for quickbusiness.co.in

Use this after you are ready to move DNS control from GoDaddy to Cloudflare.

Current status on this machine:

- cloudflared login completed
- Named tunnel created: retailerp-demo
- Tunnel ID: cab2ae0f-65a8-40d7-ab81-f03ab931037e
- DNS routes created for quickbusiness.co.in and www.quickbusiness.co.in

## 1) Cloudflare account and domain onboarding

1. Create/sign in to Cloudflare.
2. Add site: quickbusiness.co.in (Free plan).
3. Cloudflare will give two nameservers.
4. In GoDaddy, replace existing nameservers with Cloudflare nameservers.
5. Wait until Cloudflare shows the domain as Active.

From the current Cloudflare page shown in browser:

1. Click Continue to activation.
2. Copy the two Cloudflare nameservers shown on the next step.
3. Open GoDaddy for quickbusiness.co.in and replace ns75.domaincontrol.com and ns76.domaincontrol.com with those two Cloudflare nameservers.
4. Wait for Cloudflare status to become Active.

## 2) Prepare cloudflared on Windows host

Run in PowerShell from project folder:

c:\7th_Semester\RetailERP\tools\cloudflared.exe tunnel login

This opens browser auth and writes cert file under user profile cloudflared folder.

## 3) Create named tunnel

c:\7th_Semester\RetailERP\tools\cloudflared.exe tunnel create retailerp-demo

Note the tunnel UUID from output.

## 4) Route DNS through the tunnel

c:\7th_Semester\RetailERP\tools\cloudflared.exe tunnel route dns retailerp-demo quickbusiness.co.in
c:\7th_Semester\RetailERP\tools\cloudflared.exe tunnel route dns retailerp-demo www.quickbusiness.co.in

## 5) Create tunnel config

Create file at:

C:\Users\Dev\.cloudflared\config.yml

Template:

```
tunnel: cab2ae0f-65a8-40d7-ab81-f03ab931037e
credentials-file: C:\Users\Dev\.cloudflared\cab2ae0f-65a8-40d7-ab81-f03ab931037e.json

ingress:
  - hostname: quickbusiness.co.in
    service: http://localhost:8080
  - hostname: www.quickbusiness.co.in
    service: http://localhost:8080
  - service: http_status:404
```

## 6) Run tunnel

Foreground test:

c:\7th_Semester\RetailERP\tools\cloudflared.exe tunnel run --protocol http2 retailerp-demo

Optional Windows service install (recommended for always-on demo machine):

c:\7th_Semester\RetailERP\tools\cloudflared.exe service install

## 7) Verify

1. Open https://quickbusiness.co.in
2. Open https://www.quickbusiness.co.in
3. Login and invoice PDF paths should work.

## 8) App config note

For temporary quick tunnel testing, ALLOW_INSECURE_COOKIES_FOR_LOCAL_HTTP is currently enabled.
When named tunnel is stable and confirmed, set it back to false in .env.production and redeploy.
