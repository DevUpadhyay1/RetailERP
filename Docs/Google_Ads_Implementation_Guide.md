# Google Ads Implementation Guide (RetailERP)

This guide is a practical, step-by-step setup for running Google Ads for a SaaS product like RetailERP after deployment.

## 1. Objective and Funnel

Use Google Ads to drive high-intent leads, not just traffic.

Primary funnel:
1. Ad click
2. Landing page visit
3. Lead action (`demo_booked` / `trial_started`)
4. Sales action (`paid_subscription`)

Recommended primary conversion for optimization:
- `demo_booked` (early stage)
- `paid_subscription` (once volume is stable)

## 2. Accounts and Access Checklist

Before setup, ensure you have:
- Google Ads account (Admin access)
- GA4 property (Editor/Admin access)
- Google Tag Manager container (Publish access)
- Website admin access (to install GTM code)

## 3. Measurement Setup (Do This First)

### Step 3.1: Set up web conversion tracking in Google Ads
1. In Google Ads, open conversion setup.
2. Create website conversion actions for your key goals:
- `lead_form_submit`
- `demo_booked`
- `trial_started`
- `paid_subscription`
3. Mark only business-critical goals as Primary conversions.

### Step 3.2: Install GTM and base tagging
1. Install GTM container code on all pages.
2. In GTM, set a Google tag to fire on all pages.
3. Add event tags for each conversion action.

### Step 3.3: Configure conversion linker (if needed)
1. In GTM: `Tags` -> `New` -> `Conversion Linker`.
2. Trigger: `All Pages`.
3. Preview and publish.

Note: If your container already loads a Google tag on every page, extra Conversion Linker may not be required.

### Step 3.4: Enable auto-tagging in Google Ads
1. Google Ads -> `Admin` -> `Account settings` -> `Auto-tagging`.
2. Enable: "Tag the URL that people click through from my ad."
3. Save.

### Step 3.5: Link GA4 and Google Ads
1. Link Google Ads account with GA4 property.
2. Confirm Ads data is flowing to Analytics.
3. Import GA4 key events into Google Ads (if using GA4 events as conversions).

### Step 3.6: QA and validation
1. Use GTM Preview + Tag Assistant.
2. Trigger each event once (test form/demo/trial/subscription path).
3. Confirm conversion status in Google Ads.
4. Re-check after 24-48 hours.

## 4. Campaign Launch Plan (Recommended)

Start with this mix:
- 70% budget: Search campaigns (high intent)
- 20% budget: Remarketing
- 10% budget: Testing (new creatives/keywords)

### Step 4.1: Search Campaigns
1. Create campaigns by intent cluster:
- `Retail POS Software`
- `Inventory + Billing Software`
- `GST Billing + ERP`
2. Separate ad groups by keyword intent.
3. Use clear ad copy with strong CTA:
- "Book Free Demo"
- "Start Free Trial"
4. Add at least 4 ad assets/extensions.

### Step 4.2: Landing Pages
1. Use dedicated landing pages per ad group.
2. Keep one primary CTA per page.
3. Ensure fast load and mobile-first layout.
4. Add trust blocks (features, screenshots, pricing clarity, support).

### Step 4.3: Bidding
1. Start with `Maximize Conversions` once tracking is verified.
2. Move to target CPA/ROAS only after enough stable conversion data.

## 5. Naming and UTM Standards

Use consistent naming:
- Campaign: `IN_Search_RetailERP_POS_Core`
- Ad group: `pos_software`
- Ads: `v1_benefit`, `v1_pricing`, `v1_demo`

UTM example:
`?utm_source=google&utm_medium=cpc&utm_campaign=retailerp_search_core&utm_content=ad1`

## 6. Optimization Routine

### Daily (10-15 min)
- Check spend pacing
- Check conversion drops/spikes
- Pause obvious waste queries

### Weekly
- Search terms cleanup (add negatives)
- Refresh low-CTR ads
- Move budget to best-performing campaigns
- Check landing page conversion rate

### Monthly
- GEO/device/time-of-day bid analysis
- Creative refresh cycle
- Funnel review: click -> lead -> demo -> paid

## 7. KPI Benchmarks (Early Stage)

Track at minimum:
- CTR
- CPC
- Cost per lead (`demo_booked`)
- Lead-to-paid rate
- Cost per paid subscription

Do not optimize only for CTR; optimize for `cost per qualified lead` and `cost per paid`.

## 8. Common Mistakes to Avoid

- Running ads before conversion tracking is verified
- Using one generic landing page for all keywords
- Optimizing to micro-events only (e.g., page view, button click)
- No negative keywords
- No remarketing follow-up
- Changing campaigns too frequently (no learning period)

## 9. 14-Day Execution Checklist

1. Days 1-2: Tracking + GTM + conversion QA
2. Days 3-4: Campaign/ad group build
3. Days 5-7: Launch + early negatives + budget control
4. Days 8-14: First optimization cycle (terms, ads, landing page)

## 10. Official References (Google)

- Set up conversions:
  - https://support.google.com/google-ads/answer/15464305
- Track website clicks as conversions:
  - https://support.google.com/google-ads/answer/6331304
- Campaign setup best practices:
  - https://support.google.com/google-ads/answer/9451609
- Campaign settings:
  - https://support.google.com/google-ads/answer/1704395
- Auto-tagging + GCLID (GA4):
  - https://support.google.com/analytics/answer/10723132
- Conversion Linker (GTM):
  - https://support.google.com/tagmanager/answer/7549390
