# How Wrench Works actually works

Written from a live walkthrough of the running app (logged in as an Admin of the `GRAutomotive` business), cross-checked against the source. Companion to `CLAUDE.md`, which holds the rules; this file holds the mechanics.

---

## What it is

Multi-tenant SaaS for vehicle workshops. One business = one tenant. A tenant has staff (users with roles), workshop bays (zones), customers, those customers' vehicles, an inventory of parts, a calendar of bookings, and jobs that tie it all together. Billing is per-business via Stripe, and the plan gates both limits (user/zone counts) and features (inventory, messaging).

---

## The request path

Every screen in the dashboard is a client component. Nothing in the dashboard fetches on the server.

```
Browser (client component)
   │  useApi("/api/jobs")            ← SWR, hooks/use-api.ts
   ▼
Next.js route handler  /api/[...path]/route.ts
   │  proxy() reads httpOnly ww_token cookie
   │  attaches Authorization: Bearer <jwt>
   ▼
.NET API  http://localhost:5000/api/jobs
   │  JWT validated → CurrentUserService → ITenantProvider
   ▼
JobEndpoints.ListAsync            ← thin: calls the service, wraps in TypedResults
   ▼
IJobService → JobService          ← the actual work; returns a DTO, throws on failure
   │  EF global query filter injects BusinessId
   ▼
PostgreSQL
```

Key points:

- **Endpoints hold no logic.** Since 1 Sep 2026 every slice has an `I<Feature>Service`
  registered in `Features/Common/FeatureServices.cs`; the handler injects it, calls one
  method and wraps the result. DTOs and validators live in `Dtos/` and `Validators/`
  beside it. See rule 5 in CLAUDE.md for the layout and the two traps it exposed.
  *Verified*: the OpenAPI document is byte-identical before and after the move — same 78
  endpoints, same statuses, same schemas — so this changed no contract.

- **The browser never sees the JWT.** It lives in the `ww_token` httpOnly cookie. The proxy is what turns a cookie into a bearer token, which is why the frontend can't call `:5000` directly.
- **Paths are 1:1.** `proxy()` forwards `url.pathname` unchanged, so `/api/jobs` on the web maps to `/api/jobs` on the API. The only reason the catch-all exists is the cookie→bearer hop.
- **More specific route handlers win.** `src/app/api/auth/*` (login, logout, me, refresh, register, verify-email) shadow the catch-all because they need to *set* cookies, not just forward.
- **A second, currently unused path exists.** `src/lib/api-client.ts` is the Orval mutator — same cookie→bearer trick, but for generated client functions called from server components. 109 generated files, zero imports. See `CLAUDE.md` for the migration intent.

### Failure modes worth recognising

| Symptom | Meaning |
|---|---|
| `502 {"code":"proxy_error"}` | Web is up, .NET API is unreachable |
| `401` through the proxy | API is up, token missing/expired — the deny-by-default `FallbackPolicy` |
| `500 {"code":"internal_error"}` on every data call, `/health` still 200 | Postgres is down. `/health` doesn't check the DB |
| `409 {"code":"concurrency_conflict"}` | Two writers raced on the same row. Reload and retry — added 31 Aug 2026, previously a bare 500 |

Any of these now render as an **error state with a retry button** rather than as an empty list. Before 31 Aug 2026 no page read SWR's `error`, so a failure and a genuinely empty result were indistinguishable on screen — a dead `/api/zones` told an admin "No zones configured".

---

## Auth and session lifecycle

```
POST /api/auth/register     → creates Business + User + BusinessUser + Admin role
                              + BusinessSubscription (Trial, 14 days, all features)
                              + seeds 5 system roles and 19 permissions
                              → emails a verification token (ConsoleEmailSender in dev)
POST /api/auth/verify-email → flips User.EmailVerified
POST /api/auth/login        → 403 while unverified; otherwise returns { token, user }
POST /api/auth/refresh      → new JWT (requires an authenticated call)
```

On successful login the **route handler**, not the API, sets two cookies (`lib/session.ts`):

| Cookie | httpOnly | Holds | Read by |
|---|---|---|---|
| `ww_token` | **yes** | the JWT | `proxy()` / `apiClient` server-side |
| `ww_user` | no | id, name, email, businessId, businessName, **currency**, permissions, features | `use-auth` on the client, `getCurrency()` on the server |

Both expire after 24h, matching the JWT.

`use-auth` hydrates a Zustand store from `ww_user`. That store is the *only* source for UI gating — the client never decodes the JWT. So **the client's view of permissions is a cookie the user could edit**; it controls what's rendered, never what's allowed. Enforcement is entirely server-side via `RequireAuthorization("jobs.edit")`.

---

## Authorization

Two independent axes, easy to confuse:

**Permissions** — what your *role* lets you do. `<resource>.<action>`, 19 of them, seeded per business into 5 system roles (Admin, Advisor, Technician, Inventory, ReadOnly). Server-side: `.RequireAuthorization("jobs.edit")` → `PermissionAuthorizationHandler`. Client-side: `usePermission("customers.manage")` to show/hide controls.

**Features** — what your *plan* includes (`inventory`, `messaging`). Stored on `BusinessSubscription`, baked into the JWT at login, surfaced as `useFeature()` / `<FeatureGate>`.

Server-side, features are enforced **not** by the permission system but by an `AddEndpointFilter` on the route group, which short-circuits with `403 { code: "feature_disabled" }`. Only two groups have one — `Inventory` (checks `inventory`) and `Messaging` (checks `messaging`) — and they are the only two features that exist. A new plan-gated feature needs its own filter on its group; nothing enforces this by convention.

A Technician on an Enterprise plan still can't edit billing; an Admin on a plan without messaging still can't message. Both checks are needed.

---

## Multi-tenancy

`BusinessScopedEntity` + one explicit `HasQueryFilter` line per entity in `AppDbContext.OnModelCreating`. The filter reads `_currentBusinessId == null || e.BusinessId == _currentBusinessId`, where `_currentBusinessId` comes from the JWT's `business_id` claim via `ITenantProvider`.

The `== null` branch is deliberate — it's what lets anonymous auth endpoints query `Users` before a tenant exists. It also means **any code path without tenant context sees every tenant's rows**.

Filters are registered one entity at a time, so a new `BusinessScopedEntity` has *no* isolation until you add its line. Six entities have DbSets but no filter (`JobLaborLine`, `JobPartLine`, `JobAssignment`, `BusinessUserRole`, `RolePermission`, `InventoryCategory`) — EF warns about five of them (`10622`) on every startup. Those are safe only when reached via a filtered parent. `TenantIsolationTests` verifies the job line-item endpoints do exactly that.

---

## Domain model

```
Business ─┬─ BusinessUser ──── User            (membership; a User can join several)
          │       └─ BusinessUserRole ── Role ── RolePermission ── Permission
          ├─ BusinessSubscription                (plan, limits, feature flags)
          ├─ Zone                                (workshop bay, has capacity)
          ├─ Customer ── Vehicle
          ├─ Booking            → Zone, Job?     (calendar entry)
          ├─ Job ──┬─ JobLaborLine               (description, hours, rate)
          │        ├─ JobPartLine → InventoryItem (decrements stock)
          │        └─ JobAssignment → BusinessUser
          ├─ InventoryItem ─── StockMovement     (audit trail of stock deltas)
          ├─ OutboundMessage, MessageTemplate
          └─ AuditLog

InventoryCategory ── global, shared by every business (236 seeded)
```

---

## Coverage — how much of this is actually verified

This document is not yet a complete description of the system, and it should not be read as one. Depth varies by area. Keep this table honest as coverage grows; it is what stops a future session trusting a section that was only skimmed.

| Area | Depth | What that means |
|---|---|---|
| Auth, session, cookies | **Verified** | Read end to end, exercised by logging in and by `AuthTests` |
| Multi-tenancy / query filters | **Verified** | Read end to end, proven by `TenantIsolationTests` (3 tests) |
| Error middleware | **Verified** | All exception types read; validation path confirmed in-browser |
| Jobs (list, detail, labor, parts) | **Verified** | Source read, exercised in browser, 4 bugs found and fixed |
| Bookings / calendar | **Verified** | Source read; created bookings, hit the 409 conflict path, confirmed no edit/drag |
| Customers (list, detail) | **Verified** | Source read and exercised; `recentJobs` gap found |
| Vehicles (add, edit, detail, search) | **Verified** | Both modals and endpoint read; validation path exercised in-browser |
| Inventory — items, categories, adjust | **Verified** | All 7 endpoints and both modals read; Adjust Stock exercised in-browser (invalid-reason path); stock guards confirmed in source. **Add Item modal read but never submitted** |
| Settings — general, zones, users, billing | **Partial** | All four pages loaded and read; **no form submitted**, invite flow untested |
| Billing / Stripe | **Not examined** | Plan cards render; checkout, portal and webhook paths never traced or run |
| Messaging | **Deferred** | Endpoints exist (`send`, list, `retry`) with **no UI at all**. Explicitly deprioritised by the project owner (28 Aug 2026) — not an oversight; don't spend time here without being asked |
| Users / roles / invite | **Verified** | Endpoints read and exercised by `UserAccessTests`; two defects found and pinned (`/me` 401, invite dead end) |
| Zones | **Partial** | Endpoints and validators read (create/update only, no delete — deactivate via `IsActive`); CRUD never exercised in-browser |
| Job assignments | **Not examined** | `JobAssignment` entity exists; no UI found for it |

## Screens

| Route | Component type | Calls | Notes |
|---|---|---|---|
| `/` | server | — | redirects to `/login` or `/dashboard` |
| `/login`, `/register`, `/verify-email` | client | `/api/auth/*` route handlers | the only cookie-setting paths |
| `/dashboard` | **server** | `GET /api/dashboard` via the generated client | landing page after login. Today's schedule, active jobs, jobs by status, low stock, revenue this vs last month. The first page written to rules 3–4 |
| `/calendar` | client | `/api/calendar/bookings?from=&to=`, `/api/zones`, `PUT /bookings/{id}/move` | Week/Month, zone filter, create, edit, cancel, and **drag-to-move** (31 Aug 2026) |
| `/jobs` | client | `/api/jobs?page=&pageSize=&status=&search=` | list + status filter + search |
| `/jobs/[id]` | client | `/api/jobs/{id}`, `/api/zones`, `/api/inventory/items` | the real workhorse — see below |
| `/jobs/new` | client | `/api/customers/search`, `/api/customers/{id}` | customer → vehicle → job |
| `/customers`, `/customers/[id]` | client | `/api/customers*` | detail shows vehicles + history |
| `/vehicles` | client | `/api/customers/search` | **no list endpoint exists** — search-driven by design |
| `/vehicles/[id]` | client | `/api/vehicles/{id}`, `/{id}/history`, `/api/catalogue/*` | service history; Edit hydrates the catalogue picker from the vehicle's variant |
| `/inventory` | client | `/api/inventory/items`, `/categories` | stock levels, low-stock flag, adjustments |
| `/settings/general` | client | `/api/business`, `POST /api/auth/refresh` | name, phone, address, timezone; currency is a GBP/USD/EUR dropdown that refreshes the session so the new symbol applies at once |
| `/settings/zones` | client | `/api/zones` | bays + capacity |
| `/settings/tax` | client | `/api/tax/rates` | tax rates, defaults per labour/parts, optional jurisdiction breakdown — see [tax.md](tax.md) |
| `/settings/users` | client | `/api/users`, `/api/users/invite` | roles per member |
| `/settings/billing` | client | `/api/billing/subscription`, `/checkout`, `/portal` | plan cards → Stripe Checkout |

**Messaging has API endpoints but no UI.** `/api/messaging` (send, list, retry) is fully implemented server-side with no page and no nav entry.

### The job detail page

This is where the domain logic lives. Actions are gated by status:

```
Draft ─→ Scheduled ─→ InProgress ─→ Completed ─→ Invoiced ─→ Closed
                          ↕
                     WaitingParts
```

On an open job you get Edit, a status-transition button (e.g. **Waiting Parts**, **Complete**), **Reschedule**, **Add Labor**, **Add Part**, and per-line delete. On `Completed`/`Invoiced`/`Closed` the mutating controls disappear, and the API independently rejects edits — `"Cannot modify a {status} job"`.

Adding a part decrements `InventoryItem.StockOnHand` and writes a `StockMovement`; removing one returns the stock and writes the reverse movement. Totals are computed server-side in `JobDetailDto` (`laborTotal`, `partsTotal`, `grandTotal`).

---

## Bugs found during this walkthrough

### Fixed

Four of these shared a root cause: **pages hand-declare TypeScript interfaces for API responses, and some of them were wrong.** Nothing checks a hand-written interface against the real contract, so TypeScript compiled happily and the errors surfaced as garbage on screen.

1. **`£NaN` on every row of `/jobs`.** The page renders `formatCurrency(job.laborTotal + job.partsTotal)`, but `JobListItemDto` carried neither field → `undefined + undefined` → `NaN`. **Fixed server-side**: `JobListItemDto` now includes `LaborTotal` / `PartsTotal`, summed in the list projection the same way `GetAsync` computes them. The column was clearly intended — the vehicle service-history view had been showing those totals correctly all along.
2. **Blank part names on job detail.** The page read `line.inventoryItemName`; `PartLineDto` returns `itemName`. **Fixed web-side** (interface + render, and `sku` added since the DTO carries it).
3. **"0 customers" above a populated list.** The jobs and customers list endpoints return `{ items, total, page, pageSize }`, but those two pages read `data.totalCount`. **Fixed web-side.** Note the inventory page was *not* affected — it reads `data.total` correctly.
4. **Pagination never rendered on jobs or customers.** Same cause as #3: `Math.ceil(undefined / pageSize)` → `NaN`, and `NaN > 1` is `false`, so the Prev/Next block was permanently hidden and nothing past record 20 was reachable. **Fixed** by the same change.
5. **Every conditional query fired a real request to `/null`.** `useApiQuery` typed `basePath` as `string` and interpolated it into a template literal, so callers using SWR's conditional-fetch idiom (`search.length >= 2 ? "/api/customers/search" : null`) produced the key `"null"` and a live `GET /null → 404`. This also made `npm run build` fail with five type errors. **Fixed** in `hooks/use-api.ts`: `basePath` is now `string | null` and a null short-circuits to a null SWR key.

### Fixed — round two, 30–31 Aug 2026

Everything below was on the "still open" list and is now closed. Root causes kept, per the revise-never-delete rule.

6. **The `sub` claim never arrived, so `CurrentUserService.UserId` was always null.** ASP.NET's JwtBearer handler remaps inbound standard claims unless told not to, so the `sub` emitted by `JwtTokenService` reached the app as `ClaimTypes.NameIdentifier`. The custom claims (`business_id`, `business_user_id`, `permission`, `feature`) are untouched, which is exactly why tenancy and authorization worked and nobody noticed. Two effects: `/api/users/me` 401'd for **everyone**, and every `CreatedByUserId` audit column was written null — confirmed across all 20 rows in the dev database at the time. **Fixed** with one line in `Program.cs`: `options.MapInboundClaims = false`. Pinned by `UserAccessTests`. Note the columns written null before the fix are still null; nothing backfills them.
7. **`/api/users/me` sat inside a group requiring `users.manage`**, so even after the claim fix a non-admin could not read their own profile. **Fixed** — it is now outside that group and needs only authentication.
8. **Validation errors were unreadable in the UI, everywhere.** `ErrorHandlingMiddleware` returns validation failures as `{ code, errors: [{ field, message }] }` with **no top-level `message`**, while `ApiError` (`lib/fetcher.ts`) read only `data.message` and otherwise fell back to `` `Request failed with status ${status}` ``. Every *other* exception type in that middleware emits `message`, so validation was the single case that hit the fallback — and the case where the user most needed the text. Verified in-browser: a 20-character VIN returned a precise FluentValidation message and the toast read "Request failed with status 400". **Fixed** in `ApiError`, which now reads `errors[]` and exposes `fieldErrors` and `details`. One change; every form in the product.
9. **`recentJobs` on customer detail was always empty** — the page expected the array, `CustomerDetailDto` never returned one. **Fixed server-side**: the DTO and its query now include recent jobs.
10. **Status badges rendered the raw enum** (`InProgress`, `WaitingParts`) while the filter dropdown showed them spaced. **Fixed** with a `statusLabel()` display map.
11. **Two of the four Adjust Stock reasons were invalid and always failed.** The dropdown offered Manual Adjustment, **Restock**, Damaged, **Returned**; the `StockMovementReason` enum has `ManualAdjustment, JobConsumption, JobReturn, PurchaseReceived, Correction, Damaged, Other`, so `Enum.TryParse` rejected two of the four. Verified in-browser: Restock returned 400, toast read "Invalid reason", stock untouched. **Fixed** — the dropdown is now generated from the real enum values.
12. **Adding a vehicle captured less than editing one** — `AddVehicleModal` had only Make/Model/Year/Registration/VIN while the edit modal exposed engine, fuel and notes. **Fixed** by the catalogue rewrite: both modals now use the same `VehicleCataloguePicker`.
13. **A vehicle could be created entirely blank.** No field carried `required` and `CreateVehicleValidator` asserted only `CustomerId`, so an empty submit created a real row rendering as "Unnamed". **Fixed** — `variantId` and `year` are required and re-validated server-side against the variant's range.
14. **Duplicate registrations were allowed.** `VehicleConfiguration` indexes `(BusinessId, Registration)` but deliberately not `IsUnique()`, and `CreateAsync` did no duplicate check — two records for one plate split a vehicle's history silently. **Fixed** with `EnsureRegistrationIsFreeAsync`, which names the customer and vehicle already holding the plate. Note the check is read-then-write with no unique index behind it, so a genuine race can still slip through — see finding 8 in [review-findings.md](review-findings.md).

### Corrected — this was reported here and was wrong

- **"The invite flow is a dead end."** Reported as: memberships are created `Pending`, login requires `Active`, nothing transitions between them. **That was wrong.** `VerifyEmailEndpoint` activates pending memberships; the test that "pinned" the bug set `EmailVerified` directly in the database, bypassing the very endpoint that does the activation, and so proved nothing. The test was rewritten to go through `/api/auth/verify-email` and it passes. Kept here rather than deleted because the failure mode — a test that reproduces a bug by skipping the code that prevents it — is worth recognising again.

### Still open

- **The client never re-verifies identity after login.** `useAuth` reads permissions and features from the readable `ww_user` cookie and never calls the server again. If an admin changes someone's role, that user's UI keeps the old permissions until the 24h cookie expires. The server still rejects the actions, so this is display consistency rather than a hole — but there is no way to refresh a session short of logging out.
- **`npm run lint` is not configured.** It drops into an interactive ESLint setup prompt, so there is no working lint gate; `npm run build` is the real one.
- **The category dropdown loads all 236 categories inline** on every inventory render (~18 KB of `<option>`s). Fine at this size, not a pattern to scale.
- **Everything in [review-findings.md](review-findings.md).** That file is the current open list for defects found by reading the code rather than running it — including one introduced on 31 Aug that breaks vehicle editing outright.

### Why the Orval migration used not to fix this class of bug — and now does

**Resolved 1 Sep 2026.** The reasoning is kept because it is the argument that justified the
change, and because the trap it describes recurs the moment anyone writes `Task<IResult>`.

The problem was that minimal API handlers returned `Task<IResult>` with
`Results.Ok(new { ... })`. Minimal APIs cannot infer a response type from that, so the
OpenAPI document carried no response schema:

```json
"/api/jobs": { "get": { "responses": { "200": { "description": "OK" } } } }
```

Orval faithfully generated `apiClient<void>` for every one. Request bodies *were* typed
(`CreateJobRequest` is `$ref`'d, because those are typed parameters) — responses were not.
So migrating a page to the generated client bought no protection at all against bugs 1–4,
which is why the migration was not treated as the fix for them.

**The fix, 1 Sep 2026: the whole API moved from `IResult` to `TypedResults`.** All 77
handlers now declare a concrete return type (`Task<Ok<JobDetailDto>>`,
`Task<Created<ZoneDto>>`, `Task<NoContent>`, and three-way unions on the auth endpoints),
and the ~20 anonymous response objects became named records. The 28 hand-written
`.Produces<T>()` calls were deleted, because the return type now supplies both the schema
and the status code.

*Verified*: 78 of 78 endpoints carry a 2xx response schema (was 26), and `apiClient<void>`
in the generated client dropped from 78 to 9 — precisely the nine `DELETE`s that really do
return 204. `npm run build` and all 63 API tests pass.

**So the prerequisite is met: migrating a page to the generated client now does catch this
bug class.** A page reading `line.inventoryItemName` when the DTO says `itemName` is a
compile error today, for every endpoint rather than eight of them.

One finding worth carrying forward: `.Produces<List<CatalogueVariantDto>>()` on
`GET /catalogue/variants` had been declaring a type the handler never returned — the handler
returned `IEnumerable<>`. A hand-written `.Produces` is an unchecked assertion, so the
mismatch was invisible; under `TypedResults` it became `CS0029` and stopped the build. That
is the difference between documenting a response type and *having* one.

## Still undecided

**`InventoryCategory` is global.** 236 categories are seeded and shared across every business, and `CreateCategoryAsync` checks name uniqueness across all tenants — so once any business creates "Brakes", every other business gets a 409. The size and generality of the seeded taxonomy (a full automotive parts list) suggests the sharing is deliberate; the cross-tenant 409 on user-created categories looks like an unintended consequence of it. Confirm intent before changing either.
