"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useApi } from "@/hooks/use-api";
import { useCurrency } from "@/hooks/use-currency";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Input, Textarea, PageHeader, Spinner } from "@/components/ui";
import { formatDate, JOB_STATUS_COLORS, statusLabel } from "@/lib/utils";
import { ArrowLeft, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";
import { RecordActions } from "@/components/record-actions";
import { VehicleCataloguePicker, type CatalogueSelection } from "@/components/vehicle-catalogue-picker";

interface VehicleDetail {
  id: string; customerId: string; customerName: string;
  // Null on a vehicle created before the catalogue existed: it has only the deprecated
  // free-text make/model. The details list filters falsy values out, so those rows simply
  // show fewer fields, and Edit requires re-picking from the catalogue before saving.
  displayName: string; variantId: string | null; year: number | null;
  makeName: string | null; modelName: string | null;
  trim: string | null; bodyStyle: string | null;
  engineDisplacementL: number | null; fuelType: string | null; transmission: string | null;
  colourId: string | null; colourName: string | null;
  registration: string | null; vin: string | null; notes: string | null;
}

interface HistoryItem {
  jobId: string; title: string; status: string; createdAtUtc: string; laborTotal: number; partsTotal: number;
}

export default function VehicleDetailPage() {
  const { format } = useCurrency();
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canManage = usePermission("vehicles.manage");
  const { data: vehicle, isLoading: vLoading, error: vError } = useApi<VehicleDetail>(`/api/vehicles/${id}`);
  const { data: history, isLoading: hLoading, error: hError } = useApi<HistoryItem[]>(`/api/vehicles/${id}/history`);
  const [showEdit, setShowEdit] = useState(false);

  if (vLoading || hLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
  // Before the empty branch: a 500 on this vehicle is not the same as it not existing.
  if (vError || hError) {
    return (
      <ErrorState
        error={vError ?? hError}
        onRetry={() => { mutate(`/api/vehicles/${id}`); mutate(`/api/vehicles/${id}/history`); }}
      />
    );
  }
  if (!vehicle) return <p className="text-center text-surface-500 py-20">Vehicle not found</p>;

  const displayName = vehicle.displayName;

  return (
    <>
      <button onClick={() => router.back()} className="flex items-center gap-1 text-sm text-surface-500 hover:text-surface-700 mb-4">
        <ArrowLeft size={14} /> Back
      </button>

      <PageHeader title={displayName}
        description={<span><Link href={`/customers/${vehicle.customerId}`} className="text-brand-600 hover:underline dark:text-brand-400">{vehicle.customerName}</Link>{vehicle.year ? ` · ${vehicle.year}` : ""}{vehicle.registration ? ` · ${vehicle.registration}` : ""}</span> as unknown as string}
        actions={
          <div className="flex items-center gap-2">
            {canManage && (
              <Button variant="secondary" onClick={() => setShowEdit(true)}>
                <Pencil size={14} /> Edit
              </Button>
            )}
            <RecordActions
              resource="vehicles"
              id={id}
              label="vehicle"
              canManage={canManage}
              onChanged={() => mutate(`/api/vehicles/${id}`)}
              afterDelete={() => router.push(`/customers/${vehicle.customerId}`)}
            />
          </div>
        }
      />

      <div className="grid grid-cols-3 gap-6">
        <Card className="col-span-1">
          <h2 className="font-semibold text-surface-800 mb-4">Details</h2>
          <dl className="space-y-3 text-sm">
            {([["Make", vehicle.makeName], ["Model", vehicle.modelName], ["Year", vehicle.year], ["Trim", vehicle.trim], ["Body", vehicle.bodyStyle], ["Engine", vehicle.engineDisplacementL ? `${vehicle.engineDisplacementL.toFixed(1)}L` : null], ["Fuel", vehicle.fuelType], ["Transmission", vehicle.transmission], ["Colour", vehicle.colourName], ["Registration", vehicle.registration], ["VIN", vehicle.vin]] as [string, unknown][])
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
                      <span className="text-sm font-medium">{format(h.laborTotal + h.partsTotal)}</span>
                      <Badge className={JOB_STATUS_COLORS[h.status]}>{statusLabel(h.status)}</Badge>
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
  const [selection, setSelection] = useState<Partial<CatalogueSelection>>({
    // undefined, not null — the picker treats "no variant to hydrate from" as undefined,
    // and a legacy vehicle has to be re-picked from the catalogue before it can be saved.
    variantId: vehicle.variantId ?? undefined,
    year: vehicle.year ?? undefined,
    colourId: vehicle.colourId,
  });
  const [form, setForm] = useState({
    vin: vehicle.vin ?? "", registration: vehicle.registration ?? "", notes: vehicle.notes ?? "",
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((p) => ({ ...p, [f]: e.target.value }));

  const canSubmit = Boolean(selection.variantId && selection.year);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) { toast.error("Choose the vehicle's make, model, year and specification"); return; }
    setLoading(true);
    try {
      await fetcher.put(`/api/vehicles/${vehicle.id}`, {
        variantId: selection.variantId,
        year: selection.year,
        colourId: selection.colourId ?? null,
        vin: form.vin || null, registration: form.registration || null, notes: form.notes || null,
      });
      toast.success("Vehicle updated"); onSaved();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title="Edit Vehicle" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <VehicleCataloguePicker value={selection} onChange={setSelection} />

        <div className="grid grid-cols-2 gap-4 pt-2 border-t border-surface-100">
          <Input id="reg" label="Registration" value={form.registration} onChange={u("registration")} placeholder="AB12 CDE" />
          <Input id="vin" label="VIN" value={form.vin} onChange={u("vin")} />
        </div>
        <Textarea id="notes" label="Notes" value={form.notes} onChange={u("notes")} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!canSubmit}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
