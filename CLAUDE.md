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

> **Current state, 2 Sep 2026 — the types are migrated, the fetching is not.**
>
> **Every response shape now comes from `@/api/generated/models`.** There are zero hand-written API interfaces left in `src/app`: 19 page-local ones were replaced across 11 pages, and the shared modules (`calendar/_lib/booking.ts`, `jobs/[id]/_lib/job.ts`, `hooks/use-customer-vehicle.ts`) re-export the generated types under their existing names so their consumers were untouched. The models are pure `export interface` with `import type` only, so this is safe inside `"use client"` and adds nothing to the bundle — *verified*: build output byte-identical per route.
>
> This is what closes the bug class in [app-flow.md](docs/app-flow.md) — `£NaN` on the jobs list, blank part names, `0 customers` above a populated list — all four were a hand-written interface disagreeing with the real DTO. *Verified the binding is live*: renaming a field usage to one the contract lacks now fails `tsc` with "Property 'capacitySlots' does not exist on type 'ZoneDto'", where the old local interface compiled happily and rendered `undefined`.
>
> **Unchanged: the *fetching*.** Pages remain client components calling `useApi`/`useApiQuery` through the `/api/[...path]` proxy. That is a deliberate trade rather than debt — the proxy is what makes the httpOnly cookie work, and the generated client is server-only so a client component cannot call it. See the Current state note under rule 4 before treating it as something to undo. The type safety above did not wait on it.

`API_BASE_URL` and `SESSION_SECRET` are server-only env vars. Never expose the JWT or the backend URL to the browser — no `NEXT_PUBLIC_` variable should ever hold either.

### 4. Server Components by default

Pages and layouts stay server components. Add `"use client"` only where you genuinely need interactivity, hooks, SWR, Zustand, or browser APIs — and push it to the smallest leaf component rather than marking a whole page. Fetch on the server where you can.

> **Current state, 2 Sep 2026 — and read this before "migrating" anything.** 18 of 19
> `page.tsx` files are `"use client"`; `/dashboard` is the one server component, fetching
> through `getApiDashboard()`, and it is the working example of the target shape.
>
> **The `/api/[...path]` proxy is NOT the thing to remove.** It is what turns the httpOnly
> `ww_token` cookie into an `Authorization` header. Delete it and the JWT has to live
> somewhere the browser can read, or `API_BASE_URL` has to be exposed. The extra hop is the
> price of that, and it is the right trade.
>
> **A client component physically cannot call a generated function.** `lib/api-client.ts`
> imports `cookies` from `next/headers`, so it is server-only by construction. Migrating a
> page therefore means *moving the fetch to the server* and restructuring it into a server
> parent plus a client child — not swapping `useApi` for `getApiJobs`. That is a real change
> per page, which is why it has not happened by accident.
>
> **Not every page should move.** The calendar needs pointer-driven drag-to-move, job detail
> runs three modals, inventory filters live. Those are legitimately client components, and
> converting them wholesale would lose SWR's caching, focus revalidation and `mutate()` for
> nothing. The pattern that pays is narrower: **a server component fetches the initial
> payload and hands it to a client child that keeps SWR for interactivity.** Roughly the
> list pages — jobs, customers, inventory — are where it is worth it.
>
> **What it buys is the first paint, not type safety.** Response shapes are already
> contract-bound (rule 3). What stays hand-written is the *request* side: `useApi` builds
> URLs and query strings by hand, so a renamed route breaks at runtime rather than at
> compile time. Real, but smaller than it sounds — and below the open correctness and
> security items in `review-findings.md`.

### 5. Vertical slices on the API

Everything a feature needs lives in one folder under `src/WrenchWorks.Api/Features/<Feature>/`.
Restructured 1 Sep 2026 into **three layers plus its data**. All 17 existing slices follow
it and **every new one must too** — if you are adding a feature, jump to *Adding a new
slice* at the end of this rule and copy `Features/Zones/`, which is the reference shape
because it is small enough to read in one go:

```
Features/Zones/
  Api/                     the HTTP layer, and the only folder that knows about HTTP
    ZoneEndpoints.cs         routes only, no methods
    IZoneEndpointHandler.cs
    ZoneEndpointHandler.cs   the ONLY place an entity becomes a DTO
  Dtos/                    one file per record    (CreateZoneRequest, UpdateZoneRequest, ZoneDto)
  Validators/              one file per validator (CreateZoneValidator, UpdateZoneValidator)
  IZoneService.cs
  ZoneService.cs           business rules -- returns ENTITIES, throws on failure
  IZoneRepository.cs
  ZoneRepository.cs        data access -- the only thing holding AppDbContext
```

The rule in one line: **routes -> handler -> service -> repository -> EF**, and a DTO
exists only in the handler.

```csharp
group.MapGet("/",
    (IZoneEndpointHandler handler, CancellationToken ct) => handler.ListAsync(ct))
    .RequireAuthorization("calendar.view");
```

- **The lambda must invoke, not reference.** `(IZoneEndpointHandler h) => h.ListAsync` is a
  method group: minimal APIs would try to serialise the delegate instead of calling it.
  Write `=> h.ListAsync(ct)`.
- **`Api/<Feature>Endpoints.cs` contains `Map` and nothing else.** No private handlers, no
  helpers. If you are about to add a method there, it belongs in the handler.
- **Folders do not change namespaces.** `Api/`, `Dtos/` and `Validators/` files all stay in
  `WrenchWorks.Api.Features.<Feature>`, so moving a file between them is a pure move with
  no `using` churn.
- **Only the handler builds DTOs.** The service and repository speak in entities, so a
  change to the wire shape touches exactly one file per slice.
- **Only the repository touches `AppDbContext`.** Services take `I<Feature>Repository`.
  Nothing outside a repository has a `DbSet`.
- Failures are **thrown**, never returned: the `ErrorHandlingMiddleware` exception types
  map to status codes, so services never mention one. The two auth endpoints are the
  deliberate exception -- a failed login is a valid answer rather than an error, so
  `ILoginService` returns a `LoginOutcome` and the handler maps it to 401/403/200.
- Register each new slice **twice**: `<Feature>Endpoints.Map(app);` in the "Map Feature
  Endpoints" block of `Program.cs`, and the **three** `AddScoped` lines in
  `Features/Common/FeatureServices.cs`. Nothing is discovered by convention, so a missing
  registration throws on the first request rather than degrading silently.
- Handlers return a **concrete `TypedResults` type** -- `Task<Ok<JobDetailDto>>`,
  `Task<Created<ZoneDto>>`, `Task<NoContent>`. Never `Task<IResult>`: it erases the
  response type, which is what produced the `apiClient<void>` problem below. Where an
  endpoint genuinely has more than one success-or-auth status, use a union
  (`Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>`).
- Validators live in `Validators/`, are still picked up by
  `AddValidatorsFromAssemblyContaining<Program>()`, and are called by the **service**.
- `Features/Common/` is the exception to all of the above: `Archiving`, `PagedResult` and
  `TaxCalculator` are shared helpers with no slice of their own. `ArchiveResultDto` comes
  back from `Archiving.Archive` and is the one DTO a service returns, deliberately.

#### When a repository may not return entities

"Return entities" breaks down where the query is an aggregate or a narrow projection.
Loading whole graphs just to count or sum them would be a real regression, so in those
cases **the repository returns a read model** -- a domain-shaped record that is still not
the API DTO, and the handler maps it the same as any entity. Examples in the code:

| Read model | Why |
|---|---|
| `CustomerWithVehicleCount` | the entity plus a SQL `COUNT`, so the list never touches `Vehicles` |
| `TodaysBookingRow`, `ActiveJobRow`, `StatusCountRow`, `LowStockRow` | the whole dashboard is aggregates |
| `VehicleHistoryRow`, `CustomerRecentJob` | line-item totals summed in the database |
| `VariantYearRange` | two ints per variant instead of the variant |

Read models keep enums as **enums**; turning them into display strings is the handler's job.

**Three traps this restructure exposed**, all worth knowing before touching the layout:

- **A `Task`-returning method cannot carry an XML doc comment.** The .NET 10 OpenAPI
  XML-comment source generator emits `System.Void` for it and the build fails with
  `CS0673` in generated code you never wrote. `Task<T>` is fine. Use plain `//` on
  `DeleteAsync`-style methods.
- **A required parameter cannot follow an optional one**, so the injected handler goes
  **first** in a lambda, before `int page = 1`. Minimal APIs bind by type and name rather
  than position, so first is always safe.
- **Keep the defaults on optional query parameters.** A required `bool includeArchived`
  that fails to bind throws `BadHttpRequestException`, which `ErrorHandlingMiddleware`
  catches and reports as a **500** rather than a 400. Dropping `= false` from
  `GET /api/tax/rates` broke two tests exactly this way.


#### Adding a new slice — follow this exactly

**Every new feature uses this layout. There are no exceptions in the codebase and new work
should not create one.** The fastest correct route is to copy `Features/Zones/` and rename
— it is the reference slice for this reason. Nine files, in this order:

| # | File | Holds |
|---|---|---|
| 1 | `Dtos/*.cs` | one record per file: requests and responses |
| 2 | `Validators/*.cs` | one `AbstractValidator<T>` per file |
| 3 | `I<Feature>Repository.cs` | data access signatures, returning entities |
| 4 | `<Feature>Repository.cs` | the only class with `AppDbContext` |
| 5 | `I<Feature>Service.cs` | business-rule signatures, returning entities |
| 6 | `<Feature>Service.cs` | validation, rules, throws; takes the repository |
| 7 | `Api/I<Feature>EndpointHandler.cs` | `TypedResults` signatures |
| 8 | `Api/<Feature>EndpointHandler.cs` | entity → DTO; takes the service |
| 9 | `Api/<Feature>Endpoints.cs` | `Map` only |

Then **three registrations** and **one test file**:

```csharp
// Program.cs, in the "Map Feature Endpoints" block
WidgetEndpoints.Map(app);

// Features/Common/FeatureServices.cs, one line each, handler -> service -> repository
services.AddScoped<IWidgetEndpointHandler, WidgetEndpointHandler>();
services.AddScoped<IWidgetService, WidgetService>();
services.AddScoped<IWidgetRepository, WidgetRepository>();
```

Nothing is discovered by convention, so a missed registration throws on the slice's first
request rather than at startup — which is why rule 6 (an integration test per endpoint) is
what actually catches it.

If the entity is tenant-scoped, it must also inherit `BusinessScopedEntity`, get an EF
configuration, **and** be added to the explicit `HasQueryFilter` list in
`AppDbContext.OnModelCreating` — see "Things to know before touching the code". A new
entity has no tenant isolation until that line exists.

**Self-check before calling a slice done.** Both of these must print nothing:

```bash
grep -rn "private static" src/WrenchWorks.Api/Features --include=*Endpoints.cs --include=*Endpoint.cs
```

```bash
grep -rl "AppDbContext" src/WrenchWorks.Api/Features | grep -v "Repository.cs"
```

The first proves the endpoints class is routes-only; the second proves nothing outside a
repository holds a `DbSet`. Both were clean as of 1 Sep 2026 across all 17 slices.

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

These exception types are declared in `Middleware/ErrorHandlingMiddleware.cs`. Reuse them rather than returning ad-hoc `Results.BadRequest(new { ... })`. As of 1 Sep 2026 **no handler does** — the sixteen that did were converted to throws during the TypedResults migration, which is also what let their return types collapse to a single `Ok<T>`.

**A `ValidationException` thrown with a bare message has no `Errors`.** FluentValidation's `ValidationException(string)` leaves the collection empty, and `ApiError` on the web reads `errors[]` first and `message` second — so such a throw would have reached the user as "Request failed with status 400", the same bug fixed on 31 Aug one layer up. The middleware now falls back to `message` when `Errors` is empty. *Verified 1 Sep 2026*: a missing field returns `{code:"validation_error",errors:[…],message:null}` and a duplicate register returns `{code:"conflict",message:"Email already registered"}`.

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

**Client-side fetching is a deliberate trade, not debt to pay down on sight.** Every page
under `app/(dashboard)` except `/dashboard` fetches on the client through the proxy hooks.
Their *response types* now come from `@/api/generated/models`, so the hand-written-interface
bug class is closed; what remains is that data arrives after hydration, which costs a
loading spinner on first paint. See the "Current state" note under rule 4 for why this is
not a find-and-replace, and which pages are actually worth converting.

**`useApi` / `useApiQuery` are the supported way to fetch from a client component.** They
are not transitional and not deprecated — a client component has no other option, because
the generated client is server-only. Build new *client* features on them without hesitation.
Reach for a server component plus `@/api/generated/*` when a page is mostly read-only and
its first paint matters. The `/api/[...path]` proxy underpins both and stays.

**OPEN QUESTION — `InventoryCategory` tenancy.** It extends `BaseEntity`, not `BusinessScopedEntity`, and has no `HasQueryFilter` line, so categories are shared across every business. `CreateCategoryAsync` (`Features/Inventory/InventoryEndpoints.cs`) then checks name uniqueness with `IgnoreQueryFilters()` globally, so once any business creates "Brakes", every other business gets a 409 and can never create their own. It is undecided whether the shared taxonomy is deliberate or a cross-tenant defect. **Do not "fix" or build on this without asking** — the fix (scope the entity, add a filter, add a migration, scope the uniqueness check) is a breaking data change.

**The API cannot start without Postgres.** `Program.cs:145` calls `await db.Database.MigrateAsync()` outside any try/catch, so with the database down the app throws an unhandled `NpgsqlException` and exits before binding `:5000` — no graceful message. See "Starting the stack" above: bring Postgres up yourself, don't hand the crash back as a blocker.

**`/health` does not check the database.** It returns a static `{ status = "healthy" }` and never touches `AppDbContext`. Verified: with Postgres stopped, a running API still returns `200 healthy` while every data endpoint returns `500 internal_error`. It is a liveness probe only — do not treat it as a readiness check, and don't wire it to anything that decides whether the API can serve traffic.

**Unfiltered child entities.** `JobLaborLine`, `JobPartLine`, `JobAssignment`, `BusinessUserRole`, `RolePermission`, and `InventoryCategory` have DbSets but **no** `HasQueryFilter` line, so querying those sets directly crosses tenants. EF emits five model-validation warnings (`10622`) about exactly this on every startup. Reaching them through a filtered parent (`db.Jobs.Include(j => j.LaborLines)`) is safe; `db.JobLaborLines.Where(...)` is not.

The job line-item endpoints load lines from those unfiltered sets and rely on a separate `db.Jobs.FindAsync(id)` for the tenant check. **Verified safe** by `TenantIsolationTests` — `FindAsync` does honour the global query filter, so a cross-tenant read, delete, or append all return 404 and leave the data untouched. The isolation is real but *indirect*: it lives in that parent lookup, not in the line entity. If you refactor those handlers, keep the parent-job check, and keep those tests green.

**FIXED 1 Sep 2026 — `RemovePartAsync` null-handling bug.** It did `db.Jobs.FindAsync([id], ct)!` then dereferenced `job!.Status`, so a missing or filtered-out job gave a `NullReferenceException` → `500 internal_error` instead of a 404, while `RemoveLaborAsync` handled the same case correctly. The repository split forced the two to be written side by side and the asymmetry became obvious: `JobService.RemovePartAsync` now resolves the parent job **first** (which is where the tenant check lives, since `JobPartLines` has no query filter) and then the line, exactly as `RemoveLaborAsync` does. Also closes the existence-oracle half of finding 19.

**Known-vulnerable packages.** The build reports `NU1903` high-severity advisories for `Microsoft.OpenApi` 2.0.0-preview.11 and `Microsoft.Build.Tasks.Core` 17.7.2. Also `NU1603`: `Infrastructure` asks for EF Core `10.0.0-preview.3.25171.7`, which does not exist on the feed, so NuGet silently resolves `preview.4.25258.110` instead — the pinned versions are not the versions you get.

**A rate stored as a fraction needs two more decimal places than the percentage.** `TaxRate.Rate` was first declared `decimal(6,4)` on the reasoning that 8.875% needs four places. As a *fraction* that is `0.08875` — five. Postgres silently rounded it to `0.0888` and every US total came out 5p per £1000 wrong. It is `decimal(9,6)` now. Caught only because `TaxTests` asserted an exact figure; a test checking "tax is greater than zero" would have passed. Applies to any rate-like column.

**`static readonly T[]` + `.Contains()` blows up inside a LINQ query.** On .NET 10 the compiler binds an array's `.Contains()` to `MemoryExtensions.Contains(ReadOnlySpan<T>, T)`, which EF Core cannot evaluate as a query parameter — it throws `GenericArguments[1] ... violates the constraint of type parameter 'TRet'` from deep inside the expression funcletizer, and `ErrorHandlingMiddleware` masks it as a bare `500 internal_error`. Declare the set as `List<T>` instead: that binds `Enumerable.Contains` and translates to SQL `IN`. Cost an hour on `DashboardEndpoints`; caught only because `DashboardTests` existed. Related: `GroupBy(x => x.Status).Select(g => g.Key.ToString())` does not translate either — group to the enum, name it in memory.

**RESOLVED 2 Sep 2026 — the generated client no longer widens numbers.** The .NET 10 preview OpenAPI generator emits every int and decimal as `type: ["integer","string"]` (87 of 88 numeric schemas), advertising that it would *accept* a string on input. Orval faithfully turned that into `number | string`, producing 93 unusable alias types — which is the real reason nobody adopted the typed client: every arithmetic expression needed a coercion helper, and `dashboard/page.tsx` grew a `num()` for exactly that.

**Fixed at the spec, not the call sites.** `wrench-works-web/orval.transformer.cjs` collapses those unions before Orval sees them, wired up via `input.override.transformer`. Responses are always real JSON numbers, so the string half was an artifact rather than the contract — *verified on the wire*: `POST /api/zones` returns `"capacity":3` with `typeof === "number"`, and `GET /api/tax/rates` returns `"rate":0.2`.

Nullability is deliberately preserved: a nullable numeric is `["null","integer","string"]`, and an early version of the transformer dropped the `null` along with the string, which would have made `VehicleDto.year` look required. It now yields `VehicleDtoYear = number | null`.

*Result*: `grandTotal: number`, `capacity: number`, zero `number | string` aliases (was 93), and 85 fewer generated files. Delete the transformer once the preview generator stops emitting the union.

**The same OpenAPI generator also chokes on tuple-typed generics in doc comments** — `IEnumerable<(bool, bool, decimal)>` carrying a `<summary>` emits `IEnumerable` with no type argument and fails `CS0305`. Same fix: plain `//`.

**XML doc comments break the build on `Task`-returning helpers.** The .NET 10 preview OpenAPI XML-comment source generator emits `System.Void` for a `Task`-returning (void) method carrying a `<summary>`, failing with `CS0673: System.Void cannot be used from C#` in generated code you never wrote. Use a plain `//` comment on those; `Task<IResult>` and non-async methods are fine. Two helpers in `CalendarEndpoints`/`VehicleEndpoints` carry a note explaining why.

**Environment quirks that cost time once already.** `jq` is **not installed** — use `node` for JSON in scripts and hooks (that's why `.claude/hooks/docs-reminder.mjs` is a Node script). In the Bash tool, backslashes in single-quoted strings and heredocs get mangled, so build JSON test payloads with the Write tool rather than `echo`. When driving the app in the browser, `ref_N`-based clicks resolve to wrong coordinates on this project's modals — click by screenshot coordinate instead, and note `form_input` on `datetime-local` and `<textarea>` fields silently no-ops roughly half the time, so read back and retry.

**A side effect inside a React state updater fires twice.** `useDragToMove` called
`onMove(...)` — which issues `PUT /bookings/{id}/move` — from inside a
`setDrag(current => …)` updater. React treats updaters as pure and **double-invokes them
under StrictMode** (`next.config.ts` sets `reactStrictMode: true`), so every drop sent two
identical requests. The first won; the second lost the row-version race and surfaced as
*"Someone else changed this while you were working on it"* on a move that had in fact
succeeded. Fixed 2 Sep 2026 by reading `drag?.active` from the effect closure, calling
`setDrag(null)` plainly, and firing `onMove` outside the updater. *Verified in the browser*:
one `PUT …/move → 200` per drag across two consecutive drags, toast reads "Booking moved",
and the stored times moved 15:00→17:00Z and back.

**Read entities INSIDE `WithZoneLockAsync`, never before it.** `MoveBookingAsync` and
`UpdateBookingAsync` originally loaded the booking before taking the zone lock. An entity
loaded before the lock carries a concurrency token the race winner may already have bumped,
so the loser's `UPDATE … WHERE xmin = @original` matches no row and EF raises
`DbUpdateConcurrencyException` → a 409 the user cannot act on. Both now read the booking,
zone, customer and vehicle inside the lock. This is what made the double-request bug above
visible as an error rather than a harmless duplicate — two defects, one symptom.

**A `String.replace` codemod with a shorter second anchor edits the wrong place.** The
script that added the Consumable checkbox used the create modal's markup as anchor 1 and a
*substring of it* as anchor 2 for the edit modal. `String.prototype.replace` takes the first
match, so anchor 2 matched the already-modified create modal: `CreateItemModal` ended up
with **two** checkboxes (one mis-nested inside the price grid) and `EditItemModal` with
**none** — it set and submitted `isConsumable` while rendering no control, so the flag could
never be changed after creation. Fixed and *verified in the browser*: create shows one
checkbox, edit shows one reflecting the stored value. Anchor codemods on the unique
surrounding block, and check the count afterwards.

**Restoring a file from a backup can silently skip the rebuild.** `mv file.bak file.cs` gives the source the *backup*'s mtime. If that is older than the last build output, MSBuild considers the assembly up to date and `dotnet build` reports success while `dotnet test` runs the **previous** binary. This cost real time while verifying the booking lock: the restored fix appeared not to work, and the failing runs were testing the removed version. `touch` the file after any restore, or edit it rather than moving it. `--no-build` makes it worse by hiding the skipped compile entirely.

**No CI, no formatter, no analyzer gate.** There's no `.github/workflows`, no `.editorconfig`, and no `dotnet format` step. Build and test discipline is manual — actually run the commands.

**Backend test coverage is thin.** Two files, 7 tests: `AuthTests.cs` (health, register, duplicate email, unverified login) and `TenantIsolationTests.cs` (cross-tenant read/delete/append on job labor lines). Calendar conflict detection, billing, inventory, stock movements, and messaging have no tests. Rule 6 applies to new endpoints; the rest is unfilled backlog.

`TenantIsolationTests` is the template for tenant-boundary tests: register two businesses through `/api/auth/register`, flip `EmailVerified` directly in the DB (login is blocked until verified, and the token otherwise only reaches `ConsoleEmailSender`), log both in, then assert across the boundary. Assert on the **stored rows** as well as the status code — a handler that returns 404 but still deleted the row would pass a status-only check.

**RESOLVED 1 Sep 2026 — every endpoint now declares its response type.** Kept in full because the root cause is the most expensive one this project has had.

The cause: minimal APIs cannot infer a schema from `Results.Ok(new { ... })`, so a handler typed `Task<IResult>` returning an anonymous object produced `"200": { "description": "OK" }` and Orval emitted `apiClient<void>`. That is what let four response-shape bugs reach the browser with TypeScript perfectly happy.

Fixed for the endpoints that caused them: paginated lists now return the named `PagedResult<T>` (`Features/Common/PagedResult.cs`) and jobs/customers/inventory list + detail carry `.Produces<T>()`. Verified — `GET /api/jobs` `$ref`s `PagedResultOfJobListItemDto`, and the client generates `apiClient<PagedResultOfJobListItemDto>` with `laborTotal`, `partsTotal` and `total` present.

The whole Catalogue slice followed on 31 Aug 2026 — all six `GET /api/catalogue/*` endpoints declare their type, so Orval generates `CatalogueMakeDto[]`, `CatalogueVariantDto[]`, `CatalogueVariantDetailDto` and so on rather than `void`. Verified by grepping the generated `api/generated/catalogue/catalogue.ts` after `npm run generate-api`.

**The fix was structural, not endpoint-by-endpoint.** All 77 `Task<IResult>` handlers became concrete `TypedResults` types, and the ~20 anonymous return objects became named records. `.Produces<T>()` was then **deleted everywhere** — 28 calls — because `TypedResults` derives the schema *and* the status code from the return type, which a hand-written `.Produces` cannot be checked against.

*Verified 1 Sep 2026*: the OpenAPI doc went from 26 typed 2xx responses to **78 of 78**, and `apiClient<void>` in the generated client fell from 78 to **9** — exactly the nine `DELETE`s that really do return 204. `npx tsc --noEmit` and `npm run build` both clean; all 63 API tests pass.

Two things worth keeping:

- **It caught a lie immediately.** `.Produces<List<CatalogueVariantDto>>()` on `GET /catalogue/variants` was declaring a type the handler never returned (it returned `IEnumerable<>`). Under `.Produces` that mismatch is invisible; under `TypedResults` it is `CS0029` and the build stops. That is the whole argument for the change in one example.
- **Enums still serialise as numbers.** There is no `JsonStringEnumConverter` configured, which is why handlers call `.ToString()` on every status. `JobCreatedDto.Status` was deliberately left as the `JobStatus` enum to preserve the existing wire format — it is the one endpoint that returns a numeric status, and it was already doing so.

**FIXED — the `sub` claim now arrives.** `Program.cs` sets `options.MapInboundClaims = false`. Without it the JwtBearer handler remapped `sub` to `ClaimTypes.NameIdentifier`, so `CurrentUserService.UserId` was always null: `/api/users/me` returned 401 for everyone including Admins, and `Job`/`Booking`/`StockMovement.CreatedByUserId` were written null on every row ever created (8/8 bookings, 8/8 jobs, 4/4 movements in the dev database — all pre-fix rows still are). The custom claims were never remapped, which is why tenancy and permissions worked and this went unnoticed for so long. `/api/users/me` was also moved out of the `users.manage` group so a non-admin can read their own profile. Guarded by `UserAccessTests`.

**CORRECTION — the invite flow is NOT a dead end.** This file previously claimed invited users could never log in. That was wrong, and the error was in the test, not the app: `VerifyEmailEndpoint` activates every `Pending` membership as part of email verification, and the invite email carries both the temporary password and the verification token. The original test set `EmailVerified` directly in the database, bypassing the endpoint that performs the activation, and so "proved" a defect that does not exist. `UserAccessTests.InvitedUser_CanLogIn_AfterVerifyingTheirEmail` now exercises the real path. **Lesson: a test that fakes a precondition can manufacture a bug report.**

**FIXED — validation errors now reach the user.** `ApiError` in `lib/fetcher.ts` reads `errors[]` and joins the field messages, falling back to `message` then to the status text. The middleware returns validation failures as `{ code, errors: [{ field, message }] }` with no top-level `message`, so reading only `message` turned every failed form in the product into "Request failed with status 400". `ApiError` also now exposes `fieldErrors` and `details`, so a form can highlight individual fields and a booking 409 can name the clashing booking.

**FIXED — `recentJobs` on customer detail.** `CustomerDetailDto` now carries `RecentJobs` (`IEnumerable<CustomerJobDto>`) and the query populates it. Previously the page expected the field and the DTO never returned it, so the card was permanently empty — unlike the other response-shape bugs this one needed a server change, not a rename.

**[docs/review-findings.md](docs/review-findings.md) is the current defect list** — read it before starting work on either project. Six of its findings were fixed on 31 Aug 2026 (picker hydration, job zone tenancy, error states, the stock race, the backfill migration, concurrency 409s) and sit in its **Fixed** section with root causes intact; the rest are open.

**A failed fetch is now distinguishable from an empty one.** Every page-level `useApi`/`useApiQuery` call site branches on `error` **before** its empty branch, rendering `<ErrorState>` from `src/components/data-state.tsx`. Put new error branches in that order — after the empty check they are dead code, which is how a failed `/api/zones` used to tell an admin "No zones configured".

**`npm run lint` is not configured** — it drops into an interactive ESLint setup prompt. There is no working lint gate; `npm run build` (which type-checks) is the real one.

**FIXED 1 Sep 2026 — no handler returns an ad-hoc error body.** Sixteen did, all shaped `Results.BadRequest(new { code = "validation_error", message = ... })` (eight in `JobEndpoints`, four in `VerifyEmailEndpoint`, two in `InventoryEndpoints`, one each in `CalendarEndpoints` and `MessagingEndpoints`), plus `Results.Conflict` in `RegisterEndpoint` and bare `Results.NotFound()` in `BillingEndpoints`/`UserEndpoints`. All now throw the middleware's exception types. The status codes are unchanged; the two `NotFound()` calls gained a message where they previously returned an empty body.

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
