# Dose دروس

جرعتك المركزة من الفهم — a learning-session service for school students in Egypt.
Students book focused sessions (one lesson, a chapter/unit package, or a revision)
instead of paying for monthly lessons.

- Product baseline: [`docs/Dose_MVP_Specification.md`](docs/Dose_MVP_Specification.md)
- Phase 0 assessment (architecture, ERD, decisions, plan): [`docs/phase0-assessment.md`](docs/phase0-assessment.md)
- Brand & UI identity: [`docs/brand.md`](docs/brand.md)

## Stack

| Layer | Tech |
|---|---|
| API | ASP.NET Core, .NET 9 (builds on the .NET 9 or 10 SDK), modular monolith |
| Data | SQL Server / Azure SQL, EF Core (from Phase 1) |
| Jobs | Hangfire on SQL Server: group tier decisions (5 min), expire stale bookings (10 min) |
| Auth | ASP.NET Core Identity, HTTP-only same-site cookie (admin/teacher); students use secret booking links |
| Web | Angular 22, Arabic RTL, Cairo font (self-hosted) |

## Repository layout

```text
src/
  Dose.Api/             HTTP host (endpoints, auth, middleware)
  Dose.Application/     Use cases, one folder per module
  Dose.Domain/          Entities, value objects, business rules
  Dose.Infrastructure/  EF Core, file storage, notifications, meetings
  Dose.Contracts/       API DTOs
  Dose.Web/             Angular app (student, admin, teacher areas)
tests/                  Unit + integration tests (from Phase 1)
docs/                   Spec, assessment, brand
```

## Prerequisites

- .NET SDK 9.0.300 or newer (10 works too)
- Node.js 22+ and npm
- Docker (for local SQL Server)

## Run locally

Windows, one command (needs .NET 9 or 10 SDK, Node.js 22+, Docker Desktop): `powershell -ExecutionPolicy Bypass -File .\run-local.ps1`
— or with an existing SQL Server: add `-ConnectionString "Server=...;Database=Dose;..."`.

Manual steps:

```bash
# 1. Database (needed from Phase 1)
cp .env.example .env          # set DOSE_SQL_SA_PASSWORD
docker compose up -d

# 2. API  → http://localhost:5080 (migrates + seeds SAMPLE data in Development)
export ConnectionStrings__Dose="Server=localhost,1433;Database=Dose;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
export Seed__AdminEmail=admin@dose.local Seed__AdminPassword=<choose-a-password>
dotnet run --project src/Dose.Api --launch-profile http

# 3. Web  → http://localhost:4200 (proxies /api to :5080)
cd src/Dose.Web
npm install
npm start
```

## Build & test

```bash
dotnet build Dose.slnx
# Integration tests need a SQL Server (each run creates and drops its own database)
export DOSE_TEST_SQL="Server=localhost,1433;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
dotnet test --solution Dose.slnx
cd src/Dose.Web && npm run build && npm test -- --watch=false
```

Interface texts (Arabic + English) are edited in `src/Dose.Web/i18n-src/*.json`, then:

```bash
cd src/Dose.Web && python3 i18n-src/build.py && python3 i18n-src/check.py
```

New migration: `dotnet ef migrations add <Name> -p src/Dose.Infrastructure -s src/Dose.Api -o Persistence/Migrations`

## Key URLs

| URL | What |
|---|---|
| `/` | Home: choose grade, subject, track |
| `/sessions`, `/open-groups` | Available sessions / open groups |
| `/b/<token>` | Student's private booking page (secret link): details → payment → confirmation → join; group top-up choice; cancel |
| `/request-session` | Request a new session |
| `/login` | Team login (admins and teachers) |
| `/admin` | Admin: dashboard, payments, follow-ups, WhatsApp messages & templates, requests, sessions, pricing, teachers (with photo upload), catalog, payment methods, teacher availability, users, settings |
| `/teacher` | Teacher: my sessions (Meet link, attendance, end session), my availability |

## Configuration

Configuration comes from `appsettings*.json` and environment variables
(`Section__Key`). Secrets are never committed. Business values (prices, grade bands,
packages, group sizes, deadlines) live in the database and are managed by admins —
never hard-coded.

| Key | Purpose |
|---|---|
| `ConnectionStrings__Dose` | SQL Server connection |
| `Public__WhatsAppBusinessNumber` | Dose WhatsApp number (E.164) for click-to-chat |
| `Public__BusinessTimeZone` | Display time zone (default `Africa/Cairo`) |
| `Seed__AdminEmail` / `Seed__AdminPassword` | Development-only initial admin |
| `Storage__LocalRoot` | Private folder for payment screenshots (dev) |
| `Jobs__Enabled` | Run background jobs in this instance (default `true`) |
| `Public__AppBaseUrl` | Public site address — used in booking links and as the allowed origin for API calls |
| `Security__AllowedOrigins__0` | Optional extra site allowed to call the API (e.g. a staging front end) |
| `Security__HttpsPort` | Public HTTPS port for the redirect (default `443`) |
| `ASPNETCORE_WEBROOT` | Folder with the built Angular app (`dist/dose-web/browser`) when the API serves the site |

Production build served by the API (one origin): `cd src/Dose.Web && npm run build`, then run the API with
`ASPNETCORE_WEBROOT=<repo>/src/Dose.Web/dist/dose-web/browser` (or copy that folder to the API's `wwwroot`).
The service worker (PWA) is active only in production builds.

## Status

Phase 4, Arabic/English, WhatsApp messages, teacher photos, security hardening and PWA delivered — see `docs/phase0-assessment.md` sections 5–10. Next: Azure deployment (Blob storage, Key Vault, CI/CD, domain).
