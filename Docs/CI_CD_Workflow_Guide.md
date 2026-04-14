# RetailERP CI/CD Pipeline & Staging Architecture Guide

This document explains the overarching Continuous Integration and Continuous Deployment (CI/CD) ecosystem built for RetailERP. It covers what each pipeline does, how staging works, the impact on your live servers, and how you can personally verify it.

---

## 1. The Complete Deployment Flow

Whenever you push code or merge pull requests, GitHub Actions watches those changes and triggers specific pipelines based on the branch you targeted.

### A. The "Staging" (Dry-Run & Verification) Phase
- **Triggers on:** Any push to the `develop` branch (or Pull Requests targeting `main`).
- **What it does:** It **does NOT** push code to your live `quickbusiness.co.in` server. Instead, it spins up an isolated, temporary server inside GitHub Actions (labeled `ubuntu-latest`). It builds your entire application, verifies your `docker build` compiles without syntax errors, and runs automated functionality tests ("Smoke Tests"). 
- **The Result:** If it passes, you get a **Green Checkmark ✅** on GitHub. This confirms that the code is historically safe to merge. If it fails, you get a **Red X ❌**, preventing broken code from getting close to your real servers.

### B. The "Production Gate" (Manual Approval)
- **Triggers on:** A push into the `main` branch.
- **What it does:** First, it natively runs the Smoke Test to ensure it's still structurally sound. If it passes, **it pauses**. This invokes your `environment: production` lock. 
- **The Result:** The pipeline halts entirely and requests a human Administrator (you) to log into GitHub, review the pending update, and click **"Approve and Deploy"**. 

### C. The "Production Deployment" Phase
- **Triggers on:** You clicking "Approve".
- **What it does:** The GitHub Runner running secretly in the background of your Windows VPS connects to Docker. It securely injects the `.env.production` file, rebuilds the `retailerp:latest` web app container, and runs `docker compose up -d`. 
- **Safe Bootup:** The ASP.NET Web App Container will refuse to respond to users until your SQL Server Container passes its independent startup health check. This eliminates `500 Server Errors` on reboot.
- **The Result:** The changes become instantly visible at `https://quickbusiness.co.in`.

---

## 2. How to Test and Cross-Check

You can easily trigger and watch this pipeline work using the following steps without actually disrupting your live production environment:

**Step 1: Test the Staging Circuit**
1. Create a branch and call it `develop`.
2. Make a simple, completely harmless change (like adding a `// comment` to `Program.cs`) and push it up to `develop`.
3. Open your GitHub Repository -> **Actions** tab.
4. You will see an action titled **Staging & Smoke Tests** running. Click on it. You will see it build your code, run the integrity tests, and display a Green Checkmark. 
5. Cross-check your live site: `qucikbusiness.co.in`. Your arbitrary comment will **not** be there. This proves staging protects the live instance!

**Step 2: Test the Production Promotion Gate**
1. Open up a Pull Request moving `develop` into `main`, or simply commit directly to `main` locally and `git push`.
2. Go to the GitHub **Actions** tab again. 
3. You will see a job running that eventually enters a yellow **"Waiting for review"** state.
4. Click on the job. You will see a prompt asking for an Administrator to review the deployment.
5. Click **Approve**. 
6. Watch as the pipeline drops into the deployment payload phase, restarts your Docker containers, and finally pushes the change out. 

---

## 3. What is the Impact? Why Did We Build This?

Before this architecture, pushing code directly to the repository executed an almost instantaneous Docker container deployment to production.

- **The Problem:** If a developer on your team accidentally introduced a syntax parameter bug, or if Entity Framework lost mapping tracking, the live RetailERP ecosystem crashed immediately without warning. Customers would view unhandled `500` exceptions on checkout screens.
- **The Solution:** The pipeline prevents bad code entirely. 
    1. If the code breaks, the GitHub "Staging" build turns red and stops.
    2. Even if the code compiles effectively, it halts before wiping the host servers, requiring you to knowingly authorize the migration.
    3. During the 15-second reboot sequence, Docker health-checks verify SQL is ready before opening ASP.NET, preventing application startup crashes.

In short, this is an enterprise-grade mechanism that transforms RetailERP from a volatile codebase into a hardened SaaS platform.
