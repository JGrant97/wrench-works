"use client";

import { useState } from "react";
import { mutate } from "swr";
import { Plus, Pencil, X } from "lucide-react";
import toast from "react-hot-toast";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Card, Modal, Input, PageHeader, Spinner, Badge } from "@/components/ui";
import { SettingsNav } from "@/components/settings-nav";
import { ErrorState } from "@/components/data-state";
import { RecordActions } from "@/components/record-actions";
import { fetcher } from "@/lib/fetcher";

/**
 * Tax rates the business configures for itself. See docs/tax.md for why the product does
 * not ship a rate table.
 *
 * Rates are stored as fractions (0.2) but entered and shown as percentages (20), because
 * nobody thinks in fractions and "0.2" in a box labelled % is the fastest way to charge
 * someone 0.2% instead of 20%.
 */

interface TaxRateComponent {
  id: string;
  name: string;
  rate: number;
  sortOrder: number;
}

interface TaxRate {
  id: string;
  name: string;
  rate: number;
  /** "Labour" | "Parts" | "Consumables". A rate with none applies to nothing. */
  categories: string[];
  isArchived: boolean;
  components: TaxRateComponent[];
}

/**
 * The categories a rate can cover. Mirrors the TaxCategory enum on the server — the list
 * lives there, and adding one is a deliberate product decision rather than per-tenant
 * configuration, so a hard-coded list here is honest rather than lazy.
 */
const CATEGORIES = [
  { value: "Labour", label: "Labour", hint: "Not taxable in many US states" },
  { value: "Parts", label: "Parts", hint: "Fitted components" },
  { value: "Consumables", label: "Consumables", hint: "Shop supplies, disposal levies" },
] as const;

const toPercent = (fraction: number) => +(fraction * 100).toFixed(4);
const toFraction = (percent: string) => +(Number(percent) / 100).toFixed(6);

export default function SettingsTaxPage() {
  const canManage = usePermission("settings.manage");
  const { data: rates, isLoading, error } = useApi<TaxRate[]>("/api/tax/rates");
  const [editing, setEditing] = useState<TaxRate | null>(null);
  const [creating, setCreating] = useState(false);

  const refresh = () => mutate("/api/tax/rates");

  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex gap-8">
        <SettingsNav />
        <div className="flex-1 max-w-3xl">
          <Card>
            <div className="flex items-center justify-between mb-5">
              <div>
                <h2 className="font-semibold text-surface-800">Tax rates</h2>
                <p className="text-sm text-surface-500 mt-0.5">
                  Applied automatically to new labour and parts lines.
                </p>
              </div>
              {canManage && (
                <Button size="sm" onClick={() => setCreating(true)}><Plus size={14} /> Add Rate</Button>
              )}
            </div>

            {isLoading ? (
              <Spinner />
            ) : error ? (
              <ErrorState error={error} onRetry={refresh} compact />
            ) : !rates || rates.length === 0 ? (
              <p className="text-sm text-surface-400">
                No tax rates configured — nothing is charged tax until you add one.
              </p>
            ) : (
              <div className="space-y-2">
                {rates.map((r) => (
                  <div key={r.id} className="flex items-center justify-between p-3 rounded-lg bg-surface-50">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <p className="text-sm font-medium text-surface-900">{r.name}</p>
                        <span className="text-sm font-mono text-surface-600">{toPercent(r.rate)}%</span>
                        {r.categories.map((c) => (
                          <Badge key={c} className="bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300">{c}</Badge>
                        ))}
                      </div>
                      {r.components.length > 0 && (
                        <p className="text-xs text-surface-500 mt-1">
                          {r.components.map((c) => `${c.name} ${toPercent(c.rate)}%`).join(" · ")}
                        </p>
                      )}
                      {r.categories.length === 0 && (
                        <p className="text-xs text-surface-400 mt-1">Not applied to anything</p>
                      )}
                    </div>
                    {canManage && (
                      <div className="flex items-center gap-1 shrink-0">
                        <Button variant="ghost" size="sm" onClick={() => setEditing(r)}><Pencil size={14} /></Button>
                        <RecordActions
                          resource="tax/rates"
                          id={r.id}
                          label="tax rate"
                          archived={r.isArchived}
                          canManage={canManage}
                          onChanged={refresh}
                          afterDelete={refresh}
                        />
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      {creating && <RateModal onClose={() => setCreating(false)} onSaved={() => { setCreating(false); refresh(); }} />}
      {editing && <RateModal rate={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); refresh(); }} />}
    </>
  );
}

function RateModal({ rate, onClose, onSaved }: { rate?: TaxRate; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    name: rate?.name ?? "",
    percent: rate ? String(toPercent(rate.rate)) : "",
  });
  const [categories, setCategories] = useState<string[]>(
    rate?.categories ?? ["Labour", "Parts"]
  );

  const toggleCategory = (value: string) =>
    setCategories((c) => (c.includes(value) ? c.filter((x) => x !== value) : [...c, value]));
  const [components, setComponents] = useState<{ name: string; percent: string }[]>(
    rate?.components.map((c) => ({ name: c.name, percent: String(toPercent(c.rate)) })) ?? []
  );
  const [loading, setLoading] = useState(false);

  const componentTotal = components.reduce((sum, c) => sum + (Number(c.percent) || 0), 0);
  const componentsMismatch =
    components.length > 0 && Math.abs(componentTotal - Number(form.percent)) > 0.0001;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const body = {
        name: form.name,
        rate: toFraction(form.percent),
        categories,
        components: components
          .filter((c) => c.name.trim())
          .map((c, i) => ({ name: c.name, rate: toFraction(c.percent), sortOrder: i })),
      };

      if (rate) await fetcher.put(`/api/tax/rates/${rate.id}`, body);
      else await fetcher.post("/api/tax/rates", body);

      toast.success(rate ? "Rate updated" : "Rate added");
      onSaved();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={rate ? "Edit tax rate" : "Add tax rate"} wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Input
            id="name"
            label="Name"
            required
            placeholder="VAT Standard"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
          <Input
            id="percent"
            label="Rate (%)"
            type="number"
            step="0.0001"
            min="0"
            max="100"
            required
            placeholder="20"
            value={form.percent}
            onChange={(e) => setForm((f) => ({ ...f, percent: e.target.value }))}
          />
        </div>

        <div className="space-y-2">
          <p className="text-sm font-medium text-surface-700">Apply to</p>
          <div className="space-y-1.5">
            {CATEGORIES.map((c) => (
              <label key={c.value} className="flex items-start gap-2 text-sm text-surface-700">
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={categories.includes(c.value)}
                  onChange={() => toggleCategory(c.value)}
                />
                <span>
                  {c.label}
                  <span className="block text-xs text-surface-500">{c.hint}</span>
                </span>
              </label>
            ))}
          </div>
          {/* Each category belongs to exactly one rate, so ticking it here takes it from
              whichever rate held it. Saying so avoids it looking like a bug. */}
          <p className="text-xs text-surface-500">
            A category can only use one rate — ticking it here moves it from any other rate.
          </p>
        </div>

        <div className="pt-2 border-t border-surface-100">
          <div className="flex items-center justify-between mb-2">
            <div>
              <p className="text-sm font-medium text-surface-700">Jurisdiction breakdown</p>
              <p className="text-xs text-surface-500">
                Optional. Shown on invoices; the total charged always uses the rate above.
              </p>
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setComponents((c) => [...c, { name: "", percent: "" }])}
            >
              <Plus size={14} /> Add
            </Button>
          </div>

          {components.map((c, i) => (
            <div key={i} className="flex items-center gap-2 mb-2">
              <Input
                id={`comp-name-${i}`}
                placeholder="NY State"
                value={c.name}
                onChange={(e) =>
                  setComponents((list) => list.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))
                }
              />
              <Input
                id={`comp-rate-${i}`}
                type="number"
                step="0.0001"
                min="0"
                placeholder="4"
                value={c.percent}
                onChange={(e) =>
                  setComponents((list) => list.map((x, j) => (j === i ? { ...x, percent: e.target.value } : x)))
                }
              />
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setComponents((list) => list.filter((_, j) => j !== i))}
              >
                <X size={14} />
              </Button>
            </div>
          ))}

          {/* A warning, not a block: components are for display, and a business may have a
              legitimate reason for them not to sum exactly. */}
          {componentsMismatch && (
            <p className="text-xs text-amber-600">
              Components total {componentTotal.toFixed(4)}%, but the rate is {form.percent}%.
              Invoices will charge {form.percent}%.
            </p>
          )}
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>{rate ? "Save Changes" : "Add Rate"}</Button>
        </div>
      </form>
    </Modal>
  );
}
