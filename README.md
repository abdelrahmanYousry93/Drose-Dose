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
| Jobs | Hangfire (from Phase 3) |
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

# 2. API  → http://localhost:5080
dotnet run --project src/Dose.Api --launch-profile http

# 3. Web  → http://localhost:4200
cd src/Dose.Web
npm install
npm start
```

## Build & test

```bash
dotnet build Dose.slnx
cd src/Dose.Web && npm run build && npm test -- --watch=false
```

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
| `Cors__AllowedOrigins__0` | Allowed web origin |

## Status

Phase 0 — repository scaffold and assessment, awaiting approval before Phase 1.
