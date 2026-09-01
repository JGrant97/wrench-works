# Code review findings — 31 Aug 2026

Six specialist reviewers (C#, React, TypeScript, database, security, silent-failure) read both projects cold. This file is the triaged, deduplicated result.

**How to read the status column.** *Verified* means the claim was checked against the code in this repo, not merely relayed — reviewers of this kind produce confident false positives, and two did here (see "Corrections" at the end). *Reported* means it is credible and unverified; check before acting.

**Keep this file current the same way as the others** — when something is fixed, move it to Fixed with its root cause intact. See "Keeping this file and the companion docs current" in CLAUDE.md.

---

## Fixed — 31 Aug 2026

Root causes kept in full, per the revise-never-delete rule in CLAUDE.md. Each entry states
what was wrong, what changed, and how the fix was verified.

### 1. Editing any vehicle is broken — the catalogue picker never hydrates
**Verified · introduced 31 Aug 2026 during the faceted-picker rewrite**

`src/components/vehicle-catalogue-picker.tsx:84-86` — `makeId`, `modelId` and `facets` all initialise empty with no derivation from the incoming `value` prop. `EditVehicleModal` (`app/(dashboard)/vehicles/[id]/page.tsx:93-97`) seeds `selection` with the vehicle's real `variantId`, but the picker can't see it.

Consequence: `modelId` is empty → the variants fetch never fires → `variants` stays `[]` → `matching` stays `[]` → the publish effect (`:176-180`) runs on mount with `resolved === undefined`, calls `onChange`, and **wipes the vehicle's variant before the user touches anything**. Make/Model render blank and Save is disabled until the whole cascade is re-walked, just to edit a registration.

The first version of the picker had `initialMakeId`/`initialModelId` props; they were dropped in the rewrite and never replaced with a hydration path.

**Fix:** hydrate once from `value.variantId` — fetch the variant, back-fill `makeId`/`modelId`/every facet — or suppress publishing while a known-valid variant hasn't loaded yet.

**Fixed.** `GET /api/catalogue/variants/{id}` was added (returning the variant plus its
model and make), and the picker now hydrates from it in two steps: resolve the incoming
`variantId` to make/model/every facet, then wait for the variant list for that model-year
to actually arrive before publishing anything upward. Both halves are needed — ending
hydration on the detail fetch alone still published `undefined` into the gap before the
variants loaded, which is the same wipe by a narrower margin. Hydration also ends if the
variant has since been retired, so a stale variant cannot freeze the picker.

*Verified in the browser*, 31 Aug 2026: opening Edit on the 2001 MX-5 fills Make=Mazda,
Model=MX-5, Year=2001, Trim=Base, Body=Convertible, Engine=1.8L, Transmission=Manual,
Fuel=Petrol and resolves to one variant; saving a note round-tripped with every catalogue
field unchanged. Covered by `CatalogueTests.GetVariant_ReturnsItsModelAndMake`, which
asserts `EngineDisplacementL` round-trips exactly — the picker matches its Engine facet on
a formatted string, so a formatting difference would silently select nothing.

### 2. Jobs never validate Zone ownership — cross-tenant reference, then a 500 that kills the calendar
**Verified · pre-existing**

`Features/Jobs/JobEndpoints.cs` `CreateAsync`/`UpdateJobAsync` validate `CustomerId` and `VehicleId` via the tenant-filtered `FindAsync`, then assign `AssignedZoneId = request.ZoneId` with **no lookup at all**. `CalendarEndpoints` does this correctly in three places.

The chain, each link confirmed:
1. Tenant A submits tenant B's zone GUID → accepted; the FK is satisfied at the database and tenancy is never checked in the app
2. `UpdateStatusAsync` auto-creates a `Booking` on that foreign zone
3. `GetBookingsAsync` projects `b.Zone.Name` unconditionally — the zone is filtered out for tenant A → null → **NullReferenceException → 500 → the whole calendar list breaks for that tenant**

Not covered by `TenantIsolationTests`, which only exercises job labor lines.

**Fixed.** `EnsureZoneIsOursAsync` reads the zone through the tenant-filtered `db.Zones`
before either handler assigns `AssignedZoneId`, so a foreign zone is simply not found.
Called from both `CreateAsync` and `UpdateJobAsync`.

*Verified by tests*: `TenantIsolationTests.CreateJob_WithAnotherTenantsZone_IsRejected` and
`UpdateJob_WithAnotherTenantsZone_IsRejectedAndLeavesTheJobIntact`. Both assert the stored
rows as well as the 404 — the update path mutates the tracked entity before saving, so a
status-only check would have passed while still persisting the foreign zone.

### 3. Every dashboard page renders a failed request as "nothing found"
**Verified · pre-existing**

`src/hooks/use-api.ts` never destructures SWR's `error` — the word does not appear in the file, and no page reads it. On any failure `data` stays `undefined` and `isLoading` goes false, which is indistinguishable from an empty result.

Observed consequences: a `/api/zones` failure shows an admin **"No zones configured — Create bays/zones in Settings"**; a 500 on a vehicle renders "Vehicle not found" identically to a real 404; a failed vehicle search renders nothing at all, not even the empty state.

**Fix:** surface `error` from the hooks and add a third render branch. Given ~13 call sites, a shared `<DataState loading error empty>` wrapper is cheaper than patching each page.

**Correction to the finding.** The diagnosis was slightly wrong, the consequence exactly
right. `useApi`/`useApiQuery` return SWR's result object whole, so `error` was always
available to callers — the hook did not discard it. What was true is that **no call site
ever read it**, which produced every symptom described.

**Fixed.** A new `ErrorState` component (`src/components/data-state.tsx`) renders the
message plus a Try again button, and eleven call sites now branch on `error` **before** the
empty branch: calendar, jobs, jobs/[id], customers, customers/[id], inventory, vehicles,
vehicles/[id], settings/zones, settings/users, settings/billing, settings/general. The
ordering is the whole fix; an error branch placed after the empty check does nothing.

*Verified in the browser*, 31 Aug 2026: with the API stopped, `/calendar` renders "Failed to
reach backend" with a retry button instead of "No zones configured — Create bays/zones in
Settings", and `/vehicles/{id}` renders the same instead of "Vehicle not found". Restarting
the API let SWR's own retry restore the page.

### 6. Stock has a lost-update race
**Verified · pre-existing**

`JobEndpoints.cs` `AddPartAsync` and `InventoryEndpoints.cs` `AdjustStockAsync` both read `StockOnHand`, check it, then write — with no lock and no concurrency token. Only five entities call `.IsRowVersion()` (`Business`, `User`, `BusinessUser`, `Booking`, `Job`); **`InventoryItem` is not among them**, so its inherited `RowVersion` column is never incremented and never checked.

Two concurrent part-adds both read the same value, both pass the guard, and the second write silently overwrites the first. Stock can go negative and drift from the `StockMovement` trail that is supposed to reconstruct it.

**Fix:** `.IsRowVersion()` on `InventoryItem` (maps to `xmin` like the other five), and/or a single guarded `UPDATE ... SET StockOnHand = StockOnHand - @qty WHERE StockOnHand >= @qty`.

**Fixed.** `InventoryItem` now maps `.IsRowVersion()` like the other five entities.

Two things this exposed that the finding did not predict:

- **`InventoryItems` had a real `RowVersion bigint` column** that nothing ever read —
  confirmed with `\d "InventoryItems"`. The five working entities have no such column;
  they map to Postgres's `xmin` system column. So the fix was to **drop** the dead column,
  not migrate it.
- **EF scaffolded `RenameColumn("RowVersion" -> "xmin")`**, which would have failed on
  execution — `xmin` is a reserved system column name. The migration was rewritten by hand
  to `DropColumn`. *Worth remembering: a scaffolded migration for an `xmin`-mapped token
  needs reading, not trusting.*

Making the token real also made finding 20 reachable, so it was fixed in the same pass —
without it a lost update would have become a 500 rather than staying silent, which is not
obviously an improvement.

### 10. No backfill migration exists
**Verified · introduced 29 Aug 2026**

`AddVehicleCatalogue` adds nullable columns and tables only — no `Sql()`, no UPDATE. The existing MX-5 was backfilled by hand against the dev database and `docs/vehicle-catalogue.md` recorded it as done, which is true for one machine and false everywhere else.

On a fresh database that vehicle has `VariantId = null`, and `VehicleEndpoints.ToDtoAsync`'s `v.Variant!` dereferences it into an NRE → generic 500. Both the C# and silent-failure reviewers flagged the null-dereference independently; only one connected it to the missing migration.

**Fixed.** `20260831212716_BackfillVehiclesAndInventoryConcurrency` populates `DisplayName`
from the deprecated free-text columns (`Year`, `Make`, `Model`) for every row missing one.

The null-dereference is fixed separately and more fundamentally: `VehicleDto` now declares
`VariantId`, `Year`, `MakeName`, `ModelName`, `FuelType` and `Transmission` as nullable, and
`ToDtoAsync` falls back to the legacy columns instead of `v.Variant!`. A pre-catalogue
vehicle now reads back with the fields it actually has rather than 500ing — which matters
because it previously could not even be opened in order to be corrected. The detail page's
list already filtered falsy values, so those rows simply show fewer fields; Edit still
requires re-picking from the catalogue before it will save.

*Verified*: the migration applied to the dev database (`__EFMigrationsHistory` confirms it,
and `RowVersion` is gone from `InventoryItems`), and it runs on every fresh Testcontainers
database in the 21-test suite.

### 20. Concurrency conflicts surfaced as 500s
**Verified · pre-existing · fixed 31 Aug 2026**

`DbUpdateConcurrencyException` and unique-index `DbUpdateException` fell through to
`ErrorHandlingMiddleware`'s generic handler, so optimistic concurrency — configured on five
tables — was invisible when it fired. The caller got `500 internal_error` with no hint that
reloading and retrying would work.

**Fixed** for the concurrency case: the middleware now catches `DbUpdateConcurrencyException`
and returns `409 { code: "concurrency_conflict" }` with a message telling the user to reload.
**Still open:** unique-index `DbUpdateException` is unchanged and still yields a 500.

---

## Breaks something today

*Findings 1, 2 and 3 were here and are now under [Fixed](#fixed--31-aug-2026).*

### 4. `fetcher` returns `null` cast as `T` on every 204
**Reported · pre-existing**

`src/lib/fetcher.ts:66-70` — `res.json().catch(() => null)` then `return data as T`. Cancel-booking (`CalendarEndpoints.cs:301`) and both job line-item deletes (`JobEndpoints.cs:413,427`) return `NoContent()`. Any caller reading a field off the result gets a runtime `TypeError`; the `as T` hides it from the compiler.

### 5. `backendFetch` has no error path, so auth routes bypass the documented error contract
**Reported · pre-existing**

`src/lib/proxy.ts:89-107` — `proxy()` wraps `fetch` in try/catch and returns `502 { code: "proxy_error" }`; `backendFetch` does not, and none of its five callers (login, register, verify-email, refresh, me) wrap it either. Backend down on an auth route = unhandled exception and a generic Next error, not the `{code, message}` shape `ApiError` expects.

---

## Silently corrupts data

*Finding 6 was here and is now under [Fixed](#fixed--31-aug-2026).*

### 7. Double-booking is possible
**Reported · pre-existing**

`CalendarEndpoints.cs` `CheckConflictsAsync` reads the overlapping set and compares to capacity; the caller inserts afterwards. Two simultaneous requests both see zero conflicts and both commit. With the current capacity of 1, that is two vehicles in one bay. `Booking.RowVersion` doesn't help — the race is between two *inserts*.

**Fix worth considering:** a Postgres exclusion constraint (`EXCLUDE USING gist (ZoneId WITH =, tstzrange(StartUtc, EndUtc) WITH &&) WHERE (Status <> 'Cancelled')`, needs `btree_gist`). That makes it structurally impossible rather than rule-based — the same philosophy already chosen for the vehicle catalogue.

### 8. Registration is ~14 sequential saves with no transaction
**Reported · pre-existing**

`RegisterEndpoint.cs:87-114` does four `SaveChangesAsync` calls, and `PermissionSeeder.SeedDefaultRolesForBusinessAsync` adds ten more inside that. A failure after the first leaves an **Active `BusinessUser` with no Admin role** — an account that can log in and do nothing, with the user told registration succeeded.

Every `Id` is a client-generated `Guid` set at construction, so none of this needs to be sequential; it can be one save, or wrapped in a transaction.

Same shape, smaller blast radius: `UserEndpoints.InviteAsync` (membership then role), `CalendarEndpoints.CreateBookingAsync` (booking+job then the reverse FK), `JobEndpoints.UpdateStatusAsync` (status then audit log).

---

## Latent — data-loss shaped, dormant only because the endpoint doesn't exist yet

### 9. Cascade deletes destroy audit and billing history
**Verified · pre-existing · FIXED 31 Aug 2026**

FKs default to `Cascade` unless overridden. `Vehicle.VariantId`/`ColourId` were deliberately set to `Restrict` so catalogue data can't be deleted out from under a vehicle — that discipline wasn't applied to:

- `JobPartLines → InventoryItems` — deleting a part deletes every historical job line that billed it
- `StockMovements → InventoryItems` — deletes the audit trail the movements exist to provide
- `Bookings → Zones` — deleting a bay deletes every booking ever made in it
- `Vehicles`/`Jobs`/`Bookings → Customers` — deleting a customer wipes their entire history

No delete endpoints exist for these today. **Set them to `Restrict` (or model deletion as deactivation, as `Zone.IsActive` already does) before the first such endpoint is built**, or that endpoint's first use is a data-loss incident.

**Fixed, and the warning turned out to be exactly right.** Full CRUD was requested on
31 Aug 2026, which made this finding load-bearing rather than latent. Eight FKs moved to
`Restrict` in `20260831225523_AddArchivingAndRestrictCascades`:
`Vehicle→Customer`, `Booking→Zone/Customer/Vehicle`, `Job→Customer/Vehicle`,
`JobPartLine→InventoryItem`, `StockMovement→InventoryItem`.

Left as `Cascade` deliberately: every `Business → X` (so offboarding a tenant still
removes its data), `Job → LaborLines/PartLines/Assignments` (owned children of the job),
and `Make → Model → Variant` (an owned catalogue hierarchy).

Delete now removes a row only when nothing references it; anything with history is
archived instead. *Verified by tests* —
`CrudTests.DeleteCustomer_WithAVehicle_IsRefusedAndDestroysNothing` asserts the customer
**and the vehicle** both survive the refused delete, which is the assertion that would
have failed under the old cascades. *Verified in the browser*: deleting a customer with
history shows "This customer has 1 vehicle, 9 jobs, 10 bookings and cannot be deleted",
with Archive offered in the same dialog.

*Finding 10 was here and is now under [Fixed](#fixed--31-aug-2026).*

### 11. Stripe webhook has no signature verification
**Reported · pre-existing**

`BillingEndpoints.cs:63-83` is `AllowAnonymous`, reads the raw body, returns `{ received: true }`, and carries a `// TODO: Verify Stripe signature`. `StripeService` is entirely stubbed, so there is nothing to forge into today — but this becomes an unauthenticated "grant my business Enterprise" hole the moment the TODO is implemented.

---

## Medium

| # | Finding | Status |
|---|---|---|
| 12 | Register and invite discard `IEmailSender.SendAsync`'s `SendResult`; `MessagingEndpoints` checks it correctly. Silent with `ConsoleEmailSender`, silent data loss with a real provider | Verified |
| 13 | `UpdateBookingRequest`/`MoveBookingRequest` have no FluentValidation validators — empty GUIDs yield 404 where the create path yields 400 *(mine)* | Verified |
| 14 | `PATCH /bookings/{id}/status` can resurrect a `Cancelled` booking that `UpdateBookingAsync` correctly refuses to edit *(mine)* | Verified |
| 15 | `settings/general` seeds its form from an effect on `biz`, so a background SWR revalidation clobbers in-progress edits | Reported |
| 16 | `api-client.ts` never converts `AxiosError` to `ApiError` — the first page migrated to the generated client gets a different error shape than every other page | Reported |
| 17 | `use-auth.ts` casts `JSON.parse` of the `ww_user` cookie to `AuthUser` with no runtime validation; a malformed cookie makes `.includes()` throw rather than degrading to "no permissions" | Reported |
| 18 | `CustomerEndpoints.UpdateAsync` has no validation at all, while `CreateAsync` enforces name and email | Reported |
| 19 | `RemovePartAsync` queries the unfiltered `JobPartLines` *before* the tenant check on the parent job — backwards from `RemoveLaborAsync`. Not a leak (the parent check still blocks it) but an existence oracle: foreign job → 500, own missing line → 404 | Verified |
| 21 | Catalogue picker's `.catch(() => {})` on models/years/variants isn't scoped to cancellation and sets no error — a transient failure silently blocks adding a vehicle *(mine)* | Verified |
| 22 | Zone filter `<select>` and vehicle search `<input>` bypass the `Select`/`Input` primitives and have no accessible label | Reported |
| 23 | No rate limiting on login/register/verify-email; no JWT revocation (24h window, latent — nothing sets `Disabled` yet); `/api/billing/subscription` needs only authentication; user enumeration via register's 409 | Reported |

## Low / performance

- Missing `Jobs(BusinessId, CreatedAtUtc)` index — the default job list sorts on it with no covering index
- `Bookings(BusinessId, ZoneId, StartUtc, EndUtc)` leads with `ZoneId`, but the main calendar view queries without a zone; consider `(BusinessId, StartUtc, EndUtc)`
- Every search uses leading-wildcard `Contains`, which no btree index can serve — needs `pg_trgm` + GIN if volume grows. Note the comment in `VehicleEndpoints.SearchAsync` claiming the registration index serves this; it does not
- `VehicleEndpoints.GetAsync` does an existence check then re-queries; `CreateAsync`/`UpdateAsync` re-query a graph already in memory
- `Include(...)` followed by a full `.Select(...)` projection in `JobEndpoints.ListAsync` and `VehicleEndpoints.GetHistoryAsync` — EF ignores the Include; dead configuration that looks load-bearing
- Missing max lengths on `BusinessSubscription.Plan`/Stripe IDs, `User.PasswordHash`/tokens, `Business.Address`/`Phone`, `AuditLog.IpAddress`
- Redundant single-column FK indexes on `StockMovements.InventoryItemId`, `Bookings.CustomerId`, `Bookings.VehicleId` — confirm with `pg_stat_user_indexes.idx_scan` before removing
- `VpicCatalogueImporter` takes a raw `HttpClient` with no DI registration; wire it with `AddHttpClient<T>()` when it's first used
- `Job.BookingId`/`Booking.JobId` are two independent nullable FKs with nothing keeping them consistent — the codebase already works around this ("check both FK directions for robustness" appears twice in `JobEndpoints`)
- `proxy.ts`'s content-type check misses `application/problem+json`; latent, since the only `Results.Problem` caller is reached via `backendFetch`
- `SESSION_SECRET` is documented in `.env.local.example` but never read anywhere — cookies are plain JSON, not signed or encrypted

---

## Corrections to the reviewers

Both checked against the code:

- **`MoveBookingAsync`'s hand-rolled error body is not a regression.** The C# review called it "reintroduced"; it is at line 161 of `HEAD`, pre-existing. What was added alongside it was the *correct* version in `UpdateBookingAsync`. The inconsistency is real; the attribution was wrong. **Resolved 1 Sep 2026** — the TypedResults migration converted all sixteen ad-hoc `Results.BadRequest` bodies (this one included) to thrown exceptions, so the inconsistency is gone rather than merely attributed correctly.
- **`CustomerEndpoints.UpdateAsync`'s missing validation is likewise pre-existing**, not introduced by the `RecentJobs` change that touched the file.

## What was checked and found clean

Worth recording so it isn't re-derived: no SQL injection surface anywhere (all EF LINQ, no raw SQL), no SSRF (the one outbound call has a hardcoded host and URL-escaped segments), CSRF adequately covered by `SameSite=Lax` plus a bearer-only API, tenant query filters correct on every entity that needs one, every `IgnoreQueryFilters()` paired with an explicit tenant predicate, BCrypt used correctly, JWT validation parameters all set, and no secrets in tracked history — `appsettings.Development.json` is gitignored by two rules and `git log --all --full-history` confirms it was never committed.

The picker's four data-fetch effects were traced specifically for an infinite loop between auto-fill and publish: they converge, because `facetOptions[key]` depends only on facets *above* `key`, so setting a facet can narrow those below but never reopen one above. The defect there is the missing hydration, not a loop.

---

## The pattern worth remembering

Several of the worst findings are the same bug class fixed earlier the same day. `ApiError` was repaired so validation messages reach users — and the hook directly above it discards the error object entirely, `fetcher` fabricates `null` on 204, and `backendFetch` has no failure path at all. One instance was fixed and the class treated as closed.

When a bug is found, search for its siblings before calling it done.
