"use client";

import { useState, useEffect } from "react";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Input, Card, PageHeader, Spinner, Select } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";
import { SUPPORTED_CURRENCIES } from "@/lib/currency";
import { SettingsNav } from "@/components/settings-nav";

interface BusinessInfo {
  id: string;
  name: string;
  address: string | null;
  phone: string | null;
  timezone: string;
  currency: string;
  pricesIncludeTax: boolean;
  taxRegistrationNumber: string | null;
  taxLabel: string;
}

export default function SettingsGeneralPage() {
  const canManage = usePermission("settings.manage");
  const { data: biz, isLoading, error } = useApi<BusinessInfo>("/api/business");
  const [form, setForm] = useState({ name: "", address: "", phone: "", timezone: "Europe/London", currency: "GBP", pricesIncludeTax: false, taxRegistrationNumber: "", taxLabel: "Tax" });
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (biz) {
      setForm({
        name: biz.name,
        address: biz.address ?? "",
        phone: biz.phone ?? "",
        timezone: biz.timezone,
        currency: biz.currency,
        pricesIncludeTax: biz.pricesIncludeTax,
        taxRegistrationNumber: biz.taxRegistrationNumber ?? "",
        taxLabel: biz.taxLabel || "Tax",
      });
    }
  }, [biz]);

  // Widened to accept a select: the currency field is a dropdown now.
  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await fetcher.put("/api/business", form);

      // Currency lives in the ww_user cookie so every page can format money without a
      // lookup — which means changing it here has no effect until that cookie is rewritten.
      // /api/auth/refresh re-issues the token and both cookies from the server's own copy,
      // so the new symbol applies immediately instead of after the 24h expiry or a logout.
      await fetcher.post("/api/auth/refresh", {});

      toast.success("Settings saved");
      mutate("/api/business");

      // The currency is read at render time from the refreshed cookie, so the pages already
      // rendered with the old symbol need re-rendering. router.refresh() alone would not
      // re-run the client components holding it in the Zustand store.
      window.location.reload();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
  // Without this the form renders blank and a save would overwrite real settings with empties.
  if (error) return <ErrorState error={error} onRetry={() => mutate("/api/business")} />;

  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex gap-8">
        <SettingsNav />
        <div className="flex-1 max-w-2xl">
          <Card>
            <h2 className="font-semibold text-surface-800 mb-5">Workshop Details</h2>
            <form onSubmit={handleSave} className="space-y-4">
              <Input id="name" label="Workshop Name" required value={form.name} onChange={update("name")} disabled={!canManage} />
              <Input id="phone" label="Phone" value={form.phone} onChange={update("phone")} disabled={!canManage} />
              <Input id="address" label="Address" value={form.address} onChange={update("address")} disabled={!canManage} />
              <div className="grid grid-cols-2 gap-4">
                <Input id="timezone" label="Timezone" value={form.timezone} onChange={update("timezone")} disabled={!canManage} />
                <Select
                  id="currency"
                  label="Currency"
                  value={form.currency}
                  onChange={update("currency")}
                  disabled={!canManage}
                  options={SUPPORTED_CURRENCIES.map((c) => ({ value: c.code, label: c.label }))}
                />
              </div>

              <div className="pt-4 border-t border-surface-100 space-y-4">
                <div>
                  <h3 className="text-sm font-medium text-surface-700">Tax</h3>
                  <p className="text-xs text-surface-500 mt-0.5">
                    Rates are configured under <span className="font-medium">Settings → Tax</span>.
                  </p>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <Input
                    id="taxLabel"
                    label="What you call it"
                    placeholder="VAT, Sales Tax, GST"
                    value={form.taxLabel}
                    onChange={update("taxLabel")}
                    disabled={!canManage}
                  />
                  <Input
                    id="taxRegistrationNumber"
                    label="Registration number"
                    placeholder="VAT number / EIN"
                    value={form.taxRegistrationNumber}
                    onChange={update("taxRegistrationNumber")}
                    disabled={!canManage}
                  />
                </div>

                <label className="flex items-start gap-2 text-sm text-surface-700">
                  <input
                    type="checkbox"
                    className="mt-0.5"
                    checked={form.pricesIncludeTax}
                    disabled={!canManage}
                    onChange={(e) => setForm((f) => ({ ...f, pricesIncludeTax: e.target.checked }))}
                  />
                  <span>
                    My prices already include tax
                    {/* This changes the arithmetic, not just the display — worth saying so,
                        because getting it wrong silently over- or under-charges. */}
                    <span className="block text-xs text-surface-500">
                      Tax is divided out of the price you enter rather than added on top.
                      Common for UK consumer pricing; rare in the US.
                    </span>
                  </span>
                </label>
              </div>

              {canManage && (
                <div className="pt-2">
                  <Button type="submit" loading={saving}>Save Changes</Button>
                </div>
              )}
            </form>
          </Card>
        </div>
      </div>
    </>
  );
}
