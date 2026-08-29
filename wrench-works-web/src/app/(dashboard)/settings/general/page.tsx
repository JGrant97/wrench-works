"use client";

import { useState, useEffect } from "react";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Input, Card, PageHeader, Spinner } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { SettingsNav } from "@/components/settings-nav";

interface BusinessInfo {
  id: string;
  name: string;
  address: string | null;
  phone: string | null;
  timezone: string;
  currency: string;
}

export default function SettingsGeneralPage() {
  const canManage = usePermission("settings.manage");
  const { data: biz, isLoading } = useApi<BusinessInfo>("/api/business");
  const [form, setForm] = useState({ name: "", address: "", phone: "", timezone: "Europe/London", currency: "GBP" });
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (biz) {
      setForm({
        name: biz.name,
        address: biz.address ?? "",
        phone: biz.phone ?? "",
        timezone: biz.timezone,
        currency: biz.currency,
      });
    }
  }, [biz]);

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await fetcher.put("/api/business", form);
      toast.success("Settings saved");
      mutate("/api/business");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  };

  if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;

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
                <Input id="currency" label="Currency" value={form.currency} onChange={update("currency")} disabled={!canManage} />
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
