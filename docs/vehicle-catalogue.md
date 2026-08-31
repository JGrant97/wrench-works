# Vehicle catalogue — design

Replacing free-text vehicle entry with an AutoTrader-style cascade: the user picks make → model → year → variant, and the variant pins trim, body, engine, transmission and fuel. Registration/VIN and colour are the only things typed or chosen separately.

**Status: built and running** (28 Aug 2026) — schema, seed, cascade endpoints, and the create/edit UI. Two things remain: the vPIC importer is written but has never been run at scale, and the second migration that drops the legacy free-text columns has not been created. Decisions below were made by the project owner.

### What is live

| Piece | State |
|---|---|
| `VehicleMake/Model/Variant/Colour` tables | Migrated (`AddVehicleCatalogue`) |
| `Vehicle.VariantId / Year / ColourId / DisplayName` | Added, nullable; `DisplayName` backfilled by migration (31 Aug 2026). `VariantId` stays nullable for legacy rows — see below |
| Legacy `Make/Model/EngineType/FuelType` columns | Still present, no longer written — drop in a follow-up migration |
| `VehicleCatalogueSeeder` | Runs at startup; 4 makes, 5 models, 14 variants, 15 colours |
| `GET /api/catalogue/*` | Live, 5 endpoints, `vehicles.view` |
| `POST/PUT /api/vehicles` | Require `variantId` + `year`; re-validate the year against the variant range |
| `VehicleCataloguePicker` | Shared component used by Add Vehicle and Edit Vehicle; hydrates an existing selection via `/catalogue/variants/{id}` |
| Booking + job pickers, customer/vehicle pages | Read `displayName` |
| `VpicCatalogueImporter` | Written, filtered to car/truck/mpv — **never executed** |

### Two defects found on 31 Aug 2026 — both now fixed

Kept here with their causes; they are the two ways this feature was quietly broken on any
machine other than the one it was built on.

**The backfill was never written as a migration.** `AddVehicleCatalogue` added the nullable
columns and the four tables and nothing else — no `Sql()`, no `UPDATE`. The existing MX-5
was backfilled with a hand-run statement against the dev database, and the status table
above recorded that as done: true on one machine, false everywhere else. On any other
database that row had `VariantId = null` and `ToDtoAsync` dereferenced `v.Variant!` into a
NullReferenceException → generic 500 — so the vehicle could not even be opened in order to
be corrected.

**Fixed** two ways. `20260831212716_BackfillVehiclesAndInventoryConcurrency` populates
`DisplayName` from the legacy `Year`/`Make`/`Model` columns wherever it is null. And the
read path no longer assumes a variant exists: `VehicleDto` declares `VariantId`, `Year`,
`MakeName`, `ModelName`, `FuelType` and `Transmission` nullable, and `ToDtoAsync` falls back
to the legacy free-text columns. A legacy vehicle reads back with the fields it has and
shows fewer rows in the details list; Edit still requires re-picking from the catalogue
before it will save, so strict-catalogue-only is preserved for writes.

Note this means **step 5 of the build order below is still not complete** — `VariantId`
cannot be made non-nullable while legacy rows may exist, and the free-text columns are now
load-bearing for those rows rather than merely deprecated.

**Edit Vehicle was broken by the faceted rewrite.** `VehicleCataloguePicker` initialised
`makeId`, `modelId` and every facet empty and never derived them from its `value` prop, so
the vehicle's real `variantId` — which `EditVehicleModal` does pass — was invisible to it.
No model meant no variants fetch, so the publish effect fired on mount with nothing resolved
and cleared the variant before the user touched anything. Make and Model rendered blank and
Save stayed disabled until the whole cascade was re-walked, even to change a registration.
The first version of the component had `initialMakeId`/`initialModelId` props; the rewrite
dropped them without replacing the hydration path.

**Fixed** by adding `GET /api/catalogue/variants/{id}` and hydrating from it in two steps —
resolve the variant to make/model/facets, then hold off publishing until the variant list
for that model-year has actually arrived. The second step matters: ending hydration on the
detail fetch alone still publishes `undefined` in the gap before the variants load, which is
the same wipe with a shorter window. Full detail in [review-findings.md](review-findings.md).

### The picker is faceted, not a single "specification" list

Trim, Body, Engine, Transmission and Fuel are **separate dropdowns**, in that order. Make/Model/Year come from the API; everything after Year is derived client-side by filtering the variants returned for that model-year — one fetch, no extra endpoints, and each dropdown offers only values that still lead to a real vehicle.

Two behaviours that matter:

- **Choosing a field clears every field below it**, so a stale lower selection can never be submitted against a changed upper one.
- **A field with exactly one possible value fills itself in.** An MX-5 is always petrol and always a convertible, so the user is not made to confirm choices that were never choices.

There is **no "Any" option** — each facet is either a real choice between real values, or it is filled in automatically because only one value exists. The placeholder reads "Select trim" and is not a submittable state. The form submits only once the facets narrow to exactly one variant; until then it reports "N specifications match".

Verified in the browser, 28 Aug 2026:

| | |
|---|---|
| Mazda MX-5 years offered | **1998–2015 only** — no 1960, no 2024 |
| MX-5 2001 → Fuel dropdown | **Petrol only** — Diesel is not present |
| MX-5 2001 → Body | auto-selected **Convertible** (sole option) |
| MX-5 2001 → Trim "1.6" | auto-resolved Engine to **1.6L**, Transmission offers **Manual and Automatic** |
| Ford Focus 2015 → Fuel dropdown | **Diesel and Petrol** — proving the filter is per-model, not a global ban |

### Trim means spec level, not engine size

`Trim` is the **spec level** — "Base", "Sport", "Grand Touring", "10th Anniversary". Engine size lives in `EngineDisplacementL` and has its own dropdown. Putting "1.8" in Trim duplicates the engine field and leaves nowhere for the real trim names.

"Base" is suppressed in display strings — a base MX-5 reads "2001 Mazda MX-5 1.8 Convertible", not "… 1.8 Base Convertible".

### Seed corrections

The seed is hand-curated, so it is only as right as the person writing it. Corrections so far, all found by the project owner reading the data:

- **MX-5 1.6 automatic** — added 28 Aug 2026. The seed listed the 1.6 as manual-only; Mazda built an automatic.
- **MX-5 trims restructured** — 28 Aug 2026. Trim held the displacement ("1.6", "1.8", "2.0"); replaced with Base plus named editions.

Two bugs the trim fix exposed, both now fixed:

- **The seeder's duplicate check ignored displacement**, so "Base 1.6 manual" and "Base 1.8 manual" looked identical and the second would have been silently dropped. Displacement is now part of the identity.
- **Renaming rather than re-inserting.** `ApplyTrimCorrectionsAsync` renames legacy trims in place, so vehicles already pointing at those variants keep their foreign key. Deleting and re-seeding would have orphaned them.

This is the expected failure mode of hand-curation: **wrong data looks exactly like right data**, and no test catches it. The schema guarantees you cannot pick a combination that isn't in the catalogue; it cannot tell you the catalogue is wrong.

---

## Data audit — 28 Aug 2026

`tools/catalogue-audit.sql` runs seven consistency checks against a live database:

```bash
docker exec -i wrenchworks-db psql -U wrenchworks -d wrenchworks -f - < tools/catalogue-audit.sql
```

**Structurally clean:** no duplicate rows, no overlapping year ranges for the same spec, no models without variants, no trims that are bare engine sizes.

**What the audit cannot check is whether the data is true.** It found three substantive problems that only reading the rows reveals.

### 1. The seed is UK data in a catalogue that was scoped to the US

This is the significant one, and it is a direct consequence of the unresolved market question at the bottom of this document.

| Row | Problem |
|---|---|
| Focus "1.6 TDCi", "2.0 TDCi" | TDCi is a European diesel. The US Focus of that era was petrol — 2.0L, later 1.0 EcoBoost. |
| Focus "1.6 Zetec" | Zetec is a UK trim name. |
| Focus body "Estate" | UK term; the US calls it a Wagon. |
| Prius "T3", "Excel" | UK trim names. US trims were Two / Three / Four / Five. |
| MX-5 1.6 | Europe/Japan engine. The US NB was 1.8 only. |

The MX-5 1.6 automatic added earlier is **correct for the UK and wrong for the US** — the row shouldn't exist in a US catalogue at all. That correction was right about the car and points at the market decision being wrong.

### 2. Trim still holds engine designations in two models

The MX-5 fix caught trims that were *exactly* a number (`^[0-9]+\.[0-9]+$`). It missed:

- **Ford Focus** — "1.6 TDCi", "2.0 TDCi", "1.6 Zetec": engine plus trim, mixed into one field.
- **Ford Mustang** — "289 V8" is an engine, not a trim. The 289 also ran roughly 1965–68, not the 1964–73 span on the row, and displacement is recorded as 4.7L, so it is duplicated.

### 3. Coverage gaps

| Model | Catalogued | Missing |
|---|---|---|
| Mazda MX-5 | 1998–2015 | **The entire ND generation (2016+)** — a 2016+ MX-5 cannot be entered |
| Ford Mustang | 1964–1973 | Everything from 1974 on |
| Ford Focus | 2011–2018 | Earlier and later generations |
| Tesla Model 3 | 2017–2024 | The Performance trim |
| Toyota Prius | 2009–2022 | One trim per generation only |

Under strict-catalogue-only, every gap is a vehicle that cannot be booked in.

### What would actually fix this

Trim data cannot be verified from vPIC — it returns empty (measured). Any trim list written from memory carries the same risk that produced the errors above. The options are unchanged from the original design question: license a dataset, or have someone who knows the vehicles review each row. The audit script narrows what a human has to look at; it cannot replace them.

---

## Markets — added 28 Aug 2026

The product is intended for **both the US and the UK**; the owner is US-based. That makes market a property of the variant, because the same model differs by market: the MX-5 1.6 is UK/Europe, Focus TDCi is European, and trim names diverge completely (UK "Zetec" vs US "SE", UK Prius "T3" vs US "Two").

`VehicleVariant.Market` is `US`, `GB`, `Both`, or `Unknown`.

**Market is internal only. It is NOT shown in the picker and nothing is filtered by it** — the decision was one global list, and that is what the UI does. A market dropdown was briefly added and removed; do not reintroduce one without asking.

The column stays because the population plan is **US first, UK later**, which requires knowing which rows are which. It is provenance for whoever curates the seed, not a user-facing control.

Consequence, accepted deliberately: a US user sees UK trims ("Zetec", "T3") and UK-only engines in the same list, with nothing marking them apart. If that becomes a problem in use, the fix is a conversation about filtering — not a dropdown added on the quiet.

### Three bugs this exposed, all fixed

1. **The seeder could only grow.** Correcting the NC year range from 2005–2015 to 2006–2015 created new rows and left the wrong ones active, so the picker showed both. `RetireSupersededVariantsAsync` now deactivates variants of seeded models that no longer match any seed row — deactivates, not deletes, because a vehicle may hold a foreign key.

2. **The seeder skipped instead of upserting.** A row that already existed was left untouched, so adding `Market` to the seed left every pre-existing row blank. It now refreshes non-key fields on match.

3. **A real market sat at enum zero.** The migration gave existing rows an empty string, which loads back as the zero value. Assigning `US` (then zero) looked like "no change" to EF and was never written — so `US` rows stayed blank while `GB` and `Both` saved correctly. `Unknown = 0` now occupies zero, making every real assignment a genuine change. **This is a trap for any future string-converted enum on an existing table.**

---

## Decisions

| Decision | Choice |
|---|---|
| Market | **US** (note: dev data is GBP with UK-style registrations — see Risks) |
| Catalogue source | **NHTSA vPIC**, free, gaps accepted |
| Trim / engine / transmission / fuel | **Hand-curated** for the makes actually serviced |
| Pre-1981 vehicles | **Hand-seeded** as needed |
| Unknown vehicles | **Strict catalogue only** — no free-text fallback on a Vehicle |
| Registration/VIN lookup | **Not now** |

---

## What vPIC actually provides — verified 28 Aug 2026

Probed against the live API. These numbers drive the whole design; do not re-derive them by assumption.

**Coverage begins at model year 1981** — the 17-character VIN standard:

```
Ford 1965 →   0 models      Ford 1981 → 310 models
Ford 1975 →   0 models      Ford 1985 → 298 models
Ford 1980 →   0 models      Ford 2000 →  66 models
```

**Makes must be filtered by vehicle type.** `GetAllMakes` returns **12,351** makes — overwhelmingly trailer and equipment manufacturers. `GetMakesForVehicleType/car` returns **195**. Import must filter, or the first dropdown is unusable.

**Model lists are good.** `getmodelsformakeyear/make/mazda/modelyear/2001` → 7 models including `MX-5`.

**Spec data is largely absent.** Decoding a real 2001 MX-5 VIN returned:

```
Make "MAZDA"  Model "MX-5"  ModelYear "2001"  DisplacementL "1.8"
BodyClass "Convertible/Cabriolet"  Series "Coupe"
Trim ""  EngineCylinders ""  FuelTypePrimary ""  TransmissionStyle ""  DriveType ""
```

Trim, cylinders, fuel type and transmission came back **empty on a mainstream modern vehicle**. There is also no *browsable* spec endpoint — spec fields exist only on VIN decode, and VIN entry is out of scope.

**vPIC has no colour data at all.** Colour is a property of an individual car, not of a model.

**Consequence:** vPIC can seed the make → model → year spine and nothing more. Everything that enforces "an MX-5 can't be diesel" is hand-curated. That is the accepted plan, not an oversight.

---

## Schema

Four new tables. All are **global reference data**, not tenant-scoped — the same category as `InventoryCategory`.

```
VehicleMake
  Id, Name, VpicMakeId?, IsActive

VehicleModel
  Id, MakeId → VehicleMake, Name, VpicModelId?, IsActive

VehicleVariant                          ← the leaf; this is what makes invalid combos impossible
  Id, ModelId → VehicleModel
  YearFrom, YearTo                      (inclusive range, e.g. 1998–2005)
  Trim?                                 "Base", "LS", "Grand Touring"
  BodyStyle?                            "Convertible", "Sedan", "SUV"
  EngineDisplacementL?                  1.8
  EngineCylinders?                      4
  FuelType                              Petrol | Diesel | Hybrid | Electric | LPG
  Transmission                          Manual | Automatic | CVT | DCT
  DriveType?                            FWD | RWD | AWD | 4WD
  IsActive

VehicleColour
  Id, Name, HexCode?, IsActive
```

**Why a year *range* rather than a row per year:** a variant that ran 1998–2005 is one row, not eight. The user picks a year and we return variants whose range contains it.

**Why the variant is the leaf:** correctness is structural, not rule-based. There is no "MX-5 cannot be diesel" validation to write and keep in sync — there is simply no MX-5 variant row with `FuelType = Diesel`, so the option cannot be offered or submitted. Body style is on the variant for the same reason: no MX-5 row says SUV.

### Changes to `Vehicle`

```
- Make, Model, Year, EngineType, FuelType     (free text — removed)
+ VariantId    → VehicleVariant, required     (pins trim/body/engine/fuel/transmission)
+ Year         int, required                  (specific year within the variant's range)
+ ColourId?    → VehicleColour
+ DisplayName  string, required               (denormalised snapshot, see below)
  Registration, Vin, Notes                     (unchanged)
```

**`DisplayName` is deliberate denormalisation.** A vehicle's description is stamped at creation ("2001 Mazda MX-5 1.8 Convertible"). If a variant is later corrected or deactivated, historical jobs and invoices keep reading the way they did when the work was done. Every list view reads `DisplayName` and never joins the catalogue — which also removes a four-table join from the jobs list.

---

## Cascade contract

Each endpoint returns **only valid next options**, so an invalid combination is unreachable rather than rejected.

```
GET /api/catalogue/makes                              → [{id, name}]
GET /api/catalogue/makes/{makeId}/models              → [{id, name}]
GET /api/catalogue/models/{modelId}/years             → [1998..2005]  (union of variant ranges)
GET /api/catalogue/models/{modelId}/variants?year=    → [{id, label, trim, body, engine, fuel, transmission}]
GET /api/catalogue/variants/{variantId}               → one variant + its modelId/makeId and names
GET /api/catalogue/colours                            → [{id, name, hex}]
```

All require `vehicles.view`. The catalogue is **read-only at runtime** — see Curation.

Server-side, `POST /api/vehicles` takes `variantId + year + colourId?` and re-validates that the year falls inside the variant's range. The UI cannot offer an invalid pair, but the API must not trust that.

---

## Curation — who edits the catalogue

The catalogue is shared by every tenant, so **no tenant-facing write endpoints**. If one workshop could add or edit a make, it would change what every other workshop sees. This is the same trap as `InventoryCategory`, where a cross-tenant uniqueness check makes one business's category name block another's (see `CLAUDE.md`); the fix here is to not create the door.

Curation happens through:

1. **The vPIC importer** — a CLI/admin task that upserts makes and models (filtered to passenger types). Idempotent, safe to re-run.
2. **A seed file in the repo** for variants, colours, and pre-1981 classics — reviewed like code, applied by migration.

If tenant-facing curation is ever wanted, it needs an approval queue, not a direct write.

---

## Migration

Trivially small right now: the database holds **one vehicle** (2001 Mazda MX-5 1.8VVT Petrol, VO51FLM).

1. Add the four catalogue tables, seed them.
2. Add `VariantId`, `Year`, `ColourId`, `DisplayName` to `Vehicle` as **nullable**.
3. Backfill the single existing row against a seeded MX-5 variant.
4. Second migration makes `VariantId`, `Year` and `DisplayName` non-nullable and drops the free-text columns.

Two migrations rather than one so the backfill sits between them. Do this before there is real customer data; the same operation at 10,000 vehicles is a fuzzy-matching project.

---

## UI

`AddVehicleModal` (`customers/[id]/page.tsx`) becomes the cascade. Each select is disabled until its parent has a value, and each fetches only on demand — the inventory page's 236-option dropdown is the anti-pattern to avoid.

```
Make      [ select ]  → enables Model
Model     [ select ]  → enables Year
Year      [ select ]  → enables Variant
Variant   [ select ]     "1.8 Convertible · Manual · Petrol"
Colour    [ select ]     optional
Registration [ text ]
VIN          [ text ]
```

This also fixes two existing defects for free: the create form currently captures **less** than the edit form (no engine/fuel/notes), and a vehicle can currently be created **entirely blank**. With a required variant, neither is possible.

The edit modal on `/vehicles/[id]` needs the same treatment or it will keep writing free text into columns that no longer exist.

---

## Build order

1. **Schema + migrations + vPIC importer.** Makes and models only, filtered to passenger types. Verifiable on its own: 195 makes, real model lists.
2. **Catalogue endpoints + integration tests.** Cascade correctness is exactly what tests are good at — assert no MX-5 variant returns `Diesel`.
3. **Seed a first spec set** — the makes this workshop actually sees, hand-curated, plus any classics.
4. **Rebuild `AddVehicleModal` as the cascade**, and the edit modal with it.
5. **Backfill migration**, then tighten columns to non-nullable.

Steps 1–2 are independent of the curation effort and can land first.

---

## Risks and open questions

- **The market mismatch is unresolved.** The catalogue is US/vPIC, but both businesses are `Currency = GBP`, the field is called `Registration` rather than "License Plate", and the seeded data is a UK-plated Mazda. If the product is genuinely US-market, currency and terminology need revisiting; if it's UK, vPIC is the wrong source entirely and this design should be reconsidered before step 1.
- **Hand-curation is ongoing work, not a one-off.** Every model year adds variants. Without a maintenance owner the catalogue silently ages, and under strict-catalogue-only an aged catalogue means new vehicles cannot be booked in.
- **Strict-only has an operational edge.** A vehicle absent from the catalogue cannot be entered at all — the job cannot be created until someone seeds it. Worth agreeing what a service advisor does at that moment.
- **`InventoryItem.CompatibilityTagsJson`** exists and is unused. Once variants are real entities, part compatibility should become a proper relation to `VehicleVariant` rather than a JSON blob.
