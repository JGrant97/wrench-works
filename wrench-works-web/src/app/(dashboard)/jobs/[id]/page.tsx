"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { mutate } from "swr";
import { ArrowLeft, Plus, Trash2, ArrowRightCircle, Pencil } from "lucide-react";
import { useApi } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Spinner } from "@/components/ui";
import { ErrorState } from "@/components/data-state";
import { formatCurrency, formatDate, formatDateTime, JOB_STATUS_COLORS, statusLabel } from "@/lib/utils";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { STATUS_TRANSITIONS } from "./_lib/job";
import type { JobDetail } from "./_lib/job";
import Link from "next/link";
import { AddLaborModal } from "./_components/add-labor-modal";
import { AddPartModal } from "./_components/add-part-modal";
import { EditJobModal } from "./_components/edit-job-modal";

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canEdit = usePermission("jobs.edit");
  const { data: job, isLoading, error } = useApi<JobDetail>(`/api/jobs/${id}`);
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
  // Before the empty branch: a failed load is not a missing job.
  if (error) return <ErrorState error={error} onRetry={refreshJob} />;
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
            <Badge className={JOB_STATUS_COLORS[job.status]}>{statusLabel(job.status)}</Badge>
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
