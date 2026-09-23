# PRIME

**Property Registry, Information, Mapping & Evaluation System**

A Philippine LGU Real Property Information, Assessment & Tax platform.

PRIME manages the full lifecycle of a physical property — registration, ownership,
parcels, Real Property Units (RPUs), Tax Declarations, valuation, assessment,
GIS/tax mapping, billing, payments, collection, and delinquency — for Local
Government Units under RA 7160 and RA 12001.

PRIME is not a tax calculator. It is a Real Property Information, Valuation,
Assessment, Mapping, Billing and Collection Platform.

## Status

**Phase 4 — Property Registry: backend and frontend UI complete** (see
[docs/DEVELOPMENT-ROADMAP.md](docs/DEVELOPMENT-ROADMAP.md) for full
verification detail). Property/Taxpayer/Parcel/RPU/Tax Declaration
registration, the Property Profile screen and read model, property
search, ownership history, real `[Authorize]`-protected REST endpoints,
and full `AuditLog` persistence (one row per create/update/delete,
verified against a real database) all work end to end — proven by an
automated backend test that drives the entire create-property →
taxpayer → parcel → RPU → tax-declaration flow through real HTTP
requests, and by a browser-driven (Playwright) run of the same flow
through the actual UI with zero console errors. Update/Delete endpoints
and cross-entity search (by TD number, RPU number, or TIN) are not built
yet. Nothing in the repository has been committed to git yet.

## Project specification

The full functional and engineering specification for this project lives in
[CLAUDE.md](CLAUDE.md). It is the authoritative source of truth for scope,
domain rules, architecture constraints, and development phases.

## Documentation

| Document | Purpose |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture, technology stack, layering |
| [docs/DOMAIN-MODEL.md](docs/DOMAIN-MODEL.md) | Core domain entities, relationships, aggregates |
| [docs/DATABASE.md](docs/DATABASE.md) | Database strategy, ERD, migration and history strategy |
| [docs/DEVELOPMENT-ROADMAP.md](docs/DEVELOPMENT-ROADMAP.md) | Phased delivery plan and exit criteria per phase |

Additional documents (`BUSINESS-RULES.md`, `VALUATION.md`, `ASSESSMENT.md`,
`BILLING.md`, `GIS.md`, `SECURITY.md`, `API.md`, `DEPLOYMENT.md`,
`DATA-MIGRATION.md`, `TESTING.md`) will be added as their corresponding
development phases begin.

## Technology stack

- **Backend**: C# / ASP.NET Core, EF Core, Clean Architecture
- **Frontend**: React + TypeScript
- **Database / Auth / Storage**: [Supabase](https://supabase.com) (managed PostgreSQL + PostGIS, Auth, Storage) for shared/staging/production; local PostgreSQL + PostGIS for day-to-day development
- **GIS**: PostGIS + OpenLayers

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for exact versions and rationale.

## Local development setup

Prerequisites: .NET SDK 10 (LTS), Node.js 24 (LTS), PostgreSQL 16 + PostGIS
running locally.

1. Create a dedicated local database role and database (do not use the
   `postgres` superuser for the app connection):
   ```
   psql -U postgres -h localhost -c "CREATE ROLE prime WITH LOGIN PASSWORD 'choose-a-local-password'; CREATE DATABASE prime_dev OWNER prime;"
   psql -U postgres -h localhost -d prime_dev -c "CREATE EXTENSION IF NOT EXISTS postgis;"
   ```
   (`CREATE EXTENSION` needs a superuser; the migration below will no-op on
   `postgis` if it's already installed.)
2. Copy the values you need from [.env.example](.env.example) into
   `src/Prime.WebApi/appsettings.Development.json` (gitignored — create it
   if it doesn't exist) and `frontend/prime-web/.env.local` (gitignored).
   Set `ConnectionStrings:PrimeDb` to the role/password from step 1.
3. Apply migrations:
   ```
   dotnet tool install --global dotnet-ef   # first time only
   PRIME_DB_CONNECTION="Host=localhost;Port=5432;Database=prime_dev;Username=prime;Password=..." \
     dotnet-ef database update --project src/Prime.Infrastructure --startup-project src/Prime.Infrastructure
   ```
4. Run the backend: `dotnet run --project src/Prime.WebApi` (starts on
   `https://localhost:7221`; visit `/health` and `/swagger`).
5. Run the frontend: `cd frontend/prime-web && npm install && npm run dev`
   (starts on `http://localhost:5173`).

`DevAuth:Enabled: true` in `appsettings.Development.json` bypasses real
Supabase Auth locally — see docs/ARCHITECTURE.md §3.4. Never enable it
outside Development.

## Legal and domain data

PRIME never hard-codes tax rates, assessment levels, SMVs, ordinances,
exemptions, penalties, or interest. These are LGU-specific and legally
sourced values represented through configuration and effective-dated rules.
Any value in this repository prior to real LGU data being supplied is
demo/test data only and is never to be treated as authoritative.
