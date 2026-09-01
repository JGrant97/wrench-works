# Tax — design and current state

Charging tax in a way that works for a UK garage and a US shop without becoming a
compliance engine. Designed and built 31 Aug 2026.

---

## The decision that shapes everything

**Do not model countries and their rules. Model the shape both regimes share, and let the
business configure the numbers.**

A rate table for the US means ~13,000 jurisdictions changing monthly; getting it wrong is a
liability, not a bug. Every regime reduces to the same sentence: *some rate applies to some
category of line, and the resulting amount must be recorded permanently.*

## What actually differs

| | VAT/GST (UK, EU, AU, CA…) | US sales tax |
|---|---|---|
| Whose tax | Seller's, on their supply | Buyer's, seller collects |
| Rate driven by | What's sold, seller's country | Destination — state + county + city + district |
| Display | Often **inclusive** (B2C) | Always **exclusive** |
| Labour | Taxed the same as parts | **Often exempt, or taxed differently** |
| Invoice must show | Registration number, net/tax/gross | Tax line, sometimes a jurisdiction breakdown |

**The labour row is why tax lives on the line, not the job.** In many US states parts are
taxable and labour is not; in others labour on tangible property is taxable. A single job-level
rate would be wrong for a large share of the intended market.

---

## Model

### `TaxRate` — tenant-scoped, archivable

| Field | Why |
|---|---|
| `Name` | "VAT Standard", "NY State + NYC" — appears on the invoice |
| `Rate` | `decimal(9,6)`. Stored as a **fraction**, so 8.875% is `0.08875` — five decimal places. `(6,4)` was the first attempt and silently rounded it to `0.0888`; caught by `TaxTests` |
| `Categories` | Which line categories take this rate, via `TaxRateCategory`. Started as two booleans on this table and became a mapping when consumables arrived — see RESOLVED below |
| `Components` | Optional breakdown — see below |
| `ArchivedAtUtc` | A superseded rate must never be deleted; historical lines reference it |

### `TaxRateComponent` — the jurisdiction breakdown

A US invoice may need to show "NY State 4% · NYC 4.5% · MCTD 0.375%" rather than one 8.875%
line. Components are display-and-reporting only: **the line's tax is computed from the
parent's `Rate`, never by summing components.** Summing floating jurisdiction rates and
rounding each would drift from the total the customer was actually charged.

A rate with no components is the normal case and renders as a single line.

### On `Business`

| Field | Why |
|---|---|
| `PricesIncludeTax` | UK B2C quotes inclusive; the US never does. This inverts the arithmetic — see Rounding |
| `TaxRegistrationNumber` | VAT number / EIN. Legally required on a VAT invoice |
| `TaxLabel` | The word printed: "VAT", "Sales Tax", "GST". Defaults to "Tax" |

### On `Customer`

`IsTaxExempt` + `TaxExemptionReference` — US resale/government/non-profit certificates and
EU B2B reverse charge both need it. Retrofitting it later means re-touching every
calculation, so it went in with the rest.

### On `JobLaborLine` and `JobPartLine`

```
TaxRateId       → which rate was chosen (nullable: no tax)
TaxRatePercent  → the rate AS APPLIED
TaxAmount       → the computed amount
```

**The snapshot is the point.** When UK VAT went 17.5% → 20% → 15% → 20%, any system
recomputing from current settings silently rewrote its own history. An invoice raised in
March must still read as it did in March. Same reasoning as `Vehicle.DisplayName`.

---

## Rounding — decided, and written down because it has to be consistent

**Per line, `MidpointRounding.AwayFromZero`, 2 decimal places, then summed.**

Taxing the summed total instead gives a different answer, and both are defensible; mixing
them produces invoices off by a penny that customers notice. UK VAT permits either provided
it is consistent.

**Inclusive pricing inverts the arithmetic**, which is the easiest thing here to get wrong:

```
exclusive:  tax = round(net × rate)
inclusive:  net = round(gross ÷ (1 + rate));  tax = gross − net
```

Implementing only the first means a garage quoting "£60/hr including VAT" gets 20% added on
top of a price that already contained it.

---

## RESOLVED — consumables, and why the category booleans went

Raised and built 31 Aug 2026. Kept in full rather than deleted, because the reasoning is
what stops the next category being bolted on the same way.

### The tax treatment really does differ

Consumables — shop supplies, rags, cleaner, gloves, disposal levies — are not parts, and in
the US they are frequently taxed differently:

- Shop supplies are tangible personal property, so they are often taxable **even in states
  where labour is not**
- Some states exempt "materials consumed in performing a service" outright; others treat the
  *shop* as the end consumer, so the shop pays tax on purchase and charges none onward —
  the opposite direction, not merely a different rate
- Disposal levies (tyres, batteries, oil) are often statutory fees with their own rate, and
  in several states the levy itself is not subject to sales tax

In the UK it is all standard-rated, so none of this is visible from here.

### What was wrong, and what replaced it

`TaxRate` said which categories it applied to with two booleans:

```
IsDefaultForLabour
IsDefaultForParts
```

A third category meant a third boolean; the tyre levy would have meant a fourth. **A schema
migration and a code change per tax category.**

Replaced by `TaxRateCategory` — one row per (business, category) pointing at a rate, with a
**unique index on (BusinessId, Category)**. That makes "one rate per category" structural
rather than a validation rule a second write path could skip, and it is what
`SettingADefault_ClearsThePrevious` now asserts. A category with no row is untaxed, which is
exactly how a US shop says "labour is not taxable here".

The categories themselves stay an enum (`Labour`, `Parts`, `Consumables`). They are a
product decision, not per-tenant configuration — a workshop does not invent new ones — so the
list belongs in code. What did not belong in code was **a column per value**.

### Consumables are a flag, not a line type

`InventoryItem.IsConsumable`. Parts and consumables both come from inventory and both bill
through `JobPartLine`; only the tax category differs. A separate `JobConsumableLine` would
have bought nothing for tax and duplicated the stock handling.

*Verified by tests*: `ConsumablesTakeTheirOwnRate_NotThePartsRate` (parts at 20%, supplies at
5%, on the same job) and `ConsumablesAreUntaxed_WhenNoRateIsMappedToThem`.

### Still deferred

**Percentage-of-labour shop supplies** — "supplies, 5% of labour, capped at $25", the common
US billing pattern. It is a billing feature rather than a tax one and needs its own rules:
the cap, and whether the fee is itself taxable.

### Migration note

`AddTaxCategoriesAndConsumables` was **reordered by hand**. EF scaffolded the two
`DropColumn` calls *before* the table replacing them, which would have discarded every
configured mapping — job totals would then have quietly stopped including tax with nothing
to indicate why. The migration now creates the table, copies the flags across, and only then
drops the columns. *A scaffolded migration that drops a column is worth reading, not
trusting* — the same lesson as the `xmin` rename in `review-findings.md`.

---

## What is deliberately NOT built

- **Automatic rate lookup from an address.** That is Avalara / TaxJar / Stripe Tax. The
  calculation lives behind `TaxCalculator` so a provider can be slotted in without touching
  the endpoints.
- **Nexus determination**, exemption-certificate validation, filing, or returns.
- **Rate scheduling** (future effective dates). Change the rate when it changes; existing
  lines keep their snapshot, which is the behaviour that actually matters.

---

## Where it lives

| Piece | File |
|---|---|
| Calculation | `Features/Common/TaxCalculator.cs` |
| Rates CRUD | `Features/Tax/TaxEndpoints.cs` → `/api/tax/rates` |
| Business settings | `Features/Business/BusinessEndpoints.cs` |
| Job totals | `JobEndpoints.GetAsync` → `JobDetailDto` |
| UI — rates | `/settings/tax` |
| UI — consumable flag | `/inventory` item modals |
| UI — job totals | `jobs/[id]` totals card |

Tax properly belongs on an **invoice**, which does not exist yet (see the MVP gaps in
`review-findings.md`). Until it does, the job carries the money and tax attaches to job
lines. `TaxCalculator` takes lines and returns a breakdown, so it moves across unchanged
when invoicing lands.

---

## Migration safety

Existing jobs predate all of this. Every snapshot column defaults to zero and `TaxRateId`
is nullable, so **no historical total changes** when the feature is deployed. A business
sees tax only once it configures a rate.
