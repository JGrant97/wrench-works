"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Input, Textarea, PageHeader, Spinner } from "@/components/ui";
import { formatDate, formatCurrency, JOB_STATUS_COLORS } from "@/lib/utils";
import { ArrowLeft, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";

interface VehicleDetail {
  id: string; customerId: string; customerName: string;
  make: string | null; model: string | null; year: number | null;
  registration: string | null; vin: string | null;
  engineType: string | null; fuelType: string | null; notes: string | null;
}

interface HistoryItem {
  jobId: string; title: string; status: string; createdAtUtc: string; laborTotal: number; partsTotal: number;
}

export default function VehicleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canManage = usePermission("vehicles.manage");
  const { data: vehicle, isLoading: vLoading } = useApi<VehicleDetail>(`/api/vehicles/${id}`);
  const { data: history, isLoading: hLoading } = useApi<HistoryItem[]>(`/api/vehicles/${id}/history`);
  const [showEdit, setShowEdit] = useState(false);

  if (vLoading || hLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
  if (!vehicle) return <p className="text-center text-surface-500 py-20">Vehicle not found</p>;

  const displayName = [vehicle.make, vehicle.model].filter(Boolean).join(" ") || "Unnamed Vehicle";

  return (
    <>
      <button onClick={() => router.back()} className="flex items-center gap-1 text-sm text-surface-500 hover:text-surface-700 mb-4">
        <ArrowLeft size={14} /> Back
      </button>

      <PageHeader title={displayName}
        description={<span><Link href={`/customers/${vehicle.customerId}`} className="text-brand-600 hover:underline dark:text-brand-400">{vehicle.customerName}</Link>{vehicle.year ? ` · ${vehicle.year}` : ""}{vehicle.registration ? ` · ${vehicle.registration}` : ""}</span> as unknown as string}
        actions={canManage ? <Button variant="secondary" onClick={() => setShowEdit(true)}><Pencil size={14} /> Edit</Button> : undefined}
      />

      <div className="grid grid-cols-3 gap-6">
        <Card className="col-span-1">
          <h2 className="font-semibold text-surface-800 mb-4">Details</h2>
          <dl className="space-y-3 text-sm">
            {([["Make", vehicle.make], ["Model", vehicle.model], ["Year", vehicle.year], ["Registration", vehicle.registration], ["VIN", vehicle.vin], ["Engine", vehicle.engineType], ["Fuel", vehicle.fuelType]] as [string, unknown][])
              .filter(([, val]) => val)
              .map(([label, val]) => (
                <div key={label}><dt className="text-surface-400">{label}</dt><dd className="font-medium text-surface-700">{String(val)}</dd></div>
              ))}
            {vehicle.notes && <div className="pt-3 border-t border-surface-100"><dt className="text-surface-400">Notes</dt><dd className="text-surface-600 whitespace-pre-wrap">{vehicle.notes}</dd></div>}
          </dl>
        </Card>

        <Card className="col-span-2">
          <h2 className="font-semibold text-surface-800 mb-4">Service History</h2>
          {!history || history.length === 0 ? <p className="text-sm text-surface-400">No service history</p> : (
            <div className="space-y-2">
              {history.map((h) => (
                <Link key={h.jobId} href={`/jobs/${h.jobId}`}>
                  <div className="flex items-center justify-between p-3 rounded-lg bg-surface-50 hover:bg-surface-100 transition-colors cursor-pointer">
                    <div><p className="text-sm font-medium text-surface-900">{h.title}</p><p className="text-xs text-surface-500">{formatDate(h.createdAtUtc)}</p></div>
                    <div className="flex items-center gap-3">
                      <span className="text-sm font-medium">{formatCurrency(h.laborTotal + h.partsTotal)}</span>
                      <Badge className={JOB_STATUS_COLORS[h.status]}>{h.status}</Badge>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </Card>
      </div>

      {showEdit && <EditVehicleModal vehicle={vehicle} onClose={() => setShowEdit(false)} onSaved={() => { setShowEdit(false); mutate(`/api/vehicles/${id}`); }} />}
    </>
  );
}

function EditVehicleModal({ vehicle, onClose, onSaved }: { vehicle: VehicleDetail; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    make: vehicle.make ?? "", model: vehicle.model ?? "", year: vehicle.year ? String(vehicle.year) : "",
    vin: vehicle.vin ?? "", registration: vehicle.registration ?? "",
    engineType: vehicle.engineType ?? "", fuelType: vehicle.fuelType ?? "", notes: vehicle.notes ?? "",
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setLoading(true);
    try {
      await fetcher.put(`/api/vehicles/${vehicle.id}`, {
        make: form.make || null, model: form.model || null, year: form.year ? parseInt(form.year) : null,
        vin: form.vin || null, registration: form.registration || null,
        engineType: form.engineType || null, fuelType: form.fuelType || null, notes: form.notes || null,
      });
      toast.success("Vehicle updated"); onSaved();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title="Edit Vehicle">
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <Input id="make" label="Make" value={form.make} onChange={u("make")} placeholder="Ford" />
          <Input id="model" label="Model" value={form.model} onChange={u("model")} placeholder="Focus" />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Input id="year" label="Year" type="number" value={form.year} onChange={u("year")} placeholder="2022" />
          <Input id="reg" label="Registration" value={form.registration} onChange={u("registration")} placeholder="AB12 CDE" />
        </div>
        <Input id="vin" label="VIN" value={form.vin} onChange={u("vin")} />
        <div className="grid grid-cols-2 gap-4">
          <Input id="engine" label="Engine Type" value={form.engineType} onChange={u("engineType")} placeholder="2.0L Diesel" />
          <Input id="fuel" label="Fuel Type" value={form.fuelType} onChange={u("fuelType")} placeholder="Diesel" />
        </div>
        <Textarea id="notes" label="Notes" value={form.notes} onChange={u("notes")} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
