"use client";

import { useEffect, useMemo, useState } from "react";
import { Select } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";

/**
 * Vehicle catalogue picker.
 *
 *   Make → Model → Year → Trim → Body → Engine → Transmission → Fuel   (+ Colour)
 *
 * Make, Model and Year come from the API. Everything after Year is derived by
 * filtering the variants returned for that model-year, so each dropdown offers only
 * values that still lead to a real vehicle: once you pick an MX-5, Fuel offers Petrol
 * and nothing else, because no MX-5 variant row is diesel. See docs/vehicle-catalogue.md.
 *
 * Three behaviours worth knowing:
 *  - Choosing any field clears every field below it, so a stale lower selection can
 *    never be submitted against a changed upper one.
 *  - A field with exactly one possible value selects itself, so the user isn't made to
 *    confirm a choice that was never a choice (an MX-5 is always petrol).
 *  - An edit form arrives with a variantId already set. The picker resolves it back to
 *    make/model/facets via /api/catalogue/variants/{id} and publishes NOTHING upward
 *    until that has settled. Skipping the second half is what previously wiped a
 *    vehicle's variant the moment Edit was opened.
 */

export interface CatalogueSelection {
  variantId: string;
  year: number;
  colourId: string | null;
}

interface Make { id: string; name: string }
interface Model { id: string; name: string }
interface Colour { id: string; name: string; hexCode: string | null }

/** What /api/catalogue/variants/{id} returns: a variant plus its place in the cascade. */
interface VariantDetail {
  id: string;
  modelId: string;
  makeId: string;
  trim: string | null;
  bodyStyle: string | null;
  engineDisplacementL: number | null;
  fuelType: string;
  transmission: string;
}

interface Variant {
  id: string;
  label: string;
  trim: string | null;
  bodyStyle: string | null;
  engineDisplacementL: number | null;
  engineCylinders: number | null;
  fuelType: string;
  transmission: string;
  driveType: string | null;
}

/** Facets in the order they narrow the set. Order matters: each filters by the ones before it. */
type FacetKey = "trim" | "bodyStyle" | "engine" | "transmission" | "fuelType";
const FACET_ORDER: FacetKey[] = ["trim", "bodyStyle", "engine", "transmission", "fuelType"];

const FACET_LABELS: Record<FacetKey, string> = {
  trim: "Trim",
  bodyStyle: "Body",
  engine: "Engine",
  transmission: "Transmission",
  fuelType: "Fuel",
};

/**
 * Engine displacement as it appears in a dropdown. Shared by the derived options and by
 * hydration so a hydrated facet is string-equal to the option it must match — format the
 * two differently and the edit form silently fails to select anything.
 */
function engineLabel(litres: number | null): string {
  return litres !== null ? `${litres.toFixed(1)}L` : "";
}

/** The displayed value of a facet for one variant. Engine is formatted; the rest are raw. */
function facetValue(v: Variant, key: FacetKey): string {
  switch (key) {
    case "trim": return v.trim ?? "";
    case "bodyStyle": return v.bodyStyle ?? "";
    case "engine": return engineLabel(v.engineDisplacementL);
    case "transmission": return v.transmission;
    case "fuelType": return v.fuelType;
  }
}

type Facets = Partial<Record<FacetKey, string>>;

export function VehicleCataloguePicker({
  value,
  onChange,
}: {
  value: Partial<CatalogueSelection>;
  onChange: (next: Partial<CatalogueSelection>) => void;
}) {
  const [makes, setMakes] = useState<Make[]>([]);
  const [models, setModels] = useState<Model[]>([]);
  const [years, setYears] = useState<number[]>([]);
  const [variants, setVariants] = useState<Variant[]>([]);
  const [colours, setColours] = useState<Colour[]>([]);

  const [makeId, setMakeId] = useState("");
  const [modelId, setModelId] = useState("");
  const [facets, setFacets] = useState<Facets>({});
  const [error, setError] = useState<string | null>(null);

  /**
   * The variant we were handed but have not yet reconciled against a loaded variant list.
   * Non-null means "hydrating": publishing upward is suppressed, because at that point
   * nothing has resolved yet and publishing would clear the caller's real selection.
   * Read once from the initial prop — later prop changes are our own publishes coming back.
   */
  const [hydratingVariantId, setHydratingVariantId] = useState<string | null>(value.variantId ?? null);

  /** "modelId:year" of the variant list in state, so we can tell loaded-and-empty from not-loaded. */
  const [variantsKey, setVariantsKey] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [m, c] = await Promise.all([
          fetcher.get<Make[]>("/api/catalogue/makes"),
          fetcher.get<Colour[]>("/api/catalogue/colours"),
        ]);
        if (cancelled) return;
        setMakes(m);
        setColours(c);
      } catch {
        if (!cancelled) setError("Could not load the vehicle catalogue");
      }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!makeId) { setModels([]); return; }
    let cancelled = false;
    fetcher.get<Model[]>(`/api/catalogue/makes/${makeId}/models`)
      .then((m) => { if (!cancelled) { setModels(m); setError(null); } })
      .catch(() => { if (!cancelled) setError("Could not load models for that make"); });
    return () => { cancelled = true; };
  }, [makeId]);

  useEffect(() => {
    if (!modelId) { setYears([]); return; }
    let cancelled = false;
    fetcher.get<number[]>(`/api/catalogue/models/${modelId}/years`)
      .then((y) => { if (!cancelled) { setYears(y); setError(null); } })
      .catch(() => { if (!cancelled) setError("Could not load years for that model"); });
    return () => { cancelled = true; };
  }, [modelId]);

  /**
   * variantsKey is set on both success and failure so downstream code can distinguish
   * "loaded, and there are none" from "not loaded yet" — the distinction hydration needs
   * in order to give up rather than hang.
   */
  useEffect(() => {
    if (!modelId || !value.year) { setVariants([]); setVariantsKey(null); return; }
    const key = `${modelId}:${value.year}`;
    let cancelled = false;
    fetcher.get<Variant[]>(`/api/catalogue/models/${modelId}/variants?year=${value.year}`)
      .then((v) => { if (!cancelled) { setVariants(v); setVariantsKey(key); setError(null); } })
      .catch(() => {
        if (cancelled) return;
        setVariants([]);
        setVariantsKey(key);
        setError("Could not load specifications for that model year");
      });
    return () => { cancelled = true; };
  }, [modelId, value.year]);

  /**
   * Hydration step 1 — resolve the incoming variantId back to a make, a model and a full
   * set of facets. Runs once; a failure gives up rather than leaving the picker frozen.
   */
  useEffect(() => {
    if (!hydratingVariantId) return;
    let cancelled = false;
    (async () => {
      try {
        const v = await fetcher.get<VariantDetail>(`/api/catalogue/variants/${hydratingVariantId}`);
        if (cancelled) return;
        setMakeId(v.makeId);
        setModelId(v.modelId);
        setFacets({
          trim: v.trim ?? undefined,
          bodyStyle: v.bodyStyle ?? undefined,
          engine: engineLabel(v.engineDisplacementL) || undefined,
          transmission: v.transmission,
          fuelType: v.fuelType,
        });
      } catch {
        if (cancelled) return;
        setError("Could not load this vehicle's current specification");
        setHydratingVariantId(null);
      }
    })();
    return () => { cancelled = true; };
    // Deliberately keyed on the id alone: this must not re-run as the fields it sets change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hydratingVariantId]);

  /**
   * Hydration step 2 — hydration ends once the variant list for the hydrated model-year has
   * actually arrived. Ending it earlier would publish an unresolved (undefined) variant and
   * wipe the caller's selection; ending it only on success would freeze the picker whenever
   * the variant has since been retired from the catalogue.
   */
  useEffect(() => {
    if (!hydratingVariantId) return;
    // A variantId with no year can never resolve, so there is nothing to wait for.
    if (!value.year) { setHydratingVariantId(null); return; }
    if (variantsKey === `${modelId}:${value.year}`) setHydratingVariantId(null);
  }, [hydratingVariantId, variantsKey, modelId, value.year]);

  /**
   * For each facet: the distinct values still reachable, given only the facets ABOVE it.
   * Filtering by facets above (rather than all of them) is what keeps a dropdown from
   * hiding the alternatives you might switch to.
   */
  const facetOptions = useMemo(() => {
    const result = {} as Record<FacetKey, string[]>;

    FACET_ORDER.forEach((key, index) => {
      const above = FACET_ORDER.slice(0, index);
      const candidates = variants.filter((v) =>
        above.every((k) => facets[k] === undefined || facetValue(v, k) === facets[k])
      );

      result[key] = Array.from(
        new Set(candidates.map((v) => facetValue(v, key)).filter((s) => s !== ""))
      ).sort();
    });

    return result;
  }, [variants, facets]);

  /** Variants matching every chosen facet. One left means the vehicle is fully specified. */
  const matching = useMemo(
    () =>
      variants.filter((v) =>
        FACET_ORDER.every((k) => facets[k] === undefined || facetValue(v, k) === facets[k])
      ),
    [variants, facets]
  );

  // A facet with a single possible value isn't a decision — fill it in.
  useEffect(() => {
    const auto: Facets = {};
    for (const key of FACET_ORDER) {
      const options = facetOptions[key];
      if (facets[key] === undefined && options?.length === 1) auto[key] = options[0];
    }
    if (Object.keys(auto).length > 0) setFacets((f) => ({ ...f, ...auto }));
  }, [facetOptions, facets]);

  // Publish the resolved variant upward once exactly one remains — but never while
  // hydrating, when "none resolved" only means "not loaded yet".
  useEffect(() => {
    if (hydratingVariantId) return;
    const resolved = matching.length === 1 ? matching[0].id : undefined;
    if (resolved !== value.variantId) onChange({ ...value, variantId: resolved });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [matching, hydratingVariantId]);

  const resetBelow = (key: FacetKey) => {
    const index = FACET_ORDER.indexOf(key);
    setFacets((f) => {
      const next: Facets = { ...f };
      FACET_ORDER.slice(index + 1).forEach((k) => delete next[k]);
      return next;
    });
  };

  const specifying = Boolean(modelId && value.year);

  return (
    <div className="space-y-4">
      {/* A banner rather than a replacement: one failed lookup shouldn't remove the
          fields the user has already filled in. */}
      {error && <p className="text-sm text-red-600">{error}</p>}

      <div className="grid grid-cols-2 gap-4">
        <Select
          id="make"
          label="Make"
          value={makeId}
          onChange={(e) => {
            setMakeId(e.target.value);
            setModelId("");
            setFacets({});
            onChange({ variantId: undefined, year: undefined, colourId: value.colourId });
          }}
          options={[
            { value: "", label: makes.length ? "Select make" : "Loading…" },
            ...makes.map((m) => ({ value: m.id, label: m.name })),
          ]}
        />

        <Select
          id="model"
          label="Model"
          value={modelId}
          disabled={!makeId}
          onChange={(e) => {
            setModelId(e.target.value);
            setFacets({});
            onChange({ variantId: undefined, year: undefined, colourId: value.colourId });
          }}
          options={[
            { value: "", label: !makeId ? "Select a make first" : "Select model" },
            ...models.map((m) => ({ value: m.id, label: m.name })),
          ]}
        />
      </div>

      <div className="grid grid-cols-2 gap-4">
        <Select
          id="year"
          label="Year"
          value={value.year ? String(value.year) : ""}
          disabled={!modelId}
          onChange={(e) => {
            setFacets({});
            onChange({
              ...value,
              year: e.target.value ? Number(e.target.value) : undefined,
              variantId: undefined,
            });
          }}
          options={[
            {
              value: "",
              label: !modelId ? "Select a model first" : years.length ? "Select year" : "No years catalogued",
            },
            ...years.map((y) => ({ value: String(y), label: String(y) })),
          ]}
        />

        <Select
          id="colour"
          label="Colour"
          value={value.colourId ?? ""}
          onChange={(e) => onChange({ ...value, colourId: e.target.value || null })}
          options={[
            { value: "", label: "Not specified" },
            ...colours.map((c) => ({ value: c.id, label: c.name })),
          ]}
        />
      </div>

      {/* Trim · Body · Engine · Transmission · Fuel — each narrows the set further. */}
      <div className="grid grid-cols-2 gap-4">
        {FACET_ORDER.map((key) => {
          const options = facetOptions[key] ?? [];
          const unavailable = specifying && options.length === 0;

          return (
            <Select
              key={key}
              id={key}
              label={FACET_LABELS[key]}
              value={facets[key] ?? ""}
              disabled={!specifying || unavailable}
              onChange={(e) => {
                const v = e.target.value;
                setFacets((f) => ({ ...f, [key]: v || undefined }));
                resetBelow(key);
              }}
              options={[
                {
                  value: "",
                  label: !specifying
                    ? "Select a year first"
                    : unavailable
                      ? "Not specified for this model"
                      : `Select ${FACET_LABELS[key].toLowerCase()}`,
                },
                ...options.map((o) => ({ value: o, label: o })),
              ]}
            />
          );
        })}
      </div>

      {/* Resolution feedback: the user needs to know whether they've landed on one vehicle. */}
      {specifying && variants.length === 0 && (
        <p className="text-sm text-amber-600">
          This model year isn&apos;t in the catalogue yet, so the vehicle can&apos;t be added.
          Ask an administrator to add its specifications.
        </p>
      )}

      {specifying && matching.length === 1 && (
        <p className="text-sm text-green-700 dark:text-green-400">
          {value.year} {makes.find((m) => m.id === makeId)?.name}{" "}
          {models.find((m) => m.id === modelId)?.name} — {matching[0].label}
        </p>
      )}

      {specifying && matching.length > 1 && (
        <p className="text-sm text-surface-500">
          {matching.length} specifications match — narrow it down using the fields above.
        </p>
      )}

      {specifying && variants.length > 0 && matching.length === 0 && (
        <p className="text-sm text-amber-600">
          No specification matches that combination. Clear a field and try again.
        </p>
      )}
    </div>
  );
}
