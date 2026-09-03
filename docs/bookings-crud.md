# Booking CRUD — current state and what it would take to finish it

Exploration only; nothing here is implemented yet. Verified against the running app and the source on 28 Aug 2026.

---

> **Update, 31 Aug 2026 — the U is now built.** `PUT /bookings/{id}` (full update) and
> `PATCH /bookings/{id}/status` exist, an Edit button and Mark completed / No-show
> controls are in the detail modal, and conflict 409s now name the clashing booking and
> its times instead of saying "Booking conflicts detected". `/move` shares its conflict
> check and job cascade with the new update via `CascadeToJobAsync`, so they cannot drift.
>
> **Update, 31 Aug 2026 — drag-to-move is built.** `PUT /bookings/{id}/move` is finally
> called, by `_lib/use-drag-to-move.ts` driving the week grid. Pointer events rather than
> HTML5 drag (the grid is positioned by time, so the drop has to be read as pixels and
> converted back to minutes), snapped to 15 minutes, moving across days via a `data-day`
> attribute read at drop time. A 4px threshold keeps a click opening the booking rather
> than rescheduling it by a pixel. Not optimistic: the server can reject the drop as a
> double-booking, and a block that lands then jumps back is worse than one that waits.
>
> **Correction, 2 Sep 2026:** the drag was sending **two** PUTs per drop — `onMove` was
> called inside a `setDrag` updater, which React double-invokes under StrictMode. The move
> worked and then a spurious concurrency 409 toast appeared. Fixed on both sides: the side
> effect moved out of the updater, and the booking is now read inside the zone lock so a
> stale concurrency token cannot produce a phantom conflict. Re-verified with one
> `PUT …/move → 200` per drag.
>
> *Verified in the browser*, 31 Aug 2026: dragging the "Test" booking from Monday to
> Wednesday issued `PUT /move` → 200 and the grid re-rendered from the server on the new
> day. An earlier drag onto an occupied slot returned **409** and the block stayed put —
> so both the happy path and the conflict path are exercised.
>
> **Still not built:** resize handles on a block, `GET /bookings/{id}` for deep-linking,
> and the booking-conflict test suite described below. The state table is left as written so the
> reasoning behind the design survives; treat the note above as current.
>
> **Known defects in what was built** (31 Aug 2026, from the review pass — full detail in
> [review-findings.md](review-findings.md)): `UpdateBookingRequest` and `MoveBookingRequest`
> have no FluentValidation validators, so an empty GUID returns 404 where the create path
> returns 400; `PATCH /bookings/{id}/status` can resurrect a `Cancelled` booking that
> `UpdateBookingAsync` correctly refuses to edit; and `CheckConflictsAsync` was read-then-write
> with no constraint behind it, so two simultaneous requests could both pass and double-book
> a capacity-1 bay — **fixed 2 Sep 2026** by a per-zone row lock held across the insert, and
> pinned by `BookingConcurrencyTests`; see finding 7 in [review-findings.md](review-findings.md). The tests listed at the bottom of this file are still unwritten, which is
> why none of these were caught.

## Where it stands

| Operation | API | UI |
|---|---|---|
| **Create** | `POST /api/calendar/bookings` | ✅ `CreateBookingModal` |
| **Read** (range) | `GET /api/calendar/bookings?from=&to=&zoneId=` | ✅ week/month grid, zone filter |
| **Read** (one) | ❌ no `GET /bookings/{id}` | ⚠️ detail modal reads from the list payload |
| **Update** (reschedule) | ✅ `PUT /bookings/{id}/move` | ❌ **nothing calls it** |
| **Update** (title, notes, customer, vehicle) | ❌ no endpoint | ❌ |
| **Update** (status → Completed / NoShow) | ❌ no endpoint | ❌ |
| **Delete** (cancel) | ✅ `DELETE /bookings/{id}` — soft, sets `Cancelled` | ✅ Cancel button |

So it is **CRD, not CRUD**. The gap is entirely in U.

Three specifics worth knowing before designing anything:

1. **`PUT /bookings/{id}/move` is fully implemented, conflict-checked, and completely unreachable from the UI.** It updates zone + start + end, and cascades the new schedule to the linked job. There is no drag handler, no reschedule dialog, no `fetcher.put` anywhere in `calendar/page.tsx`. This is the single biggest win available — the hard half already exists and is untested only because nothing calls it.
2. **Once a booking is created, its title, notes, customer and vehicle are immutable.** No endpoint can change them. The only recourse is cancel-and-recreate, which closes the linked job as a side effect (see below).
3. **`BookingStatus.Completed` and `NoShow` are unreachable.** The enum has four values and `BOOKING_STATUS_COLORS` styles all four, but nothing in the system can set them — `Confirmed` on create, `Cancelled` on delete. The existing data has a `Completed` booking, so these were presumably set by hand or by an earlier code path.

---

## Behaviour the design has to respect

**Conflict detection** (`CheckConflictsAsync`) is capacity-aware: it collects bookings in the same zone whose time ranges overlap, excluding cancelled ones and (on move) the booking being moved, and rejects only when `overlapping.Count >= zone.Capacity`. Both current zones have capacity 1, so any overlap blocks. Rejection is a `409` with `details: { conflictingBookingIds }`.

**The UI throws that detail away.** `fetcher` builds its `ApiError` from `data.message` only, so a clash surfaces as a toast reading "Booking conflicts detected" with no indication of *which* booking or *when*.

Confirmed empirically — booking Ramp 1 at 10:00–13:00 against an existing 09:00–12:00 returned:

```json
{"code":"conflict","message":"Booking conflicts detected",
 "details":{"conflictingBookingIds":["76c0cb76-8e86-47cc-af16-dad5e8d2554f"]}}
```

The user saw only the message. The modal does stay open with its input intact, so nothing is lost — but the one piece of information that would let someone resolve the clash is dropped on the floor. This is the weakest part of the current create flow, and it gets worse with drag-to-move, where conflicts become routine rather than exceptional.

**Bookings and jobs are coupled both ways.** Creating with `CreateJob: true` (the default in the modal) creates a `Job` in `Scheduled` status and cross-links `booking.JobId` / `job.BookingId` in two saves. Moving a booking rewrites the job's `AssignedZoneId`, `ScheduledStartUtc`, `ScheduledEndUtc`. Cancelling a booking **closes the linked job** unless it is already `Completed`, `Invoiced` or `Closed`. Any new update path has to decide, deliberately, what it does to the job.

**The business timezone setting is not used for anything.** `Business.Timezone` is editable on `/settings/general` and stored, but no rendering code reads it — `grep` finds it only in that form. Every date in the UI is formatted in the *browser's* timezone.

Verified by creating a booking through the UI: entering 09:00–12:00 stored `2026-08-28 14:00–17:00Z`, because the create modal does `new Date(form.startUtc).toISOString()` on a `datetime-local` value, which reads it as browser-local (UTC-5 here). Round-tripping is self-consistent — it renders back as 09:00 in the same browser — but for a business configured as `UTC`, a booking the user entered as 09:00 is 14:00 in the business's own timezone. Two staff in different timezones see different times for the same booking, and the setting they'd expect to control that does nothing.

This is worth settling **before** building edit/drag, because every new write path will need the same conversion and will bake the assumption in further.

---

## What finishing it would take

### API

**1. `PUT /api/calendar/bookings/{id}`** — full update. Mirror `CreateBookingRequest` minus `CreateJob`:

```
record UpdateBookingRequest(Guid ZoneId, Guid CustomerId, Guid VehicleId,
                            string Title, DateTime StartUtc, DateTime EndUtc, string? Notes);
```

Reuse `CheckConflictsAsync(..., excludeBookingId: id, ...)` exactly as `/move` does, and apply the same job-schedule cascade. `/move` then becomes a narrower special case of this — keep it (drag-to-move sends less data and is the hot path) but implement both against one shared helper so the conflict and cascade rules cannot drift apart.

**2. `PATCH /api/calendar/bookings/{id}/status`** — to reach `Completed` / `NoShow`. Follow the shape of `PATCH /api/jobs/{id}/status`, which already exists and does exactly this for jobs. Decide whether completing a booking should also advance the linked job.

**3. `GET /api/calendar/bookings/{id}`** — optional, but without it a booking cannot be deep-linked; the detail modal only works because the row is already in the list response.

All three should declare response types (`.Produces<BookingDto>(200)` or `TypedResults.Ok`) rather than returning anonymous objects — see the note in `CLAUDE.md` about why the generated client is currently untyped. New endpoints are the cheapest place to start fixing that.

### UI

**1. Drag-to-move on the week grid** → `PUT /move`. The grid is already absolutely positioned by time, so hit-testing a drop target is the bulk of the work. Optimistic update via SWR `mutate`, rolling back on a 409.

**2. Resize handles** on a booking block to change duration → the same `/move` call with an adjusted `EndUtc`.

**3. An Edit mode in `BookingDetailModal`** → `PUT /bookings/{id}`. ✅ Built. The shared customer/vehicle search this called for also exists now: `hooks/use-customer-vehicle.ts`, used by both New Booking and New Job (31 Aug 2026).

**4. Status controls** in the detail modal — "Mark completed" / "Mark no-show", gated on `usePermission("calendar.edit")` as the Cancel button already is.

**5. Surface conflicts properly.** Extend `ApiError` to carry `details`, and on a 409 highlight the clashing blocks in the grid and name them in the message. This benefits create as much as move.

### Tests

`TenantIsolationTests` is the template. Bookings are worth covering because the conflict logic is the most intricate rule in the codebase and currently has **zero** tests:

- overlap in the same zone is rejected; the same times in a different zone are accepted
- a zone with capacity > 1 permits concurrent bookings up to capacity
- moving a booking onto its own slot is not a conflict with itself (the `excludeBookingId` path)
- moving cascades to the linked job's schedule
- cancelling closes an open linked job but leaves an `Invoiced` one alone
- a booking in another tenant's business returns 404 on move, update and delete

---

## Suggested order

1. `PUT /bookings/{id}` + integration tests — unblocks all editing, no UI risk.
2. Edit mode in the detail modal — most user-visible gain per line of code.
3. Conflict details surfaced through `ApiError` — small, and makes 4 much better.
4. Drag-to-move + resize — largest UI effort; the endpoint is already waiting.
5. Status transitions — smallest, and can ride along with 2.
