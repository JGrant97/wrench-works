# CLAUDE.md

Wrench Works — workshop management SaaS. Two independent projects in one repo root:

| Path | What it is |
|---|---|
| `wrench-works-api/` | .NET 10 (preview) minimal-API backend, PostgreSQL, multi-tenant |
| `wrench-works-web/` | Next.js 15 App Router frontend (React 19, TypeScript, Tailwind) |

They are separate solutions/packages — always `cd` into the right one before running commands.

### Companion docs — imported, always in context

The five files below are **imported** at the end of this file, so their full contents load with it every session. You do not need to open them, and you should not re-derive what they already record. They are project memory, not reference reading:

- **[docs/app-flow.md](docs/app-flow.md)** — how a request travels from a component to the database, the auth/session lifecycle, the permission vs. feature split, the domain model, a screen-by-screen map, and every bug found by actually running the app (fixed and open).
- **[docs/bookings-crud.md](docs/bookings-crud.md)** — the state of booking CRUD, what's missing, verified behaviour of conflict detection and the timezone handling, and a proposed build order.
- **[docs/vehicle-catalogue.md](docs/vehicle-catalogue.md)** — designed, not built: replacing free-text vehicle entry with a make → model → year → variant cascade, plus what NHTSA vPIC actually does and doesn't provide (measured, not assumed).
- **[docs/tax.md](docs/tax.md)** — charging tax across VAT and US sales-tax regimes without becoming a compliance engine: why rates are configured rather than shipped, why tax lives on the line and not the job, the rounding rule, and what is deliberately not built.
- **[docs/review-findings.md](docs/review-findings.md)** — the triaged output of a six-reviewer pass over both projects (31 Aug 2026): what breaks today, what silently corrupts data, what is latent, each marked *Verified* or *Reported*, plus what was checked and found clean.

Because they are always loaded, keeping them accurate is not optional — see "Keeping this file and the companion docs current" at the end of this file. A stale line in an imported doc is a wrong fact in every future session.

---

## Tech stack

**API** — .NET 10 minimal APIs · EF Core 10 + Npgsql (PostgreSQL) · vertical slice architecture · JWT bearer auth with permission-based policies · FluentValidation · BCrypt · Stripe · OpenAPI + Scalar docs · xUnit + FluentAssertions + Testcontainers.

**Web** — Next.js 15 (App Router) · React 19 · TypeScript · Tailwind + Radix UI · SWR (server state) + Zustand (client state) · react-hook-form + Zod · axios · Orval (generates the typed API client from the backend's OpenAPI doc) · lucide-react · react-hot-toast · date-fns.

---

## Commands

### Starting the stack — always do this first

**The API cannot boot without Postgres** (it runs migrations at startup and crashes otherwise). So before running or restarting the API, make sure the database is actually up, and start it if it isn't. Don't report a connection-refused crash back to the user as a blocker — bring Postgres up and carry on.

1. **Is the Docker daemon running?** `docker info` — if it errors, start Docker Desktop and wait for it, then continue:

```bash
powershell -NoProfile -Command "Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe'"
```

Give it time — the daemon typically takes 30–60s to accept connections. Poll `docker info` until it succeeds rather than assuming a fixed delay. Starting Docker Desktop is fine to do unprompted; it's a local dev dependency.

2. **Is Postgres up?** From `wrench-works-api`, this is idempotent — it starts the container if stopped and returns immediately if already healthy:

```bash
docker compose up postgres -d --wait
```

`--wait` blocks on the container's healthcheck, so when it returns the database is genuinely accepting connections, not merely started. The container is named `wrenchworks-db`; `docker inspect --format '{{.State.Health.Status}}' wrenchworks-db` gives its health directly.

3. **Then** start the API and web app.

If the API dies immediately on startup, check Postgres before debugging anything else — that is nearly always the cause.

### API (`cd wrench-works-api`)

```bash
docker compose up --build
```

Postgres + API together; API on http://localhost:5000. Or Postgres only, with the API from source (the usual dev loop):

```bash
docker compose up postgres -d --wait
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

`dotnet test` requires Docker to be running — Testcontainers spins up a real `postgres:16-alpine` per test fixture (separate from the dev database, so tests never touch dev data).

**Stop the API before running tests.** On Windows a running `dotnet run` holds a lock on `WrenchWorks.Domain.dll` / `WrenchWorks.Infrastructure.dll`, and the test build fails with `MSB3027 / MSB3021: the file is locked by "WrenchWorks.Api"`. That error means "shut the API down and re-run", not anything about the tests.

**The test project has no `GlobalUsings.cs` and `ImplicitUsings` doesn't cover xunit.** Every test file needs `using Xunit;` explicitly, plus `using System.Net.Http.Json;` for `PostAsJsonAsync` / `ReadFromJsonAsync`. Omitting them produces a wall of `CS0246: 'Fact' could not be found`-style errors that look like a broken package reference but aren't.

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
npm run generate-api
```

`npm run build` is the verification gate — it type-checks, and `npm run lint` is currently unconfigured (it opens an interactive ESLint setup prompt, so don't put it in a script or CI without fixing it first).

**Never run `npm run build` while `npm run dev` is running.** They share `.next`, and the production build overwrites the dev server's chunks — the running app then 404s on every `_next/static/*` request until you restart the dev server. Stop dev, build, restart dev.

Worse if you *interrupt* a build that was racing the dev server: `.next` is left half-written, every page returns a bare `Internal Server Error`, and restarting dev alone does not fix it. Recovery is `rm -rf .next` then restart. (Confirmed 31 Aug 2026 — the error surfaced as `ENOENT ... prerender-manifest.json` in the browser console.)

**To type-check without touching `.next`, use `npx tsc --noEmit`.** It is the same check `npm run build` performs, runs in seconds, and is safe with the dev server up — which makes it the right gate while iterating. Still run a full `npm run build` (dev stopped) before calling a change done, since it also catches build-time issues tsc cannot see.

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

**Delete removes a row only when nothing references it; everything else archives.** Decided 31 Aug 2026. `IArchivable` (`Domain/Entities/BaseEntity.cs`) adds `ArchivedAtUtc` to `Customer`, `Vehicle`, `Job` and `InventoryItem`; `Zone` keeps its existing `IsActive` instead. `Features/Common/Archiving.cs` holds the rule: `EnsureDeletable` counts dependents and throws a 409 naming them ("This customer has 1 vehicle, 9 jobs, 10 bookings…"), and the UI offers archiving in the same dialog (`components/record-actions.tsx`).

Two things to preserve when adding a delete endpoint anywhere else:

- **Archived rows are excluded by list endpoints, NOT by the global query filter.** Filtering them globally would blank the customer name on a historical job — the exact loss archiving exists to prevent. Lists take `?includeArchived=true`; detail and history lookups always resolve.
- **The FKs are `Restrict` now and must stay that way.** Eight were `Cascade` (finding 9), so a delete written without a dependency check would not have errored — it would have returned 204 and destroyed the customer's history. `Business → *` stays `Cascade` so tenant offboarding still works, as do job → line-items and make → model → variant, which are genuinely owned children.

**Currency is a business setting, and every amount follows it.** GBP, USD and EUR, chosen on `/settings/general`. The vocabulary is closed and enforced server-side by `SupportedCurrencies` in `Features/Business/BusinessEndpoints.cs` — the dropdown is not the guard, since a request need not come from the dropdown.

The code travels in the readable `ww_user` cookie next to permissions and features, which is what lets client and server components format identically without either doing an extra fetch:

- **Client components:** `useCurrency()` (`hooks/use-currency.ts`) → `{ currency, symbol, format }`. Use `format(total)`, not a bare `formatCurrency(total)` — the bare call falls back to GBP regardless of the business, which is the bug this replaced.
- **Server components:** `getCurrency()` (`lib/currency-server.ts`), then `formatCurrency(amount, currency)`. Separate file because it imports `next/headers`, which cannot be pulled into a client bundle.
- **Symbols in labels** ("Rate (£/hr)") come from `symbol`, not a literal.

There is deliberately **no module-level "current currency"**. Server components share module scope across requests, so a mutable would let one tenant's currency bleed into another tenant's render — the same class of leak the query filters exist to prevent.

Changing the setting calls `POST /api/auth/refresh` and reloads, because the cookie is what every page reads; without that the change would not appear until the 24h expiry. Guarded by `BusinessTests`.

**The billing page's plan prices stay hard-coded in £.** Those are what *you* charge the workshop for the SaaS, not the workshop's own money — rendering "€29/mo" for a price you bill in pounds would be a lie. Do not "fix" them to follow this setting.

**Times are UTC.** Fields are named `...Utc` (`ScheduledStartUtc`, `CreatedAtUtc`); keep that convention and convert for display only in the UI.

**`Business.Timezone` is stored but never used.** It's editable on `/settings/general` and nothing reads it — every date renders in the *browser's* timezone. Verified: a booking entered as 09:00 on a UTC-5 machine stores as `14:00Z`, so a business configured as `UTC` sees a different time than the person who booked it. Self-consistent within one browser, wrong across two. Settle this before adding more write paths — see [docs/bookings-crud.md](docs/bookings-crud.md).

**Web UI conventions.** Build on the primitives in `src/components/ui` (Radix + Tailwind); compose classes with the `cn()` helper in `@/lib/utils`. Forms use react-hook-form with a Zod resolver. Toasts via react-hot-toast, icons via lucide-react. Don't introduce a competing UI, form, or data-fetching library.

**A page that grows past ~250 lines gets colocated `_components/` and `_lib/` folders.** The underscore makes them private to the App Router, so they sit next to the page they serve without becoming routes. Established 31 Aug 2026 when `calendar/page.tsx` (1002 lines, six components) and `jobs/[id]/page.tsx` (533 lines, three modals) were split:

- `calendar/_lib/booking.ts` — types, layout constants, and the pure lane-packing/multi-day helpers the week and month grids share
- `calendar/_components/` — `week-view`, `month-view`, and the detail/create/edit modals
- `jobs/[id]/_lib/job.ts` — types plus `STATUS_TRANSITIONS`, which **mirrors `ValidTransitions` in `JobEndpoints.cs`**; change one and you must change the other
- `jobs/[id]/_components/` — the add-labor, add-part and edit-job modals

Shared across pages rather than within one, so it lives in `src/hooks`: **`useCustomerVehicle`** (`hooks/use-customer-vehicle.ts`) is the customer-search-then-pick-a-vehicle pair used by New Booking and New Job. Both previously had their own copy including their own inline response types — the duplication `bookings-crud.md` had flagged. Use it rather than re-declaring those shapes.

**Feature and permission gating** in the UI uses `use-permission`, `use-feature`, and `<FeatureGate>` — reuse them rather than reading the session cookie directly.

**Config.** Web: copy `.env.local.example` → `.env.local`. API: `appsettings.json` locally, `__`-delimited env vars in Docker (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Cors__Origins__0`). The credentials in `docker-compose.yml` are dev-only; never reuse them anywhere real, and never commit a live `Jwt:Key` or Stripe secret.

**.NET 10 is on preview packages.** Local SDK is 10.0.400. Don't casually "fix" the ASP.NET Core preview version numbers to stable ones. Note that the EF Core pins in `Infrastructure` don't resolve to what they claim — see the `NU1603` note in Known gaps.

---

## Known gaps and open questions

Verified against the code — these are the places where the codebase and the rules above disagree, or where the intent is genuinely undecided.

**Dashboard pages are slated for migration.** Every page under `app/(dashboard)` is a client component fetching via the proxy hooks with hand-written response types. The target is a server component fetching via `@/api/generated/*` with generated models. Migrate opportunistically when you're already working in a page; don't do a big-bang rewrite unprompted. Once a page is migrated, its inline `interface JobDetail`-style declarations should go.

**`useApi` / `useApiQuery` / the `/api/[...path]` proxy are transitional.** They stay until the pages above are migrated. Don't delete them, and don't build new features on them.

**OPEN QUESTION — `InventoryCategory` tenancy.** It extends `BaseEntity`, not `BusinessScopedEntity`, and has no `HasQueryFilter` line, so categories are shared across every business. `CreateCategoryAsync` (`Features/Inventory/InventoryEndpoints.cs`) then checks name uniqueness with `IgnoreQueryFilters()` globally, so once any business creates "Brakes", every other business gets a 409 and can never create their own. It is undecided whether the shared taxonomy is deliberate or a cross-tenant defect. **Do not "fix" or build on this without asking** — the fix (scope the entity, add a filter, add a migration, scope the uniqueness check) is a breaking data change.

**The API cannot start without Postgres.** `Program.cs:145` calls `await db.Database.MigrateAsync()` outside any try/catch, so with the database down the app throws an unhandled `NpgsqlException` and exits before binding `:5000` — no graceful message. See "Starting the stack" above: bring Postgres up yourself, don't hand the crash back as a blocker.

**`/health` does not check the database.** It returns a static `{ status = "healthy" }` and never touches `AppDbContext`. Verified: with Postgres stopped, a running API still returns `200 healthy` while every data endpoint returns `500 internal_error`. It is a liveness probe only — do not treat it as a readiness check, and don't wire it to anything that decides whether the API can serve traffic.

**Unfiltered child entities.** `JobLaborLine`, `JobPartLine`, `JobAssignment`, `BusinessUserRole`, `RolePermission`, and `InventoryCategory` have DbSets but **no** `HasQueryFilter` line, so querying those sets directly crosses tenants. EF emits five model-validation warnings (`10622`) about exactly this on every startup. Reaching them through a filtered parent (`db.Jobs.Include(j => j.LaborLines)`) is safe; `db.JobLaborLines.Where(...)` is not.

The job line-item endpoints load lines from those unfiltered sets and rely on a separate `db.Jobs.FindAsync(id)` for the tenant check. **Verified safe** by `TenantIsolationTests` — `FindAsync` does honour the global query filter, so a cross-tenant read, delete, or append all return 404 and leave the data untouched. The isolation is real but *indirect*: it lives in that parent lookup, not in the line entity. If you refactor those handlers, keep the parent-job check, and keep those tests green.

**`RemovePartAsync` null-handling bug.** It does `db.Jobs.FindAsync([id], ct)!` then dereferences `job!.Status`. A missing or filtered-out job gives a `NullReferenceException` → `500 internal_error` instead of a 404. `RemoveLaborAsync` two methods below handles the same case correctly with `?? throw new NotFoundException(...)`.

**Known-vulnerable packages.** The build reports `NU1903` high-severity advisories for `Microsoft.OpenApi` 2.0.0-preview.11 and `Microsoft.Build.Tasks.Core` 17.7.2. Also `NU1603`: `Infrastructure` asks for EF Core `10.0.0-preview.3.25171.7`, which does not exist on the feed, so NuGet silently resolves `preview.4.25258.110` instead — the pinned versions are not the versions you get.

**A rate stored as a fraction needs two more decimal places than the percentage.** `TaxRate.Rate` was first declared `decimal(6,4)` on the reasoning that 8.875% needs four places. As a *fraction* that is `0.08875` — five. Postgres silently rounded it to `0.0888` and every US total came out 5p per £1000 wrong. It is `decimal(9,6)` now. Caught only because `TaxTests` asserted an exact figure; a test checking "tax is greater than zero" would have passed. Applies to any rate-like column.

**`static readonly T[]` + `.Contains()` blows up inside a LINQ query.** On .NET 10 the compiler binds an array's `.Contains()` to `MemoryExtensions.Contains(ReadOnlySpan<T>, T)`, which EF Core cannot evaluate as a query parameter — it throws `GenericArguments[1] ... violates the constraint of type parameter 'TRet'` from deep inside the expression funcletizer, and `ErrorHandlingMiddleware` masks it as a bare `500 internal_error`. Declare the set as `List<T>` instead: that binds `Enumerable.Contains` and translates to SQL `IN`. Cost an hour on `DashboardEndpoints`; caught only because `DashboardTests` existed. Related: `GroupBy(x => x.Status).Select(g => g.Key.ToString())` does not translate either — group to the enum, name it in memory.

**The generated client types every number as `number | string`.** The .NET 10 preview OpenAPI generator emits numeric DTO fields with a string validation `pattern`, so Orval widens them. Any page using `@/api/generated/*` has to coerce at the boundary — `dashboard/page.tsx` has a `num()` helper for exactly this. Verified 31 Aug 2026: `decimal RevenueThisMonth` generated as `DashboardDtoRevenueThisMonth = number | string`.

**The same OpenAPI generator also chokes on tuple-typed generics in doc comments** — `IEnumerable<(bool, bool, decimal)>` carrying a `<summary>` emits `IEnumerable` with no type argument and fails `CS0305`. Same fix: plain `//`.

**XML doc comments break the build on `Task`-returning helpers.** The .NET 10 preview OpenAPI XML-comment source generator emits `System.Void` for a `Task`-returning (void) method carrying a `<summary>`, failing with `CS0673: System.Void cannot be used from C#` in generated code you never wrote. Use a plain `//` comment on those; `Task<IResult>` and non-async methods are fine. Two helpers in `CalendarEndpoints`/`VehicleEndpoints` carry a note explaining why.

**Environment quirks that cost time once already.** `jq` is **not installed** — use `node` for JSON in scripts and hooks (that's why `.claude/hooks/docs-reminder.mjs` is a Node script). In the Bash tool, backslashes in single-quoted strings and heredocs get mangled, so build JSON test payloads with the Write tool rather than `echo`. When driving the app in the browser, `ref_N`-based clicks resolve to wrong coordinates on this project's modals — click by screenshot coordinate instead, and note `form_input` on `datetime-local` and `<textarea>` fields silently no-ops roughly half the time, so read back and retry.

**No CI, no formatter, no analyzer gate.** There's no `.github/workflows`, no `.editorconfig`, and no `dotnet format` step. Build and test discipline is manual — actually run the commands.

**Backend test coverage is thin.** Two files, 7 tests: `AuthTests.cs` (health, register, duplicate email, unverified login) and `TenantIsolationTests.cs` (cross-tenant read/delete/append on job labor lines). Calendar conflict detection, billing, inventory, stock movements, and messaging have no tests. Rule 6 applies to new endpoints; the rest is unfilled backlog.

`TenantIsolationTests` is the template for tenant-boundary tests: register two businesses through `/api/auth/register`, flip `EmailVerified` directly in the DB (login is blocked until verified, and the token otherwise only reaches `ConsoleEmailSender`), log both in, then assert across the boundary. Assert on the **stored rows** as well as the status code — a handler that returns 404 but still deleted the row would pass a status-only check.

**Response types: the list and detail endpoints now declare them; the rest don't yet.** Minimal APIs cannot infer a schema from `Results.Ok(new { ... })`, so anonymous returns produce `"200": { "description": "OK" }` and Orval emits `apiClient<void>`. That is what let four response-shape bugs reach the browser with TypeScript happy.

Fixed for the endpoints that caused them: paginated lists now return the named `PagedResult<T>` (`Features/Common/PagedResult.cs`) and jobs/customers/inventory list + detail carry `.Produces<T>()`. Verified — `GET /api/jobs` `$ref`s `PagedResultOfJobListItemDto`, and the client generates `apiClient<PagedResultOfJobListItemDto>` with `laborTotal`, `partsTotal` and `total` present.

The whole Catalogue slice followed on 31 Aug 2026 — all six `GET /api/catalogue/*` endpoints declare their type, so Orval generates `CatalogueMakeDto[]`, `CatalogueVariantDto[]`, `CatalogueVariantDetailDto` and so on rather than `void`. Verified by grepping the generated `api/generated/catalogue/catalogue.ts` after `npm run generate-api`.

**Still to do:** every write endpoint (POST/PUT/PATCH/DELETE) and the remaining slices — calendar, zones, users, billing, business — still return anonymous objects and still generate `void`. Add `.Produces<T>()` as you touch them; a named record beats an anonymous object every time.

**FIXED — the `sub` claim now arrives.** `Program.cs` sets `options.MapInboundClaims = false`. Without it the JwtBearer handler remapped `sub` to `ClaimTypes.NameIdentifier`, so `CurrentUserService.UserId` was always null: `/api/users/me` returned 401 for everyone including Admins, and `Job`/`Booking`/`StockMovement.CreatedByUserId` were written null on every row ever created (8/8 bookings, 8/8 jobs, 4/4 movements in the dev database — all pre-fix rows still are). The custom claims were never remapped, which is why tenancy and permissions worked and this went unnoticed for so long. `/api/users/me` was also moved out of the `users.manage` group so a non-admin can read their own profile. Guarded by `UserAccessTests`.

**CORRECTION — the invite flow is NOT a dead end.** This file previously claimed invited users could never log in. That was wrong, and the error was in the test, not the app: `VerifyEmailEndpoint` activates every `Pending` membership as part of email verification, and the invite email carries both the temporary password and the verification token. The original test set `EmailVerified` directly in the database, bypassing the endpoint that performs the activation, and so "proved" a defect that does not exist. `UserAccessTests.InvitedUser_CanLogIn_AfterVerifyingTheirEmail` now exercises the real path. **Lesson: a test that fakes a precondition can manufacture a bug report.**

**FIXED — validation errors now reach the user.** `ApiError` in `lib/fetcher.ts` reads `errors[]` and joins the field messages, falling back to `message` then to the status text. The middleware returns validation failures as `{ code, errors: [{ field, message }] }` with no top-level `message`, so reading only `message` turned every failed form in the product into "Request failed with status 400". `ApiError` also now exposes `fieldErrors` and `details`, so a form can highlight individual fields and a booking 409 can name the clashing booking.

**FIXED — `recentJobs` on customer detail.** `CustomerDetailDto` now carries `RecentJobs` (`IEnumerable<CustomerJobDto>`) and the query populates it. Previously the page expected the field and the DTO never returned it, so the card was permanently empty — unlike the other response-shape bugs this one needed a server change, not a rename.

**[docs/review-findings.md](docs/review-findings.md) is the current defect list** — read it before starting work on either project. Six of its findings were fixed on 31 Aug 2026 (picker hydration, job zone tenancy, error states, the stock race, the backfill migration, concurrency 409s) and sit in its **Fixed** section with root causes intact; the rest are open.

**A failed fetch is now distinguishable from an empty one.** Every page-level `useApi`/`useApiQuery` call site branches on `error` **before** its empty branch, rendering `<ErrorState>` from `src/components/data-state.tsx`. Put new error branches in that order — after the empty check they are dead code, which is how a failed `/api/zones` used to tell an admin "No zones configured".

**`npm run lint` is not configured** — it drops into an interactive ESLint setup prompt. There is no working lint gate; `npm run build` (which type-checks) is the real one.

**Some handlers return ad-hoc error bodies.** e.g. `CreateCategoryAsync` returns `Results.BadRequest(new { code, message })` directly instead of throwing. New code should throw the middleware's exception types.

---

## Working agreements

- Nullable reference types and implicit usings are on in every project. Don't introduce new nullability warnings.
- **The API build is not warning-clean:** `GenerateDocumentationFile=true` on `WrenchWorks.Api` with no XML doc comments produces ~324 `CS1591` warnings on every build. They're noise, and they bury real warnings. Don't try to silence them by writing XML comments across every DTO; if this gets fixed, the fix is `<NoWarn>$(NoWarn);CS1591</NoWarn>` in the csproj (or dropping `GenerateDocumentationFile`). Until then, when checking a build, filter them: `dotnet build 2>&1 | grep -v CS1591`.
- Prefer `record` types for DTOs, as the existing slices do.
- After backend changes: `dotnet build` and `dotnet test`. After web changes: `npm run build` (not `npm run lint` — see above). Report failures with the real output rather than glossing over them.
- If a change spans both projects, finish the loop: API → `npm run generate-api` → update the web code.

---

## Keeping this file and the companion docs current

**These six files are the project's memory.** `CLAUDE.md`, `docs/app-flow.md`, `docs/bookings-crud.md`, `docs/vehicle-catalogue.md`, `docs/review-findings.md` and `docs/tax.md` record things the source cannot tell you: behaviour verified by running the app, decisions and their reasons, traps that cost someone an hour, and questions still open. A stale entry here is worse than no entry — it produces confident, wrong work.

Updating them is part of finishing a task, not an optional extra. Do it in the same turn as the change.

### The rule that matters most: revise, never delete

**When a finding stops being true, move it to a resolved state — keep the finding, its root cause, and what changed.** Never quietly delete it.

The bugs section of `app-flow.md` is the worked example: when four response-shape bugs were fixed, they moved under a **Fixed** heading, each keeping its symptom, its root cause, and a note of the fix; the ones that remained moved under **Still open**. Nothing was lost, and the root-cause pattern — hand-written interfaces drifting from the real contract — stayed visible, which is the part that prevents a repeat.

Delete an entry only when it was *wrong*, and then say so plainly rather than erasing it (this file already carries several corrections written that way).

### Triggers

| When you | Update |
|---|---|
| Change an endpoint, DTO, route or permission | `app-flow.md` — screen table and/or the request-path section |
| Add, remove or restructure a page or route | `app-flow.md` — screen table |
| Fix a bug listed in a doc | Move it to **Fixed** with its root cause; do not delete |
| Find a new bug, trap or surprising behaviour | Add it, with how you verified it |
| Answer one of the open questions | Replace the question with the answer **and the evidence**, and note who decided |
| Touch bookings or the calendar | `bookings-crud.md` — the state table and the build order |
| Migrate a page to the generated client, or off `useApiQuery` | `CLAUDE.md` rules 3–4 "Current state" callouts, and `app-flow.md`'s screen table |
| Add a response type (`.Produces<T>` / `TypedResults`) | The Orval `void`-responses entry — that's the blocker being lifted |
| Learn a command, gotcha or environment fact the hard way | `CLAUDE.md` — Commands or Known gaps |
| Fix, or disprove, one of the review findings | `review-findings.md` — move it to **Fixed** with its root cause, or mark it *Disproved* and say why |
| Run another review pass | `review-findings.md` — merge into the existing sections; do not start a second file |

### Standard of evidence

State how you know. "Verified by running X", "confirmed in the browser", "from reading the source" and "assumed" are four different claims, and mixing them up is how this file becomes untrustworthy. If something is unverified, **say so** — `PUT /bookings/{id}/move` is documented as written-but-never-executed for exactly this reason. Date anything time-sensitive.

---

## Imported project memory

The files below are loaded in full with this one. Treat their contents as part of these instructions.

@docs/app-flow.md
@docs/bookings-crud.md
@docs/vehicle-catalogue.md
@docs/tax.md
@docs/review-findings.md
