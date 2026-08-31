"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useCustomerVehicle } from "@/hooks/use-customer-vehicle";
import { Button, Card, Input, Select, Textarea, PageHeader } from "@/components/ui";
import { ArrowLeft } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";

export default function NewJobPage() {
  const router = useRouter();
  const [customerSearch, setCustomerSearch] = useState("");
  const [form, setForm] = useState({
    customerId: "",
    customerName: "",
    vehicleId: "",
    title: "",
    priority: "Normal",
    internalNotes: "",
    customerNotes: "",
  });
  const [loading, setLoading] = useState(false);

  // keepSearchingAfterSelect: false — New Job hides the result list once a customer is
  // picked, so the form reads as a completed step rather than an open search.
  const { customers: searchResults, vehicles } =
    useCustomerVehicle(customerSearch, form.customerId, { keepSearchingAfterSelect: false });

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      const result = await fetcher.post<{ id: string }>("/api/jobs", {
        customerId: form.customerId,
        vehicleId: form.vehicleId,
        title: form.title,
        priority: form.priority,
        internalNotes: form.internalNotes || null,
        customerNotes: form.customerNotes || null,
      });
      toast.success("Job created");
      router.push(`/jobs/${result.id}`);
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed to create job");
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <button onClick={() => router.back()} className="flex items-center gap-1 text-sm text-surface-500 hover:text-surface-700 mb-4">
        <ArrowLeft size={14} /> Back
      </button>

      <PageHeader title="New Job" />

      <Card className="max-w-2xl">
        <form onSubmit={handleSubmit} className="space-y-5">
          {/* Customer search */}
          <div>
            <Input
              id="customerSearch"
              label="Customer"
              placeholder="Search by name or phone..."
              value={form.customerId ? form.customerName : customerSearch}
              onChange={(e) => {
                if (form.customerId) {
                  setForm((f) => ({ ...f, customerId: "", customerName: "", vehicleId: "" }));
                }
                setCustomerSearch(e.target.value);
              }}
            />
            {searchResults && searchResults.length > 0 && !form.customerId && (
              <div className="mt-1 border border-surface-200 rounded-lg max-h-40 overflow-y-auto">
                {searchResults.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    className="w-full text-left px-3 py-2 text-sm hover:bg-surface-50 transition-colors"
                    onClick={() => {
                      setForm((f) => ({ ...f, customerId: c.id, customerName: c.name }));
                      setCustomerSearch("");
                    }}
                  >
                    {c.name} {c.phone && <span className="text-surface-400">· {c.phone}</span>}
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Vehicle selection */}
          {form.customerId && (
            <Select
              id="vehicleId"
              label="Vehicle"
              required
              value={form.vehicleId}
              onChange={update("vehicleId")}
              placeholder="Select vehicle"
              options={vehicles.map((v) => ({
                value: v.id,
                label: [v.displayName, v.registration].filter(Boolean).join(" · "),
              }))}
            />
          )}

          <Input id="title" label="Job Title" required value={form.title} onChange={update("title")} placeholder="e.g. Full service and MOT" />

          <Select
            id="priority"
            label="Priority"
            value={form.priority}
            onChange={update("priority")}
            options={[
              { value: "Low", label: "Low" },
              { value: "Normal", label: "Normal" },
              { value: "High", label: "High" },
              { value: "Urgent", label: "Urgent" },
            ]}
          />

          <Textarea id="internalNotes" label="Internal Notes" value={form.internalNotes} onChange={update("internalNotes")} placeholder="Notes visible to your team only" />
          <Textarea id="customerNotes" label="Customer Notes" value={form.customerNotes} onChange={update("customerNotes")} placeholder="Notes related to the customer request" />

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="ghost" onClick={() => router.back()}>Cancel</Button>
            <Button type="submit" loading={loading} disabled={!form.customerId || !form.vehicleId}>Create Job</Button>
          </div>
        </form>
      </Card>
    </>
  );
}
