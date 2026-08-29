# CLAUDE.md

Wrench Works — workshop management SaaS. Two independent projects in one repo root:

| Path | What it is |
|---|---|
| `wrench-works-api/` | .NET 10 (preview) minimal-API backend, PostgreSQL, multi-tenant |
| `wrench-works-web/` | Next.js 15 App Router frontend (React 19, TypeScript, Tailwind) |

They are separate solutions/packages — always `cd` into the right one before running commands.

---

## Tech stack

**API** — .NET 10 minimal APIs · EF Core 10 + Npgsql (PostgreSQL) · vertical slice architecture · JWT bearer auth with permission-based policies · FluentValidation · BCrypt · Stripe · OpenAPI + Scalar docs · xUnit + FluentAssertions + Testcontainers.

**Web** — Next.js 15 (App Router) · React 19 · TypeScript · Tailwind + Radix UI · SWR (server state) + Zustand (client state) · react-hook-form + Zod · axios · Orval (generates the typed API client from the backend's OpenAPI doc) · lucide-react · react-hot-toast · date-fns.

---

## Commands

### API (`cd wrench-works-api`)

```bash
docker compose up --build
```

Postgres + API together; API on http://localhost:5000. Or run Postgres only and the API from source:

```bash
docker compose up postgres -d
```

```bash
dotnet run --project src/WrenchWorks.Api
```

```bash
dotnet build
```

```bash
dotnet test
```

`dotnet test` requires Docker to be running — Testcontainers spins up a real `postgres:16-alpine` per test fixture.

Scalar API docs (Development only): http://localhost:5000/scalar/v1 · OpenAPI doc: http://localhost:5000/openapi/v1.json

**Migrations** (run from `src/WrenchWorks.Api`):

```bash
dotnet ef migrations add <Name> -p ../WrenchWorks.Infrastructure -s .
```

Migrations are applied automatically at startup (`Program.cs` calls `db.Database.MigrateAsync()` and then `PermissionSeeder`), so a manual `dotnet ef database update` is usually unnecessary.

### Web (`cd wrench-works-web`)

```bash
npm run dev
```

```bash
npm run build
```

```bash
npm run lint
```

```bash
npm run generate-api
```

There is no test runner in the web app. Do not add Jest/Vitest/Playwright without asking.

---

## Project rules

### 1. Regenerate the API client after every contract change

Any change to an endpoint route, request DTO, response DTO, or tag leaves the Orval client stale. After changing the API:

1. Make sure the API is running on `http://localhost:5000` (Orval reads `/openapi/v1.json` live).
2. `cd wrench-works-web && npm run generate-api`.
3. Fix any TypeScript fallout in the web app before calling the task done.

Skipping this leaves the web app compiling against a contract that no longer exists.

### 2. Never edit `wrench-works-web/src/api/generated/**`

That whole tree is generated output — including `models/`. Edits there are silently destroyed on the next `npm run generate-api`. To change the shape of the client, change the API's endpoints/DTOs and regenerate.

The one hand-written piece of the client is the mutator, `src/lib/api-client.ts` — that file is ours and is safe to edit.

### 3. Never hand-write API calls in the web app

No raw `fetch` or `axios` against the backend from components. Data flows through exactly two paths:

- **Server components / route handlers** → Orval-generated functions from `@/api/generated/*`, which go through the `apiClient` mutator (it reads the httpOnly `ww_token` cookie and attaches the bearer token server-side).
- **Client components** → the `useApi` / `useApiQuery` hooks in `@/hooks/use-api`, which hit the Next.js proxy routes under `/api/*` (`src/app/api/[...path]/route.ts` forwards to the backend with the cookie's token).

`API_BASE_URL` and `SESSION_SECRET` are server-only env vars. Never expose the JWT or the backend URL to the browser — no `NEXT_PUBLIC_` variable should ever hold either.

### 4. Server Components by default

Pages and layouts stay server components. Add `"use client"` only where you genuinely need interactivity, hooks, SWR, Zustand, or browser APIs — and push it to the smallest leaf component rather than marking a whole page. Fetch on the server where you can.

### 5. Vertical slices on the API

Everything a feature needs lives in one folder under `src/WrenchWorks.Api/Features/<Feature>/`: request records, response DTOs, FluentValidation validators, and the static `*Endpoints` class with `Map(IEndpointRouteBuilder app)` plus its private handler methods. `Features/Jobs/JobEndpoints.cs` is the reference shape.

- No controllers. No shared cross-feature service layer — a slice may use `AppDbContext` and infrastructure services directly.
- Register each new slice with an explicit `<Feature>Endpoints.Map(app);` line in the "Map Feature Endpoints" block of `Program.cs`.
- Group routes with `app.MapGroup("/api/<feature>").WithTags("<Feature>").RequireAuthorization()`; the tag drives Orval's `tags-split` output folder, so keep it stable and meaningful.
- Handlers return `IResult`, take `AppDbContext` (and other services) as parameters, and accept a `CancellationToken`.
- Validators live beside the request record and are picked up by `AddValidatorsFromAssemblyContaining<Program>()`.

### 6. Every new endpoint gets an integration test

Add tests to `tests/WrenchWorks.Tests/`, following the `ApiFactory` pattern in `AuthTests.cs` (a `WebApplicationFactory<Program>` with a Testcontainers Postgres, consumed via `IClassFixture<ApiFactory>`). Assert with FluentAssertions. Cover the happy path, validation failure, and — for tenant-scoped resources — that another business cannot read or mutate the row. Run `dotnet test` and report the actual result before saying a change is done.

---

## Things to know before touching the code

**Multi-tenancy is enforced by EF global query filters.** Business-scoped entities inherit `BusinessScopedEntity` and are filtered by the `business_id` JWT claim via `ITenantProvider` (implemented by `CurrentUserService`). Never call `IgnoreQueryFilters()` on tenant data, and never trust a `BusinessId` sent in a request body. A new tenant-scoped entity must inherit `BusinessScopedEntity` and get an EF configuration under `Infrastructure/Persistence/Configurations/`.

**Auth is deny-by-default.** `Program.cs` sets a `FallbackPolicy` requiring an authenticated user, so anything anonymous (`/api/auth/*`, `/api/billing/webhook`, `/health`, the OpenAPI/Scalar endpoints) must say `.AllowAnonymous()` explicitly. Authorized endpoints declare a permission string — `.RequireAuthorization("jobs.edit")` — resolved by `PermissionAuthorizationHandler`. Permission names follow `<resource>.<action>` (`view`, `create`, `edit`, `manage`, `send`) and are seeded per business by `PermissionSeeder` into five system roles: Admin, Advisor, Technician, Inventory, ReadOnly. A new permission must be added to the seeder, or no role will ever have it.

**Errors** go through `ErrorHandlingMiddleware` — throw, don't hand-roll error responses in handlers.

**Times are UTC.** Fields are named `...Utc` (`ScheduledStartUtc`, `CreatedAtUtc`); keep that convention and convert for display only in the UI.

**Web UI conventions.** Build on the primitives in `src/components/ui` (Radix + Tailwind); compose classes with the `cn()` helper in `@/lib/utils`. Forms use react-hook-form with a Zod resolver. Toasts via react-hot-toast, icons via lucide-react. Don't introduce a competing UI, form, or data-fetching library.

**Feature and permission gating** in the UI uses `use-permission`, `use-feature`, and `<FeatureGate>` — reuse them rather than reading the session cookie directly.

**Config.** Web: copy `.env.local.example` → `.env.local`. API: `appsettings.json` locally, `__`-delimited env vars in Docker (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Cors__Origins__0`). The credentials in `docker-compose.yml` are dev-only; never reuse them anywhere real, and never commit a live `Jwt:Key` or Stripe secret.

**.NET 10 is on preview packages.** Don't "fix" the ASP.NET Core preview version numbers to stable ones — they're pinned deliberately.

---

## Working agreements

- Nullable reference types and implicit usings are on in every project — keep the build warning-clean.
- Prefer `record` types for DTOs, as the existing slices do.
- After backend changes: `dotnet build` and `dotnet test`. After web changes: `npm run lint` and `npm run build`. Report failures with the real output rather than glossing over them.
- If a change spans both projects, finish the loop: API → `npm run generate-api` → update the web code.
