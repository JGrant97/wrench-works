"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useApi, useMutation } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Input, Textarea, Select, PageHeader, Spinner } from "@/components/ui";
import { formatCurrency, formatDate, formatDateTime, JOB_STATUS_COLORS } from "@/lib/utils";
import { ArrowLeft, Plus, Trash2, ArrowRightCircle, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import Link from "next/link";

interface LaborLine {
  id: string;
  description: string;
  hours: number;
  rate: number;
  total: number;
}

interface PartLine {
  id: string;
  inventoryItemId: string;
  itemName: string;
  sku: string | null;
  quantity: number;
  unitPrice: number;
  total: number;
}

interface JobDetail {
  id: string;
  title: string;
  status: string;
  priority: string;
  customerId: string;
  customerName: string;
  vehicleId: string;
  vehicleDisplay: string;
  internalNotes: string | null;
  customerNotes: string | null;
  scheduledStartUtc: string | null;
  scheduledEndUtc: string | null;
  createdAtUtc: string;
  laborLines: LaborLine[];
  partLines: PartLine[];
  laborTotal: number;
  partsTotal: number;
  grandTotal: number;
}

const STATUS_TRANSITIONS: Record<string, { value: string; label: string }[]> = {
  Draft: [
    { value: "Scheduled", label: "Schedule" },
    { value: "Closed", label: "Close" },
  ],
  Scheduled: [
    { value: "InProgress", label: "Start Work" },
    { value: "Closed", label: "Close" },
  ],
  InProgress: [
    { value: "WaitingParts", label: "Waiting Parts" },
    { value: "Completed", label: "Complete" },
  ],
  WaitingParts: [
    { value: "InProgress", label: "Resume Work" },
  ],
  Completed: [
    { value: "Invoiced", label: "Mark Invoiced" },
  ],
  Invoiced: [
    { value: "Closed", label: "Close" },
  ],
};

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canEdit = usePermission("jobs.edit");
  const { data: job, isLoading } = useApi<JobDetail>(`/api/jobs/${id}`);
  const [showAddLabor, setShowAddLabor] = useState(false);
  const [showAddPart, setShowAddPart] = useState(false);
  const [showEditJob, setShowEditJob] = useState(false);

  const refreshJob = () => mutate(`/api/jobs/${id}`);

  const closedStatuses = ["Closed", "Completed", "Invoiced"];
  const isEditable = canEdit && job != null && !closedStatuses.includes(job.status);

  const updateStatus = async (newStatus: string) => {
    try {
      await fetcher.patch(`/api/jobs/${id}/status`, { status: newStatus });
      toast.success(`Job ${newStatus.toLowerCase()}`);
      refreshJob();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to update status");
    }
  };

  const removeLabor = async (lineId: string) => {
    try {
      await fetcher.delete(`/api/jobs/${id}/labor/${lineId}`);
      toast.success("Labor line removed");
      refreshJob();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to remove");
    }
  };

  const removePart = async (lineId: string) => {
    try {
      await fetcher.delete(`/api/jobs/${id}/parts/${lineId}`);
      toast.success("Part removed");
      refreshJob();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to remove");
    }
  };

  if (isLoading) return <div className="flex justify-center py-20"><Spinner /></div>;
  if (!job) return <p className="text-center text-surface-500 py-20">Job not found</p>;

  const transitions = STATUS_TRANSITIONS[job.status] ?? [];

  return (
    <>
      <button onClick={() => router.back()} className="flex items-center gap-1 text-sm text-surface-500 hover:text-surface-700 mb-4">
        <ArrowLeft size={14} /> Back to jobs
      </button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-surface-900 font-display">{job.title}</h1>
            <Badge className={JOB_STATUS_COLORS[job.status]}>{job.status}</Badge>
          </div>
          <p className="text-sm text-surface-500 mt-1">
            <Link href={`/customers/${job.customerId}`} className="hover:text-brand-600">{job.customerName}</Link>
            {" · "}
            <Link href={`/vehicles/${job.vehicleId}`} className="hover:text-brand-600">{job.vehicleDisplay}</Link>
            {" · Created "}
            {formatDate(job.createdAtUtc)}
          </p>
        </div>
        {(isEditable || (canEdit && transitions.length > 0)) && (
          <div className="flex gap-2">
            {isEditable && (
              <Button variant="secondary" size="sm" onClick={() => setShowEditJob(true)}>
                <Pencil size={14} /> Edit
              </Button>
            )}
            {canEdit && transitions.map((t) => (
              <Button key={t.value} variant={t.value === "Closed" ? "ghost" : "primary"} size="sm" onClick={() => updateStatus(t.value)}>
                <ArrowRightCircle size={14} /> {t.label}
              </Button>
            ))}
          </div>
        )}
      </div>

      <div className="grid grid-cols-3 gap-6 mb-6">
        <Card className="col-span-2">
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div className="col-span-2 flex items-center justify-between">
              <div>
                <p className="text-surface-400">Scheduled</p>
                <p className="font-medium">
                  {job.scheduledStartUtc
                    ? `${formatDateTime(job.scheduledStartUtc)} — ${formatDateTime(job.scheduledEndUtc!)}`
                    : "Not scheduled"}
                </p>
              </div>
              {isEditable && (
                <Button variant="ghost" size="sm" onClick={() => setShowEditJob(true)}>
                  Reschedule
                </Button>
              )}
            </div>
            <div>
              <p className="text-surface-400">Priority</p>
              <p className="font-medium capitalize">{job.priority}</p>
            </div>
            {job.internalNotes && (
              <div className="col-span-2">
                <p className="text-surface-400">Internal Notes</p>
                <p className="font-medium whitespace-pre-wrap">{job.internalNotes}</p>
              </div>
            )}
            {job.customerNotes && (
              <div className="col-span-2">
                <p className="text-surface-400">Customer Notes</p>
                <p className="font-medium whitespace-pre-wrap">{job.customerNotes}</p>
              </div>
            )}
          </div>
        </Card>

        <Card>
          <p className="text-sm text-surface-400 mb-3">Total</p>
          <p className="text-3xl font-bold text-surface-900 font-display">{formatCurrency(job.grandTotal)}</p>
          <div className="mt-3 space-y-1 text-sm">
            <div className="flex justify-between"><span className="text-surface-500">Labor</span><span>{formatCurrency(job.laborTotal)}</span></div>
            <div className="flex justify-between"><span className="text-surface-500">Parts</span><span>{formatCurrency(job.partsTotal)}</span></div>
          </div>
        </Card>
      </div>

      {/* Labor lines */}
      <Card className="mb-6">
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-semibold text-surface-800">Labor</h2>
          {isEditable && (
            <Button variant="ghost" size="sm" onClick={() => setShowAddLabor(true)}>
              <Plus size={14} /> Add Labor
            </Button>
          )}
        </div>
        {job.laborLines.length === 0 ? (
          <p className="text-sm text-surface-400">No labor lines</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-surface-400 border-b border-surface-100">
                <th className="pb-2 font-medium">Description</th>
                <th className="pb-2 font-medium text-right">Hours</th>
                <th className="pb-2 font-medium text-right">Rate</th>
                <th className="pb-2 font-medium text-right">Total</th>
                {isEditable && <th className="pb-2 w-10" />}
              </tr>
            </thead>
            <tbody>
              {job.laborLines.map((line) => (
                <tr key={line.id} className="border-b border-surface-50">
                  <td className="py-2">{line.description}</td>
                  <td className="py-2 text-right">{line.hours}</td>
                  <td className="py-2 text-right">{formatCurrency(line.rate)}</td>
                  <td className="py-2 text-right font-medium">{formatCurrency(line.total)}</td>
                  {isEditable && (
                    <td className="py-2">
                      <button onClick={() => removeLabor(line.id)} className="text-surface-300 hover:text-red-500 transition-colors">
                        <Trash2 size={14} />
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Part lines */}
      <Card>
        <div className="flex items-center justify-between mb-4">
          <h2 className="font-semibold text-surface-800">Parts</h2>
          {isEditable && (
            <Button variant="ghost" size="sm" onClick={() => setShowAddPart(true)}>
              <Plus size={14} /> Add Part
            </Button>
          )}
        </div>
        {job.partLines.length === 0 ? (
          <p className="text-sm text-surface-400">No parts</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-surface-400 border-b border-surface-100">
                <th className="pb-2 font-medium">Part</th>
                <th className="pb-2 font-medium text-right">Qty</th>
                <th className="pb-2 font-medium text-right">Unit Price</th>
                <th className="pb-2 font-medium text-right">Total</th>
                {isEditable && <th className="pb-2 w-10" />}
              </tr>
            </thead>
            <tbody>
              {job.partLines.map((line) => (
                <tr key={line.id} className="border-b border-surface-50">
                  <td className="py-2">{line.itemName}</td>
                  <td className="py-2 text-right">{line.quantity}</td>
                  <td className="py-2 text-right">{formatCurrency(line.unitPrice)}</td>
                  <td className="py-2 text-right font-medium">{formatCurrency(line.total)}</td>
                  {isEditable && (
                    <td className="py-2">
                      <button onClick={() => removePart(line.id)} className="text-surface-300 hover:text-red-500 transition-colors">
                        <Trash2 size={14} />
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {/* Add Labor Modal */}
      {showAddLabor && (
        <AddLaborModal
          jobId={id}
          onClose={() => setShowAddLabor(false)}
          onAdded={() => { setShowAddLabor(false); refreshJob(); }}
        />
      )}

      {/* Add Part Modal */}
      {showAddPart && (
        <AddPartModal
          jobId={id}
          onClose={() => setShowAddPart(false)}
          onAdded={() => { setShowAddPart(false); refreshJob(); }}
        />
      )}

      {showEditJob && (
        <EditJobModal
          job={job}
          onClose={() => setShowEditJob(false)}
          onSaved={() => { setShowEditJob(false); refreshJob(); }}
        />
      )}
    </>
  );
}

function AddLaborModal({ jobId, onClose, onAdded }: { jobId: string; onClose: () => void; onAdded: () => void }) {
  const [form, setForm] = useState({ description: "", hours: "", rate: "" });
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.post(`/api/jobs/${jobId}/labor`, {
        description: form.description,
        hours: parseFloat(form.hours),
        rate: parseFloat(form.rate),
      });
      toast.success("Labor added");
      onAdded();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Add Labor">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="desc" label="Description" required value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} placeholder="e.g. Brake pad replacement" />
        <div className="grid grid-cols-2 gap-4">
          <Input id="hours" label="Hours" type="number" step="0.25" min="0" required value={form.hours} onChange={(e) => setForm((f) => ({ ...f, hours: e.target.value }))} />
          <Input id="rate" label="Rate (£/hr)" type="number" step="0.01" min="0" required value={form.rate} onChange={(e) => setForm((f) => ({ ...f, rate: e.target.value }))} />
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Add</Button>
        </div>
      </form>
    </Modal>
  );
}

function AddPartModal({ jobId, onClose, onAdded }: { jobId: string; onClose: () => void; onAdded: () => void }) {
  const [search, setSearch] = useState("");
  const [selectedItem, setSelectedItem] = useState<{ id: string; name: string; sellPrice: number; quantityOnHand: number } | null>(null);
  const [quantity, setQuantity] = useState("1");
  const [loading, setLoading] = useState(false);

  const { data: items } = useApi<{ items: { id: string; name: string; sellPrice: number; quantityOnHand: number }[] }>(
    search.length >= 2 ? `/api/inventory/items?search=${encodeURIComponent(search)}` : null
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedItem) return;
    setLoading(true);
    try {
      await fetcher.post(`/api/jobs/${jobId}/parts`, {
        inventoryItemId: selectedItem.id,
        quantity: parseFloat(quantity),
        unitPrice: selectedItem.sellPrice,
      });
      toast.success("Part added");
      onAdded();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Add Part">
      <form onSubmit={handleSubmit} className="space-y-4">
        {!selectedItem ? (
          <div>
            <Input id="partSearch" label="Search parts" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search by name or SKU..." />
            {items?.items && items.items.length > 0 && (
              <div className="mt-2 border border-surface-200 rounded-lg max-h-48 overflow-y-auto">
                {items.items.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    className="w-full text-left px-3 py-2 text-sm hover:bg-surface-50 flex justify-between"
                    onClick={() => setSelectedItem(item)}
                  >
                    <span>{item.name}</span>
                    <span className="text-surface-400">{formatCurrency(item.sellPrice)} · {item.quantityOnHand} in stock</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          <>
            <div className="p-3 bg-surface-50 rounded-lg flex justify-between items-center">
              <div>
                <p className="font-medium text-sm">{selectedItem.name}</p>
                <p className="text-xs text-surface-500">{formatCurrency(selectedItem.sellPrice)} each · {selectedItem.quantityOnHand} in stock</p>
              </div>
              <button type="button" onClick={() => setSelectedItem(null)} className="text-xs text-brand-600 hover:underline">Change</button>
            </div>
            <Input id="qty" label="Quantity" type="number" step="1" min="1" required value={quantity} onChange={(e) => setQuantity(e.target.value)} />
          </>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading} disabled={!selectedItem}>Add Part</Button>
        </div>
      </form>
    </Modal>
  );
}

function EditJobModal({ job, onClose, onSaved }: { job: JobDetail; onClose: () => void; onSaved: () => void }) {
  // Convert UTC ISO to datetime-local value (YYYY-MM-DDTHH:mm)
  const toLocal = (iso: string | null) => {
    if (!iso) return "";
    const d = new Date(iso);
    const offset = d.getTimezoneOffset();
    const local = new Date(d.getTime() - offset * 60000);
    return local.toISOString().slice(0, 16);
  };

  const { data: zones } = useApi<{ id: string; name: string; isActive: boolean }[]>("/api/zones");
  const activeZones = (zones ?? []).filter((z) => z.isActive);

  const [form, setForm] = useState({
    title: job.title,
    internalNotes: job.internalNotes ?? "",
    customerNotes: job.customerNotes ?? "",
    priority: job.priority,
    scheduledStart: toLocal(job.scheduledStartUtc),
    scheduledEnd: toLocal(job.scheduledEndUtc),
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.put(`/api/jobs/${job.id}`, {
        title: form.title,
        internalNotes: form.internalNotes || null,
        customerNotes: form.customerNotes || null,
        priority: form.priority,
        zoneId: null,
        scheduledStartUtc: form.scheduledStart ? new Date(form.scheduledStart).toISOString() : null,
        scheduledEndUtc: form.scheduledEnd ? new Date(form.scheduledEnd).toISOString() : null,
      });
      toast.success("Job updated");
      onSaved();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Edit Job" wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="title" label="Title" required value={form.title} onChange={u("title")} />

        <Select
          id="priority"
          label="Priority"
          value={form.priority}
          onChange={u("priority")}
          options={[
            { value: "Low", label: "Low" },
            { value: "Normal", label: "Normal" },
            { value: "High", label: "High" },
            { value: "Urgent", label: "Urgent" },
          ]}
        />

        <div className="grid grid-cols-2 gap-4">
          <Input
            id="scheduledStart"
            label="Scheduled Start"
            type="datetime-local"
            value={form.scheduledStart}
            onChange={u("scheduledStart")}
          />
          <Input
            id="scheduledEnd"
            label="Scheduled End"
            type="datetime-local"
            value={form.scheduledEnd}
            onChange={u("scheduledEnd")}
          />
        </div>

        <Textarea id="internalNotes" label="Internal Notes" value={form.internalNotes} onChange={u("internalNotes")} />
        <Textarea id="customerNotes" label="Customer Notes" value={form.customerNotes} onChange={u("customerNotes")} />

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}
