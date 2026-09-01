"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { mutate } from "swr";
import { ArrowLeft, Plus, Trash2, ArrowRightCircle, Pencil } from "lucide-react";
import { useApi } from "@/hooks/use-api";
import { useCurrency } from "@/hooks/use-currency";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Spinner } from "@/components/ui";
import { ErrorState } from "@/components/data-state";
import { RecordActions } from "@/components/record-actions";
import { formatDate, formatDateTime, JOB_STATUS_COLORS, statusLabel } from "@/lib/utils";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { STATUS_TRANSITIONS } from "./_lib/job";
import type { JobDetail } from "./_lib/job";
import Link from "next/link";
import { AddLaborModal } from "./_components/add-labor-modal";
import { AddPartModal } from "./_components/add-part-modal";
import { EditJobModal } from "./_components/edit-job-modal";

/**
 * The tax charged on one line, next to its price.
 *
 * A line with no tax shows an em dash rather than a zero amount — the whole point of the
 * column is telling a customer which items were taxed, and "£0.00" reads as "taxed, at
 * nothing" rather than "not taxed at all".
 */
function LineTaxCell({
  line,
  format,
}: {
  line: { taxRatePercent: number; taxAmount: number };
  format: (n: number) => string;
}) {
  if (!line.taxRatePercent) {
    return <td className="py-2 text-right text-surface-300">—</td>;
  }

  return (
    <td className="py-2 text-right">
      <span className="text-surface-700">{format(line.taxAmount)}</span>
      {/* The rate matters when a job mixes them — parts taxed, labour exempt. */}
      <span className="block text-xs text-surface-400">
        {+(line.taxRatePercent * 100).toFixed(4)}%
      </span>
    </td>
  );
}

export default function JobDetailPage() {
  const { format } = useCurrency();
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const canEdit = usePermission("jobs.edit");
  // jobs.delete is seeded to Admin only — an Advisor can edit a job but not remove it.
  const canDelete = usePermission("jobs.delete");
  const { data: job, isLoading, error } = useApi<JobDetail>(`/api/jobs/${id}`);
  const [showAddLabor, setShowAddLabor] = useState(false);
  const [showAddPart, setShowAddPart] = useState(false);
  const [showEditJob, setShowEditJob] = useState(false);

  const refreshJob = () => mutate(`/api/jobs/${id}`);

  const closedStatuses = ["Closed", "Completed", "Invoiced"];
  const isEditable = canEdit && job != null && !closedStatuses.includes(job.status);

  // The tax column appears only once there is tax to show, so a workshop that has not
  // configured any rates sees exactly the table it saw before the feature existed.
  const showsTax = (job?.taxTotal ?? 0) > 0;

  // With tax-inclusive pricing the Total column already contains the tax, so the header
  // has to say that the figure is part of the total rather than added to it.
  const taxColumnLabel = job?.pricesIncludeTax
    ? `${job.taxLabel} (incl.)`
    : (job?.taxLabel ?? "Tax");

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
          <RecordActions
            resource="jobs"
            id={id}
            label="job"
            canManage={canDelete}
            onChanged={refreshJob}
            afterDelete={() => router.push("/jobs")}
          />
        </div>
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
          <p className="text-3xl font-bold text-surface-900 font-display">{format(job.grandTotal)}</p>
          <div className="mt-3 space-y-1 text-sm">
            <div className="flex justify-between"><span className="text-surface-500">Labor</span><span>{format(job.laborTotal)}</span></div>
            <div className="flex justify-between"><span className="text-surface-500">Parts</span><span>{format(job.partsTotal)}</span></div>

            {/* The tax split only appears once there is tax. A business with no rates
                configured sees exactly what it saw before this feature existed. */}
            {job.taxTotal > 0 && (
              <>
                <div className="flex justify-between pt-1 border-t border-surface-100">
                  <span className="text-surface-500">Subtotal</span>
                  <span>{format(job.subTotal)}</span>
                </div>

                {job.taxBreakdown.map((t) => (
                  <div key={`${t.name}-${t.ratePercent}`}>
                    <div className="flex justify-between">
                      <span className="text-surface-500">
                        {t.name} ({+(t.ratePercent * 100).toFixed(4)}%)
                      </span>
                      <span>{format(t.amount)}</span>
                    </div>
                    {/* Jurisdiction split, for a US invoice that has to itemise it. */}
                    {t.components.length > 0 && (
                      <p className="text-xs text-surface-400 pl-2">
                        {t.components
                          .map((c) => `${c.name} ${+(c.ratePercent * 100).toFixed(4)}%`)
                          .join(" · ")}
                      </p>
                    )}
                  </div>
                ))}

                <div className="flex justify-between font-medium pt-1 border-t border-surface-100">
                  <span className="text-surface-600">Total {job.taxLabel.toLowerCase()}</span>
                  <span>{format(job.taxTotal)}</span>
                </div>
              </>
            )}

            {job.pricesIncludeTax && job.taxTotal > 0 && (
              <p className="text-xs text-surface-400 pt-1">Prices include {job.taxLabel.toLowerCase()}</p>
            )}

            {job.customerIsTaxExempt && (
              <p className="text-xs text-amber-600 pt-1">
                Customer is {job.taxLabel.toLowerCase()}-exempt — no tax charged
              </p>
            )}
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
                {showsTax && <th className="pb-2 font-medium text-right">{taxColumnLabel}</th>}
                <th className="pb-2 font-medium text-right">Total</th>
                {isEditable && <th className="pb-2 w-10" />}
              </tr>
            </thead>
            <tbody>
              {job.laborLines.map((line) => (
                <tr key={line.id} className="border-b border-surface-50">
                  <td className="py-2">{line.description}</td>
                  <td className="py-2 text-right">{line.hours}</td>
                  <td className="py-2 text-right">{format(line.rate)}</td>
                  {showsTax && <LineTaxCell line={line} format={format} />}
                  <td className="py-2 text-right font-medium">{format(line.total)}</td>
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
                {showsTax && <th className="pb-2 font-medium text-right">{taxColumnLabel}</th>}
                <th className="pb-2 font-medium text-right">Total</th>
                {isEditable && <th className="pb-2 w-10" />}
              </tr>
            </thead>
            <tbody>
              {job.partLines.map((line) => (
                <tr key={line.id} className="border-b border-surface-50">
                  <td className="py-2">{line.itemName}</td>
                  <td className="py-2 text-right">{line.quantity}</td>
                  <td className="py-2 text-right">{format(line.unitPrice)}</td>
                  {showsTax && <LineTaxCell line={line} format={format} />}
                  <td className="py-2 text-right font-medium">{format(line.total)}</td>
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
