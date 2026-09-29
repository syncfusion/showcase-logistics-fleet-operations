# showcase-logistics-fleet-operations

Operate a delivery fleet from one dashboard. Built with Charts, Maps, Scheduler, Gantt, Kanban, and DataGrid for vehicle tracking, driver shift planning, delivery timelines, and shipment exception triage.

The app is wired around two database identities that enforce a real split at the database level —
not just in code:

- **`lfo_app`** (write) — used by EF Core migrations and every authenticated write endpoint
  (exceptions, schedules, route/driver edits, admin reset).
- **`lfo_readonly`** (read) — used by every public `GET` endpoint, including the overview, live
  map, schedules, timeline, exceptions list, and reference data. Has no write grants at all.

A JWT (`Jwt:SigningKey`) is also required to sign tokens for the demo login.

---

## Prerequisites

- **.NET SDK 10.0** (`dotnet --version` should report `10.x`).
- **Node.js 24+** and **npm** for the React app.
- **PostgreSQL 14+** (any managed instance works — Azure Database for PostgreSQL, local Postgres,
  etc.). The connection strings expect `Npgsql`-style `Host=...;Port=5432;Database=...;Username=...;Password=...`.
- **`psql`** or any PostgreSQL admin tool, to create roles and the database.

---

## 1. PostgreSQL — create the database and the two roles

Run these once, from any role that can `CREATE ROLE` / `CREATE DATABASE` (e.g. the server admin
user your provider gives you):

```sql
-- Two login roles: one with write/DDL grants, one strictly read-only.
CREATE ROLE lfo_app      LOGIN PASSWORD '<choose-a-strong-password>';
CREATE ROLE lfo_readonly LOGIN PASSWORD '<choose-a-different-password>';

CREATE DATABASE logistics_fleet_operations
    OWNER lfo_app
    ENCODING 'UTF8';
```

Grant the write role the rights it needs to run EF Core migrations and seed data, and the
read-only role just the read access its connection string will use:

```sql

-- lfo_app: schema, tables, DDL, DML, and (so the demo seeder can rebuild rows) the right to
-- TRUNCATE during a reset. Mirrors what EF Core + the standalone seed command need.
GRANT CONNECT, TEMP ON DATABASE logistics_fleet_operations TO lfo_app;
GRANT USAGE, CREATE ON SCHEMA public TO lfo_app;
GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
    ON ALL TABLES    IN SCHEMA public TO lfo_app;
GRANT USAGE, SELECT, UPDATE
    ON ALL SEQUENCES IN SCHEMA public TO lfo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES    TO lfo_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT, UPDATE                              ON SEQUENCES TO lfo_app;

-- lfo_readonly: only enough to run SELECTs on whatever tables lfo_app has already created.
GRANT CONNECT ON DATABASE logistics_fleet_operations TO lfo_readonly;
GRANT USAGE        ON SCHEMA public TO lfo_readonly;
GRANT SELECT       ON ALL TABLES    IN SCHEMA public TO lfo_readonly;
GRANT SELECT       ON ALL SEQUENCES IN SCHEMA public TO lfo_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES    TO lfo_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON SEQUENCES TO lfo_readonly;
```

> The read-only role is intentionally a **server-side** constraint — public `GET` endpoints bind
> to that connection string, and Postgres will reject any accidental write attempt with
> `permission denied`, not the application.

---

## 2. Backend — ASP.NET Core Web API

All commands below are run from the repository root unless noted.

### 2.1 Set user-secrets for the API project

The API project declares its own `<UserSecretsId>` GUID (visible in `webapi/src/Api/LogisticsFleetOperations.Api.csproj`),
so all secrets land in the shared store used by `webapi/src/Api`, the standalone seed tool, and
the SeedRunner WebJob.

> See Microsoft's docs on [Secret Manager in development in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
> for the full reference — including how to initialize a secrets store, list stored keys, and
> remove a secret.

```powershell
# Write connection string (lfo_app). Substitute your own host, username, and password.
dotnet user-secrets set "ConnectionStrings:App" "Host=localhost;Port=5432;Database=logistics_fleet_operations;Username=lfo_app;Password=<lfo_app_password>;Ssl Mode=Require" --project webapi/src/Api

# Read-only connection string (lfo_readonly). Same Host/Port/Database, different Username.
dotnet user-secrets set "ConnectionStrings:ReadOnly" "Host=localhost;Port=5432;Database=logistics_fleet_operations;Username=lfo_readonly;Password=<lfo_readonly_password>;Ssl Mode=Require" --project webapi/src/Api

# JWT signing key -- any high-entropy random string, at least 32 bytes (256 bits) long.
# PowerShell example (concatenates three GUIDs and strips the dashes):
$jwt = ([guid]::NewGuid().ToString() + [guid]::NewGuid().ToString() + [guid]::NewGuid().ToString()) -replace '-',''
dotnet user-secrets set "Jwt:SigningKey" $jwt --project webapi/src/Api

# (Optional) CORS origins for the SPA. In Development, the API defaults to http://localhost:5175
# and http://127.0.0.1:5175 without any configuration. In Production, leaving this empty causes
# startup to fail closed. Set one entry per allowed origin:
dotnet user-secrets set "Cors:AllowedOrigins:0" "http://localhost:5175" --project webapi/src/Api
```


### 2.2 Apply EF Core migrations (creates the schema)

```powershell
dotnet ef database update --project webapi/src/Infrastructure --startup-project webapi/src/Api
```

### 2.3 Seed the demo dataset (one-time, idempotent)


```powershell
# Standalone seed console (preferred for initial provisioning; no API needs to be running)
dotnet run --project webapi/src/Seed


Expected output ends with row counts for depots, vehicles, drivers, routes, route stops,
shipments, exceptions, and schedule entries.

### 2.4 Run the API

```powershell
dotnet run --project webapi/src/Api
```

The API listens on:

- `http://localhost:5269` (HTTP profile, `launchSettings.json`)
- `https://localhost:7232` and `http://localhost:5269` (HTTPS profile)


### 2.5 Demo login

When the sign-in dialog opens in the UI, the credentials are:

- **Email:** `demo@logistics-fleet-operations.showcase`
- **Password:** `FleetDemo!2026`

These are hardcoded in `DemoAuthProvider` for showcase purposes only.


---

## 3. Frontend — React + Vite

All commands below are run from `react/`.

### 3.1 Install dependencies

```powershell
cd react
npm install
```

### 3.2 (Optional) Configure an absolute API origin

By default, the SPA calls relative `/api/*` paths, which Vite's dev proxy forwards to
`http://localhost:5269` (set in `vite.config.ts`). You only need to override this if your backend
lives somewhere other than the default — e.g. an Azure staging API:

```powershell
# .env.local (git-ignored)
VITE_API_BASE_URL=https://your-api.com
```

If `VITE_API_BASE_URL` is empty, the SPA uses the Vite dev proxy in development and the
same-origin reverse proxy in production.

### 3.3 Run the dev server

```powershell
npm run dev
```

The dev server is on:

- `http://127.0.0.1:5175`

### 3.4 Other useful scripts

```powershell
npm run typecheck   # tsc --noEmit
npm run test        # node --test src/domain/*.test.ts
npm run build       # tsc --noEmit && vite build  (produces ./dist)
npm run preview     # serves the production build on http://127.0.0.1:4175
```

### 3.5 Run frontend + backend together

Open two terminals from the repository root:

```powershell
# Terminal 1 — backend
dotnet run --project webapi/src/Api

# Terminal 2 — frontend
cd react
npm run dev
```

Then open `http://127.0.0.1:5175`. The Vite dev proxy automatically routes `/api/*` requests to
`http://localhost:5269`, so the browser sees a same-origin app and no CORS configuration is
needed in development.

---

## Project layout

```
showcase-logistics-fleet-operations/
├── react/                                    # Vite + React 19 + Syncfusion EJ2
│   ├── src/
│   │   ├── App.tsx                           # Shell, navigation, auth, reset dialog
│   │   ├── pages/                            # Overview, LiveMap, DriverSchedules,
│   │   │                                     # DeliveryTimeline, Exceptions, Health
│   │   ├── api/                              # Typed fetch client and DTOs
│   │   ├── domain/                           # Pure helpers (map positions, status)
│   │   └── state/                            # Auth, session-overrides store
│   └── vite.config.ts                        # /api/* dev proxy -> http://localhost:5269
└── webapi/
    ├── src/
    │   ├── Api/                              # ASP.NET Core Web API host
    │   │   ├── Controllers/                  # Overview, Routes, Shipments, Exceptions,
    │   │   │                                 # Schedule, ReferenceData, Auth, Admin
    │   │   ├── HealthChecks.cs
    │   │   └── Program.cs                    # DI, CORS, JWT, /health, /api/health
    │   ├── Application/                      # DTOs, abstractions, services
    │   ├── Domain/                           # Entities and SLA rules
    │   ├── Infrastructure/                   # EF Core DbContexts, repositories, JWT auth
    │   ├── Seed/                             # Standalone `dotnet run` seed console
    └── tests/                                # xUnit + WebApplicationFactory
```
