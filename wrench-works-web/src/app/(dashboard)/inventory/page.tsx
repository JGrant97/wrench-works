"use client";

import { useState } from "react";
import { useApi, useApiQuery } from "@/hooks/use-api";
import { useCurrency } from "@/hooks/use-currency";
import { usePermission } from "@/hooks/use-permission";
import { Button, Badge, Card, Modal, Input, Select, PageHeader, Spinner, EmptyState } from "@/components/ui";
import { Plus, Package, Search, AlertTriangle, Pencil } from "lucide-react";
import { fetcher } from "@/lib/fetcher";
import toast from "react-hot-toast";
import { mutate } from "swr";
import { ErrorState } from "@/components/data-state";
import { FeatureGate } from "@/components/feature-gate";
import type { InventoryItemDto as InventoryItem, InventoryCategoryDto as Category, PagedResultOfInventoryItemDto as ListResponse } from "@/api/generated/models";




export default function InventoryPage() {
  const { format } = useCurrency();
  const canManage = usePermission("inventory.manage");
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [showCreate, setShowCreate] = useState(false);
  const [editItem, setEditItem] = useState<InventoryItem | null>(null);
  const [adjustItem, setAdjustItem] = useState<InventoryItem | null>(null);

  const { data: categories } = useApi<Category[]>("/api/inventory/categories");
  const { data, isLoading, error } = useApiQuery<ListResponse>("/api/inventory/items", {
    search: search || undefined, categoryId: categoryId || undefined,
    lowStockOnly: lowStockOnly ? "true" : undefined, page: String(page), pageSize: "25",
  });

  const items = data?.items ?? [];
  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1;
  const refresh = () => mutate((key: string) => typeof key === "string" && key.startsWith("/api/inventory"));

  return (
    <FeatureGate feature="inventory" featureName="Inventory">
    <>
      <PageHeader title="Inventory" description={`${data?.total ?? 0} items`}
        actions={canManage ? <Button onClick={() => setShowCreate(true)}><Plus size={16} /> Add Item</Button> : undefined} />

      <Card className="mb-6 p-4">
        <div className="flex gap-4 items-end">
          <div className="flex-1 relative">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-surface-400" />
            <input type="text" placeholder="Search by name or SKU..." className="w-full pl-9 pr-3 py-2 rounded-lg border border-surface-300 bg-surface-0 text-sm focus:outline-none focus:ring-2 focus:ring-brand-400"
              value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          </div>
          <Select options={[{ value: "", label: "All categories" }, ...(categories ?? []).map((c) => ({ value: c.id, label: c.name }))]}
            value={categoryId} onChange={(e) => { setCategoryId(e.target.value); setPage(1); }} className="w-48" />
          <label className="flex items-center gap-2 text-sm text-surface-600 whitespace-nowrap">
            <input type="checkbox" checked={lowStockOnly} onChange={(e) => { setLowStockOnly(e.target.checked); setPage(1); }} className="rounded border-surface-300" />
            Low stock only
          </label>
        </div>
      </Card>

      {isLoading ? <div className="flex justify-center py-20"><Spinner /></div>
      : error ? <ErrorState error={error} onRetry={refresh} />
      : items.length === 0 ? <EmptyState icon={<Package size={48} />} title="No items found" description={search || categoryId ? "Try different filters" : "Add inventory items to track stock"} />
      : (
        <>
          <Card className="overflow-hidden p-0">
            <table className="w-full text-sm">
              <thead><tr className="bg-surface-50 text-left text-surface-500">
                <th className="px-4 py-3 font-medium">Item</th><th className="px-4 py-3 font-medium">SKU</th>
                <th className="px-4 py-3 font-medium">Category</th><th className="px-4 py-3 font-medium text-right">Stock</th>
                <th className="px-4 py-3 font-medium text-right">Cost</th><th className="px-4 py-3 font-medium text-right">Retail</th>
                {canManage && <th className="px-4 py-3 w-32" />}
              </tr></thead>
              <tbody>{items.map((item) => (
                <tr key={item.id} className="border-t border-surface-100 hover:bg-surface-50">
                  <td className="px-4 py-3 font-medium text-surface-900">{item.name}</td>
                  <td className="px-4 py-3 text-surface-500 font-mono text-xs">{item.sku ?? "—"}</td>
                  <td className="px-4 py-3 text-surface-500">{item.categoryName ?? "—"}</td>
                  <td className="px-4 py-3 text-right">
                    <span className={item.lowStock ? "text-red-600 font-semibold" : ""}>
                      {item.lowStock && <AlertTriangle size={12} className="inline mr-1" />}{item.stockOnHand}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right text-surface-500">{format(item.unitCost)}</td>
                  <td className="px-4 py-3 text-right font-medium">{item.retailPrice != null ? format(item.retailPrice) : "—"}</td>
                  {canManage && (
                    <td className="px-4 py-3 text-right space-x-1">
                      <Button variant="ghost" size="sm" onClick={() => setEditItem(item)}><Pencil size={14} /></Button>
                      <Button variant="ghost" size="sm" onClick={() => setAdjustItem(item)}>Adjust</Button>
                    </td>
                  )}
                </tr>
              ))}</tbody>
            </table>
          </Card>
          {totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-6">
              <Button variant="ghost" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
              <span className="flex items-center text-sm text-surface-500">Page {page} of {totalPages}</span>
              <Button variant="ghost" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>Next</Button>
            </div>
          )}
        </>
      )}

      {showCreate && <CreateItemModal categories={categories ?? []} onClose={() => setShowCreate(false)} onCreated={() => { setShowCreate(false); refresh(); }} />}
      {editItem && <EditItemModal item={editItem} categories={categories ?? []} onClose={() => setEditItem(null)} onSaved={() => { setEditItem(null); refresh(); }} />}
      {adjustItem && <AdjustStockModal item={adjustItem} onClose={() => setAdjustItem(null)} onAdjusted={() => { setAdjustItem(null); refresh(); }} />}
    </>
    </FeatureGate>
  );
}

function CreateItemModal({ categories, onClose, onCreated }: { categories: Category[]; onClose: () => void; onCreated: () => void }) {
  const { symbol } = useCurrency();
  const [form, setForm] = useState({ name: "", sku: "", categoryId: "", unitCost: "", retailPrice: "", stockOnHand: "0", reorderThreshold: "5", isConsumable: false });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setLoading(true);
    try {
      await fetcher.post("/api/inventory/items", {
        name: form.name, sku: form.sku || null, categoryId: form.categoryId || null,
        unitCost: parseFloat(form.unitCost) || 0, retailPrice: form.retailPrice ? parseFloat(form.retailPrice) : null,
        stockOnHand: parseInt(form.stockOnHand) || 0, reorderThreshold: parseInt(form.reorderThreshold) || 0,
        isConsumable: form.isConsumable,
      });
      toast.success("Item created"); onCreated();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title="Add Inventory Item">
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={u("name")} placeholder="Brake Pads - Front" />
        <div className="grid grid-cols-2 gap-4">
          <Input id="sku" label="SKU" value={form.sku} onChange={u("sku")} placeholder="BP-F-001" />
          <Select id="catId" label="Category" value={form.categoryId} onChange={u("categoryId")} placeholder="Select" options={categories.map((c) => ({ value: c.id, label: c.name }))} />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Input id="unitCost" label={`Unit Cost (${symbol})`} type="number" step="0.01" min="0" required value={form.unitCost} onChange={u("unitCost")} />
          <Input id="retailPrice" label={`Retail Price (${symbol})`} type="number" step="0.01" min="0" value={form.retailPrice} onChange={u("retailPrice")} />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Input id="stock" label="Opening Stock" type="number" min="0" value={form.stockOnHand} onChange={u("stockOnHand")} />
          <Input id="reorder" label="Reorder Level" type="number" min="0" value={form.reorderThreshold} onChange={u("reorderThreshold")} />
        {/* Only affects which tax category a job line takes; consumables still come from
            stock and still bill as a part line. See docs/tax.md. */}
        <label className="flex items-start gap-2 text-sm text-surface-700">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={form.isConsumable}
            onChange={(e) => setForm((f) => ({ ...f, isConsumable: e.target.checked }))}
          />
          <span>
            Consumable
            <span className="block text-xs text-surface-500">
              Shop supplies and disposal levies, taxed separately from fitted parts.
            </span>
          </span>
        </label>

        </div>
        {/* Only affects which tax category a job line takes; consumables still come from
            stock and still bill as a part line. See docs/tax.md. */}
        <label className="flex items-start gap-2 text-sm text-surface-700">
          <input
            type="checkbox"
            className="mt-0.5"
            checked={form.isConsumable}
            onChange={(e) => setForm((f) => ({ ...f, isConsumable: e.target.checked }))}
          />
          <span>
            Consumable
            <span className="block text-xs text-surface-500">
              Shop supplies and disposal levies, taxed separately from fitted parts.
            </span>
          </span>
        </label>

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Create</Button>
        </div>
      </form>
    </Modal>
  );
}

function EditItemModal({ item, categories, onClose, onSaved }: { item: InventoryItem; categories: Category[]; onClose: () => void; onSaved: () => void }) {
  const { symbol } = useCurrency();
  const [form, setForm] = useState({
    name: item.name, sku: item.sku ?? "", categoryId: item.categoryId ?? "",
    unitCost: String(item.unitCost), retailPrice: item.retailPrice != null ? String(item.retailPrice) : "", reorderThreshold: String(item.reorderThreshold),
    isConsumable: item.isConsumable,
  });
  const [loading, setLoading] = useState(false);
  const u = (f: string) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setForm((p) => ({ ...p, [f]: e.target.value }));

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setLoading(true);
    try {
      await fetcher.put(`/api/inventory/items/${item.id}`, {
        name: form.name, sku: form.sku || null, categoryId: form.categoryId || null,
        unitCost: parseFloat(form.unitCost) || 0, retailPrice: form.retailPrice ? parseFloat(form.retailPrice) : null,
        reorderThreshold: parseInt(form.reorderThreshold) || 0,
        isConsumable: form.isConsumable,
      });
      toast.success("Item updated"); onSaved();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title={`Edit: ${item.name}`}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="name" label="Name" required value={form.name} onChange={u("name")} />
        <div className="grid grid-cols-2 gap-4">
          <Input id="sku" label="SKU" value={form.sku} onChange={u("sku")} />
          <Select id="catId" label="Category" value={form.categoryId} onChange={u("categoryId")} placeholder="None" options={categories.map((c) => ({ value: c.id, label: c.name }))} />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <Input id="unitCost" label={`Unit Cost (${symbol})`} type="number" step="0.01" min="0" required value={form.unitCost} onChange={u("unitCost")} />
          <Input id="retailPrice" label={`Retail Price (${symbol})`} type="number" step="0.01" min="0" value={form.retailPrice} onChange={u("retailPrice")} />
        </div>
        <Input id="reorder" label="Reorder Level" type="number" min="0" value={form.reorderThreshold} onChange={u("reorderThreshold")} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Save Changes</Button>
        </div>
      </form>
    </Modal>
  );
}

function AdjustStockModal({ item, onClose, onAdjusted }: { item: InventoryItem; onClose: () => void; onAdjusted: () => void }) {
  const [form, setForm] = useState({ quantityDelta: "", reason: "ManualAdjustment", notes: "" });
  const [loading, setLoading] = useState(false);
  // Values MUST match the StockMovementReason enum exactly — Enum.TryParse rejects
  // anything else with a 400. "Restock" and "Returned" used to be offered here and
  // always failed; the real values are PurchaseReceived and JobReturn.
  // JobConsumption is omitted deliberately: it is written by the job part flow, not by hand.
  const reasons = [
    { value: "ManualAdjustment", label: "Manual adjustment" },
    { value: "PurchaseReceived", label: "Purchase received (restock)" },
    { value: "JobReturn", label: "Returned from job" },
    { value: "Damaged", label: "Damaged" },
    { value: "Correction", label: "Stock-take correction" },
    { value: "Other", label: "Other" },
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setLoading(true);
    try {
      await fetcher.post(`/api/inventory/items/${item.id}/adjust`, {
        quantityDelta: parseInt(form.quantityDelta), reason: form.reason, notes: form.notes || null,
      });
      toast.success("Stock adjusted"); onAdjusted();
    } catch (err: unknown) { toast.error(err instanceof Error ? err.message : "Failed"); } finally { setLoading(false); }
  };

  return (
    <Modal open onClose={onClose} title={`Adjust Stock: ${item.name}`}>
      <p className="text-sm text-surface-500 mb-4">Current stock: <strong className="text-surface-900">{item.stockOnHand}</strong></p>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input id="qty" label="Quantity Change" type="number" required value={form.quantityDelta} onChange={(e) => setForm((f) => ({ ...f, quantityDelta: e.target.value }))} placeholder="e.g. 10 or -3" />
        <Select id="reason" label="Reason" value={form.reason} onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))} options={reasons} />
        <Input id="notes" label="Notes (optional)" value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="ghost" onClick={onClose}>Cancel</Button>
          <Button type="submit" loading={loading}>Adjust Stock</Button>
        </div>
      </form>
    </Modal>
  );
}
