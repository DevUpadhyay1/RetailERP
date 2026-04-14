# Prometheus & Grafana Monitoring Guide — RetailERP

This guide explains how to verify, configure, and use the Prometheus + Grafana monitoring stack that is now embedded in your production Docker cluster.

---

## What Changed

Three files were modified/created:

| File | Change |
|------|--------|
| `deploy/prometheus.yml` | NEW — Prometheus scrape config targeting `app:8080/metrics` |
| `docker-compose.prod.yml` | MODIFIED — Added `prometheus` and `grafana` containers + `grafana-data` volume |
| `.env.production` | MODIFIED — Added `OperationalEndpoints__AllowAnonymousMetrics=true` |

---

## After Deployment: Step-by-Step Verification

Once the GitHub Actions pipeline deploys successfully (or you manually run `docker compose up -d`), follow these steps:

### Step 1 — Confirm All Containers Are Running

Open PowerShell on your production server and run:

```powershell
docker ps
```

You should see **5 containers**:

| Container Name | Port | Purpose |
|---------------|------|---------|
| `retailerp-app-1` | 8080 | Your web application |
| `retailerp-sqlserver-1` | — | SQL Server database |
| `retailerp-redis-1` | — | Redis cache |
| `retailerp-prometheus` | 9090 | Metrics collector |
| `retailerp-grafana` | 3000 | Dashboard visualization |

If `retailerp-prometheus` or `retailerp-grafana` are missing, run:

```powershell
docker compose -p retailerp --env-file C:\7th_Semester\RetailERP\.env.production -f docker-compose.prod.yml up -d
```

---

### Step 2 — Verify Prometheus Is Scraping Your App

Open your browser and navigate to:

```
http://localhost:9090/targets
```

You should see one target entry:

| Job | Endpoint | State |
|-----|----------|-------|
| `retailerp` | `http://app:8080/metrics` | **UP** (green) |

If the state shows **DOWN** (red), check:
1. That your app container is running (`docker ps`)
2. That the `/metrics` endpoint is accessible: `curl http://localhost:8080/metrics`
3. That `.env.production` has `OperationalEndpoints__AllowAnonymousMetrics=true`

---

### Step 3 — Open Grafana

Open your browser:

```
http://localhost:3000
```

Login with:
- **Username:** `admin`
- **Password:** `admin123`

> **Note:** Grafana will ask you to change the password on first login. You can skip or set a new one.

---

### Step 4 — Connect Prometheus as a Data Source

1. Click the **hamburger menu** (☰) on the left sidebar
2. Go to **Connections → Data sources**
3. Click **Add data source**
4. Select **Prometheus**
5. In the **Prometheus server URL** field, enter: `http://prometheus:9090`
6. Scroll down and click **Save & test**
7. You should see: ✅ **"Data source is working"**

> **Why `prometheus:9090` and not `localhost:9090`?** Both Grafana and Prometheus are running inside Docker. Inside the Docker network, containers talk to each other using their service names, not `localhost`.

---

### Step 5 — Create Your RetailERP Dashboard

1. Click **☰ → Dashboards → New → New Dashboard**
2. Click **Add visualization**
3. Select **Prometheus** as the data source

#### Panel 1 — Requests Per Minute

- **Query:** `rate(retailerp_requests_total[1m])`
- **Title:** `Requests per minute`
- Click **Apply**

#### Panel 2 — Errors Per Minute

- Click **Add** → **Visualization**
- **Query:** `rate(retailerp_errors_total[1m])`
- **Title:** `Errors per minute`
- Click **Apply**

#### Panel 3 — Average Latency (ms)

- Click **Add** → **Visualization**
- **Query:** `rate(retailerp_request_duration_ms_total[1m]) / rate(retailerp_requests_total[1m])`
- **Title:** `Average latency (ms)`
- Click **Apply**

4. Click the **💾 Save** icon at the top
5. Name it: **RetailERP Overview**
6. Click **Save**

---

### Step 6 — Set Up an Email Alert

1. Click **☰ → Alerting → Alert rules → New alert rule**
2. **Name:** `Health check down`
3. In the query editor, enter: `absent(retailerp_requests_total)`
4. Set **Condition:** When query returns no data for **2 minutes**
5. Click **Save rule**

#### Configure Email Contact Point:

1. Click **☰ → Alerting → Contact points**
2. Click **Add contact point**
3. **Name:** `Email Alerts`
4. **Type:** Email
5. **Addresses:** Enter your email
6. Click **Save contact point**

---

### Step 7 — Verify Everything End to End

1. Hit your app several times at `https://quickbusiness.co.in` (browse around, open the POS page, etc.)
2. Go back to Grafana at `http://localhost:3000`
3. Open the **RetailERP Overview** dashboard
4. Confirm the **Requests per minute** panel shows real activity spikes

---

## Architecture Diagram

```
   Users
     │
     ▼
┌──────────────┐
│ quickbusiness│  (Port 8080)
│   .co.in     │
│   [app]      │──────── /metrics ────────┐
└──────────────┘                          │
                                          ▼
                                ┌──────────────────┐
                                │   Prometheus      │ (Port 9090)
                                │  Scrapes every    │
                                │    15 seconds     │
                                └────────┬─────────┘
                                         │
                                         ▼
                                ┌──────────────────┐
                                │    Grafana        │ (Port 3000)
                                │  Dashboards +     │
                                │   Alerts          │
                                └──────────────────┘
```

---

## Security Notes

- Prometheus and Grafana ports (`9090` and `3000`) are only exposed to `localhost`. External users on the internet **cannot** access them unless you explicitly open those ports in your firewall.
- The `/metrics` endpoint on your app is exposed anonymously so Prometheus can scrape it. This is safe because it only exposes aggregate counters (request count, error count, latency), not business data.
- Change the default Grafana password (`admin123`) to something stronger on first login.
