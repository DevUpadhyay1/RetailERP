# Contributing to RetailERP

## Workflow

1. Create a branch from `main` / `develop`.
2. Make changes; keep commits focused.
3. Run locally before pushing:
   ```bash
   dotnet build RetailERP.sln -c Release
   dotnet test RetailERP.sln -c Release
   ```
4. Open a pull request; CI (`.github/workflows/ci.yml`) must pass.

## Code style

- Prefer **nullable reference types**; fix new warnings where practical.
- **Money / stock paths:** add or extend tests in `RetailERP.Tests` (in-memory EF is fine for service logic).
- New cross-cutting setup belongs in `Infrastructure/` (e.g. `WebApplicationBuilderExtensions.cs`), not only in `Program.cs`.

## API changes

- Secure new endpoints with `[Authorize]` and appropriate roles.
- Document in Swagger via XML comments where helpful.

## Secrets

Never commit production connection strings, JWT secrets, or payment provider keys. Use User Secrets locally and secure configuration in deployment environments.
