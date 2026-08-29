"use client";

import { useState } from "react";
import { useApi } from "@/hooks/use-api";
import { Button, Card, Modal, Input, PageHeader, Spinner } from "@/components/ui";
import { SettingsNav } from "@/components/settings-nav";
import { Plus, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";

interface Zone {
  id: string;
  name: string;
  color: string | null;
  capacity: number;
  isActive: boolean;
}

export default function SettingsZonesPage() {
  const { data: zones, isLoading } = useApi<Zone[]>("/api/zones");
  const [editZone, setEditZone] = useState<Zone | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const refresh = () => mutate("/api/zones");

  return (
    <>
      <PageHeader title="Settings" />
      <div className="flex gap-8">
        <SettingsNav />
        <div className="flex-1 max-w-2xl">
          <Card>
            <div className="flex items-center justify-between mb-5">
              <h2 className="font-semibold text-surface-800">Zones / Bays</h2>
              <Button size="sm" onClick={() => setShowCreate(true)}><Plus size={14} /> Add Zone</Button>
            </div>
            {isLoading ? (
              <Spinner />
            ) : !zones || zones.length === 0 ? (
              <p className="text-sm text-surface-400">No zones configured yet</p>
            ) : (
              <div className="space-y-2">
                {zones.map((z) => (
                  <div key={z.id} className="flex items-center justify-between p-3 rounded-lg bg-surface-50">
                    <div className="flex items-center gap-3">
                      <div className="w-4 h-4 rounded-full" style={{ backgroundColor: z.color ?? "#6b7280" }} />
                      <div>
                        <p className="text-sm font-medium text-surface-900">{z.name}</p>
                        <p className="text-xs text-surface-500">Capacity: {z.capacity} · {z.isActive ? "Active" : "Inactive"}</p>
                      </div>
                    </div>
                    <Button variant="ghost" size="sm" onClick={() => setEditZone(z)}><Pencil size={14} /></Button>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      {(showCreate || editZone) && (
        <ZoneModal
          zone={editZone}
          onClose={() => { setShowCreate(false); setEditZone(null); }}
          onSaved={() => { setShowCreate(false); setEditZone(null); refresh(); }}
        />
      )}
    </>
  );
}

function ZoneModal({ zone, onClose, onSaved }: { zone: Zone | null; onClose: () => void; onSaved: () => void }) {
  const isEdit = !!zone;
  const [form, setForm] = useState({
    name: zone?.name ?? "",
    color: zone?.color ?? "#f59e0b",
    capacity: String(zone?.capacity ?? 1),
    isActive: zone?.isActive ?? true,
  });
  const [loading, setLoading] = useState(false);

  const update = (f: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const payload = { name: form.name, color: form.color, capacity: parseInt(form.capacity), isActive: form.isActive };
      if (isEdit) {
        await fetcher.put(`/api/zones/${zone!.id}`, payload);
      } else {
        await fetcher.post("/api/zones", payload);
      }
      toast.success(isEdit ? "Zone updated" : "Zone created");
      onSaved();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={isEdit ? "Edit Zone" : "New Zone"}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={update("name")} placeholder="Bay 1" />
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <label className="block text-sm font-medium text-surface-700">Color</label>
            <input type="color" value={form.color} onChange={(e) => setForm((f) => ({ ...f, color: e.target.value }))} className="w-full h-10 rounded-lg border border-surface-300 cursor-pointer" />
          </div>
          <Input id="capacity" label="Capacity" type="number" min="1" required value={form.capacity} onChange={update("capacity")} />
        </div>
        {isEdit && (
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} className="rounded border-surface-300" />
            Active
          </label>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>{isEdit ? "Save" : "Create"}</Button>
        </div>
      </form>
    </Modal>
  );
}
