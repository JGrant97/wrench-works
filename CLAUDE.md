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

No raw `fetch` or `axios` against the backend from components, and no hand-written TypeScript interfaces mirroring API responses.

**New code fetches through the Orval-generated functions in `@/api/generated/*`**, called from server components or route handlers. Those go through the `apiClient` mutator in `src/lib/api-client.ts`, which reads the httpOnly `ww_token` cookie and attaches the bearer token server-side. Response types come from `@/api/generated/models` — never redeclare them by hand.

> **Current state:** the generated client exists (109 files) but nothing imports it yet. Every existing page uses the legacy pattern — `useApi` / `useApiQuery` from `@/hooks/use-api` hitting the `/api/[...path]` proxy, with response shapes declared as local interfaces inside the page file. **That pattern is legacy, not the model to copy.** Migrate pages to the generated client as you touch them; see "Known gaps" below.

`API_BASE_URL` and `SESSION_SECRET` are server-only env vars. Never expose the JWT or the backend URL to the browser — no `NEXT_PUBLIC_` variable should ever hold either.

### 4. Server Components by default

Pages and layouts stay server components. Add `"use client"` only where you genuinely need interactivity, hooks, SWR, Zustand, or browser APIs — and push it to the smallest leaf component rather than marking a whole page. Fetch on the server where you can.

> **Current state:** 17 of 20 `page.tsx` / `layout.tsx` files are `"use client"` — every dashboard page. Only `app/layout.tsx`, `app/(auth)/layout.tsx`, and `app/page.tsx` are server components today. These pages predate the rule and are slated for migration; do not treat them as the pattern.

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

**Multi-tenancy is enforced by EF global query filters.** Business-scoped entities inherit `BusinessScopedEntity` and are filtered by the `business_id` JWT claim via `ITenantProvider` (implemented by `CurrentUserService`). A new tenant-scoped entity must inherit `BusinessScopedEntity`, get an EF configuration under `Infrastructure/Persistence/Configurations/`, **and** be added to the explicit `HasQueryFilter` list in `AppDbContext.OnModelCreating` — the filters are registered one entity at a time, so a new entity silently has no tenant isolation until you add its line.

The filter is written `_currentBusinessId == null || e.BusinessId == _currentBusinessId`, so **when there is no tenant context every row passes through**. That is what makes the anonymous auth endpoints work, and it means any code path running without a resolved `BusinessId` sees all tenants' data.

`IgnoreQueryFilters()` is used deliberately in several places and is not banned: the auth slices (Login, RefreshToken, VerifyEmail) run before a tenant context exists, and `Users` needs cross-business lookups when inviting an existing user or resolving `/users/me`. When you do use it, pair it with an explicit `&& x.BusinessId == businessId` predicate — that is the existing convention in `UserEndpoints`. Never trust a `BusinessId` sent in a request body; take it from `CurrentUserService`.

Not every entity is tenant-scoped: `Business` and `User` are global by design, and **`InventoryCategory` is also global** (`: BaseEntity`, no query filter) — categories are shared across all businesses. See the open question in `CreateCategoryAsync` before building on that.

**Auth is deny-by-default.** `Program.cs` sets a `FallbackPolicy` requiring an authenticated user, so anything anonymous (`/api/auth/*`, `/api/billing/webhook`, `/health`, the OpenAPI/Scalar endpoints) must say `.AllowAnonymous()` explicitly. Authorized endpoints declare a permission string — `.RequireAuthorization("jobs.edit")` — resolved by `PermissionAuthorizationHandler`. Permission names follow `<resource>.<action>` (`view`, `create`, `edit`, `manage`, `send`) and are seeded per business by `PermissionSeeder` into five system roles: Admin, Advisor, Technician, Inventory, ReadOnly. A new permission must be added to the seeder, or no role will ever have it.

**Errors** go through `ErrorHandlingMiddleware` — throw, don't hand-roll error responses in handlers. It maps a fixed vocabulary of exceptions to status codes and a `{ code, message }` JSON shape:

| Throw | Status | `code` |
|---|---|---|
| `ValidationException` (FluentValidation) | 400 | `validation_error` (plus a `field`/`message` list) |
| `UnauthorizedAccessException` | 401 | `unauthorized` |
| `ForbiddenException` | 403 | `forbidden` |
| `NotFoundException` | 404 | `not_found` |
| `ConflictException` | 409 | `conflict` (optional `details`) |
| `LimitReachedException` | 422 | `limit_reached` |
| anything else | 500 | `internal_error` (logged, message not leaked) |

These exception types are declared in `Middleware/ErrorHandlingMiddleware.cs`. Reuse them rather than returning ad-hoc `Results.BadRequest(new { ... })` — a few older handlers still do that, and it produces an inconsistent client contract.

**Times are UTC.** Fields are named `...Utc` (`ScheduledStartUtc`, `CreatedAtUtc`); keep that convention and convert for display only in the UI.

**Web UI conventions.** Build on the primitives in `src/components/ui` (Radix + Tailwind); compose classes with the `cn()` helper in `@/lib/utils`. Forms use react-hook-form with a Zod resolver. Toasts via react-hot-toast, icons via lucide-react. Don't introduce a competing UI, form, or data-fetching library.

**Feature and permission gating** in the UI uses `use-permission`, `use-feature`, and `<FeatureGate>` — reuse them rather than reading the session cookie directly.

**Config.** Web: copy `.env.local.example` → `.env.local`. API: `appsettings.json` locally, `__`-delimited env vars in Docker (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Cors__Origins__0`). The credentials in `docker-compose.yml` are dev-only; never reuse them anywhere real, and never commit a live `Jwt:Key` or Stripe secret.

**.NET 10 is on preview packages.** Don't "fix" the ASP.NET Core preview version numbers to stable ones — they're pinned deliberately.

---

## Known gaps and open questions

Verified against the code — these are the places where the codebase and the rules above disagree, or where the intent is genuinely undecided.

**Dashboard pages are slated for migration.** Every page under `app/(dashboard)` is a client component fetching via the proxy hooks with hand-written response types. The target is a server component fetching via `@/api/generated/*` with generated models. Migrate opportunistically when you're already working in a page; don't do a big-bang rewrite unprompted. Once a page is migrated, its inline `interface JobDetail`-style declarations should go.

**`useApi` / `useApiQuery` / the `/api/[...path]` proxy are transitional.** They stay until the pages above are migrated. Don't delete them, and don't build new features on them.

**OPEN QUESTION — `InventoryCategory` tenancy.** It extends `BaseEntity`, not `BusinessScopedEntity`, and has no `HasQueryFilter` line, so categories are shared across every business. `CreateCategoryAsync` (`Features/Inventory/InventoryEndpoints.cs`) then checks name uniqueness with `IgnoreQueryFilters()` globally, so once any business creates "Brakes", every other business gets a 409 and can never create their own. It is undecided whether the shared taxonomy is deliberate or a cross-tenant defect. **Do not "fix" or build on this without asking** — the fix (scope the entity, add a filter, add a migration, scope the uniqueness check) is a breaking data change.

**No CI, no formatter, no analyzer gate.** There's no `.github/workflows`, no `.editorconfig`, and no `dotnet format` step. Build and test discipline is manual — actually run the commands.

**Backend test coverage is one file.** `AuthTests.cs` covers health, register, and login. Every other slice — jobs, calendar conflict detection, billing, inventory, multi-tenancy isolation — has no tests. Rule 6 applies to new endpoints; the existing gap is unfilled backlog.

**Some handlers return ad-hoc error bodies.** e.g. `CreateCategoryAsync` returns `Results.BadRequest(new { code, message })` directly instead of throwing. New code should throw the middleware's exception types.

---

## Working agreements

- Nullable reference types and implicit usings are on in every project — keep the build warning-clean.
- Prefer `record` types for DTOs, as the existing slices do.
- After backend changes: `dotnet build` and `dotnet test`. After web changes: `npm run lint` and `npm run build`. Report failures with the real output rather than glossing over them.
- If a change spans both projects, finish the loop: API → `npm run generate-api` → update the web code.
