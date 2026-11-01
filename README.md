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
| API | ASP.NET Core, .NET 10 (LTS), modular monolith |
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

- .NET SDK 10.0
- Node.js 22+ and npm
- Docker (for local SQL Server)

## Run locally

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

## Status

Phase 4 plus Arabic/English, WhatsApp messages and teacher photos delivered — see `docs/phase0-assessment.md` sections 5–9. Next: Phase 5 (hardening, PWA, Azure deployment).
