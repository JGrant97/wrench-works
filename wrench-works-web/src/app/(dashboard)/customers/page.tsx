"use client";

import { useState } from "react";
import Link from "next/link";
import { useApiQuery } from "@/hooks/use-api";
import { usePermission } from "@/hooks/use-permission";
import { Button, Card, Modal, Input, Textarea, PageHeader, Spinner, EmptyState } from "@/components/ui";
import { formatDate } from "@/lib/utils";
import { Plus, Users, Search, Phone, Mail } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";

interface Customer {
  id: string;
  name: string;
  phone: string | null;
  email: string | null;
  vehicleCount: number;
  createdAtUtc: string;
}

interface CustomerListResponse {
  items: Customer[];
  total: number;
  page: number;
  pageSize: number;
}

export default function CustomersPage() {
  const canManage = usePermission("customers.manage");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);

  const { data, isLoading } = useApiQuery<CustomerListResponse>("/api/customers", {
    search: search || undefined,
    page: String(page),
    pageSize: "25",
  });

  const customers = data?.items ?? [];
  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1;

  return (
    <>
      <PageHeader
        title="Customers"
        description={`${data?.total ?? 0} customers`}
        actions={
          canManage ? (
            <Button onClick={() => setShowCreate(true)}>
              <Plus size={16} /> New Customer
            </Button>
          ) : undefined
        }
      />

      <Card className="mb-6 p-4">
        <div className="relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-surface-400" />
          <input
            type="text"
            placeholder="Search by name, phone, or email..."
            className="w-full pl-9 pr-3 py-2 rounded-lg border border-surface-300 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>
      </Card>

      {isLoading ? (
        <div className="flex justify-center py-20"><Spinner /></div>
      ) : customers.length === 0 ? (
        <EmptyState
          icon={<Users size={48} />}
          title="No customers found"
          description={search ? "Try a different search" : "Add your first customer to get started"}
          action={canManage ? <Button onClick={() => setShowCreate(true)}>Add Customer</Button> : undefined}
        />
      ) : (
        <>
          <div className="space-y-2">
            {customers.map((c) => (
              <Link key={c.id} href={`/customers/${c.id}`}>
                <Card className="p-4 hover:border-brand-200 transition-colors cursor-pointer">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-medium text-surface-900">{c.name}</p>
                      <div className="flex items-center gap-4 mt-1">
                        {c.phone && (
                          <span className="flex items-center gap-1 text-xs text-surface-500">
                            <Phone size={12} /> {c.phone}
                          </span>
                        )}
                        {c.email && (
                          <span className="flex items-center gap-1 text-xs text-surface-500">
                            <Mail size={12} /> {c.email}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="text-right flex-shrink-0">
                      <p className="text-sm text-surface-500">{c.vehicleCount} vehicle{c.vehicleCount !== 1 ? "s" : ""}</p>
                      <p className="text-xs text-surface-400">Since {formatDate(c.createdAtUtc)}</p>
                    </div>
                  </div>
                </Card>
              </Link>
            ))}
          </div>

          {totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-6">
              <Button variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
              <span className="flex items-center text-sm text-surface-500">Page {page} of {totalPages}</span>
              <Button variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
            </div>
          )}
        </>
      )}

      {showCreate && (
        <CreateCustomerModal
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            mutate((key: string) => typeof key === "string" && key.startsWith("/api/customers"));
          }}
        />
      )}
    </>
  );
}

function CreateCustomerModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [form, setForm] = useState({ name: "", phone: "", email: "", address: "", notes: "" });
  const [loading, setLoading] = useState(false);

  const update = (field: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      await fetcher.post("/api/customers", form);
      toast.success("Customer created");
      onCreated();
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="New Customer">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={update("name")} placeholder="John Smith" />
        <div className="grid grid-cols-2 gap-4">
          <Input id="phone" label="Phone" value={form.phone} onChange={update("phone")} placeholder="07700 900000" />
          <Input id="email" label="Email" type="email" value={form.email} onChange={update("email")} placeholder="john@example.com" />
        </div>
        <Input id="address" label="Address" value={form.address} onChange={update("address")} />
        <Textarea id="notes" label="Notes" value={form.notes} onChange={update("notes")} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Create</Button>
        </div>
      </form>
    </Modal>
  );
}
