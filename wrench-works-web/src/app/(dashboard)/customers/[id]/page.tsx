"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Input, Textarea, PageHeader, Spinner } from "@/components/ui";
import { formatDate, formatCurrency, JOB_STATUS_COLORS, statusLabel } from "@/lib/utils";
import { ArrowLeft, Plus, Car, Phone, Mail, MapPin, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";
import { VehicleCataloguePicker, type CatalogueSelection } from "@/components/vehicle-catalogue-picker";

interface Vehicle {
  id: string;
  displayName: string;
  year: number | null;
  registration: string | null;
  colourName: string | null;
}

interface CustomerDetail {
  id: string;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  notes: string | null;
  vehicles: Vehicle[];
  recentJobs: { id: string; title: string; status: string; vehicleDisplay: string | null; total: number; createdAtUtc: string }[];
}

export default function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canManage = usePermission("customers.manage");
  const canManageVehicles = usePermission("vehicles.manage");
  const { data: customer, isLoading, error } = useApi<CustomerDetail>(`/api/customers/${id}`);
  const [showAddVehicle, setShowAddVehicle] = useState(false);
  const [showEditCustomer, setShowEditCustomer] = useState(false);

  if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
  // Before the empty branch: a failed load is not a missing customer.
  if (error) return <ErrorState error={error} onRetry={() => mutate(`/api/customers/${id}`)} />;
  if (!customer) return <p className="text-center text-surface-500 py-20">Customer not found</p>;

  return (
    <>
      <button onClick={() => router.back()} className="flex items-center gap-1 text-sm text-surface-500 hover:text-surface-700 mb-4">
        <ArrowLeft size={14} /> Back
      </button>

      <PageHeader title={customer.name} actions={canManage ? <Button variant="secondary" onClick={() => setShowEditCustomer(true)}><Pencil size={14} /> Edit</Button> : undefined} />

      <div className="grid grid-cols-3 gap-6">
        {/* Info */}
        <Card className="col-span-1">
          <h2 className="font-semibold text-surface-800 mb-4">Contact</h2>
          <div className="space-y-3 text-sm">
            {customer.phone && (
              <div className="flex items-center gap-2 text-surface-600"><Phone size={14} /> {customer.phone}</div>
            )}
            {customer.email && (
              <div className="flex items-center gap-2 text-surface-600"><Mail size={14} /> {customer.email}</div>
            )}
            {customer.address && (
              <div className="flex items-center gap-2 text-surface-600"><MapPin size={14} /> {customer.address}</div>
            )}
            {customer.notes && (
              <div className="pt-3 border-t border-surface-100">
                <p className="text-xs text-surface-400 mb-1">Notes</p>
                <p className="text-surface-600 whitespace-pre-wrap">{customer.notes}</p>
              </div>
            )}
          </div>
        </Card>

        <div className="col-span-2 space-y-6">
          {/* Vehicles */}
          <Card>
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-semibold text-surface-800">Vehicles</h2>
              {canManageVehicles && (
                <Button variant="ghost" size="sm" onClick={() => setShowAddVehicle(true)}>
                  <Plus size={14} /> Add Vehicle
                </Button>
              )}
            </div>
            {customer.vehicles.length === 0 ? (
              <p className="text-sm text-surface-400">No vehicles</p>
            ) : (
              <div className="space-y-2">
                {customer.vehicles.map((v) => (
                  <Link key={v.id} href={`/vehicles/${v.id}`}>
                    <div className="flex items-center gap-3 p-3 rounded-lg bg-surface-50 hover:bg-surface-100 transition-colors cursor-pointer">
                      <Car size={16} className="text-surface-400" />
                      <div>
                        <p className="text-sm font-medium text-surface-900">{v.displayName}</p>
                        <p className="text-xs text-surface-500">
                          {[v.registration, v.colourName].filter(Boolean).join(" · ")}
                        </p>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </Card>

          {/* Recent Jobs */}
          <Card>
            <h2 className="font-semibold text-surface-800 mb-4">Recent Jobs</h2>
            {customer.recentJobs?.length === 0 ? (
              <p className="text-sm text-surface-400">No jobs yet</p>
            ) : (
              <div className="space-y-2">
                {customer.recentJobs?.map((j) => (
                  <Link key={j.id} href={`/jobs/${j.id}`}>
                    <div className="flex items-center justify-between p-3 rounded-lg bg-surface-50 hover:bg-surface-100 transition-colors cursor-pointer">
                      <div>
                        <p className="text-sm font-medium text-surface-900">{j.title}</p>
                        <p className="text-xs text-surface-500">
                          {[j.vehicleDisplay, formatDate(j.createdAtUtc)].filter(Boolean).join(" · ")}
                        </p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm font-medium">{formatCurrency(j.total)}</span>
                        <Badge className={JOB_STATUS_COLORS[j.status]}>{statusLabel(j.status)}</Badge>
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </Card>
        </div>
      </div>

      {showAddVehicle && (
        <AddVehicleModal
          customerId={id}
          onClose={() => setShowAddVehicle(false)}
          onAdded={() => { setShowAddVehicle(false); mutate(`/api/customers/${id}`); }}
        />
      )}

      {showEditCustomer && (
        <EditCustomerModal
          customer={customer}
          onClose={() => setShowEditCustomer(false)}
          onSaved={() => { setShowEditCustomer(false); mutate(`/api/customers/${id}`); }}
        />
      )}
    </>
  );
}

function AddVehicleModal({ customerId, onClose, onAdded }: { customerId: string; onClose: () => void; onAdded: () => void }) {
  const [selection, setSelection] = useState<Partial<CatalogueSelection>>({ colourId: null });
  const [form, setForm] = useState({ registration: "", vin: "" });
  const [loading, setLoading] = useState(false);

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  // The vehicle IS its catalogue entry — without a variant there is nothing to save.
  const canSubmit = Boolean(selection.variantId && selection.year);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) {
      toast.error("Choose the vehicle's make, model, year and specification");
      return;
    }
    setLoading(true);
    try {
      await fetcher.post("/api/vehicles", {
        customerId,
        variantId: selection.variantId,
        year: selection.year,
        colourId: selection.colourId ?? null,
        registration: form.registration || null,
        vin: form.vin || null,
      });
      toast.success("Vehicle added");
      onAdded();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Add Vehicle" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <VehicleCataloguePicker value={selection} onChange={setSelection} />

        <div className="grid grid-cols-2 gap-4 pt-2 border-t border-surface-100">
          <Input id="registration" label="Registration" value={form.registration} onChange={update("registration")} placeholder="AB12 CDE" />
          <Input id="vin" label="VIN" value={form.vin} onChange={update("vin")} placeholder="Optional" />
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!canSubmit}>Add Vehicle</Button>
        </div>
      </form>
    </Modal>
  );
}

function EditCustomerModal({ customer, onClose, onSaved }: { customer: { id: string; name: string; phone: string | null; email: string | null; address: string | null; notes: string | null }; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    name: customer.name, phone: customer.phone ?? "", email: customer.email ?? "",
    address: customer.address ?? "", notes: customer.notes ?? "",
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setLoading(true);
    try {
      await fetcher.put(`/api/customers/${customer.id}`, {
        name: form.name, phone: form.phone || null, email: form.email || null,
        address: form.address || null, notes: form.notes || null,
      });
      toast.success("Customer updated"); onSaved();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title="Edit Customer">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={u("name")} />
        <div className="grid grid-cols-2 gap-4">
          <Input id="phone" label="Phone" type="tel" value={form.phone} onChange={u("phone")} />
          <Input id="email" label="Email" type="email" value={form.email} onChange={u("email")} />
        </div>
        <Input id="address" label="Address" value={form.address} onChange={u("address")} />
        <Textarea id="notes" label="Notes" value={form.notes} onChange={u("notes")} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
