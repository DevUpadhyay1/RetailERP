# RetailERP: Real-World Engineering Mapping

This document bridges the gap between the code we wrote and how it applies to real-world cloud applications. Use this guide to explain *why* these features matter during interviews, vivas, or when introducing new developers to the codebase.

---

## 1. End-To-End Correlation ID (`X-Correlation-Id`)
**What we built:**  
A custom `CorrelationIdMiddleware` that attaches a unique `X-Correlation-Id` to every incoming request. We configured Serilog to push this ID into every single log message generated during that request.

**Real-world match (Observability):**  
In massive systems (like Amazon or Netflix), a single customer click might trigger dozens of underlying services. If an error happens, looking at thousands of random log lines is impossible. A Correlation ID acts like a tracking number for a package—allowing Operations to securely filter `grep 'my-id' logs.txt` and see the exact chronological lifecycle of *only* that specific user's request.

**How to prove it works:**
1. Run the app: `dotnet run` (Make note of the "Now listening on:" port in the console — typically `http://localhost:5820` or `https://localhost:7240`).
2. Open PowerShell or a terminal and run: `curl.exe -i https://localhost:7240/health` (Note: use `curl.exe` in PowerShell, not just `curl`).
3. Look at the response headers; you will see `X-Correlation-Id`.
4. Open the file `Logs/retailerp-*.log` and observe that the same ID is stamped on every database and system log line for that action.

---

## 2. API Security & Rate Limiting
**What we built:**  
Applied `[EnableRateLimiting("Login")]` on critical endpoints and enforced strict `[Authorize]` with role-based JWT authentication across the MVC and API layers.

**Real-world match (DDoS & Brute Force Protection):**  
SaaS applications like Shopify are constantly hammered by automated bots trying to guess passwords or scrape data. If these requests hit the database without limits, the server will crash (DDoS), or worse, a weak password will be guessed. Rate limiting creates a strict speed limit, dropping bots instantly in memory before they consume database resources.

**How to prove it works:**
1. Try to call the `POST /api/auth/login` endpoint rapidly (e.g., holding down Enter in Postman or a script).
2. Within seconds, the server will stop processing and return **HTTP 429: Too Many Requests**, protecting your database.

---

## 3. Integration Testing via WebApplicationFactory
**What we built:**  
Wired up `CustomWebApplicationFactory` and `DevelopmentWebApplicationFactory` to spin up a complete in-memory replica of our Web Server using an `InMemory` / `SQLite` database provider.

**Real-world match (CI/CD Confidence):**  
Professional teams don't rely only on Unit Tests, because Unit Tests don't catch routing errors, missing middleware, or database mapping crashes. Integration tests spin up the exact HTTP pipeline a real customer experiences. This guarantees that you can merge code on a Friday afternoon knowing that the `/health` and `login` buttons still work end-to-end.

**How to prove it works:**
1. Run `dotnet test RetailERP.sln -c Release`.
2. Notice how it completes 38 entire end-to-end and service tests in just a few seconds.

---

## 4. Operational Health Probes
**What we built:**  
Implemented `/health` and `/health/ready` endpoints that actively ping SQL Server and Redis connections.

**Real-world match (Self-Healing Cloud Infrastructure):**  
When you deploy to Azure, AWS, or Kubernetes, you configure the Cloud Load Balancer to ping `/health`. If a server freezes or loses its database connection, `/health` immediately returns `HTTP 503 Unhealthy`. The cloud router automatically removes that server from the pool so customers don't see errors, granting the system "self-healing" capabilities.

**How to prove it works:**
1. Start the app.
2. In your browser, hit `https://localhost:7240/health/ready` (or whatever port `dotnet run` showed).
3. You get a JSON payload returning `Healthy`. Stop SQL Server in your services, reload the page, and the JSON instantly shifts to `Unhealthy`.

---

## 5. Defense-in-Depth against Forgery (CSRF)
**What we built:**  
Globally enforced the `AutoValidateAntiforgeryTokenAttribute` on MVC controllers while explicitly opting out API endpoints using `[IgnoreAntiforgeryToken]`.

**Real-world match (Banking Security):**  
CSRF (Cross-Site Request Forgery) attacks occur when an attacker tricks a logged-in admin into visiting a malicious website. Inside that malicious website, hidden scripts submit forms back to your application (e.g., granting the attacker Admin rights). Enforcing Antiforgery Tokens ensures that forms only succeed if they were actually rendered by *your* application.

**How to prove it works:**
1. Log in to the portal as an Admin.
2. Inspect the HTML of any form and delete the hidden `<input name="__RequestVerificationToken">`.
3. Submit the form—the server will immediately reject the change with an `HTTP 400 Bad Request`.
